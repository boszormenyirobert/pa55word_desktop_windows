


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