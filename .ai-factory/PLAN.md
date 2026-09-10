<!-- handoff:task:84220c30-df50-4adf-890e-a969105d613d -->
# Implementation Plan: Modelează punctele geospațiale în MongoDB

Branch: main
Created: 2026-09-10

## Settings
- [ ] Testing: no
- [ ] Logging: verbose
- [ ] Docs: no

## Context relevant din codebase

- [x] Backend-ul MongoDB e doar bootstrap-at până acum: `src/TalentMap.Api/Extensions/MongoServiceCollectionExtensions.cs`
  (`AddMongoDb` înregistrează `IMongoClient` + `IMongoDatabase` ca singleton) și
  `src/TalentMap.Api/Extensions/MongoStartupExtensions.cs` (`LogMongoRegistration`,
  `CheckMongoConnectivityAsync`, apelate din `Program.cs`).
- [x] Nu există încă niciun folder `Models/`, `Repositories/`, `Services/` sau `Controllers/` în
  `src/TalentMap.Api/` — acesta e un backend greenfield din perspectiva domeniului de business.
- [x] `appsettings.json` conține deja `ConnectionStrings:MongoDb` și `MongoDbSettings:DatabaseName`;
  `compose.yml` rutează containerul API către MongoDB de pe host (`host.docker.internal:27017`).
- [x] `MongoDB.Driver` (v3.11.1) e deja referențiat în `TalentMap.Api.csproj` — include namespace-ul
  `MongoDB.Driver.GeoJsonObjectModel` cu tipul `GeoJsonPoint<GeoJson2DGeographicCoordinates>`, care
  serializează nativ la forma cerută `{ type: "Point", coordinates: [longitude, latitude] }` fără a
  necesita pachete suplimentare sau un POCO custom pentru geometrie. Se folosește explicit
  `GeoJson2DGeographicCoordinates` (nu `GeoJson2DCoordinates`) — este varianta destinată coordonatelor
  geografice longitudine/latitudine (WGS84) pentru index `2dsphere` și validează intervalele valide
  (longitudine -180..180, latitudine -90..90) la construcție; `GeoJson2DCoordinates` reprezintă
  coordonate planare (x, y) generice, folosite pentru index legacy `2d`, nu pentru `2dsphere`.
- [x] Nu există nicio convenție `BsonElement`/`BsonId` stabilită încă în proiect — se stabilește
  acum, prima dată.
- [x] Frontend-ul (`src/talent-map-client/src/app/map/map.ts`) lucrează deja cu perechi
  `[longitude, latitude]` (convenția MapLibre/Turf/GeoJSON), deci ordinea de coordonate se aliniază
  natural cu formatul MongoDB — nu e nevoie de reordonare.
- [x] Niciun apel de creare de index (`CreateIndexAsync`) nu există momentan în codebase — acesta e
  primul index din proiect.

## Tasks

### Task 1: Creează modelul `MapPoint` cu proprietate `Location` de tip GeoJSON Point

**File:** `src/TalentMap.Api/Models/MapPoint.cs` (fișier nou)

- [x] Definește clasa publică `MapPoint` cu proprietățile:
  - [x] `Id` (`string?`, atribute `[BsonId]` + `[BsonRepresentation(BsonType.ObjectId)]`) — identificator
    Mongo standard.
  - [x] `Name` (`string`, `[BsonElement("name")]`) — eticheta/titlul punctului afișat pe hartă.
  - [x] `Description` (`string?`, `[BsonElement("description")]`) — detaliu opțional afișat în popup pe hartă.
  - [x] `Location` (`GeoJsonPoint<GeoJson2DGeographicCoordinates>`, `[BsonElement("location")]`) — din
    namespace-ul `MongoDB.Driver.GeoJsonObjectModel`; se serializează automat la
    `{ type: "Point", coordinates: [longitude, latitude] }`. Se folosește
    `GeoJson2DGeographicCoordinates` (constructor `(double longitude, double latitude)`), nu
    `GeoJson2DCoordinates`, pentru a reprezenta corect un punct geografic destinat unui index
    `2dsphere` (vezi Task 2) și pentru validarea intervalelor de longitudine/latitudine.
  - [x] `CreatedAt` (`DateTime`, `[BsonElement("createdAt")]`) — timestamp UTC de creare, implicit
    `DateTime.UtcNow` la instanțiere.
- [x] Adaugă `using MongoDB.Bson;`, `using MongoDB.Bson.Serialization.Attributes;`,
  `using MongoDB.Driver.GeoJsonObjectModel;` și namespace `TalentMap.Api.Models`.
- [x] Nu adăuga logică de business în model — doar proprietăți de date (POCO), consecvent cu
  stratul "Models" descris în `.ai-factory/ARCHITECTURE.md`.

### Task 2: Creează indexul 2dsphere pe `location` la pornirea aplicației

**File:** `src/TalentMap.Api/Extensions/MongoStartupExtensions.cs`

**Depinde de:** Task 1

- [x] Adaugă o constantă `private const string MapPointsCollectionName = "mapPoints";` la nivel de
  clasă `MongoStartupExtensions`.
- [x] Adaugă o metodă publică nouă `EnsureMapPointIndexesAsync(this WebApplication app)`, urmând
  exact același pattern ca `CheckMongoConnectivityAsync` (rezolvă `ILogger<Program>` și
  `IMongoDatabase` din `app.Services`, măsoară timpul cu `Stopwatch`):
  - [x] Obține colecția: `database.GetCollection<MapPoint>(MapPointsCollectionName)`.
  - [x] Construiește indexul cu `Builders<MapPoint>.IndexKeys.Geo2DSphere(p => p.Location)` și
    apelează `collection.Indexes.CreateOneAsync(new CreateIndexModel<MapPoint>(...))`.
  - [x] La succes, loghează la `LogInformation` numele colecției, numele indexului rezultat și timpul
    scurs în ms (ex: `"2dsphere index ensured on {CollectionName}.location in {ElapsedMs} ms. Index name: {IndexName}"`).
  - [x] La eroare (`try/catch`), loghează la `LogWarning` cu excepția inclusă, consecvent cu
    tratarea erorii din `CheckMongoConnectivityAsync` (nu trebuie să oprească pornirea aplicației).
  - [x] Adaugă `using MongoDB.Driver;` (deja prezent) și `using TalentMap.Api.Models;` la începutul
    fișierului.
- [x] În `src/TalentMap.Api/Program.cs`, apelează noua metodă imediat după
  `await app.CheckMongoConnectivityAsync();`: `await app.EnsureMapPointIndexesAsync();`.

## Commit Plan

Plan mic (2 task-uri) — un singur commit la finalul implementării:

```
feat(api): model geospatial map points and create 2dsphere index
```
