param(
    [string]$HostName = "com.zerointrusion.trayapp"
)

$ErrorActionPreference = 'Stop'

$registryKey = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$HostName"
if (Test-Path -LiteralPath $registryKey) {
    Remove-Item -LiteralPath $registryKey -Recurse -Force
    Write-Host "Removed registry key: $registryKey"
}
else {
    Write-Host "Registry key does not exist: $registryKey"
}

$manifestPath = Join-Path $env:LOCALAPPDATA "ZeroIntrusion\TrayApp\NativeMessaging\$HostName.json"
if (Test-Path -LiteralPath $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
    Write-Host "Removed manifest: $manifestPath"
}
else {
    Write-Host "Manifest does not exist: $manifestPath"
}
