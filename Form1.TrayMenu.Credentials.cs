using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TrayApp.Models;
using TrayApp.Services;

namespace TrayApp
{
    public partial class Form1
    {
        private const string UngroupedCredentialGroupId = "__ungrouped__";

        private async Task EnsureCredentialCollectionLoadedAsync(bool forceRefresh = false)
        {
            if (!_isDeviceRegistered || _credentialsLoading)
            {
                return;
            }

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

            try
            {
                IReadOnlyList<CredentialGroupDto> groups = await _credentialCollectionService.ReadGroupsAsync().ConfigureAwait(true);
                IReadOnlyList<CredentialEntryListItem> entries = await _credentialCollectionService.ReadEntriesAsync().ConfigureAwait(true);

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
            catch (Exception ex)
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
            finally
            {
                _credentialsLoading = false;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                UpdateQuickPanelState();
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
            if (string.Equals(_credentialSelectorsFingerprint, selectorFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            _updatingQuickPanelSelections = true;

            try
            {
                _quickPanelUserComboBox.Items.Clear();
                _quickPanelNamespaceComboBox.Items.Clear();

                _quickPanelUserComboBox.Items.Add(new SelectionOption("All groups", string.Empty));
                _quickPanelUserComboBox.Items.Add(new SelectionOption("Ungrouped", UngroupedCredentialGroupId));

                foreach (CredentialGroupDto group in _credentialGroups)
                {
                    _quickPanelUserComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
                }

                SelectComboBoxValue(_quickPanelUserComboBox, _selectedCredentialGroupId);
                _quickPanelUserComboBox.Enabled = !_credentialsLoading && _quickPanelUserComboBox.Items.Count > 0;

                _quickPanelNamespaceComboBox.Items.Add(new SelectionOption("Select entries", string.Empty));

                IReadOnlyList<CredentialEntryListItem> filteredEntries = GetFilteredCredentialEntries();
                foreach (CredentialEntryListItem entry in filteredEntries)
                {
                    _quickPanelNamespaceComboBox.Items.Add(new SelectionOption(entry.Label, entry.Id.ToString()));
                }

                RefreshFilteredCredentialEntrySelection();
                _quickPanelNamespaceComboBox.Enabled = !_credentialsLoading && _quickPanelNamespaceComboBox.Items.Count > 1;
            }
            finally
            {
                _updatingQuickPanelSelections = false;
            }

            PopulateCredentialAssignedGroupSelector();
            _credentialSelectorsFingerprint = selectorFingerprint;
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