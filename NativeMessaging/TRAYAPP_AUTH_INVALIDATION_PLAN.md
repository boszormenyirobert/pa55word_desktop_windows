# TrayApp Auth Invalidation Plan

Ez a dokumentum a TrayApp oldali implementációs tervet írja le ahhoz a működéshez, hogy a tray folyamatosan ellenőrizze az auth állapotot, és invalid állapot esetén az extension azonnal bezárható legyen.

## Cél

- A TrayApp háttérben rendszeresen ellenőrizze, hogy van-e még aktív attestation vagy érvényes auth állapot.
- Ha az auth továbbra is érvényes, ne történjen semmi.
- Ha az auth már nem érvényes, a TrayApp azonnal invalidálja a lokális sessiont.
- A TrayApp adjon olyan state választ az extensionnek, amelyből az extension egyértelműen tudja, hogy az érzékeny UI-t azonnal zárni kell.

## Jelenlegi helyzet

- Az auth állapot ellenőrzése jelenleg kérésvezérelt a `AuthenticationService` belsejében történik.
- A native messaging jelenleg request-response alapú.
- A TrayApp most nem tud spontán, push jellegű üzenetet küldeni az extensionnek.

Érintett jelenlegi fájlok:

- [Services/AuthenticationService.cs](Services/AuthenticationService.cs)
- [Services/BrowserCommandHandler.cs](Services/BrowserCommandHandler.cs)
- [Services/AuthorizationSessionStore.cs](Services/AuthorizationSessionStore.cs)
- [Form1.cs](Form1.cs)
- [Models/MessagingAction.cs](Models/MessagingAction.cs)

## Javasolt architektúra

### 1. Új monitor service

Új komponens:

- `AuthStateMonitorService`

Feladata:

- háttér task vagy timer indítása
- 5 másodpercenként auth state ellenőrzés
- explicit invalid válasz esetén session revoke
- extension-close jelzés állapotba írása

Javasolt poll intervallum:

- `5s`

## 2. Közös in-memory session state

Új komponens:

- `ExtensionSessionStateStore`

Javasolt mezők:

```csharp
internal sealed class ExtensionSessionState
{
    public bool Authorized { get; set; }
    public bool CloseExtensionRequested { get; set; }
    public long AuthVersion { get; set; }
    public DateTimeOffset? InvalidatedAtUtc { get; set; }
    public string? Reason { get; set; }
}
```

Feladata:

- auth monitor által írt állapot tárolása
- BrowserCommandHandler számára olvasható state biztosítása
- új login után state reset

## 3. AuthenticationService bővítés

Javasolt új publikus metódus:

- `CheckAuthorizationStateAsync()`

Elvárt viselkedés:

- ne indítson interaktív login flow-t
- csak ellenőrizze az aktuális auth állapotot
- explicit invalid esetet különítse el átmeneti hálózati hibától

Javasolt eredménytípus:

```csharp
internal sealed class AuthorizationHealthResult
{
    public bool Authorized { get; init; }
    public bool ExplicitlyInvalid { get; init; }
    public bool TransientFailure { get; init; }
    public string? Reason { get; init; }
}
```

## 4. Background auth monitor működés

Minden ciklusban:

1. auth state lekérdezése
2. ha `Authorized == true`, nem változtat semmit
3. ha `ExplicitlyInvalid == true`:
   - `RevokeAuthorization()`
   - `CloseExtensionRequested = true`
   - `Authorized = false`
   - `AuthVersion++`
   - `Reason = "authorization_revoked"` vagy hasonló érték
4. ha `TransientFailure == true`:
   - ne zárja be az extensiont automatikusan
   - opcionális logolás

## 5. Új native messaging session-state végpont

Új request contract a TrayApp felé:

```json
{
  "rest": "state",
  "resource": "session"
}
```

Javasolt success response:

```json
{
  "authorized": true,
  "closeExtension": false,
  "authVersion": 3,
  "reason": null
}
```

Invalid állapotban:

```json
{
  "authorized": false,
  "closeExtension": true,
  "authVersion": 4,
  "reason": "authorization_revoked"
}
```

## 6. BrowserCommandHandler bővítés

Szükséges változások:

- új `state` action támogatása
- új `session` resource támogatása
- külön handler a session-state response visszaadására

Fontos szabály:

- a session-state végpont invalid auth állapotban is válaszoljon
- a credential CRUD hívások invalid auth esetén továbbra is bukjanak

## 7. Form1 integráció

Javasolt indítási pont:

- registered mode aktiválásakor induljon a monitor

Javasolt leállítási pont:

- app exit
- logout
- user switch esetén monitor reset vagy újraindítás

Valószínű érintett hely:

- [Form1.cs](Form1.cs)

## 8. AuthorizationSessionStore használat

Meglévő state alapján:

- auth session revoke történjen továbbra is itt
- extension close flag ne ide kerüljön fájlba első körben

Indok:

- az extension bezárás egy rövid élettartamú runtime state
- ezt nem szükséges perzisztálni diszkre első körben

## 9. Hiba- és policy szabályok

### Explicit invalid eset

Példák:

- auth state endpoint `active = false`
- `403`
- `404`
- revoke állapot

Elvárt reakció:

- session revoke
- extension close kérés

### Tranziens hiba eset

Példák:

- timeout
- DNS hiba
- backend átmeneti elérhetetlenség

Elvárt reakció:

- ne zárja be az extensiont automatikusan
- maradjon a korábbi state
- logolja az eseményt

## 10. Implementációs sorrend

1. `ExtensionSessionStateStore` létrehozása
2. `AuthenticationService` health-check metódus hozzáadása
3. `AuthStateMonitorService` létrehozása
4. `BrowserCommandHandler` session-state bővítése
5. `MessagingAction` és resource bővítése
6. `Form1` indítási és leállítási integráció
7. célzott teszt script vagy smoke flow hozzáadása

## 11. Elfogadási kritériumok

1. A TrayApp 5 másodperces periódussal ellenőrzi az auth state-et.
2. Invalid auth esetén a lokális session revoke megtörténik.
3. A session-state native message válasz `closeExtension = true` értéket ad explicit invalid esetben.
4. Tranziens backend hiba önmagában nem zárja be az extensiont.
5. Új login után a close-state visszaáll normálra.