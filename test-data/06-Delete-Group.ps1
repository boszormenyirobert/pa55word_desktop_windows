. "$PSScriptRoot\_trayapp-message-tools.ps1"

$GroupId = ""

Require-Value -Name "GroupId" -Value $GroupId

$response = Invoke-TrayAppMessage -Payload @{
    rest = "delete"
    resource = "group"
    groupId = $GroupId
}

$state = Load-TestState
$state.groups = @($state.groups | Where-Object { $_.groupId -ne $GroupId })

foreach ($entry in @($state.entries | Where-Object { $_.group -and $_.group.groupId -eq $GroupId })) {
    $entry.credentialGroupId = $null
    $entry.group = $null
}

Save-TestState -State $state

$response | ConvertTo-Json -Depth 12