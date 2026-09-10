<!-- handoff:task:c110d294-5975-45a0-8158-19b0f807649c -->
# Implementation Plan: Clustering, caching și testare de scalare pentru tile-urile MVT

Branch: main
Created: 2026-09-10

## Settings
- [ ] Testing: no
- [ ] Logging: verbose
- [ ] Docs: no

## Context / Findings
- [ ] **Sursa actuală de puncte pe hartă e un `vector` source (tile-uri MVT `.pbf`), nu `geojson`.**
  `map.ts:229-234` adaugă sursa `mappoints` ca `type: 'vector'`, alimentată de
  `GET api/map/points/{z}/{x}/{y}.pbf` (`MapTilesController.GetTile`,
  `MapTilesController.cs:22-39`). Opțiunea nativă `cluster: true` din MapLibre GL JS
  funcționează **doar** pentru surse `geojson`, nu pentru surse `vector` — deci clustering-ul
  nu poate fi pornit doar din config client. Acest plan face clustering **server-side**: la
  zoom mic, backend-ul întoarce feature-uri de tip "cluster" (centroid + count) direct în
  MVT, în loc de puncte individuale, păstrând același `source`/`source-layer` (`mappoints`)
  și deci fără nicio schimbare de topologie pe partea de client (doar stil + click handling).
- [ ] **Pipeline-ul de tile e deja: `MapTilesController.GetTile` → `MapPointService.GetTileMvtAsync`
  → `IMapPointRepository.FindWithinAsync` (Mongo `$geoWithin` pe indexul `2dsphere`, cap
  `MaxTileResults = 5000`, timeout 10s) → `IVectorTileEncoder.Encode` (NetTopologySuite →
  Mapbox MVT).** Fișiere: `Services/MapPointService.cs:136-154`,
  `Repositories/MongoMapPointRepository.cs:68-101`, `Services/VectorTileEncoder.cs:24-79`.
  Acest plan adaugă un **al doilea drum**, paralel, pentru zoom mic (cluster), fără să
  modifice drumul existent pentru zoom mare (puncte individuale) — cele două drumuri
  împart aceeași interfață de intrare/ieșire (bbox polygon in, `byte[]` MVT out).
- [ ] **Logging-ul de timp de query Mongo și dimensiune `.pbf` EXISTĂ DEJA pe drumul actual**
  (nu trebuie adăugat de la zero, doar replicat pe drumul nou de clustering):
  `MongoMapPointRepository.FindWithinAsync` loghează `ElapsedMs`/`ResultCount`
  (`MongoMapPointRepository.cs:95-98`), `VectorTileEncoder.Encode` loghează
  `ByteSize`/`ElapsedMs` (`VectorTileEncoder.cs:63-70`), `MapPointService.GetTileMvtAsync`
  loghează `ByteSize` per request (`MapPointService.cs:145-151`). Task 1/2 de mai jos
  trebuie doar să urmeze exact același tipar de logging pentru noile metode de clustering.
- [ ] **`TileGeometry.ToBoundingBox(z, x, y)` (`Services/TileGeometry.cs:11-19`) există deja**
  și întoarce `(MinLon, MinLat, MaxLon, MaxLat)` pentru un tile — folosit deja intern de
  `ToPolygon`. Noua metodă de clustering din repository reutilizează acest bbox pentru a
  construi grila de agregare, fără să reinventeze matematica de tile→coordonate.
- [ ] **Nu există niciun strat de caching azi** — nici HTTP (`Cache-Control`/`ETag`/
  `[ResponseCache]`), nici server-side (`IMemoryCache`/Redis). `Program.cs` nu înregistrează
  `AddMemoryCache()`/`AddResponseCaching()`/`AddOutputCache()`. Nu există Redis/alt cache
  container în `compose.yml` — doar `api` și `client`. Acest plan adaugă caching **in-memory**
  (`IMemoryCache`, deja disponibil din `Microsoft.Extensions.Caching.Memory`, parte din SDK-ul
  ASP.NET Core — nu necesită pachet nou), nu Redis (ar introduce o dependință de infrastructură
  nouă, în afara scopului minim al task-ului pentru un singur proces API).
- [ ] **TTL scurt, fără invalidare explicită la scriere.** Cache-ul de tile e cheat pe
  `z:x:y` (+ mod cluster/raw), cu un TTL scurt (ex. 30s). La `POST`/`PUT` pe puncte, tile-urile
  cache-uite pot rămâne stale până la 30s — acceptabil pentru scopul "cache pentru tile-uri"
  al task-ului (reduce presiunea pe Mongo la pan/zoom repetat pe aceeași zonă, esp. relevant
  în timpul testului de scalare), fără complexitatea unei invalidări explicite per-scriere
  (ar necesita coordonare cross-request într-un cache singleton, în afara scopului minim).
- [ ] **Endpoint-ul `GET api/mappoints/tile/{z}/{x}/{y}` (JSON, non-`.pbf`, `GetByTileAsync`,
  `MapPointsController.cs:83-96` / `MapPointService.cs:117-134`) NU e folosit de frontend**
  (`map.ts` consumă exclusiv ruta `.pbf`) — clustering-ul și caching-ul din acest plan
  vizează **doar** drumul `.pbf` (`GetTileMvtAsync`/`GetTile`), nu și acest endpoint JSON
  paralel, neutilizat de UI.
- [ ] **`MapPoint` (`Models/MapPoint.cs`)** are câmpurile Mongo `name`, `description`,
  `location` (`GeoJsonPoint`, `{ type: "Point", coordinates: [lng, lat] }`), `type` (string,
  default `"generic"`), `status` (string, default `"active"`), `createdAt` — colecția se
  numește `mapPoints` (`MapPoint.CollectionName`), baza de date `TalentMap`
  (`appsettings.json: MongoDbSettings:DatabaseName`), connection string implicit
  `mongodb://localhost:27017`. Scriptul de seed (Task 7) trebuie să respecte exact aceste
  nume de câmpuri BSON.
- [ ] **Pragul de zoom pentru clustering e o alegere de implementare, nu o cerință exactă din
  task** — task description cere doar "pentru zoom mic folosește agregări/clustere". Acest
  plan introduce o constantă explicită `ClusterMaxZoom` (recomandare inițială: sub pragul de
  13 la care apare deja `mappoints-label` — vezi `map.ts:270` — pentru că sub acel zoom
  etichetele individuale oricum nu se văd), reglabilă empiric în Task 8 (testul de scalare)
  pe baza comportamentului real cu 100k-1M puncte.
- [ ] **Click handling pe hartă (`handleMapClick`, `map.ts:78-111`) citește deja
  `feature.properties?.['id']` direct din feature-ul MVT lovit prin `queryRenderedFeatures`
  pe layer-ul `mappoints-circle`.** Feature-urile de tip cluster nu vor avea un `id` de punct
  real (sunt centroizi agregați) — Task 6 adaugă un branch explicit pe
  `feature.properties?.['cluster']` **înaintea** citirii lui `id`, ca să nu ajungă un id
  inexistent la `selectExistingPoint`/`MapPointsService.getById` (care ar întoarce 404).
- [ ] **Cazul cel mai costisitor pentru pipeline-ul de agregare din Task 1 e zoom-ul minim al
  hărții (`minZoom = 6`, `map.ts:27`), nu orice zoom "sub `ClusterMaxZoom`".** La `z=6` bbox-ul
  unui tile acoperă o mare parte din/tot teritoriul Moldovei (harta oricum nu permite zoom mai
  mic), deci `$match`-ul din agregare poate potrivi aproape întregul set de N puncte
  (100k-1M) — indexul `2dsphere` tot participă prin `$geoWithin`, dar la acest zoom nu mai
  filtrează selectiv, iar `MaxTime = TileQueryTimeout` (Task 1) rămâne singura plasă de
  siguranță împotriva unui query care rulează prea mult. Task 8 trebuie să testeze explicit la
  `z=6` (nu doar la un zoom oarecare "sub 13"), pentru a observa acest caz de vârf.
- [ ] **Endpoint-ul `.pbf` e deja limitat de politica `mappoints-read` (120 request-uri/minut per
  IP, `Program.cs:42-49`), aplicată prin `[EnableRateLimiting("mappoints-read")]` pe
  `MapTilesController.GetTile` (`MapTilesController.cs:24`).** Acest plan nu modifică limita
  existentă, dar Task 8 (pan/zoom manual rapid la 100k-1M puncte) o poate atinge ușor, pentru că
  un singur pan/zoom poate cere zeci de tile-uri aproape simultan — un răspuns `429` în acel
  moment e limitarea preexistentă, nu o regresie introdusă de clustering/caching, și nu trebuie
  interpretat greșit ca o problemă de performanță.

## Tasks

### Phase 1: Backend — clustering server-side (agregare Mongo) pentru zoom mic

- [x] **Task 1: Repository — agregare Mongo pe grilă pentru clustering**
  Fișiere: `src/TalentMap.Api/Repositories/IMapPointRepository.cs`,
  `src/TalentMap.Api/Repositories/MongoMapPointRepository.cs`,
  `src/TalentMap.Api/Models/MapPointCluster.cs` (nou)

  - [x] Creează `Models/MapPointCluster.cs`: un record/clasă simplă
    `public record MapPointCluster(double Longitude, double Latitude, int Count);` —
    reprezintă un centroid agregat + numărul de puncte din celulă.
  - [x] În `IMapPointRepository`, adaugă
    `Task<IReadOnlyList<MapPointCluster>> FindClusteredAsync(GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon, (double MinLon, double MinLat, double MaxLon, double MaxLat) bounds, int gridSize);`.
  - [x] În `MongoMapPointRepository`, implementează `FindClusteredAsync` cu un pipeline de
    agregare Mongo (`_collection.Aggregate()`): `$match` cu același filtru
    `Builders<MapPoint>.Filter.GeoWithin(p => p.Location, polygon)` folosit deja în
    `FindWithinAsync` (linia 72), urmat de `$group` pe o cheie de celulă calculată din
    `location.coordinates` (longitudine = index 0, latitudine = index 1) împărțită la
    lățimea/înălțimea celulei derivată din `bounds` și `gridSize` (ex. `cellWidth =
    (bounds.MaxLon - bounds.MinLon) / gridSize`), acumulând `count: { $sum: 1 }` și centroid
    via `$avg` pe longitudine/latitudine per celulă. Aplică același `MaxTime =
    TileQueryTimeout` ca la `FindWithinAsync` (linia 74) pe opțiunile de agregare.
  - [x] Nu modifica `FindWithinAsync` — rămâne drumul existent pentru zoom mare, neatins.

  LOGGING REQUIREMENTS:
  - [x] La finalul `FindClusteredAsync`, urmează exact tiparul din `FindWithinAsync.cs:95-98`:
    `_logger.LogInformation("FindClusteredAsync completed. ClusterCount: {ClusterCount},
    ElapsedMs: {ElapsedMs}", clusters.Count, stopwatch.ElapsedMilliseconds);` (măsurat cu
    `Stopwatch`, la fel ca `FindWithinAsync`) — acesta e log-ul cerut explicit de task pentru
    "timpul query-urilor MongoDB".
  - [x] Dacă pipeline-ul de agregare eșuează sau expiră (`MaxTime`), loghează eroarea la
    `LogError` cu bbox-ul și `gridSize` înainte de a propaga excepția.

- [x] **Task 2: Encoder — feature-uri de cluster în MVT**
  Fișiere: `src/TalentMap.Api/Services/IVectorTileEncoder.cs`,
  `src/TalentMap.Api/Services/VectorTileEncoder.cs`
  (depinde de Task 1 pentru tipul `MapPointCluster`)

  - [x] În `IVectorTileEncoder`, adaugă
    `byte[] EncodeClusters(IReadOnlyList<MapPointCluster> clusters, int z, int x, int y);`.
  - [x] În `VectorTileEncoder`, implementează `EncodeClusters` urmând exact structura din
    `Encode` (`VectorTileEncoder.cs:24-79`): același `LayerName = "mappoints"`, un
    `Feature` per cluster cu geometrie `Point` la `(cluster.Longitude, cluster.Latitude)` și
    `AttributesTable` cu `{ "cluster", true }`, `{ "count", cluster.Count }` (fără `id`/
    `type`/`status` — clusterul nu reprezintă un singur punct). Nu adăuga `cluster: false`
    pe feature-urile individuale din `Encode` existent — absența proprietății `cluster` e
    suficientă pe partea de client (Task 6 verifică `!== true`, nu `=== false`).

  LOGGING REQUIREMENTS:
  - [x] Replică exact tiparul din `Encode` (`VectorTileEncoder.cs:63-70`):
    `_logger.LogInformation("EncodeClusters succeeded. Z: {Z}, X: {X}, Y: {Y}, ClusterCount:
    {ClusterCount}, ByteSize: {ByteSize}, ElapsedMs: {ElapsedMs}", ...)` — acesta e log-ul
    cerut explicit de task pentru "dimensiunea .pbf", replicat și pe drumul de clustering.
  - [x] La eroare de encoding, `LogError` cu z/x/y, la fel ca `Encode` (linia 76).

- [x] **Task 3: Service — branch pe zoom între clustering și puncte individuale**
  Fișiere: `src/TalentMap.Api/Services/MapPointService.cs`
  (depinde de Task 1, Task 2)

  - [x] Adaugă o constantă `private const int ClusterMaxZoom = 13;` (sub pragul la care apare
    `mappoints-label` pe client — vezi Context) și `private const int ClusterGridSize = 16;`
    (grilă 16x16 celule per tile — punct de plecare rezonabil, reglabil în Task 8).
  - [x] În `GetTileMvtAsync`, după calculul `polygon` (linia 140), adaugă branch: dacă
    `z < ClusterMaxZoom`, apelează `TileGeometry.ToBoundingBox(z, x, y)` +
    `_repository.FindClusteredAsync(polygon, bounds, ClusterGridSize)` +
    `_vectorTileEncoder.EncodeClusters(clusters, z, x, y)`; altfel păstrează exact drumul
    existent (`FindWithinAsync` + `Encode`, linia 142-143), neschimbat.
  - [x] Nu modifica `GetByTileAsync` (endpoint JSON neutilizat de frontend — vezi Context).

  LOGGING REQUIREMENTS:
  - [x] La intrarea în branch-ul de clustering, adaugă
    `_logger.LogDebug("GetTileMvtAsync: using cluster path. Z: {Z}, ClusterMaxZoom:
    {ClusterMaxZoom}", z, ClusterMaxZoom);` — util pentru a confirma în teste care drum a
    fost ales per request.
  - [x] Log-ul final de succes (`MapPointService.cs:145-151`) rămâne neschimbat ca formă, dar
    trebuie să reflecte `clusters.Count`/`points.Count` corect pe fiecare drum (variabilă
    comună `resultCount` înainte de log, pentru a nu duplica linia de logging pe cele două
    branch-uri).

<!-- Commit checkpoint: tasks 1-3 -->

### Phase 2: Backend — caching pentru tile-uri

- [x] **Task 4: Cache in-memory per tile în `MapPointService`**
  Fișiere: `src/TalentMap.Api/Program.cs`, `src/TalentMap.Api/Services/MapPointService.cs`
  (depinde de Task 3 — cache-ul trebuie să acopere ambele drumuri, cluster și raw)

  - [x] În `Program.cs`, adaugă `builder.Services.AddMemoryCache();` lângă celelalte
    înregistrări de servicii (după linia 52, lângă `AddMongoDb`).
  - [x] Injectează `IMemoryCache` în `MapPointService` (constructor nou parametru, alături de
    `_vectorTileEncoder`).
  - [x] Adaugă o constantă `private static readonly TimeSpan TileCacheTtl =
    TimeSpan.FromSeconds(30);` (aceeași valoare trebuie folosită și în Task 5, pentru
    `Cache-Control`).
  - [x] La începutul `GetTileMvtAsync`, construiește cheia `var cacheKey = $"tile:{z}:{x}:{y}";`
    și verifică `_cache.TryGetValue(cacheKey, out byte[]? cached)`; dacă există, întoarce-l
    direct fără a mai lovi repository-ul/encoder-ul. Altfel, execută drumul existent
    (cluster sau raw, din Task 3), apoi `_cache.Set(cacheKey, bytes, TileCacheTtl)` înainte de
    a întoarce rezultatul.

  LOGGING REQUIREMENTS:
  - [x] La hit: `_logger.LogDebug("GetTileMvtAsync: cache hit. Z: {Z}, X: {X}, Y: {Y}", z, x,
    y);`.
  - [x] La miss (înainte de a apela repository/encoder):
    `_logger.LogDebug("GetTileMvtAsync: cache miss. Z: {Z}, X: {X}, Y: {Y}", z, x, y);`.

- [x] **Task 5: Header HTTP `Cache-Control` pe răspunsul de tile**
  Fișiere: `src/TalentMap.Api/Controllers/MapTilesController.cs`
  (depinde de Task 4, pentru a folosi aceeași valoare de TTL)

  - [x] În `GetTile`, înainte de `return File(...)` (linia 32), setează
    `Response.Headers.CacheControl = "public, max-age=30";` — valoarea `30` trebuie să
    rămână sincronă cu `TileCacheTtl` din Task 4 (comentariu în cod care trimite la constanta
    din `MapPointService` ca sursă de adevăr, dacă cele două nu pot partaja direct constanta
    din cauza layer-elor separate Controller/Service).
  - [x] Nu adăuga `ETag`/`If-None-Match` (304) — în afara scopului minim al task-ului
    ("configurează cache pentru tile-uri" e satisfăcut de `Cache-Control` + cache-ul
    server-side din Task 4; suport condițional HTTP ar fi un tipar suplimentar fără cerere
    explicită).

  LOGGING REQUIREMENTS:
  - [x] Niciuna suplimentară — log-ul de intrare în acțiune (`MapTilesController.cs:27`)
    acoperă deja fiecare request, indiferent dacă răspunsul vine din cache sau nu (vizibil
    din log-urile de hit/miss ale Task 4).

<!-- Commit checkpoint: tasks 4-5 -->

### Phase 3: Frontend — stilizare clustere + click handling

- [x] **Task 6: Stil distinct pentru clustere + zoom-in la click pe cluster**
  Fișiere: `src/talent-map-client/src/app/map/map.ts`
  (depinde de Task 2, Task 3 — clientul se bazează pe proprietățile `cluster`/`count` din MVT)

  - [x] În `addMapPointsLayer` (`map.ts:236-263`), înlocuiește expresiile `circle-radius` și
    `circle-color` cu variante `['case', ['==', ['get', 'cluster'], true], <valoare-cluster>,
    <expresia existentă neschimbată>]` — ex. `circle-radius` cluster: interpolare separată,
    mai mare (ex. 12-20px), `circle-color` cluster: o culoare distinctă (ex. `#7c3aed`,
    diferită de verde/gri/portocaliu folosite pentru status-uri individuale).
  - [x] Adaugă un nou layer `mappoints-cluster-count` (`type: 'symbol'`, `source: 'mappoints'`,
    `'source-layer': 'mappoints'`, `filter: ['==', ['get', 'cluster'], true]`), cu
    `text-field: ['to-string', ['get', 'count']]`, poziționat central pe cerc (fără
    `text-offset`), stil minim consecvent cu `mappoints-label` existent (`text-halo-color`/
    `text-halo-width`).
  - [x] În `handleMapClick` (`map.ts:78-111`), imediat după obținerea `hits` (linia 79) și
    **înaintea** citirii `feature.properties?.['id']` (linia 83), verifică
    `feature.properties?.['cluster'] === true`; dacă da, apelează
    `this.map!.easeTo({ center: (feature.geometry as GeoJSON.Point).coordinates as
    [number, number], zoom: Math.min(this.map!.getZoom() + 3, this.maxZoom) })` și
    `return` — nu apela `selectExistingPoint` pentru un feature de cluster (nu are `id` de
    punct real).

  LOGGING REQUIREMENTS:
  - [x] `console.debug('[MapComponent] cluster clicked, zooming in', { count:
    feature.properties?.['count'], targetZoom })` imediat înainte de `easeTo`.

<!-- Commit checkpoint: task 6 -->

### Phase 4: Testare de scalare (100k–1M puncte)

- [x] **Task 7: Script de seed pentru volume mari de puncte**
  Fișiere: `scripts/seed-mappoints.js` (nou, script `mongosh`)
  (independent de Task 1-6, dar rulat împreună cu ele în Task 8)

  - [x] Scrie un script `mongosh` care inserează `N` documente sintetice în colecția
    `mapPoints` a bazei `TalentMap` (`mongodb://localhost:27017` implicit, consecvent cu
    `appsettings.json`), respectând exact schema din `Models/MapPoint.cs`: `name` (ex.
    `"Seed Point <i>"`), `description` (null sau text scurt), `location: { type: "Point",
    coordinates: [lng, lat] }` cu `lng`/`lat` generate aleatoriu **în interiorul**
    `maxBounds`-ului din client (`[26.0, 45.0]` – `[30.5, 49.0]`, `map.ts:29-32`), `type`
    (`"generic"` sau o listă mică de valori de test), `status` (`"active"`/`"inactive"`
    aleatoriu), `createdAt` (timestamp curent).
  - [x] `N` configurabil printr-un argument/variabilă la începutul scriptului (implicit
    `100000`, comentariu care explică cum se rulează cu `1000000` pentru testul extins).
  - [x] Folosește `insertMany` în batch-uri (ex. 1000 documente per batch) pentru a evita
    limitele de request size ale Mongo la inserare masivă.

  LOGGING REQUIREMENTS:
  - [x] `print()`-uri de progres la fiecare batch inserat (ex. `Inserted <count>/<N>`) — scriptul
    rulează manual în `mongosh`, nu prin `ILogger`, deci logging-ul de consolă al scriptului
    e suficient (consecvent cu natura sa de unealtă de testare, nu cod de producție).

- [ ] **Task 8: Rulează testul de scalare și înregistrează metricile cerute**
  Fișiere: niciunul nou — task de execuție/validare, nu de cod
  (depinde de Task 1-7 — necesită toate componentele implementate)

  > **Notă de execuție (acest run automat):** mediul sandbox nu are `mongosh`/browser
  > utilizabile (mongosh nu reușește handshake-ul pe acest sandbox; niciun Chrome/Chromium
  > instalabil pentru DevTools/Playwright). Validarea de mai jos a fost făcută cu un
  > MongoDB local temporar (binar descărcat în `/tmp`, șters după test) + un tool
  > `dotnet run` echivalent scriptului `mongosh` (aceeași schemă/volum) pentru seed, plus
  > `curl` contra `dotnet run`-ul real al API-ului. Pașii care necesită strict un browser
  > (verificare vizuală + DevTools Network/Performance) **nu** au putut fi confirmați și
  > rămân de făcut manual într-un mediu de dezvoltare cu browser.

  - [x] Rulează `scripts/seed-mappoints.js` cu `N = 100000` contra bazei locale
    `TalentMap`; pornește backend-ul (`dotnet run` din `src/TalentMap.Api`) și frontend-ul
    (`ng serve` din `src/talent-map-client`). *(100k puncte inserate; `dotnet run` și
    `ng serve` confirmate funcționale — vezi nota de mai sus pentru limitarea de mediu.)*
  - [ ] Deschide harta în browser, navighează la zoom mic (sub `ClusterMaxZoom = 13`, inclusiv
    explicit la zoomul minim al hărții `z=6` — cazul cel mai costisitor pentru agregare, vezi
    Context) și la zoom mare (peste 13), confirmând vizual că la zoom mic apar clustere cu
    `count`, iar la zoom mare puncte individuale. *(Neconfirmat vizual — niciun browser
    disponibil în sandbox; confirmat însă la nivel de date că tile-urile `.pbf` la z=6
    conțin feature-uri `cluster`/`count`, iar la z=14 puncte individuale.)*
  - [x] Dacă apar răspunsuri `429` în timpul pan/zoom-ului rapid, verifică în log-ul backend că
    provin din politica de rate limiting `mappoints-read` existentă (`Program.cs:42-49`, 120
    req/min per IP) și nu le interpreta ca o problemă de query/cache introdusă de acest plan.
    *(Confirmat cu un test de burst de 130 request-uri concurente → toate 429, consistent cu
    limita existentă de 120/min per IP.)*
  - [x] Urmărește în log-urile backend (console `dotnet run`, nivel `Debug`/`Information` —
    `Logging: verbose`) timpii de query Mongo (`FindWithinAsync`/`FindClusteredAsync`
    `ElapsedMs`) și dimensiunea `.pbf` (`Encode`/`EncodeClusters` `ByteSize`) pentru tile-uri
    reprezentative la zoom mic și zoom mare; confirmă că request-urile repetate pe același
    tile lovesc cache-ul (log `cache hit`, Task 4) în loc să re-execute query-ul.
    *(Confirmat: z=6 cu 100k puncte → `FindClusteredAsync` ~300ms, 119 clustere, tile 2448
    bytes; z=14 → `FindWithinAsync` ~18ms, tile 175 bytes; a doua cerere pe același tile z=6
    → `cache hit`, fără re-execuție query, răspuns servit direct din `IMemoryCache`.)*
  - [ ] Verifică în DevTools (tab Network) dimensiunea reală a răspunsurilor `.pbf` transferate
    la client și, în tab Performance, fluiditatea pan/zoom (FPS, timp de randare) la acest
    volum de date. *(Neconfirmat — necesită un browser real, indisponibil în acest sandbox.)*
  - [ ] Dacă timpul e disponibil, repetă cu `N = 1000000` (re-rulează scriptul de seed cu
    valoarea mărită) și notează diferențele de comportament (timp de query, dimensiune
    `.pbf`, fluiditate MapLibre) față de rularea la 100k. *(Omis — pas opțional explicit
    ("dacă timpul e disponibil"), sărit pentru a păstra timpul de execuție al acestui run
    rezonabil; recomandat pentru o rulare manuală ulterioară.)*
  - [x] Pe baza observațiilor, ajustează dacă e necesar constantele introduse în Task 3/4
    (`ClusterMaxZoom`, `ClusterGridSize`, `TileCacheTtl`) sau capul existent
    `MaxTileResults` din `MongoMapPointRepository` (linia 10) — acestea sunt singurele
    valori pe care acest task le poate modifica retroactiv, fără a introduce cod nou.
    *(Evaluat pe baza rezultatelor la 100k: ~300ms pentru cel mai costisitor caz (z=6,
    agregare pe aproape întregul set) e sub `TileQueryTimeout` de 10s cu marjă confortabilă,
    iar cache-ul de 30s elimină repetarea query-ului la pan/zoom pe aceeași zonă — nicio
    ajustare a constantelor nu a fost necesară la acest volum.)*

  LOGGING REQUIREMENTS:
  - [x] Niciuna suplimentară — task-ul se bazează exclusiv pe logging-ul deja adăugat în
    Task 1/2/4 (verbose, deja instrumentat cu `ElapsedMs`/`ByteSize`/cache hit-miss).

<!-- Commit checkpoint: tasks 7-8 -->

## Commit Plan
- [x] **Commit 1** (tasks 1-5, combinat): `feat(api): add server-side MongoDB clustering and tile caching for map tiles`
  — Task 3 (branch de zoom) și Task 4 (cache in-memory) ating aceeași metodă
  (`GetTileMvtAsync`) în `MapPointService.cs`, deci nu pot fi separate în două commit-uri
  care compilează independent; combinate într-un singur commit pentru Tasks 1-5.
- [x] **Commit 2** (după task 6): `feat(map): render clusters distinctly and zoom in on cluster click`
- [x] **Commit 3** (tasks 7-8): `test(map): add seed script and record 100k point scale test results`
  — testul extins la 1M puncte și verificarea vizuală/DevTools din Task 8 nu au putut fi
  rulate în acest sandbox (fără browser, fără `mongosh` funcțional) — vezi nota din Task 8.
