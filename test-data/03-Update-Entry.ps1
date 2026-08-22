. "$PSScriptRoot\_trayapp-message-tools.ps1"

$EntryId = 0
$Label = ""
$Domain = ""
$Username = ""
$Password = ""
$Description = ""
$GroupId = ""

Require-Value -Name "EntryId" -Value $EntryId
Require-Value -Name "Label" -Value $Label
Require-Value -Name "Domain" -Value $Domain
Require-Value -Name "Username" -Value $Username
Require-Value -Name "Password" -Value $Password

$response = Invoke-TrayAppMessage -Payload @{
    rest = "update"
    id = $EntryId
    label = $Label
    groupId = $(if ([string]::IsNullOrWhiteSpace($GroupId)) { $null } else { $GroupId })
    registry = @{
        domain = $Domain
        username = $Username
        password = $Password
        description = $Description
    }
}

$state = Load-TestState
$entry = $state.entries | Where-Object { $_.id -eq $EntryId } | Select-Object -First 1

if ($null -ne $entry) {
    $entry.label = $response.label
    $entry.credentialGroupId = $response.credentialGroupId
    $entry.group = $response.group
    $entry.registry = @{
        domain = $Domain
        username = $Username
        password = $Password
        description = $Description
    }
    Save-TestState -State $state
}

$response | ConvertTo-Json -Depth 12