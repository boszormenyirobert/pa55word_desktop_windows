. "$PSScriptRoot\_trayapp-message-tools.ps1"

$groupNames = @(
    "Finance",
    "Engineering",
    "Operations",
    "Human Resources",
    "Sales"
)

$serviceTemplates = @(
    [ordered]@{ Label = "GitHub"; Domain = "github.com"; Description = "source control" },
    [ordered]@{ Label = "Jira"; Domain = "jira.company.local"; Description = "issue tracking" }
)

$firstNames = @(
    "anna","david","emma","daniel","olivia","martin","sofia","peter","luca","kevin",
    "nora","adam","linda","tamas","julia","mate","rebeka","gergo","sara","andras"
)

$lastNames = @(
    "kovacs","nagy","farkas","szabo","toth","molnar","horvath","varga","balogh","kiss",
    "lakatos","papp","jakab","bene","fodor","simon","feher","halasz","veres","orosz"
)

$createdGroups = New-Object System.Collections.Generic.List[object]
$createdEntries = New-Object System.Collections.Generic.List[object]

function New-LongDescription {
    param(
        [Parameter(Mandatory = $true)]
        [string]$GroupName,

        [Parameter(Mandatory = $true)]
        [string]$ServiceLabel,

        [Parameter(Mandatory = $true)]
        [string]$ServiceDescription,

        [Parameter(Mandatory = $true)]
        [string]$Username,

        [Parameter(Mandatory = $true)]
        [int]$EntryNumber
    )

    $baseText = @(
        "Credential dataset entry $EntryNumber.",
        "Department: $GroupName.",
        "System: $ServiceLabel.",
        "Purpose: $ServiceDescription.",
        "Assigned username: $Username.",
        "This record is part of the TrayApp seeded test dataset used for bulk native-messaging validation.",
        "It intentionally contains a long description body so backend and client flows can be tested with large realistic text payloads.",
        "The text repeats structured operational notes, ownership hints, access review context, and audit-friendly phrases."
    ) -join ' '

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append($baseText)

    $sentenceIndex = 1
    while ($builder.Length -lt 8000) {
        [void]$builder.Append(' ')
        [void]$builder.AppendFormat(
            "Operational note {0}: {1} uses {2} under the {3} group, ownership review cycle remains active, credential rotation evidence is retained, and test payload expansion is intentional for integration coverage.",
            $sentenceIndex,
            $Username,
            $ServiceLabel,
            $GroupName)
        $sentenceIndex++
    }

    return $builder.ToString().Substring(0, [Math]::Max(8000, $builder.Length))
}

$groupCount = $groupNames.Count
$entryCount = $groupCount * $serviceTemplates.Count

Write-Host "Creating $groupCount groups..."

for ($groupIndex = 0; $groupIndex -lt $groupNames.Count; $groupIndex++) {
    $groupName = $groupNames[$groupIndex]
    $groupId = [Guid]::NewGuid().ToString()

    $groupResponse = Invoke-TrayAppMessage -Payload @{
        rest = "create"
        resource = "group"
        groupId = $groupId
        groupName = $groupName
    }

    $createdGroups.Add([ordered]@{
        id = $groupResponse.id
        groupId = $groupResponse.groupId
        groupName = $groupResponse.groupName
    })

    Write-Host ("[{0}/{1}] Group created: {2} ({3})" -f ($groupIndex + 1), $groupCount, $groupResponse.groupName, $groupResponse.groupId)
}

Write-Host "Creating $entryCount entries..."

for ($groupIndex = 0; $groupIndex -lt $createdGroups.Count; $groupIndex++) {
    $group = $createdGroups[$groupIndex]

    for ($entryOffset = 0; $entryOffset -lt $serviceTemplates.Count; $entryOffset++) {
        $entryNumber = ($groupIndex * $serviceTemplates.Count) + $entryOffset + 1
        $service = $serviceTemplates[$entryOffset]
        $firstName = $firstNames[($entryNumber - 1) % $firstNames.Count]
        $lastName = $lastNames[($entryNumber - 1) % $lastNames.Count]
        $username = "$firstName.$lastName"
        $label = "$($group.groupName) $($service.Label)"
        $description = New-LongDescription `
            -GroupName $group.groupName `
            -ServiceLabel $service.Label `
            -ServiceDescription $service.Description `
            -Username $username `
            -EntryNumber $entryNumber
        $registry = [ordered]@{
            domain = $service.Domain
            username = $username
            password = New-StrongPassword -Prefix ($service.Label -replace "\\s+", "") -Index $entryNumber
            description = $description
        }

        $entryResponse = Invoke-TrayAppMessage -Payload @{
            rest = "create"
            label = $label
            groupId = $group.groupId
            registry = $registry
        }

        $createdEntries.Add([ordered]@{
            id = $entryResponse.id
            label = $entryResponse.label
            credentialGroupId = $entryResponse.credentialGroupId
            group = $entryResponse.group
            registry = $registry
        })

        Write-Host ("[{0}/{1}] Entry created: {2}" -f $entryNumber, $entryCount, $entryResponse.label)
    }
}

$state = [ordered]@{
    createdAt = [DateTimeOffset]::UtcNow.ToString("o")
    groups = $createdGroups
    entries = $createdEntries
}

Save-TestState -State $state

Write-Host "Seed finished."
Write-Host "State saved to $script:StateFilePath"