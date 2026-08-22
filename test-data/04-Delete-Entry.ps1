. "$PSScriptRoot\_trayapp-message-tools.ps1"

$EntryId = 0

Require-Value -Name "EntryId" -Value $EntryId

$response = Invoke-TrayAppMessage -Payload @{
    rest = "delete"
    id = $EntryId
}

$state = Load-TestState
$state.entries = @($state.entries | Where-Object { $_.id -ne $EntryId })
Save-TestState -State $state

$response | ConvertTo-Json -Depth 12