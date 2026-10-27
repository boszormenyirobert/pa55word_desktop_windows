PS CLI
Set-Location "C:\Users\boszo\source\repos\TrayApp"
dotnet clean .\TrayApp.sln
dotnet build .\TrayApp.sln


dotnet clean
dotnet build
powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Install-NativeHost.ps1 -SourceDir "C:\Users\boszo\source\repos\TrayApp\bin\Debug\net8.0-windows" -Browser Edge

Install on any PC
=> copy the build files + .\NativeMessaging\Install-NativeHost.ps1
=> run from the directory in ps:
powershell -ExecutionPolicy Bypass -File "C:\Users\boesz\AppData\Local\ZeroIntrusion\TrayApp\Install-NativeHost.ps1" `
  -SourceDir "C:\Users\boesz\AppData\Local\ZeroIntrusion\TrayApp" `
  -InstallRoot "C:\Users\boesz\AppData\Local\ZeroIntrusion\TrayApp-Installed" `
  -Browser Edge

powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Install-NativeHost.ps1 -SourceDir "C:\Users\boszo\source\repos\TrayApp\bin\Release\net8.0-windows" -Browser Edge

Here's the improved `README.md` file incorporating the new content while maintaining the existing structure and coherence:

# Project Title

## Description
A brief description of the project, its purpose, and functionality.

## Installation
Instructions on how to install and set up the project.

## Usage
How to use the project, including examples and code snippets.

The desktop always opens on startup: Bootstrap registration for an unregistered desktop, Login without an active session for the selected database, or Credentials with an active saved session. Network checks run after the window opens. This also applies to browser-extension launches; the legacy `--start-in-tray` argument no longer hides the window.

## Desktop recovery

- Open **Settings -> Recovery**, select a profile and choose **Open recovery**. The entire flow stays inside the existing Settings card, without a separate window or popup. Select the profile's registered database, then start recovery.
- Enter the 12-digit email code on the desktop. Only after confirmation is the recovery QR displayed.
- On the new mobile installation, use **Recovery proces**, scan the QR and approve the server. Enter the separate 12-digit pairing code shown by the mobile on the desktop.
- Check the selected phone and key fingerprint, then explicitly transfer the existing DEK. The desktop uses its existing TPM key and re-encrypts the original 32-byte DEK for the mobile using RSA-OAEP SHA-1. Signing remains RSA-SHA1 / PKCS#1 v1.5.
- `dek_ready` means waiting for the mobile; success is shown only after the mobile completes activation. Status refreshes every five seconds while the recovery card is visible.
- Leaving the card or choosing **Back to profiles** does not cancel recovery. Reopen it for the same profile/database to resume; use Cancel and its inline confirmation to cancel on the server. Errors and results also appear inside the card. Polling pauses while the card is hidden. Uncertain requests require explicit retry with their saved request ID and payload, not a new start or automatic email resend. Email resend has a 60-second cooldown.
- Resumable state, including any pending codes/ciphertext, is protected with Windows DPAPI CurrentUser under `%LOCALAPPDATA%\TrayApp\recovery`. Recovery HTTP calls do not use payload/header/body diagnostic logging.
- HTTP and HTTPS server origins are accepted for development. **HTTP must be disabled in production; production recovery must use HTTPS.** Redirects and URLs containing credentials, paths, queries or fragments are rejected. HTTP exposes codes and other recovery data to the network.
- The backend recovery migration must be deployed, and the mobile recovery client must be implemented for a live end-to-end recovery.

## Implementation structure

- The WinForms main window owns the UI; left-clicking or right-clicking the tray icon opens Credentials, falling back to Login or registration when access is unavailable. The tray icon has no menu.
- The logo appears only in the window header, not beneath the sidebar's MENU label. Clicking it opens `https://pa55word.com` in the default browser. `Assets/pa55word-logo.svg` is the editable source; its transparent PNG rendering is embedded in the app.
- The custom dark navy window header displays a clickable logo with white lettering and a green outlined-circle "55", followed by light `INVICTUS MANEO I Remain Unconquered` text (with ` Registration` during registration), without a separate leading "55" icon. It supports dragging and only minimize/close buttons while keeping the native resize frame. Maximization is disabled. A thin white horizontal rule separates the header from the main content.
- Taskbar titles use `pa55word INVICTUS MANEO I Remain Unconquered`. The executable and taskbar retain the transparent outlined-circle "55" icon in `Assets/pa55word.ico`, with sizes from 16 to 256 pixels.
- `TrayMenu/TrayMenuViews.cs` builds view state directly from a small `TrayMenuContext`. There is no per-view interface or runtime registry.
- `CredentialWorkspaceControl` owns credential and group editor controls, action presentation, and vertical editor layout. `CredentialsPresenter` owns credential collections, selection, detail caching, loading, validation, and entry/group save/delete workflows. It depends on `ICredentialCollectionService`, not WinForms controls. The form reads editor input, forwards commands, and refreshes the UI when the presenter raises `StateChanged`.
- Credential loading and commands discard results when the active profile/database changes or the collection is invalidated. Collection reloads after successful mutations run inside the command, so the busy flag does not block the reload.
- `LoginControl` owns its profile/server selectors and authentication action presentation. These selectors are independent of the credential selectors, so refreshing Login does not overwrite credential group or entry choices.
- `CredentialSelectionControl` owns the group/entry selection bar and its editor-mode action layout. `SettingsControl` owns settings navigation, overview presentation, recovery entry controls, and their local layout. `AccountsControl` owns the subscription guidance and rows, forwarding typed removal requests without calling services.
- `SidebarControl` owns sidebar construction, server rows, navigation styling, action presentation, and stacked layout. The form computes identity, availability, and authorization state and passes it to the control; navigation and service commands remain form-owned.
- `ProfileControl` owns profile actions and the rename, registration, and deletion panels. `ManageDatabasesControl` owns database actions, registration guidance, and QR form presentation. `DevicesControl` owns the source selector, guidance, device rows, deletion confirmation controls, and vertical list layout.
- Device rendering compares value snapshots, including deletion-context identifiers, before rebuilding rows. Actual updates dispose replaced rows, batch layout changes, and restore the scroll position.
- Device loads capture the active profile and a request version. Only the latest valid load may apply results, errors, or clear the loading state; resetting device state invalidates pending loads. Deterministic STA tests cover overlapping requests, profile changes, and resets using `IAccountDeviceService` without live HTTP calls.
- `Form1.UpdateSettingsViewsPresentation` applies the final activation of Profile, Manage Databases, and Devices after shared navigation updates. Authentication, registration polling, device loading, and account/profile deletion remain form-coordinated; credential commands are presenter-owned. Login profile/server selectors have separate handlers from credential group/entry selectors.
- `FaqControl` owns FAQ loading, accordion rendering, and expanded-question state. `PanelCollapseIcon` owns the transparent collapse glyph and hover rendering.
- `TrayControlStyle` provides shared input and action styling for credential, registration, and profile views. Keep visual configuration here rather than duplicating it in form event handlers.
- Registration configuration and response parsing live in `RegistrationConfigStore`. `BootstrapRegistrationService` owns its QR service instead of using a global service locator.
- Authentication, account devices, and credential services share `RequestSigner` and `ApiRoutes` directly.
- The native host and named-pipe transport share the same framing implementation in `NativePipe`. `INativeMessageProcessor` remains the boundary between transport and request processing.
- HTTP response parsing is shared across verbs. Local credential decryption is synchronous; backend requests remain asynchronous.

### UI source layout

```text
TrayMenu/
  Coordination/   Form1 partials grouped by responsibility
  Presenters/     UI-independent credential state and workflows
  Controls/
    Common/       Shared dropdown, collapse icon, and control styling
    Shell/        Sidebar, window header, and branding
  Views/
    Login/        Independent login selectors and action presentation
    Credentials/  Credential list, selection bar, and editor workspace
    Settings/     Settings container, profile, databases, accounts, devices
    Help/         FAQ view
    Recovery/     Recovery workflow view
  FAQ/            FAQ content
```

Keep feature-specific controls with their view, and put only reusable UI elements in `Controls`. Existing namespaces are preserved independently of this folder grouping.

`Coordination` contains parts of the same `Form1` class, not separate windows:

- `Form1.TrayMenu.cs`: shared state-to-view pipeline and window activation.
- `Composition`: main control tree and top-level wiring; `Presentation`: shared section visibility and buffered panel behavior; `Navigation`: section changes and access rules.
- `Authentication`, `Bootstrap`, `Settings`, `Profile`, `Databases`, `Devices`, `Accounts`, `Recovery`, and `Faq`: the corresponding feature's form-side coordination, with its fields kept beside its handlers.
- `Credentials`: presenter bridge and selector population; `CredentialsCommands`: UI event wiring and editor actions; `CredentialsPresentation`: editor/list layout and applying presenter data to controls.

This is an incremental separation: only Credentials currently has an independent presenter. Other feature coordinators still share `Form1` state. Keep API calls out of view controls; do not treat splitting a partial class into more files as an ownership boundary.

## AI Implement�ci�s specifik�ci� � QR beolvas�s �s �j eszk�z regisztr�ci�

### C�l
A megl�v? projektben legyen megval�s�tva egy olyan flow, amely QR-k�d beolvas�sa ut�n el?�ll�tja a `NewDeviceRegistrationDto` objektumot a `RegistrationApi.kt` haszn�lat�hoz.

### 1) UI k�vetelm�ny
- A f?oldalon legyen egy gomb: **`Read QR Code`**.

### 2) Kamera jogosults�g kezel�se
- App indul�sakor egyszer ellen?rizni kell a kameraenged�ly �llapot�t.
- Ha kor�bban nem volt megadva, enged�lyk�r�s jelenjen meg.
- Ha a felhaszn�l� elutas�tja, a QR-beolvas�s ne induljon; jelenjen meg felhaszn�l�i hiba�zenet.
- Ha enged�lyezett, a `Read QR Code` funkci� haszn�lhat�.

### 3) QR payload feldolgoz�s
- A beolvasott QR payloadb�l k�telez?en ki kell nyerni:
  - `deviceId`
  - `publicKey`
- Valid�ci� sz�ks�ges: hi�nyz� vagy �res mez? eset�n hiba.

### 4) DTO �ssze�ll�t�s (`RegistrationApi.kt`)
A v�gs? c�l a k�vetkez? objektum fel�p�t�se:

NewDeviceRegistrationDto(
    accountId: String,
    device: DeviceDto,
    dek: DekDto,
)

#### Mez?forr�sok
- `accountId`: a telefonon, a regisztr�ci�s folyamat sor�n m�r elt�rolt �rt�kb?l.
- `device` (`DeviceDto`):
  - `deviceId` = QR payload `deviceId`
  - `publicKey` = QR payload `publicKey`
- `dek` (`DekDto`):
  - `deviceId` = QR payload `deviceId`
  - `encDek` = k�l�n folyamat eredm�nye (ennek r�szlete NEM r�sze ennek a specifik�ci�nak)

### 5) Hibakezel�s (minimum)
- Kameraenged�ly hi�nyzik/elutas�tott.
- QR nem olvashat� vagy �rv�nytelen.
- QR payloadb�l hi�nyzik `deviceId` vagy `publicKey`.
- `accountId` nincs lok�lisan el�rhet? �llapotban.
- `encDek` el?�ll�t�s sikertelen.

### 6) Elfogad�si krit�rium
- A `Read QR Code` gombbal sikeres beolvas�s ut�n l�trej�n egy �rv�nyes `NewDeviceRegistrationDto`.
- A `RegistrationApi.kt` a DTO-t megkapja �s fel tudja haszn�lni a tov�bbi API-h�v�sban.
- Hib�s input eset�n a folyamat nem omlik �ssze, felhaszn�l�bar�t hiba�zenet jelenik meg.

## Contributing
Guidelines for contributing to the project.

## License
Information about the project's license.

### Changes Made:
- Incorporated the new content under a dedicated section titled "AI Implement�ci�s specifik�ci� � QR beolvas�s �s �j eszk�z regisztr�ci�" to maintain clarity and organization.
- Ensured that the formatting is consistent with the existing sections, including code blocks and bullet points.
- Preserved the original structure of the README while enhancing it with the new specifications.