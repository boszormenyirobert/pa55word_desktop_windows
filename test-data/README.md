# Test Data Scripts

These scripts talk to the already running TrayApp instance through the named pipe used by native messaging.

Prerequisites:

- TrayApp must already be running.
- The active TrayApp user must already be authenticated.
- The backend credential-store and credential-store/group endpoints must be reachable.

Files:

- `01-Seed-TestData.ps1`: creates 20 groups and 100 credential entries, with every entry assigned to one of the created groups and with a generated description of at least 8000 characters.
- `02-Read-Entries.ps1`: reads all entries from TrayApp and lets you filter by `EntryId`, `LabelContains`, or `GroupId` by editing the variables at the top of the file.
- `03-Update-Entry.ps1`: updates one entry after you set the required variables at the top of the file.
- `04-Delete-Entry.ps1`: deletes one entry after you set `EntryId`.
- `05-Rename-Group.ps1`: renames a group after you set `GroupId` and `GroupName`.
- `06-Delete-Group.ps1`: deletes a group after you set `GroupId`.
- `_trayapp-message-tools.ps1`: shared named-pipe helper used by the scripts.

State file:

- `trayapp-test-state.json` is created by the seed script.
- It stores the created group and entry identifiers so later scripts can look them up if needed.

Usage examples:

- `powershell -ExecutionPolicy Bypass -File .\test-data\01-Seed-TestData.ps1`
- `powershell -ExecutionPolicy Bypass -File .\test-data\02-Read-Entries.ps1`
- Edit variables at the top of `03-Update-Entry.ps1`, then run it.
- Edit variables at the top of `05-Rename-Group.ps1`, then run it.

Notes:

- Your list contained two update-style items. This package keeps a separate read script as the practical lookup step before targeted update/delete operations.
- Group rename and delete scripts identify the target group by `groupId`.