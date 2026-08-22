. "$PSScriptRoot\_trayapp-message-tools.ps1"

$EntryId = $null
$LabelContains = ""
$GroupId = ""

$entries = Invoke-TrayAppMessage -Payload @{
    rest = "read"
}

$filteredEntries = @($entries)

if ($null -ne $EntryId) {
    $filteredEntries = @($filteredEntries | Where-Object { $_.id -eq $EntryId })
}

if (-not [string]::IsNullOrWhiteSpace($LabelContains)) {
    $filteredEntries = @($filteredEntries | Where-Object { $_.label -like "*$LabelContains*" })
}

if (-not [string]::IsNullOrWhiteSpace($GroupId)) {
    $filteredEntries = @($filteredEntries | Where-Object { $_.group -and $_.group.groupId -eq $GroupId })
}

$filteredEntries | ConvertTo-Json -Depth 12