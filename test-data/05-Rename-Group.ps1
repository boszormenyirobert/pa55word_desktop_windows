. "$PSScriptRoot\_trayapp-message-tools.ps1"

$GroupId = ""
$GroupName = ""

Require-Value -Name "GroupId" -Value $GroupId
Require-Value -Name "GroupName" -Value $GroupName

$response = Invoke-TrayAppMessage -Payload @{
    rest = "update"
    resource = "group"
    groupId = $GroupId
    groupName = $GroupName
}

$state = Load-TestState
$group = $state.groups | Where-Object { $_.groupId -eq $GroupId } | Select-Object -First 1

if ($null -ne $group) {
    $group.id = $response.id
    $group.groupName = $response.groupName
}

foreach ($entry in @($state.entries | Where-Object { $_.group -and $_.group.groupId -eq $GroupId })) {
    if ($entry.group) {
        $entry.group.groupName = $response.groupName
    }
}

Save-TestState -State $state

$response | ConvertTo-Json -Depth 12