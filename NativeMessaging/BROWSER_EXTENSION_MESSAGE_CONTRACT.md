# Browser Extension Message Contract

Ez a dokumentum azt írja le, hogy a browser extension milyen native message payloadokat küldhet a tray appnak, és a tray app mit vár el pontosan a küldő oldaltól.

A tray app nem UI-ként működik ebben a flow-ban. A szerepe:

- native message request fogadása az extensiontől
- backend API hívások végrehajtása
- backend válasz visszaadása az extensionnek

Az alábbi contract a tray oldali jelenlegi implementációhoz igazodik.

## Rövid összefoglaló

- Credential műveletnél az `id` a credential numerikus azonosítója.
- Group response-ben az `id` a group belső numerikus relációs azonosítója.
- Group műveletnél az azonosításra használt mező a `groupId`.
- A `groupId` a caller-facing group azonosító, ezt kell create után eltárolni és később update/delete során visszaküldeni.
- A `credentialGroupId` a credential és a group közötti belső reláció numerikus azonosítója.
- Credential listázásnál a `registry` response-ben titkosított Base64 ciphertext érkezik vissza, nem decryptelt objektum.
- Credential detail olvasásnál, valamint create/update/delete credential response-ben a `registry` response-ben decryptelt JSON string érkezik vissza.
- Credential listázásnál az extension kap `iv` és `authTag` mezőket is, hogy a kiválasztott elemet külön detail-read requesttel tudja decrypteltetni a tray-jel.

## Általános szabályok

- A műveletet a `rest` mező mondja meg.
- Támogatott értékek: `read`, `create`, `update`, `delete`.
- Ha a `resource` mező hiányzik, a tray credential műveletként kezeli a requestet.
- Ha a `resource` értéke `group`, a tray credential group műveletként kezeli a requestet.
- Ha a `resource` értéke `credential-detail` és a `rest` értéke `read`, a tray a már korábban listázott, titkosított credential payloadból egyetlen elemet decryptel vissza.
- A tray végzi a backendhez szükséges `accountId`, `deviceId`, `nonce` és `x-signature` előállítását.
- Az extensionnek ezeket nem kell küldenie.
- A tray a `registry` objektumot titkosítja, mielőtt továbbküldi a backendnek.
- Hiba esetén a response séma marad a korábbi forma.

## Közös payload mezők

```json
{
  "rest": "read|create|update|delete",
  "resource": "group",
  "id": 1,
  "label": "Github",
  "groupId": "0f6f3773-1bd2-4f03-b0b9-3a98d863f5ce",
  "groupName": "Work",
  "iv": "hex-iv",
  "authTag": "hex-auth-tag",
  "encryptedRegistry": "base64-ciphertext",
  "registry": {
    "domain": "github.com",
    "username": "john.doe",
    "password": "secret",
    "description": "optional"
  }
}
```

Megjegyzések:

- `resource` csak group művelethez kell. Credential műveletnél elhagyható.
- `resource: "credential-detail"` csak credential detail read művelethez kell.
- Credential műveletnél az `id` numerikus azonosító.
- Group read response-ben az `id` a belső relációs azonosító numerikusan.
- Group műveletnél a `groupId` a UUID alapú üzleti azonosító.
- Group update és group delete esetén a tray nem a numerikus `id`, hanem a `groupId` alapján azonosít.
- `groupId` credential create/update esetén opcionális.
- `groupName` csak group create/update esetén kötelező.
- `registry` csak credential create/update esetén releváns.
- `iv`, `authTag`, `encryptedRegistry` csak credential detail read műveletnél releváns request mezők.

## Credential CRUD

### Create credential

Küldendő mezők:

- `rest: "create"`
- `label`
- `registry`
- opcionálisan `groupId`

Példa request:

```json
{
  "rest": "create",
  "label": "Github",
  "groupId": "550e8400-e29b-41d4-a716-446655440000",
  "registry": {
    "domain": "github.com",
    "username": "john.doe",
    "password": "secret",
    "description": "main account"
  }
}
```

Elvárás az extension felé:

- A `label` ne legyen üres.
- A `registry` objektumot plain JSON-ként küldje.
- Ha credentialt rögtön grouphoz kell kötni, küldjön egy valós, már létező group `groupId` értéket.
- Ha nincs group hozzárendelés, a `groupId` elhagyható.

Várható success response:

```json
{
  "id": 101,
  "label": "Github",
  "credentialGroupId": 1,
  "registry": "{\"domain\":\"github.com\",\"username\":\"john.doe\",\"password\":\"secret\",\"description\":\"main account\"}",
  "group": {
    "groupId": "550e8400-e29b-41d4-a716-446655440000",
    "groupName": "Work"
  }
}
```

Megjegyzés:

- A tray a visszakapott registry-t már dekódolt stringként adja vissza.
- A `group` értéke lehet `null`.

### Read credentials

Küldendő mezők:

- `rest: "read"`

Példa request:

```json
{
  "rest": "read"
}
```

Várható success response:

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
  },
  {
    "id": 102,
    "label": "Bank",
    "credentialGroupId": null,
    "iv": "11223344556677889900aabb",
    "authTag": "00112233445566778899aabbccddeeff",
    "registry": "BASE64_ENCRYPTED_CIPHERTEXT",
    "group": null
  }
]
```

Elvárás az extension felé:

- Az extension ne feltételezze, hogy a response csak `id`, `label`, `registry` mezőkből áll.
- Kezelje a `credentialGroupId` numerikus mezőt.
- Kezelje a nullable `group` mezőt.
- A `read` response `registry` mezőjét ne parse-olja JSON-ként, mert ez itt még titkosított ciphertext.
- A lista response `iv`, `authTag`, `registry` mezőit tartsa meg a kiválasztott elem detail-read requestjéhez.
- A lista response-ből ne építsen közvetlen editort vagy autofill állapotot.

### Read credential detail

Küldendő mezők:

- `rest: "read"`
- `resource: "credential-detail"`
- `id`
- `label`
- `iv`
- `authTag`
- `encryptedRegistry`
- opcionálisan `groupId`, `groupName`

Példa request:

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

Várható success response:

```json
{
  "id": 101,
  "label": "Github",
  "credentialGroupId": null,
  "iv": "0f1e2d3c4b5a69788796a5b4",
  "authTag": "8f7e6d5c4b3a2910ffeeddccbbaa9988",
  "registry": "{\"domain\":\"github.com\",\"username\":\"john.doe\",\"password\":\"secret\",\"description\":\"main account\"}",
  "group": {
    "groupId": "550e8400-e29b-41d4-a716-446655440000",
    "groupName": "Work"
  }
}
```

Elvárás az extension felé:

- Ezt a requestet csak akkor küldje, amikor a user ténylegesen kiválaszt egy credentialt szerkesztéshez vagy megjelenítéshez.
- A `registry` mezőt itt már decryptelt JSON stringként kezelje, és ebből építse fel a form state-et és az autofill cache-t.
- Ne kérje le előre az összes credential detailjét háttérben.

### Update credential

Küldendő mezők:

- `rest: "update"`
- `id`
- `label`
- `registry`
- opcionálisan `groupId`

Példa request:

```json
{
  "rest": "update",
  "id": 101,
  "label": "Github Personal",
  "groupId": "550e8400-e29b-41d4-a716-446655440000",
  "registry": {
    "domain": "github.com",
    "username": "john.doe",
    "password": "updated-secret",
    "description": "personal"
  }
}
```

Elvárás az extension felé:

- Az `id` kötelező.
- A tray a `groupId`-t továbbítja a backend felé, ha az jelen van.
- Ha a grouping nem változik, a `groupId` elhagyható.
- Ha a grouping változik, akkor a previously created vagy read során kapott `groupId` értéket kell visszaküldeni.
- A detach műveletre nincs külön "null-like" contract bevezetve ezen a native message rétegen.

Várható success response:

- Ugyanaz a shape, mint create esetén, a frissített credential adattal.

### Delete credential

Küldendő mezők:

- `rest: "delete"`
- `id`

Példa request:

```json
{
  "rest": "delete",
  "id": 101
}
```

Elvárás az extension felé:

- Az `id` kötelező.
- Más mező nem szükséges.

Várható success response:

- Ha a backend törölt elemet ad vissza, a tray azt visszaküldi a bővített credential response shape szerint.
- Ha a backend üres body-val válaszol, a tray ezt adja vissza:

```json
{
  "ok": true
}
```

## Credential Group CRUD

Fontos routing szabály:

- Group műveleteknél az extension küldje: `resource: "group"`

### Create group

Küldendő mezők:

- `rest: "create"`
- `resource: "group"`
- `groupName`
- opcionálisan `groupId`

Példa request:

```json
{
  "rest": "create",
  "resource": "group",
  "groupName": "Work"
}
```

Elvárás az extension felé:

- A `groupName` kötelező és ne legyen üres.
- A `groupId` küldhető, de nem kötelező.
- Ha nincs `groupId`, a tray UUID-t generál.

Várható success response:

```json
{
  "id": 1,
  "groupId": "550e8400-e29b-41d4-a716-446655440000",
  "groupName": "Work"
}
```

### Read groups

Küldendő mezők:

- `rest: "read"`
- `resource: "group"`

Példa request:

```json
{
  "rest": "read",
  "resource": "group"
}
```

Várható success response:

```json
[
  {
    "id": 1,
    "groupId": "550e8400-e29b-41d4-a716-446655440000",
    "groupName": "Finance"
  },
  {
    "id": 2,
    "groupId": "0f6f3773-1bd2-4f03-b0b9-3a98d863f5ce",
    "groupName": "Work"
  }
]
```

Elvárás az extension felé:

- A group lista nem lapozott.
- A primary caller oldali kulcs a `groupId` legyen.
- Rename és delete művelethez az extension kizárólag a `groupId` mezőt használja azonosításra.
- A numerikus `id` mezőt csak response/meta adatként kezelje.

### Update group

Küldendő mezők:

- `rest: "update"`
- `resource: "group"`
- `groupId`
- `groupName`

Példa request:

```json
{
  "rest": "update",
  "resource": "group",
  "groupId": "0f6f3773-1bd2-4f03-b0b9-3a98d863f5ce",
  "groupName": "Updated Work"
}
```

Elvárás az extension felé:

- A `groupId` kötelező.
- A `groupName` kötelező és nem lehet üres.
- A kliens oldali hivatkozás továbbra is `groupId` alapján történjen.
- A tray az update requestet `groupId` alapján küldi tovább, és átadja a `groupName`-et.
- A response-ben visszakapott numerikus `id` nem kérendő vissza a következő update/delete hívásokhoz.

Várható success response:

```json
{
  "id": 1,
  "groupId": "0f6f3773-1bd2-4f03-b0b9-3a98d863f5ce",
  "groupName": "Updated Work"
}
```

### Delete group

Küldendő mezők:

- `rest: "delete"`
- `resource: "group"`
- `groupId`

Példa request:

```json
{
  "rest": "delete",
  "resource": "group",
  "groupId": "0f6f3773-1bd2-4f03-b0b9-3a98d863f5ce"
}
```

Elvárás az extension felé:

- A `groupId` kötelező.
- A belső numerikus `id` csak response adat, nem ezen kell azonosítani a groupot módosításkor.

## Típusok röviden

### Credential request

```ts
{
  rest: 'create' | 'update' | 'delete' | 'read'
  id?: number
  label?: string
  groupId?: string
  registry?: {
    domain: string
    username: string
    password: string
    description: string
  }
}
```

### Credential read response item

```ts
{
  id: number
  label: string
  credentialGroupId: number | null
  registry: string
  group: {
    groupId: string
    groupName: string
  } | null
}
```

### Group request

```ts
{
  rest: 'create' | 'update' | 'delete' | 'read'
  resource: 'group'
  groupId?: string
  groupName?: string
}
```

### Group read response item

```ts
{
  id: number
  groupId: string
  groupName: string
}
```

Várható success response:

```json
{
  "id": 1,
  "groupId": "0f6f3773-1bd2-4f03-b0b9-3a98d863f5ce",
  "groupName": "Updated Work"
}
```

## Error response

Hiba esetén a tray a meglévő sémát adja vissza:

```json
{
  "ok": false,
  "error": {
    "code": "processing_error",
    "message": "..."
  }
}
```

Tipikus tray oldali hibakódok:

- `parse_error`
- `authorization_declined`
- `authorization_expired`
- `unsupported_operation`
- `processing_error`

## Rövid implementációs elvárás az extension felé

- Credential műveletnél ne küldjön `resource` mezőt, vagy hagyja el.
- Group műveletnél mindig küldjön `resource: "group"` mezőt.
- Group rename/delete esetén a `groupId` mezőt küldje vissza azonosításra.
- A backend numerikus `id` mezőjét csak olvasási célra kezelje.
- Credential listánál kezelje az új `credentialGroupId` és `group` mezőket.
- Credential create/update esetén csak akkor küldjön `groupId`-t, ha valóban grouphoz kell rendelni a credentialt.
- Ha `groupId`-t küld, az egy korábban create/read response-ből származó valós group azonosító legyen.