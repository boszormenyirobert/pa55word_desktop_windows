param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [string]$InstallRoot = "$env:LOCALAPPDATA\ZeroIntrusion\TrayApp",
    [ValidateSet('Edge', 'Chrome', 'Both')]
    [string]$Browser = 'Edge',
    [string]$HostName = 'com.zerointrusion.trayapp',
    [string]$ExtensionId = 'jdkllibgcnaikemfnmahbokkemknbgfl'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SourceDir)) {
    throw "SourceDir does not exist: $SourceDir"
}

$sourceExePath = Join-Path $SourceDir 'TrayApp.exe'
if (-not (Test-Path -LiteralPath $sourceExePath)) {
    throw "TrayApp.exe was not found in SourceDir: $SourceDir"
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
Copy-Item -Path (Join-Path $SourceDir '*') -Destination $InstallRoot -Recurse -Force

$installedExePath = Join-Path $InstallRoot 'TrayApp.exe'
if (-not (Test-Path -LiteralPath $installedExePath)) {
    throw "Install failed. TrayApp.exe missing at: $installedExePath"
}

$manifestDir = Join-Path $InstallRoot 'NativeMessaging'
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
$manifestPath = Join-Path $manifestDir "$HostName.json"

$manifest = [ordered]@{
    name = $HostName
    description = 'ZeroIntrusion Tray Native Messaging Host'
    path = $installedExePath
    type = 'stdio'
    allowed_origins = @(
        "chrome-extension://$ExtensionId/"
    )
}

$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))

$registryRoots = @()
switch ($Browser) {
    'Edge'   { $registryRoots += 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts' }
    'Chrome' { $registryRoots += 'HKCU:\Software\Google\Chrome\NativeMessagingHosts' }
    'Both' {
        $registryRoots += 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts'
        $registryRoots += 'HKCU:\Software\Google\Chrome\NativeMessagingHosts'
    }
}

foreach ($root in $registryRoots) {
    $keyPath = Join-Path $root $HostName
    New-Item -Path $keyPath -Force | Out-Null
    Set-Item -Path $keyPath -Value $manifestPath
    Write-Host "Registered: $keyPath"
}

Write-Host 'Native messaging host install completed.'
Write-Host "Installed exe: $installedExePath"
Write-Host "Manifest: $manifestPath"
Write-Host "Browser target: $Browser"
