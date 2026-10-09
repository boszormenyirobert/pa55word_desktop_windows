using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;

namespace TrayApp
{
    public partial class Form1
    {
        private const string UngroupedCredentialGroupId = "__ungrouped__";

        private void SelectCredentialFromList(CredentialEntryListItem entry)
        {
            _selectedCredentialGroupId = string.IsNullOrWhiteSpace(entry.GroupId)
                ? UngroupedCredentialGroupId
                : entry.GroupId;
            _selectedCredentialEntryId = entry.Id.ToString();
            _credentialWorkspaceMode = CredentialWorkspaceMode.ViewEntry;
            _deletionConfirmation = CredentialDeletionConfirmation.None;
            ClearCredentialEditorStatus();
            ClearGroupEditorStatus();
            PopulateCredentialSelectors();
            RefreshCredentialQuickPanelState();
        }

        private async Task EnsureCredentialCollectionLoadedAsync(bool forceRefresh = false)
        {
            if (!_isDeviceRegistered || (_credentialsLoading && !_credentialLoadDeferredUntilBackendRefresh))
            {
                return;
            }

            _credentialLoadDeferredUntilBackendRefresh = false;

            string cacheKey = BuildCredentialCollectionCacheKey();
            if (!forceRefresh &&
                !string.IsNullOrWhiteSpace(_credentialCollectionCacheKey) &&
                string.Equals(_credentialCollectionCacheKey, cacheKey, StringComparison.Ordinal) &&
                _credentialCollectionLoaded &&
                string.IsNullOrWhiteSpace(_credentialsErrorMessage))
            {
                return;
            }

            _credentialsLoading = true;
            _credentialsErrorMessage = string.Empty;
            UpdateQuickPanelState();
            bool selectionChangedDuringLoad = false;

            try
            {
                IReadOnlyList<CredentialGroupDto> groups = await _credentialCollectionService.ReadGroupsAsync().ConfigureAwait(true);
                IReadOnlyList<CredentialEntryListItem> entries = await _credentialCollectionService.ReadEntriesAsync().ConfigureAwait(true);

                selectionChangedDuringLoad = !string.Equals(cacheKey, BuildCredentialCollectionCacheKey(), StringComparison.Ordinal);
                if (!selectionChangedDuringLoad)
                {
                    _credentialGroups = groups
                        .OrderBy(group => group.GroupName, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                    _credentialEntries = entries
                        .OrderBy(entry => entry.Label, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                    _credentialEntryDetails.Clear();
                    _credentialDetailLoading = false;
                    _credentialCollectionCacheKey = cacheKey;
                    _credentialCollectionLoaded = true;

                    if (!string.IsNullOrWhiteSpace(_selectedCredentialGroupId) &&
                        !string.Equals(_selectedCredentialGroupId, UngroupedCredentialGroupId, StringComparison.Ordinal) &&
                        !_credentialGroups.Any(group => string.Equals(group.GroupId, _selectedCredentialGroupId, StringComparison.Ordinal)))
                    {
                        _selectedCredentialGroupId = string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(_selectedManagedGroupId))
                    {
                        _selectedManagedGroupId = _credentialGroups.FirstOrDefault()?.GroupId ?? string.Empty;
                    }

                    RefreshFilteredCredentialEntrySelection();
                }
            }
            catch (Exception ex)
            {
                selectionChangedDuringLoad = !string.Equals(cacheKey, BuildCredentialCollectionCacheKey(), StringComparison.Ordinal);
                if (!selectionChangedDuringLoad)
                {
                    _credentialGroups = Array.Empty<CredentialGroupDto>();
                    _credentialEntries = Array.Empty<CredentialEntryListItem>();
                    _credentialEntryDetails.Clear();
                    _selectedCredentialGroupId = string.Empty;
                    _selectedCredentialEntryId = string.Empty;
                    _credentialDetailLoading = false;
                    _credentialCollectionLoaded = false;
                    _credentialsErrorMessage = ex.Message;
                    Debug.WriteLine($"[TRAY] Credential collection load failed: {ex.Message}");
                }
            }
            finally
            {
                _credentialsLoading = false;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                if (!selectionChangedDuringLoad)
                {
                    UpdateQuickPanelState();
                }
            }

            if (selectionChangedDuringLoad &&
                _trayMenuController?.ActiveSection == TrayApp.TrayMenu.TrayMenuSection.Credentials &&
                _authenticationService.IsAuthorizedForActiveNamespace)
            {
                await EnsureCredentialCollectionLoadedAsync(forceRefresh: true).ConfigureAwait(true);
            }
        }

        private string BuildCredentialCollectionCacheKey()
        {
            string activeUserId = AppEnvironment.GetActiveUserId() ?? string.Empty;
            string activeNamespaceId = AppEnvironment.GetActiveNamespaceId() ?? string.Empty;
            return $"{activeUserId}|{activeNamespaceId}";
        }

        private IReadOnlyList<CredentialEntryListItem> GetFilteredCredentialEntries()
        {
            if (string.IsNullOrWhiteSpace(_selectedCredentialGroupId))
            {
                return _credentialEntries;
            }

            if (string.Equals(_selectedCredentialGroupId, UngroupedCredentialGroupId, StringComparison.Ordinal))
            {
                return _credentialEntries
                    .Where(entry => string.IsNullOrWhiteSpace(entry.GroupId))
                    .ToArray();
            }

            return _credentialEntries
                .Where(entry => string.Equals(entry.GroupId, _selectedCredentialGroupId, StringComparison.Ordinal))
                .ToArray();
        }

        private string ResolveSelectedCredentialGroupLabel()
        {
            if (string.IsNullOrWhiteSpace(_selectedCredentialGroupId))
            {
                return string.Empty;
            }

            if (string.Equals(_selectedCredentialGroupId, UngroupedCredentialGroupId, StringComparison.Ordinal))
            {
                return "Ungrouped";
            }

            return _credentialGroups
                .FirstOrDefault(group => string.Equals(group.GroupId, _selectedCredentialGroupId, StringComparison.Ordinal))
                ?.GroupName ?? string.Empty;
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
                foreach (CredentialGroupDto group in _credentialGroups)
                {
                    groupOptions.Add(new SelectionOption(group.GroupName, group.GroupId));
                }

                bool groupOptionsChanged = !CredentialSelectorItemsMatch(_quickPanelUserComboBox, groupOptions);
                if (groupOptionsChanged)
                {
                    _quickPanelUserComboBox.BeginUpdate();
                    try
                    {
                        _quickPanelUserComboBox.Items.Clear();
                        foreach (SelectionOption option in groupOptions)
                        {
                            _quickPanelUserComboBox.Items.Add(option);
                        }

                        SelectComboBoxValue(_quickPanelUserComboBox, _selectedCredentialGroupId);
                    }
                    finally
                    {
                        _quickPanelUserComboBox.EndUpdate();
                    }
                }
                else
                {
                    SelectComboBoxValue(_quickPanelUserComboBox, _selectedCredentialGroupId);
                }
                _quickPanelUserComboBox.Enabled = _quickPanelUserComboBox.Items.Count > 0;

                var entryOptions = new List<SelectionOption>
                {
                    new("Select entries", string.Empty)
                };
                IReadOnlyList<CredentialEntryListItem> filteredEntries = GetFilteredCredentialEntries();
                foreach (CredentialEntryListItem entry in filteredEntries)
                {
                    entryOptions.Add(new SelectionOption(entry.Label, entry.Id.ToString()));
                }

                bool entryOptionsChanged = !CredentialSelectorItemsMatch(_quickPanelNamespaceComboBox, entryOptions);
                if (entryOptionsChanged)
                {
                    _quickPanelNamespaceComboBox.BeginUpdate();
                    try
                    {
                        _quickPanelNamespaceComboBox.Items.Clear();
                        foreach (SelectionOption option in entryOptions)
                        {
                            _quickPanelNamespaceComboBox.Items.Add(option);
                        }

                        RefreshFilteredCredentialEntrySelection();
                    }
                    finally
                    {
                        _quickPanelNamespaceComboBox.EndUpdate();
                    }
                }
                else
                {
                    RefreshFilteredCredentialEntrySelection();
                }
                _quickPanelNamespaceComboBox.Enabled = !_credentialsLoading && _quickPanelNamespaceComboBox.Items.Count > 1;
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
            IReadOnlyList<CredentialEntryListItem> filteredEntries = GetFilteredCredentialEntries();
            string groupFingerprint = string.Join("\u001f", _credentialGroups.Select(group => $"{group.GroupId}\u001e{group.GroupName}"));
            string entryFingerprint = string.Join("\u001f", filteredEntries.Select(entry => $"{entry.Id}\u001e{entry.Label}"));

            return $"{_credentialsLoading}\u001d{_selectedCredentialGroupId}\u001d{_selectedCredentialEntryId}\u001d{groupFingerprint}\u001d{entryFingerprint}";
        }

        private void RefreshFilteredCredentialEntrySelection()
        {
            IReadOnlyList<CredentialEntryListItem> filteredEntries = GetFilteredCredentialEntries();
            if (filteredEntries.Count == 0)
            {
                _selectedCredentialEntryId = string.Empty;
                SelectComboBoxValue(_quickPanelNamespaceComboBox, string.Empty);
                return;
            }

            if (filteredEntries.Any(entry => string.Equals(entry.Id.ToString(), _selectedCredentialEntryId, StringComparison.Ordinal)))
            {
                SelectComboBoxValue(_quickPanelNamespaceComboBox, _selectedCredentialEntryId);
                return;
            }

            _selectedCredentialEntryId = string.Empty;
            SelectComboBoxValue(_quickPanelNamespaceComboBox, string.Empty);
        }
    }
}