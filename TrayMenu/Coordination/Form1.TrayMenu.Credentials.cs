using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
using TrayApp.TrayMenu.Presenters;

namespace TrayApp
{
    public partial class Form1
    {
        private readonly CredentialsPresenter _credentialsPresenter;
        private const string UngroupedCredentialGroupId = CredentialsPresenter.UngroupedGroupId;
        private string _credentialSelectorsFingerprint = string.Empty;

        private void HandleCredentialsStateChanged(object? sender, EventArgs e)
        {
            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            UpdateQuickPanelState();
        }

        private async Task LoadCredentialCollectionAsync(bool forceRefresh = false)
        {
            if (!_isDeviceRegistered) return;
            bool scopeChanged = await _credentialsPresenter.LoadAsync(forceRefresh);
            if (scopeChanged && _trayMenuController?.ActiveSection == TrayMenuSection.Credentials &&
                _authenticationService.IsAuthorizedForActiveNamespace)
            {
                await LoadCredentialCollectionAsync(forceRefresh: true);
            }
        }

        private void SynchronizeCredentialEntrySelection()
        {
            _credentialsPresenter.ValidateSelectedEntry();
            SelectComboBoxValue(_credentialSelection.EntrySelector, _credentialsPresenter.SelectedEntryId);
        }

        private async void HandleCredentialSaveRequested(object? sender, EventArgs e)
        {
            await _credentialsPresenter.SaveEntryAsync(_credentialWorkspace.LabelTextBox.Text,
                BuildCredentialRegistryFromEditor(), GetSelectedAssignedCredentialGroupId());
        }

        private async void HandleCredentialDeleteRequested(object? sender, EventArgs e)
        {
            await _credentialsPresenter.DeleteEntryAsync();
        }

        private async void HandleCredentialGroupSaveRequested(object? sender, EventArgs e)
        {
            await _credentialsPresenter.SaveGroupAsync(_credentialWorkspace.ManagedGroupNameTextBox.Text,
                _credentialWorkspace.NewGroupNameTextBox.Text);
        }

        private async void HandleCredentialGroupDeleteRequested(object? sender, EventArgs e)
        {
            await _credentialsPresenter.DeleteGroupAsync();
        }

        private void PopulateSelectedCredentialDetail()
        {
            PopulateCredentialAssignedGroupSelector();
            try
            {
                ApplyCredentialDetailToEditor(_credentialsPresenter.ReadSelectedEntryDetail());
            }
            catch (Exception exception)
            {
                ApplyCredentialDetailToEditor(null);
                SetCredentialEditorStatus(exception.Message);
            }
        }

        private void SelectCredentialFromList(CredentialEntryListItem entry)
        {
            _credentialsPresenter.SelectEntry(entry);
            ClearCredentialEditorStatus();
            ClearGroupEditorStatus();
            PopulateCredentialSelectors();
            RefreshCredentialQuickPanelState();
        }

        private string BuildCredentialCollectionCacheKey()
        {
            string activeUserId = AppEnvironment.GetActiveUserId() ?? string.Empty;
            string activeNamespaceId = AppEnvironment.GetActiveNamespaceId() ?? string.Empty;
            return $"{activeUserId}|{activeNamespaceId}";
        }

        private void PopulateCredentialSelectors()
        {
            string selectorFingerprint = BuildCredentialSelectorsFingerprint();
            _updatingQuickPanelSelections = true;

            try
            {
                var groupOptions = new List<SelectionOption>
                {
                    new("All groups", string.Empty),
                    new("Ungrouped", UngroupedCredentialGroupId)
                };
                foreach (CredentialGroupDto group in _credentialsPresenter.Groups)
                {
                    groupOptions.Add(new SelectionOption(group.GroupName, group.GroupId));
                }

                bool groupOptionsChanged = !CredentialSelectorItemsMatch(_credentialSelection.GroupSelector, groupOptions);
                if (groupOptionsChanged)
                {
                    _credentialSelection.GroupSelector.BeginUpdate();
                    try
                    {
                        _credentialSelection.GroupSelector.Items.Clear();
                        foreach (SelectionOption option in groupOptions)
                        {
                            _credentialSelection.GroupSelector.Items.Add(option);
                        }

                        SelectComboBoxValue(_credentialSelection.GroupSelector, _credentialsPresenter.SelectedGroupId);
                    }
                    finally
                    {
                        _credentialSelection.GroupSelector.EndUpdate();
                    }
                }
                else
                {
                    SelectComboBoxValue(_credentialSelection.GroupSelector, _credentialsPresenter.SelectedGroupId);
                }
                _credentialSelection.GroupSelector.Enabled = _credentialSelection.GroupSelector.Items.Count > 0;

                var entryOptions = new List<SelectionOption>
                {
                    new("Select entries", string.Empty)
                };
                IReadOnlyList<CredentialEntryListItem> filteredEntries = _credentialsPresenter.GetFilteredEntries();
                foreach (CredentialEntryListItem entry in filteredEntries)
                {
                    entryOptions.Add(new SelectionOption(entry.Label, entry.Id.ToString()));
                }

                bool entryOptionsChanged = !CredentialSelectorItemsMatch(_credentialSelection.EntrySelector, entryOptions);
                if (entryOptionsChanged)
                {
                    _credentialSelection.EntrySelector.BeginUpdate();
                    try
                    {
                        _credentialSelection.EntrySelector.Items.Clear();
                        foreach (SelectionOption option in entryOptions)
                        {
                            _credentialSelection.EntrySelector.Items.Add(option);
                        }

                        SynchronizeCredentialEntrySelection();
                    }
                    finally
                    {
                        _credentialSelection.EntrySelector.EndUpdate();
                    }
                }
                else
                {
                    SynchronizeCredentialEntrySelection();
                }
                _credentialSelection.EntrySelector.Enabled = !_credentialsPresenter.IsLoading && _credentialSelection.EntrySelector.Items.Count > 1;
            }
            finally
            {
                _updatingQuickPanelSelections = false;
            }

            PopulateCredentialAssignedGroupSelector();
            _credentialSelectorsFingerprint = selectorFingerprint;
        }

        private static bool CredentialSelectorItemsMatch(
            TrayDropdown comboBox,
            IReadOnlyList<SelectionOption> expectedOptions)
        {
            if (comboBox.Items.Count != expectedOptions.Count)
            {
                return false;
            }

            for (int index = 0; index < expectedOptions.Count; index++)
            {
                if (comboBox.Items[index] is not SelectionOption currentOption ||
                    !string.Equals(currentOption.Value, expectedOptions[index].Value, StringComparison.Ordinal) ||
                    !string.Equals(currentOption.ToString(), expectedOptions[index].ToString(), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private string BuildCredentialSelectorsFingerprint()
        {
            IReadOnlyList<CredentialEntryListItem> filteredEntries = _credentialsPresenter.GetFilteredEntries();
            string groupFingerprint = string.Join("\u001f", _credentialsPresenter.Groups.Select(group => $"{group.GroupId}\u001e{group.GroupName}"));
            string entryFingerprint = string.Join("\u001f", filteredEntries.Select(entry => $"{entry.Id}\u001e{entry.Label}"));

            return $"{_credentialsPresenter.IsLoading}\u001d{_credentialsPresenter.SelectedGroupId}\u001d{_credentialsPresenter.SelectedEntryId}\u001d{groupFingerprint}\u001d{entryFingerprint}";
        }
    }
}