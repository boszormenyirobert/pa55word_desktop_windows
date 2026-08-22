# Angular Extension Auth Invalidation Plan

Ez a dokumentum az Angular alapú browser extension oldali implementációs tervet írja le ahhoz a működéshez, hogy invalid auth esetén az érzékeny adatok azonnal eltűnjenek, és az extension bezárható legyen.

## Cél

- Az extension folyamatosan tudja, hogy a TrayApp szerint még érvényes-e az auth.
- Ha az auth érvényes, az extension normálisan működik tovább.
- Ha az auth invalid lesz, az extension:
  - törölje a memóriában tartott érzékeny adatokat
  - szakítsa meg a futó adatlekéréseket
  - zárja be a popupot vagy vigye lockolt állapotba a felületet

## Javasolt működési modell

Az extension a TrayAppot polling jelleggel kérdezi le egy lightweight session-state requesttel.

Javasolt request:

```json
{
  "rest": "state",
  "resource": "session"
}
```

Javasolt response:

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

## Javasolt Angular struktúra

### 1. Native messaging service bővítés

Meglévő vagy új service:

- `NativeMessagingService`

Új metódus:

```ts
getSessionState(): Observable<SessionStateResponse>
```

Javasolt interfész:

```ts
export interface SessionStateResponse {
  authorized: boolean;
  closeExtension: boolean;
  authVersion: number;
  reason: string | null;
}
```

## 2. Session watchdog service

Új service:

- `SessionWatchdogService`

Feladata:

- polling indítása, amikor érzékeny adat látható
- polling leállítása, amikor az érzékeny nézet bezárul
- invalid auth esetén központi clear + close triggerelése

Javasolt polling gyakoriság:

- `1000ms`

Javasolt RxJS felépítés:

```ts
interval(1000).pipe(
  switchMap(() => this.nativeMessagingService.getSessionState()),
  takeUntil(this.destroy$)
)
```

## 3. Sensitive state clear service vagy facade

Ha van store vagy facade réteg, kell egy központi clear művelet.

Példák:

- `clearCredentials()`
- `clearSelectedCredential()`
- `clearCredentialGroups()`
- `clearSearchResults()`

Fontos szabály:

- bezárás előtt a state clear mindig fusson le

## 4. UI bezárási stratégia

### Popup alapú extension

Javasolt reakció:

```ts
window.close()
```

### Tartósabb UI, sidepanel vagy route-alapú nézet

Javasolt reakció:

- redirect `locked` vagy `session-expired` képernyőre
- közben minden érzékeny state törlése

## 5. Watchdog aktiválási szabály

A watchdog ne fusson mindig feleslegesen.

Javasolt aktiválás:

- credential lista nézet
- credential detail nézet
- grouphoz kötött érzékeny adat nézet
- bármilyen decryptelt credential adatot mutató komponens

Javasolt leállítás:

- popup bezárás
- route elhagyása
- logout

## 6. Hibaágak

### A TrayApp invalid authot jelez

Elvárt reakció:

1. minden érzékeny state clear
2. in-flight request cancel
3. popup close vagy locked route

### A TrayApp nem elérhető

Itt policy döntés kell.

Ajánlott első körös stratégia:

- ne zárjon rögtön csak attól, hogy egy polling hibázik
- több egymást követő hiba után menjen lockolt állapotba

Javasolt minimál policy:

- `3` egymást követő watchdog hiba után lock vagy close

### Az authVersion megváltozik

Ez arra használható, hogy az extension akkor is reagáljon, ha a státusz logikailag visszaállt, de session reset történt.

Ajánlott reakció:

- ha változik és közben `closeExtension = true`, azonnali clear + close
- ha változik és `authorized = true`, opcionálisan session refresh

## 7. Angular komponens integráció

Érintett rétegek javasoltan:

1. native messaging adapter service
2. credential facade vagy state service
3. popup root component vagy credential shell component
4. optional route guard

Route guard javaslat:

- ha lockolt session állapotban vagyunk, ne engedjen érzékeny route-ra navigálni

## 8. Implementációs sorrend

1. `SessionStateResponse` típus létrehozása
2. `NativeMessagingService.getSessionState()` megírása
3. `SessionWatchdogService` bevezetése
4. sensitive state clear metódusok központosítása
5. popup close vagy locked route logika bekötése
6. watchdog bekötése az érzékeny komponensek lifecycle-jába
7. error policy finomhangolása

## 9. Elfogadási kritériumok

1. Nyitott popup vagy érzékeny nézet mellett a watchdog fut.
2. Ha a TrayApp invalid authot jelez, az extension 1-2 másodpercen belül eltünteti az érzékeny adatot.
3. Popup esetén a felület bezárul, vagy route alapú UI esetén lock képernyő jelenik meg.
4. A memóriában maradt credential state törlődik.
5. Egyetlen átmeneti kommunikációs hiba nem okoz felesleges bezárást.

## 10. Javasolt Angular belső API

```ts
export interface SessionStateResponse {
  authorized: boolean;
  closeExtension: boolean;
  authVersion: number;
  reason: string | null;
}

export abstract class NativeMessagingService {
  abstract getSessionState(): Observable<SessionStateResponse>;
}

export abstract class SensitiveStateService {
  abstract clearAllSensitiveData(): void;
}
```

## 11. Javasolt tesztesetek

1. Credential nézet nyitva, auth aktív: nincs UI változás.
2. Credential nézet nyitva, auth revoke: state clear + close.
3. Popup újranyitás auth nélkül: érzékeny adat nem jelenik meg.
4. TrayApp átmeneti hiba: watchdog retry történik, nem zár azonnal.
5. Új login után a flow újra működik.