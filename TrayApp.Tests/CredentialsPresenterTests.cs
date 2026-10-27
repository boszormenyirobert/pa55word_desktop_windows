using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu.Presenters;
using Xunit;

namespace TrayApp.Tests;

public sealed class CredentialsPresenterTests
{
    [Fact]
    public async Task Load_SortsCollectionsAndReusesCache()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "profile|database");
        await presenter.LoadAsync();
        await presenter.LoadAsync();
        Assert.Equal(1, service.ReadCount);
        Assert.Equal(new[] { "Alpha", "Zulu" }, presenter.Entries.Select(entry => entry.Label));
        Assert.Equal("Work", Assert.Single(presenter.Groups).GroupName);
        Assert.False(presenter.IsLoading);
    }

    [Theory]
    [InlineData("", "7")]
    [InlineData("work", "7")]
    [InlineData("__ungrouped__", "")]
    public async Task Selection_PreservesOnlyEntriesInTheSelectedGroup(string group, string expected)
    {
        var presenter = new CredentialsPresenter(new FakeCredentialCollectionService(), () => "scope");
        await presenter.LoadAsync();
        presenter.SelectedGroupId = group;
        presenter.SelectedEntryId = "7";
        presenter.ValidateSelectedEntry();
        Assert.Equal(expected, presenter.SelectedEntryId);
    }

    [Fact]
    public async Task Load_IgnoresResultsAfterDatabaseChanges()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<CredentialGroupDto>>();
        var service = new FakeCredentialCollectionService { PendingGroups = pending.Task };
        string scope = "first";
        var presenter = new CredentialsPresenter(service, () => scope);
        Task<bool> loading = presenter.LoadAsync();
        scope = "second";
        pending.SetResult(service.Groups);
        Assert.True(await loading);
        Assert.Empty(presenter.Entries);
        Assert.False(presenter.IsLoading);
        service.PendingGroups = null;
        Assert.False(await presenter.LoadAsync());
        Assert.NotEmpty(presenter.Entries);
    }

    [Fact]
    public async Task Load_FailureClearsDataAndExposesError()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        service.Failure = new InvalidOperationException("Unavailable");
        await presenter.LoadAsync(forceRefresh: true);
        Assert.Empty(presenter.Entries);
        Assert.Equal("Unavailable", presenter.ErrorMessage);
        Assert.False(presenter.IsLoading);
    }

    [Fact]
    public async Task Save_CreateReloadsAndSelectsTheNewEntry()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.WorkspaceMode = CredentialWorkspaceMode.CreateEntry;
        await presenter.SaveEntryAsync(" New entry ", new BrowserRegistryDto(), "work");
        Assert.Equal("9", presenter.SelectedEntryId);
        Assert.Contains(presenter.Entries, entry => entry.Id == 9 && entry.Label == "New entry");
        Assert.Equal(CredentialWorkspaceMode.ViewEntry, presenter.WorkspaceMode);
        Assert.Equal("Credential created.", presenter.EntryStatusMessage);
        Assert.Equal(2, service.ReadCount);
    }

    [Fact]
    public async Task Save_RejectsMissingLabelWithoutCallingBackend()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.SaveEntryAsync(" ", new BrowserRegistryDto(), "");
        Assert.Equal(0, service.SaveCount);
        Assert.Equal("Please enter a credential label.", presenter.EntryStatusMessage);
    }

    [Fact]
    public async Task Save_FailureClearsBusyStateAndNotifiesView()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.SelectedEntryId = "7";
        service.Failure = new InvalidOperationException("Save failed");
        var states = new List<bool>();
        presenter.StateChanged += (_, _) => states.Add(presenter.IsLoading);
        await presenter.SaveEntryAsync("Updated", new BrowserRegistryDto(), "work");
        Assert.Equal(new[] { true, false }, states);
        Assert.Equal("Save failed", presenter.EntryStatusMessage);
        Assert.False(presenter.IsLoading);
    }

    [Fact]
    public async Task Delete_RequiresConfirmationThenReloads()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.SelectedEntryId = "7";
        await presenter.DeleteEntryAsync();
        Assert.Equal(0, service.DeleteCount);
        Assert.Equal(CredentialDeletionConfirmation.Credential, presenter.DeletionConfirmation);
        await presenter.DeleteEntryAsync();
        Assert.Equal(1, service.DeleteCount);
        Assert.DoesNotContain(presenter.Entries, entry => entry.Id == 7);
        Assert.Empty(presenter.SelectedEntryId);
        Assert.Equal(CredentialDeletionConfirmation.None, presenter.DeletionConfirmation);
    }

    [Fact]
    public async Task DetailCache_IsClearedWhenScopeIsInvalidated()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.SelectedEntryId = "7";
        Assert.Same(presenter.ReadSelectedEntryDetail(), presenter.ReadSelectedEntryDetail());
        Assert.Equal(1, service.DetailReadCount);
        presenter.InvalidateCollection();
        presenter.ReadSelectedEntryDetail();
        Assert.Equal(2, service.DetailReadCount);
    }

    [Fact]
    public async Task Group_CreateRenameAndConfirmedDeleteReloadCollections()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.WorkspaceMode = CredentialWorkspaceMode.CreateGroup;
        await presenter.SaveGroupAsync("", " Personal ");
        Assert.Contains(presenter.Groups, group => group.GroupName == "Personal");
        Assert.Equal("new-group", presenter.SelectedManagedGroupId);
        presenter.WorkspaceMode = CredentialWorkspaceMode.ManageGroup;
        await presenter.SaveGroupAsync("Renamed", "");
        Assert.Contains(presenter.Groups, group => group.GroupName == "Renamed");
        presenter.SelectGroup("new-group");
        await presenter.DeleteGroupAsync();
        Assert.Equal(CredentialDeletionConfirmation.Group, presenter.DeletionConfirmation);
        Assert.Contains(presenter.Groups, group => group.GroupId == "new-group");
        await presenter.DeleteGroupAsync();
        Assert.DoesNotContain(presenter.Groups, group => group.GroupId == "new-group");
        Assert.Empty(presenter.SelectedGroupId);
        Assert.False(presenter.IsLoading);
    }

    [Fact]
    public async Task Save_DoesNotOverwriteSelectionAfterScopeInvalidation()
    {
        var pending = new TaskCompletionSource<CredentialEntryListItem?>();
        var service = new FakeCredentialCollectionService { PendingSave = pending.Task };
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.WorkspaceMode = CredentialWorkspaceMode.CreateEntry;
        Task saving = presenter.SaveEntryAsync("New", new BrowserRegistryDto(), "");
        presenter.Reset();
        pending.SetResult(new CredentialEntryListItem(9, "New", "", "", "", "", ""));
        await saving;
        Assert.Empty(presenter.SelectedEntryId);
        Assert.Empty(presenter.Entries);
        Assert.Empty(presenter.EntryStatusMessage);
        Assert.False(presenter.IsLoading);
    }

    [Fact]
    public async Task Save_RejectsUnsupportedGroupRemoval()
    {
        var service = new FakeCredentialCollectionService();
        var presenter = new CredentialsPresenter(service, () => "scope");
        await presenter.LoadAsync();
        presenter.SelectedEntryId = "7";
        await presenter.SaveEntryAsync("Updated", new BrowserRegistryDto(), "");
        Assert.Equal(0, service.SaveCount);
        Assert.Contains("not supported", presenter.EntryStatusMessage);
    }
}

internal sealed class FakeCredentialCollectionService : ICredentialCollectionService
{
    internal IReadOnlyList<CredentialGroupDto> Groups { get; set; } = new[]
    {
        new CredentialGroupDto { GroupId = "work", GroupName = "Work" }
    };
    internal IReadOnlyList<CredentialEntryListItem> Entries { get; set; } = new[]
    {
        new CredentialEntryListItem(7, "Zulu", "work", "Work", "", "", ""),
        new CredentialEntryListItem(8, "Alpha", "", "", "", "", "")
    };
    internal Task<IReadOnlyList<CredentialGroupDto>>? PendingGroups { get; set; }
    internal Task<CredentialEntryListItem?>? PendingSave { get; set; }
    internal Exception? Failure { get; set; }
    internal int ReadCount { get; private set; }
    internal int SaveCount { get; private set; }
    internal int DeleteCount { get; private set; }
    internal int DetailReadCount { get; private set; }

    public Task<IReadOnlyList<CredentialGroupDto>> ReadGroupsAsync()
    {
        ReadCount++;
        if (Failure != null) throw Failure;
        return PendingGroups ?? Task.FromResult(Groups);
    }
    public Task<IReadOnlyList<CredentialEntryListItem>> ReadEntriesAsync() => Task.FromResult(Entries);
    public CredentialEntryDetail ReadEntryDetail(CredentialEntryListItem item)
    {
        DetailReadCount++;
        return new CredentialEntryDetail(item.Id, item.Label, new BrowserRegistryDto(), item.GroupId, item.GroupName);
    }
    public Task<CredentialEntryListItem?> CreateEntryAsync(CredentialEntryDraft draft)
    {
        SaveCount++;
        if (Failure != null) throw Failure;
        if (PendingSave != null) return PendingSave;
        var entry = new CredentialEntryListItem(draft.Id ?? 9, draft.Label, draft.GroupId ?? "", "", "", "", "");
        Entries = Entries.Where(item => item.Id != entry.Id).Append(entry).ToArray();
        return Task.FromResult<CredentialEntryListItem?>(entry);
    }
    public Task<CredentialEntryListItem?> UpdateEntryAsync(CredentialEntryDraft draft) => CreateEntryAsync(draft);
    public Task DeleteEntryAsync(int credentialId)
    {
        DeleteCount++;
        Entries = Entries.Where(entry => entry.Id != credentialId).ToArray();
        return Task.CompletedTask;
    }
    public Task<CredentialGroupDto?> CreateGroupAsync(string groupName, string? groupId = null)
    {
        var group = new CredentialGroupDto { GroupId = groupId ?? "new-group", GroupName = groupName };
        Groups = Groups.Where(item => item.GroupId != group.GroupId).Append(group).ToArray();
        return Task.FromResult<CredentialGroupDto?>(group);
    }
    public Task<CredentialGroupDto?> UpdateGroupAsync(string groupId, string groupName) => CreateGroupAsync(groupName, groupId);
    public Task<CredentialGroupDto?> DeleteGroupAsync(string groupId)
    {
        Groups = Groups.Where(group => group.GroupId != groupId).ToArray();
        return Task.FromResult<CredentialGroupDto?>(null);
    }
}