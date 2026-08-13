# Edge Native Messaging registration

This folder contains scripts for registering `TrayApp.exe` as a native messaging host for Microsoft Edge.

## Fixed install location

The scripts assume this executable path:

- `C:\Users\<current-user>\AppData\Local\ZeroIntrusion\TrayApp\TrayApp.exe`

It is resolved at runtime as:

- `$env:LOCALAPPDATA\ZeroIntrusion\TrayApp\TrayApp.exe`

## Host metadata

- Host name: `com.zerointrusion.trayapp`
- Extension id: `jdkllibgcnaikemfnmahbokkemknbgfl`
- Registry hive: `HKCU`
- Browser key: `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.zerointrusion.trayapp`

## Full install (copy + manifest + registry)

Run:

- `powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Install-NativeHost.ps1 -SourceDir "<publish-folder>" -Browser Edge`

Examples:

- Edge only: `powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Install-NativeHost.ps1 -SourceDir "C:\Users\boszo\source\repos\TrayApp\bin\Debug\net8.0-windows" -Browser Edge`
- Edge + Chrome: `powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Install-NativeHost.ps1 -SourceDir "C:\Users\boszo\source\repos\TrayApp\bin\Debug\net8.0-windows" -Browser Both`

## Register only

Run:

- `powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Register-EdgeNativeHost.ps1`

## Unregister

Run:

- `powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Unregister-EdgeNativeHost.ps1`

## Installer integration

Call `Install-NativeHost.ps1` (preferred) or `Register-EdgeNativeHost.ps1` at install/upgrade time.
