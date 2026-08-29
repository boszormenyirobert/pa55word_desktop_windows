# Browser Extension Credential Alignment Plan

Ez a dokumentum pontosan leírja, hogyan kell a browser extension credential flow-ját átírni, hogy a decrypt viselkedés a tray-app credential nézetével egyezzen meg.

## Kiinduló probléma

A jelenlegi extension flow:

1. küld egy `read` native message requestet a tray appnak
2. a tray app backendről beolvassa az összes credentialt
3. a tray app az összes `registry` payloadot decrypteli
4. az extension az összes decryptelt credentialt memóriában tartja, és ebből építi a listát meg az editort

Ez nem egyezik a tray-app saját credential flow-jával.

## Célállapot

A tray-app credential flow-ja valójában két külön lépésből áll:

1. a backendről beolvassa a credential listaelemeket titkosított `registry`-vel
2. csak a ténylegesen kiválasztott elemet decrypteli lokálisan

Az extensionnek ugyanezt kell követnie.

## Kötelező viselkedési szabályok

1. A credential lista betöltése nem adhat vissza decryptelt `registry` adatot.
2. A lista response-nek meg kell tartania a decrypthez szükséges `iv`, `authTag`, `registry` ciphertext mezőket.
3. Az extension a listaelemekből csak selector/lista state-et építhet.
4. Az extension nem használhatja a lista response-t közvetlen editor state-nek.
5. A decrypt csak explicit kiválasztáskor történhet meg.
6. A create/update/delete response továbbra is visszaadhat egyetlen decryptelt credentialt, mert ott csak egy rekordról van szó.

## Új message contract

### 1. Credential list read

Request:

```json
{
  "rest": "read"
}
```

Response:

```json
[
  {
    "id": 101,
    "label": "Github",
    "credentialGroupId": 1,
    "iv": "0f1e2d3c4b5a69788796a5b4",
    "authTag": "8f7e6d5c4b3a2910ffeeddccbbaa9988",
    "registry": "BASE64_ENCRYPTED_CIPHERTEXT",
    "group": {
      "groupId": "550e8400-e29b-41d4-a716-446655440000",
      "groupName": "Work"
    }
  }
]
```

Ez summary response. Ebből tilos közvetlenül editor mezőket építeni.

### 2. Selected credential detail read

Request:

```json
{
  "rest": "read",
  "resource": "credential-detail",
  "id": 101,
  "label": "Github",
  "iv": "0f1e2d3c4b5a69788796a5b4",
  "authTag": "8f7e6d5c4b3a2910ffeeddccbbaa9988",
  "encryptedRegistry": "BASE64_ENCRYPTED_CIPHERTEXT",
  "groupId": "550e8400-e29b-41d4-a716-446655440000",
  "groupName": "Work"
}
```

Response:

```json
{
  "id": 101,
  "label": "Github",
  "registry": "{\"domain\":\"github.com\",\"username\":\"john.doe\",\"password\":\"secret\",\"description\":\"main account\"}",
  "group": {
    "groupId": "550e8400-e29b-41d4-a716-446655440000",
    "groupName": "Work"
  }
}
```

Ez az egyetlen read útvonal, ahol decryptelt `registry` visszamehet az extensionnek.

## Pontos extension oldali feladatok

### Native bridge service

Az extension native bridge rétegében szét kell választani:

1. `readVaultItems()`
2. `readVaultItemDetail(selectedItem)`

`readVaultItems()` csak summary elemeket ad vissza.

`readVaultItemDetail(selectedItem)` a kiválasztott listaelem `id`, `iv`, `authTag`, `registry` ciphertext mezőiből építi fel a `credential-detail` read requestet.

### Vault workspace state

Az editor state nem támaszkodhat közvetlenül a listában kapott `raw` rekordra.

Szükséges külön állapotok:

1. `vaultItems`: summary lista
2. `selectedVaultId`: kiválasztott listaelem azonosító
3. `isVaultDetailLoading`: folyamatjelző a detail-readhez
4. `selectedVaultFields`: kizárólag a detail-read decryptelt válaszából épülhet
5. `selectedVaultDetailRequestVersion`: race-condition védelem gyors váltás esetére

### Selection flow

Credential kiválasztáskor a pontos sorrend:

1. kiválasztott `id` eltárolása
2. korábbi editor mezők ürítése
3. `credential-detail` read request küldése
4. a decryptelt `registry` JSON string parse/flatten
5. editor state és autofill state frissítése

### Refresh flow

Refreshkor:

1. lista újratöltése summary response-ből
2. group lista újratöltése
3. ha maradt kiválasztott elem, annak detail-read újrahívása

### Create flow

Create módban ne a lista első elemének `raw` payloadját használja template-ként, mert a lista többé nem tart decryptelt registry objektumot.

Helyette explicit alap credential sablont kell használni:

```json
{
  "id": "",
  "label": "",
  "registry": {
    "domain": "",
    "username": "",
    "password": "",
    "description": ""
  }
}
```

## Pontos tray oldali feladatok

### BrowserCommandHandler

1. a sima `read` többé nem decryptelhet minden rekordot
2. a sima `read` response-nek tovább kell adnia az `iv`, `authTag`, `registry` ciphertext mezőket
3. be kell vezetni a `resource: "credential-detail"` + `rest: "read"` útvonalat
4. a detail-read a requestben kapott egyetlen encrypted rekordot decrypteli vissza

### DTO módosítások

Szükséges mezők:

1. `MessagingResource.CredentialDetail`
2. `BrowserExtensionPayloadDto.Iv`
3. `BrowserExtensionPayloadDto.AuthTag`
4. `BrowserExtensionPayloadDto.EncryptedRegistry`
5. `ConnectionReadResultDto.Iv`
6. `ConnectionReadResultDto.AuthTag`

## Implementációs eredmény

Az átállítás után:

1. a tray credential list read már nem ad vissza minden rekordra decryptelt `registry`-t
2. az extension csak a kiválasztott credentialhez kér decryptelt detailt
3. a lista és az editor state szétválik
4. a viselkedés a tray-app credential nézetéhez igazodik

## Ellenőrzési lista

1. A credential lista megnyitásakor a native `read` response-ben a `registry` mező nem JSON object/string credential tartalom, hanem ciphertext.
2. A credential kiválasztásakor külön `credential-detail` request megy ki.
3. Gyors választásváltáskor a későn érkező response nem írhatja felül az újabb kiválasztás state-jét.
4. Refresh után a korábban kiválasztott credential detailje újratöltődik.
5. Create továbbra is működik akkor is, ha még egyetlen decryptelt detail sem volt betöltve.