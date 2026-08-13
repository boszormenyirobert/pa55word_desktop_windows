param(
    [string]$ExePath = "$env:LOCALAPPDATA\ZeroIntrusion\TrayApp\TrayApp.exe",
    [string]$HostName = "com.zerointrusion.trayapp",
    [string]$ExtensionId = "jdkllibgcnaikemfnmahbokkemknbgfl"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Tray app executable was not found at: $ExePath"
}

$nativeHostDir = Join-Path $env:LOCALAPPDATA "ZeroIntrusion\TrayApp\NativeMessaging"
New-Item -ItemType Directory -Path $nativeHostDir -Force | Out-Null

$manifestPath = Join-Path $nativeHostDir "$HostName.json"

$manifest = [ordered]@{
    name = $HostName
    description = "ZeroIntrusion Tray Native Messaging Host"
    path = $ExePath
    type = "stdio"
    allowed_origins = @(
        "chrome-extension://$ExtensionId/"
    )
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$registryKey = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$HostName"
New-Item -Path $registryKey -Force | Out-Null
Set-ItemProperty -Path $registryKey -Name '(default)' -Value $manifestPath

Write-Host "Edge native messaging host registered successfully."
Write-Host "Manifest: $manifestPath"
Write-Host "Registry key: $registryKey"
