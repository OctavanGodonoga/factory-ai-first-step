<!-- handoff:task:1b645d88-ef31-4b77-9bbb-e471011d95d8 -->
# Implementation Plan: Generează și expune Vector Tiles MVT

Branch: main
Created: 2026-09-10

## Settings
- [ ] Testing: no
- [ ] Logging: verbose
- [ ] Docs: no

## Context / Findings
- [ ] `.ai-factory/DESCRIPTION.md` și `.ai-factory/ARCHITECTURE.md` sunt neactualizate — descriu
  persistență pe fișiere JSON, dar codul real folosește deja MongoDB (`MongoDB.Driver` 3.11.1,
  colecția `mapPoints`, index `2dsphere` pe `location`). Acest plan tratează codul ca sursă de
  adevăr, nu documentele stale.
- [ ] Există deja un endpoint JSON de tile: `GET api/mappoints/tile/{z}/{x}/{y}`
  (`Controllers/MapPointsController.cs`), care folosește `TileGeometry.ToPolygon(z,x,y)`
  (`Services/TileGeometry.cs`) pentru a construi un poligon de bounding-box și
  `IMapPointRepository.FindWithinAsync` (`Repositories/MongoMapPointRepository.cs`) pentru
  interogarea MongoDB cu `$geoWithin`, plafonat la 5000 rezultate / 10s timeout. Endpoint-ul nou
  `.pbf` trebuie să refolosească exact acest pipeline de interogare, nu să-l duplice.
- [ ] **Gap important:** modelul `MapPoint` (`Models/MapPoint.cs`) NU are în prezent câmpuri
  `type`/`status` — doar `name`, `description`, `location`, `createdAt`. Cerința de business
  ("include doar proprietăți minime precum id, type, status") cere ca aceste câmpuri să existe
  pe model. Acest plan adaugă `Type`/`Status` pe modelul de domeniu `MapPoint` cu valori implicite
  constante (fără a extinde fluxul de creare/actualizare — `MapPointRequest`/validare rămân
  neschimbate, este în afara scopului acestui task). Documentele Mongo existente, care nu au
  aceste câmpuri, se vor deserializa cu valorile implicite ale proprietăților C# (driverul Mongo
  nu cere elementele BSON lipsă dacă proprietatea nu e `[BsonRequired]`).
- [ ] Nu există nicio librărie de encodare MVT în proiect (`TalentMap.Api.csproj` are doar
  `Microsoft.AspNetCore.OpenApi` și `MongoDB.Driver`). Se adaugă `NetTopologySuite`,
  `NetTopologySuite.IO.VectorTiles` și `NetTopologySuite.IO.VectorTiles.Mapbox` (v1.1.0,
  verificate ca publicate pe NuGet.org). Aceste pachete gestionează intern proiecția
  WGS84 → coordonate locale de tile (extent) la scriere — geometriile se adaugă în lon/lat, nu
  este nevoie de o transformare de pixeli scrisă manual.
- [ ] Endpoint-urile de citire existente sunt publice (`[AllowAnonymous]`) dar rate-limited prin
  policy-ul `mappoints-read` (120 req/min/IP, `Program.cs`). Endpoint-ul nou `.pbf` refolosește
  aceeași policy — dacă traficul real de tile-uri (multe cereri per pan/zoom) se dovedește
  insuficient acoperit, o policy dedicată se poate adăuga ulterior într-un task separat.
- [ ] Ruta cerută explicit de task este `GET /api/map/points/{z}/{x}/{y}.pbf` — diferită ca segment
  de ruta JSON existentă (`api/mappoints/tile/...`). Se creează un controller nou, dedicat,
  `MapTilesController` cu `[Route("api/map")]` + `[HttpGet("points/{z:int}/{x:int}/{y:int}.pbf")]`,
  pentru a respecta convenția de attribute-routing a proiectului (fără override-uri de rută
  absolută) și pentru a separa clar servirea de tile-uri binare de CRUD-ul JSON din
  `MapPointsController`.

## Commit Plan
- [x] **Commit 1** (după task-urile 1-3): "feat(api): add MVT encoding dependency and point type/status fields"
- [ ] **Commit 2** (după task-urile 4-6): "feat(api): add MVT tile endpoint for map points"

## Tasks

### Phase 1: Fundație (model + dependințe)

- [x] **Task 1: Adaugă pachetele NuGet pentru encodare MVT**
  Fișier: `src/TalentMap.Api/TalentMap.Api.csproj`
  Adaugă `<PackageReference>` pentru:
  - [x] `NetTopologySuite` (ultima versiune stabilă compatibilă cu .NET 10, ex. 2.6.x)
  - [x] `NetTopologySuite.Features` (>= 2.1.0 — cerută explicit deoarece Task 3 folosește direct
    tipurile `NetTopologySuite.Features.Feature`/`AttributesTable`; altfel ar fi rezolvată doar
    tranzitiv prin `NetTopologySuite.IO.VectorTiles`)
  - [x] `NetTopologySuite.IO.VectorTiles` (1.1.0)
  - [x] `NetTopologySuite.IO.VectorTiles.Mapbox` (1.1.0)
  Rulează `dotnet restore` pentru a confirma că pachetele se rezolvă corect împreună cu
  `MongoDB.Driver` existent (fără conflicte de versiuni de dependențe tranzitive).
  Nicio cerință de logging — este o modificare de configurare de build.

  > Notă implementare: `NetTopologySuite.Features` cea mai recentă versiune publicată e 2.2.0
  > (nu 2.6.x ca `NetTopologySuite`); am folosit 2.2.0, care satisface minimul >= 2.1.0 cerut de
  > `NetTopologySuite.IO.VectorTiles` 1.1.0. `dotnet restore` confirmat OK, fără conflicte.

- [x] **Task 2: Adaugă câmpurile `Type` și `Status` pe modelul `MapPoint`**
  Fișier: `src/TalentMap.Api/Models/MapPoint.cs`
  Adaugă două proprietăți noi pe clasa `MapPoint`, mapate BSON:
  - [x] `[BsonElement("type")] public string Type { get; set; } = "generic";`
  - [x] `[BsonElement("status")] public string Status { get; set; } = "active";`
  Nu modifica `MapPointRequest`, `MapPointDto` sau validarea din `MapPointService` — popularea
  acestor câmpuri din fluxul de creare/actualizare este în afara scopului acestui task (rămân la
  valoarea implicită până la un task viitor dedicat).
  Nicio cerință de logging — este o schimbare de model de date fără logică.

- [x] **Task 3: Creează serviciul de encodare MVT `IVectorTileEncoder`**
  Fișiere noi: `src/TalentMap.Api/Services/IVectorTileEncoder.cs`,
  `src/TalentMap.Api/Services/VectorTileEncoder.cs`
  (depinde de Task 1, Task 2)

  Interfață:
  ```csharp
  public interface IVectorTileEncoder
  {
      byte[] Encode(IReadOnlyList<MapPoint> points, int z, int x, int y);
  }
  ```

  Implementare `VectorTileEncoder`:
  - [x] Construiește un `NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z)` și un
    `NetTopologySuite.IO.VectorTiles.VectorTile { TileId = tile.Id }`.
  - [x] Un singur `Layer` (nume: `"mappoints"`).
  - [x] Pentru fiecare `MapPoint`, creează un `NetTopologySuite.Geometries.Point` din
    `point.Location.Coordinates.Longitude/Latitude` (via `GeometryFactory`) și un
    `NetTopologySuite.Features.Feature` cu `AttributesTable` conținând **doar**:
    `id` (string), `type` (`point.Type`), `status` (`point.Status`) — nicio altă proprietate
    (nu `name`, nu `description`, nu `createdAt` — cerință explicită de minimalism din task).
  - [x] Scrie tile-ul cu `vectorTile.Write(stream, MapboxTileWriter.DefaultMinLinealExtent,
    MapboxTileWriter.DefaultMinPolygonalExtent)` într-un `MemoryStream` și returnează
    `stream.ToArray()`.
  - [x] Verifică la implementare denumirea exactă a proprietăților (`Tile.Id` vs `Tile.id`) în
    versiunea de pachet instalată — API-ul de mai sus e confirmat din documentația publică a
    proiectului, dar poate varia ușor între minor-versiuni.

  > Notă implementare: `Tile` există atât în `NetTopologySuite.IO.VectorTiles.Tiles` cât și în
  > `NetTopologySuite.IO.VectorTiles.Mapbox` (tip protobuf intern) — ambiguitate de nume rezolvată
  > cu un alias `using TileCoordinate = NetTopologySuite.IO.VectorTiles.Tiles.Tile;`. Verificat prin
  > reflecție + sursă publică GitHub (commit-ul din nuspec) că `Tile(int x, int y, int zoom)` are
  > exact această ordine de parametri și că `vectorTile.Write(...)` e o metodă de extensie din
  > `MapboxTileWriter` care citește coordonatele geometriei direct ca lon/lat (nicio transformare
  > manuală necesară). Validat end-to-end cu un harness temporar: encode → decode cu
  > `MapboxTileReader` confirmă layer `mappoints` cu exact proprietățile `id`/`type`/`status`.

  LOGGING REQUIREMENTS:
  - [x] `ILogger<VectorTileEncoder>` injectat prin constructor.
  - [x] DEBUG la intrare: `Z`, `X`, `Y`, `PointCount` (numărul de puncte primite).
  - [x] INFO la finalizare reușită: `Z`, `X`, `Y`, `PointCount`, `ByteSize` (lungimea array-ului
    rezultat), `ElapsedMs` (măsurat cu `Stopwatch`).
  - [x] ERROR dacă encodarea aruncă excepție neașteptată, cu `Z`/`X`/`Y` în context, apoi re-throw.
  - [x] Format mesaje consecvent cu restul proiectului (ex. `MongoMapPointRepository`):
    `"Encode called. Z: {Z}, X: {X}, Y: {Y}, PointCount: {PointCount}"`.

### Phase 2: Integrare serviciu + endpoint

- [ ] **Task 4: Adaugă metoda de generare tile MVT în `IMapPointService`/`MapPointService`**
  Fișiere: `src/TalentMap.Api/Services/IMapPointService.cs`,
  `src/TalentMap.Api/Services/MapPointService.cs`
  (depinde de Task 3)

  Adaugă în interfață: `Task<byte[]> GetTileMvtAsync(int z, int x, int y);`

  Implementare în `MapPointService`:
  - [ ] Injectează `IVectorTileEncoder` prin constructor (adaugă câmp `_vectorTileEncoder`).
  - [ ] Reutilizează exact fluxul din `GetByTileAsync`: `TileGeometry.ToPolygon(z, x, y)` (prinde
    `ArgumentOutOfRangeException` și aruncă `MapPointValidationException`, identic cu
    `GetByTileAsync`), apoi `_repository.FindWithinAsync(polygon)` pentru lista de `MapPoint`.
  - [ ] Apelează `_vectorTileEncoder.Encode(points, z, x, y)` și returnează bytes-ii.
  - [ ] Nu duplica validarea — extrage, dacă e util, un helper privat comun pentru
    `TileGeometry.ToPolygon` cu try/catch, folosit de ambele metode (`GetByTileAsync` și
    `GetTileMvtAsync`), pentru a evita codul duplicat.

  LOGGING REQUIREMENTS:
  - [ ] INFO la intrare: `"GetTileMvtAsync called. Z: {Z}, X: {X}, Y: {Y}"`.
  - [ ] WARN dacă `TileGeometry.ToPolygon` aruncă (coordonate tile invalide), identic ca stil cu
    `GetByTileAsync`.
  - [ ] INFO la succes: `"GetTileMvtAsync succeeded. Z: {Z}, X: {X}, Y: {Y}, PointCount: {PointCount},
    ByteSize: {ByteSize}"`.

- [ ] **Task 5: Înregistrează `IVectorTileEncoder` în DI**
  Fișier: `src/TalentMap.Api/Program.cs`
  (depinde de Task 3)
  Adaugă `builder.Services.AddSingleton<IVectorTileEncoder, VectorTileEncoder>();` lângă
  înregistrările existente (`IMoldovaBorderValidator`, `IMapPointRepository`) — serviciul e
  stateless (nu ține stare între request-uri), deci singleton e consistent cu restul.
  Nicio cerință de logging suplimentară (folosește deja `app.LogMongoRegistration()`-style logging
  existent pentru pornire, dacă e cazul; altfel nu e necesar logging separat pentru o linie de DI).

- [ ] **Task 6: Adaugă controller-ul `MapTilesController` cu endpoint-ul `.pbf`**
  Fișier nou: `src/TalentMap.Api/Controllers/MapTilesController.cs`
  (depinde de Task 4, Task 5)

  ```csharp
  [ApiController]
  [Route("api/map")]
  public class MapTilesController : ControllerBase
  {
      private readonly ILogger<MapTilesController> _logger;
      private readonly IMapPointService _mapPointService;

      public MapTilesController(ILogger<MapTilesController> logger, IMapPointService mapPointService)
      { ... }

      [HttpGet("points/{z:int}/{x:int}/{y:int}.pbf")]
      [AllowAnonymous]
      [EnableRateLimiting("mappoints-read")]
      public async Task<IActionResult> GetTile(int z, int x, int y)
      {
          try
          {
              var bytes = await _mapPointService.GetTileMvtAsync(z, x, y);
              return File(bytes, "application/vnd.mapbox-vector-tile");
          }
          catch (MapPointValidationException ex)
          {
              return BadRequest(new { errors = ex.Errors });
          }
      }
  }
  ```
  - [ ] Content-Type răspuns: `application/vnd.mapbox-vector-tile`.
  - [ ] `[AllowAnonymous]` + `[EnableRateLimiting("mappoints-read")]` la nivel de acțiune, la fel ca
    `GetByTile` din `MapPointsController` (controller-ul nou nu are `[Authorize]` la nivel de
    clasă, deci nu e nevoie de override — dar adaugă explicit `[AllowAnonymous]` pentru claritate
    și consecvență vizuală cu restul codului).
  - [ ] Controller-ul rămâne subțire: validare implicită prin route constraints (`:int`), apel unic
    către `IMapPointService.GetTileMvtAsync`, formatare răspuns.

  LOGGING REQUIREMENTS:
  - [ ] INFO la intrare: `"GetTile action called. Route: GET api/map/points/{Z}/{X}/{Y}.pbf"`.
  - [ ] WARN la `MapPointValidationException` (coordonate tile invalide), cu mesajele de eroare din
    excepție — identic stil cu celelalte acțiuni din `MapPointsController`.
