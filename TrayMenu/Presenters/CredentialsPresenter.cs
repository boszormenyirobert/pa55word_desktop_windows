using TrayApp.Models;
using TrayApp.Services;

namespace TrayApp.TrayMenu.Presenters;

internal sealed class CredentialsPresenter
{
    internal const string UngroupedGroupId = "__ungrouped__";
    private readonly ICredentialCollectionService _service;
    private readonly Func<string> _getScopeKey;
    private readonly Dictionary<int, CredentialEntryDetail> _details = new();
    private string _cacheKey = string.Empty;
    private bool _loaded;
    private int _scopeVersion;

    internal CredentialsPresenter(ICredentialCollectionService service, Func<string> getScopeKey)
    {
        _service = service;
        _getScopeKey = getScopeKey;
    }

    internal event EventHandler? StateChanged;
    internal IReadOnlyList<CredentialGroupDto> Groups { get; private set; } = Array.Empty<CredentialGroupDto>();
    internal IReadOnlyList<CredentialEntryListItem> Entries { get; private set; } = Array.Empty<CredentialEntryListItem>();
    internal string SelectedGroupId { get; set; } = string.Empty;
    internal string SelectedEntryId { get; set; } = string.Empty;
    internal string SelectedManagedGroupId { get; set; } = string.Empty;
    internal CredentialWorkspaceMode WorkspaceMode { get; set; } = CredentialWorkspaceMode.ViewEntry;
    internal CredentialDeletionConfirmation DeletionConfirmation { get; set; }
    internal bool IsLoading { get; private set; }
    internal string ErrorMessage { get; private set; } = string.Empty;
    internal string EntryStatusMessage { get; set; } = string.Empty;
    internal string GroupStatusMessage { get; set; } = string.Empty;

    internal IReadOnlyList<CredentialEntryListItem> GetFilteredEntries()
    {
        if (string.IsNullOrWhiteSpace(SelectedGroupId)) return Entries;
        return Entries.Where(entry => SelectedGroupId == UngroupedGroupId
            ? string.IsNullOrWhiteSpace(entry.GroupId)
            : string.Equals(entry.GroupId, SelectedGroupId, StringComparison.Ordinal)).ToArray();
    }

    internal string GetSelectedGroupLabel() => SelectedGroupId == UngroupedGroupId
        ? "Ungrouped"
        : Groups.FirstOrDefault(group => string.Equals(group.GroupId, SelectedGroupId, StringComparison.Ordinal))?.GroupName ?? string.Empty;

    internal CredentialEntryListItem? GetSelectedEntry() => Entries.FirstOrDefault(entry =>
        string.Equals(entry.Id.ToString(), SelectedEntryId, StringComparison.Ordinal));

    internal void ValidateSelectedEntry()
    {
        if (!GetFilteredEntries().Any(entry => string.Equals(entry.Id.ToString(), SelectedEntryId, StringComparison.Ordinal)))
            SelectedEntryId = string.Empty;
    }

    internal void SelectEntry(CredentialEntryListItem entry)
    {
        SelectedGroupId = string.IsNullOrWhiteSpace(entry.GroupId) ? UngroupedGroupId : entry.GroupId;
        SelectedEntryId = entry.Id.ToString();
        WorkspaceMode = CredentialWorkspaceMode.ViewEntry;
        DeletionConfirmation = CredentialDeletionConfirmation.None;
        EntryStatusMessage = string.Empty;
        GroupStatusMessage = string.Empty;
    }

    internal void SelectGroup(string groupId)
    {
        SelectedGroupId = groupId;
        SelectedEntryId = string.Empty;
    }

    internal CredentialEntryDetail? ReadSelectedEntryDetail()
    {
        CredentialEntryListItem? entry = GetSelectedEntry();
        if (entry == null) return null;
        if (!_details.TryGetValue(entry.Id, out CredentialEntryDetail? detail))
        {
            detail = _service.ReadEntryDetail(entry);
            _details[entry.Id] = detail;
        }
        return detail;
    }

    internal void InvalidateCollection()
    {
        _scopeVersion++;
        _cacheKey = string.Empty;
        _loaded = false;
        _details.Clear();
    }

    internal void Reset()
    {
        InvalidateCollection();
        Groups = Array.Empty<CredentialGroupDto>();
        Entries = Array.Empty<CredentialEntryListItem>();
        SelectedGroupId = string.Empty;
        SelectedEntryId = string.Empty;
        SelectedManagedGroupId = string.Empty;
        DeletionConfirmation = CredentialDeletionConfirmation.None;
        EntryStatusMessage = string.Empty;
        GroupStatusMessage = string.Empty;
        ErrorMessage = string.Empty;
    }

    internal async Task<bool> LoadAsync(bool forceRefresh = false)
    {
        if (IsLoading || (!forceRefresh && _loaded && _cacheKey == _getScopeKey() && ErrorMessage.Length == 0))
            return false;

        IsLoading = true;
        ErrorMessage = string.Empty;
        NotifyChanged();
        bool scopeChanged = false;
        try
        {
            scopeChanged = await ReadCollectionAsync();
            return scopeChanged;
        }
        finally
        {
            IsLoading = false;
            if (!scopeChanged) NotifyChanged();
        }
    }

    private async Task<bool> ReadCollectionAsync()
    {
        string scopeKey = _getScopeKey();
        int version = _scopeVersion;
        try
        {
            IReadOnlyList<CredentialGroupDto> groups = await _service.ReadGroupsAsync();
            if (!MatchesScope(scopeKey, version)) return true;
            IReadOnlyList<CredentialEntryListItem> entries = await _service.ReadEntriesAsync();
            if (!MatchesScope(scopeKey, version)) return true;

            Groups = groups.OrderBy(group => group.GroupName, StringComparer.CurrentCultureIgnoreCase).ToArray();
            Entries = entries.OrderBy(entry => entry.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
            _details.Clear();
            _cacheKey = scopeKey;
            _loaded = true;
            ErrorMessage = string.Empty;
            if (SelectedGroupId != UngroupedGroupId && !Groups.Any(group => group.GroupId == SelectedGroupId))
                SelectedGroupId = string.Empty;
            if (string.IsNullOrWhiteSpace(SelectedManagedGroupId))
                SelectedManagedGroupId = Groups.FirstOrDefault()?.GroupId ?? string.Empty;
            ValidateSelectedEntry();
            return false;
        }
        catch (Exception exception)
        {
            if (!MatchesScope(scopeKey, version)) return true;
            Reset();
            ErrorMessage = exception.Message;
            return false;
        }
    }

    internal async Task SaveEntryAsync(string label, BrowserRegistryDto registry, string assignedGroupId)
    {
        if (IsLoading) return;
        if (DeletionConfirmation == CredentialDeletionConfirmation.Credential)
        {
            DeletionConfirmation = CredentialDeletionConfirmation.None;
            EntryStatusMessage = string.Empty;
            NotifyChanged();
            return;
        }
        label = label.Trim();
        if (string.IsNullOrWhiteSpace(label))
        {
            EntryStatusMessage = "Please enter a credential label.";
            NotifyChanged();
            return;
        }

        await RunCommandAsync(async () =>
        {
            CredentialEntryListItem? saved;
            bool create = WorkspaceMode == CredentialWorkspaceMode.CreateEntry;
            CredentialEntryListItem? selected = GetSelectedEntry();
            if (!create && selected == null)
            {
                EntryStatusMessage = "Please select a credential entry first.";
                return;
            }
            if (!create && string.IsNullOrWhiteSpace(assignedGroupId) && !string.IsNullOrWhiteSpace(selected!.GroupId))
            {
                EntryStatusMessage = "Removing a group assignment is not supported by the current tray contract.";
                return;
            }
            string scope = _getScopeKey();
            int version = _scopeVersion;
            var draft = new CredentialEntryDraft(create ? null : selected!.Id, label, registry, assignedGroupId);
            saved = create ? await _service.CreateEntryAsync(draft) : await _service.UpdateEntryAsync(draft);
            if (!MatchesScope(scope, version)) return;
            WorkspaceMode = CredentialWorkspaceMode.ViewEntry;
            EntryStatusMessage = create ? "Credential created." : "Credential updated.";
            SelectedEntryId = saved?.Id.ToString() ?? selected?.Id.ToString() ?? string.Empty;
            await ReadCollectionAsync();
        }, groupCommand: false);
    }

    internal async Task DeleteEntryAsync()
    {
        if (IsLoading) return;
        CredentialEntryListItem? selected = GetSelectedEntry();
        if (selected == null)
        {
            EntryStatusMessage = "Please select a credential entry first.";
            NotifyChanged();
            return;
        }
        if (DeletionConfirmation != CredentialDeletionConfirmation.Credential)
        {
            DeletionConfirmation = CredentialDeletionConfirmation.Credential;
            EntryStatusMessage = string.Empty;
            NotifyChanged();
            return;
        }
        await RunCommandAsync(async () =>
        {
            string scope = _getScopeKey();
            int version = _scopeVersion;
            await _service.DeleteEntryAsync(selected.Id);
            if (!MatchesScope(scope, version)) return;
            DeletionConfirmation = CredentialDeletionConfirmation.None;
            SelectedEntryId = string.Empty;
            EntryStatusMessage = "Credential deleted.";
            await ReadCollectionAsync();
        }, groupCommand: false);
    }

    internal async Task SaveGroupAsync(string editedName, string newName)
    {
        if (IsLoading) return;
        if (DeletionConfirmation == CredentialDeletionConfirmation.Group)
        {
            DeletionConfirmation = CredentialDeletionConfirmation.None;
            GroupStatusMessage = string.Empty;
            NotifyChanged();
            return;
        }
        await RunCommandAsync(async () =>
        {
            bool create = WorkspaceMode == CredentialWorkspaceMode.CreateGroup;
            string name = (create ? newName : editedName).Trim();
            if (!create && string.IsNullOrWhiteSpace(SelectedManagedGroupId))
            {
                GroupStatusMessage = "Please select a group first.";
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                GroupStatusMessage = "Please enter a group name.";
                return;
            }
            string scope = _getScopeKey();
            int version = _scopeVersion;
            CredentialGroupDto? group = create
                ? await _service.CreateGroupAsync(name)
                : await _service.UpdateGroupAsync(SelectedManagedGroupId, name);
            if (!MatchesScope(scope, version)) return;
            if (create)
            {
                WorkspaceMode = CredentialWorkspaceMode.ViewEntry;
                SelectedManagedGroupId = group?.GroupId ?? SelectedManagedGroupId;
            }
            GroupStatusMessage = create ? "Group created." : "Group updated.";
            await ReadCollectionAsync();
        }, groupCommand: true);
    }

    internal async Task DeleteGroupAsync()
    {
        if (IsLoading) return;
        CredentialGroupDto? selected = Groups.FirstOrDefault(group => group.GroupId == SelectedManagedGroupId);
        if (selected == null)
        {
            GroupStatusMessage = "Please select a group first.";
            NotifyChanged();
            return;
        }
        if (DeletionConfirmation != CredentialDeletionConfirmation.Group)
        {
            DeletionConfirmation = CredentialDeletionConfirmation.Group;
            GroupStatusMessage = string.Empty;
            NotifyChanged();
            return;
        }
        await RunCommandAsync(async () =>
        {
            string scope = _getScopeKey();
            int version = _scopeVersion;
            await _service.DeleteGroupAsync(selected.GroupId);
            if (!MatchesScope(scope, version)) return;
            DeletionConfirmation = CredentialDeletionConfirmation.None;
            if (SelectedGroupId == selected.GroupId) SelectedGroupId = string.Empty;
            SelectedManagedGroupId = string.Empty;
            GroupStatusMessage = "Group deleted.";
            await ReadCollectionAsync();
        }, groupCommand: true);
    }

    private async Task RunCommandAsync(Func<Task> command, bool groupCommand)
    {
        string scope = _getScopeKey();
        int version = _scopeVersion;
        IsLoading = true;
        if (groupCommand) GroupStatusMessage = string.Empty;
        else EntryStatusMessage = string.Empty;
        NotifyChanged();
        try
        {
            await command();
        }
        catch (Exception exception)
        {
            if (MatchesScope(scope, version))
            {
                DeletionConfirmation = CredentialDeletionConfirmation.None;
                if (groupCommand) GroupStatusMessage = exception.Message;
                else EntryStatusMessage = exception.Message;
            }
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    private bool MatchesScope(string key, int version) => version == _scopeVersion && key == _getScopeKey();
    private void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}

internal enum CredentialWorkspaceMode { ViewEntry, CreateEntry, ManageGroup, CreateGroup }
internal enum CredentialDeletionConfirmation { None, Credential, Group }