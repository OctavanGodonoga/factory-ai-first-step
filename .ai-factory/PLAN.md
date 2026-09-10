<!-- handoff:task:b0ca18fb-6ff5-4aa4-85af-c33bffd66e25 -->
# Implementation Plan: Implementează salvarea și validarea server-side

Branch: main
Created: 2026-09-10

## Settings
- [ ] Testing: no
- [ ] Logging: verbose
- [ ] Docs: no

## Context relevant din codebase

- [x] Modelul `MapPoint` există deja (`src/TalentMap.Api/Models/MapPoint.cs`), cu proprietatea
  `Location` de tip `GeoJsonPoint<GeoJson2DGeographicCoordinates>` și index `2dsphere` creat la
  pornire (`MongoStartupExtensions.EnsureMapPointIndexesAsync`, colecție `mapPoints`). Numele
  colecției e momentan definit ca `private const string MapPointsCollectionName` doar în
  `MongoStartupExtensions.cs` — trebuie mutat/reexpus astfel încât noul Repository să nu-l
  duplice.
- [x] Nu există încă niciun folder `Controllers/`, `Services/` sau `Repositories/` în
  `src/TalentMap.Api/` — acesta este primul endpoint HTTP de business din proiect. Se respectă
  `.ai-factory/ARCHITECTURE.md` (Layered Architecture): Controllers → Services → Repositories.
- [x] Validarea "coordonata e în interiorul Moldovei" există deja **doar pe client**, în
  `src/talent-map-client/src/app/map/map.ts` (`booleanPointInPolygon` din Turf, aplicat pe
  poligonul încărcat din `public/data/moldova-border.geojson`). Sarcina cere explicit ca această
  validare să fie făcută și server-side, în ASP.NET Core, înainte de salvare — deci nu ne putem
  baza doar pe verificarea din client.
- [x] `src/talent-map-client/public/data/moldova-border.geojson` conține un `Feature` de tip
  `Polygon` cu **un singur ring** (1149 puncte, fără găuri/holes) — confirmat prin inspecție.
  Aceasta simplifică algoritmul server-side de point-in-polygon (nu trebuie tratate ring-uri
  interioare).
- [x] Proiectul `TalentMap.Api.csproj` nu are încă niciun `<Content Include>` — fișierele
  suplimentare (ex. geojson-ul de frontieră) nu sunt copiate automat în output-ul de build; trebuie
  adăugat explicit un item `Content` cu `CopyToOutputDirectory`.
- [x] `MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates` validează la construcție
  intervalele standard de longitudine/latitudine (-180..180 / -90..90), dar aruncă propria
  excepție dacă e construit direct cu valori invalide — validarea de interval trebuie făcută
  explicit în Service **înainte** de a construi acest tip, ca să controlăm mesajul de eroare
  întors clientului (400, nu 500).
- [x] Nu există încă niciun endpoint GET pentru `MapPoint` — task-ul cere doar creare/modificare
  (POST + PUT), fără listare/citire individuală.

## Tasks

### Faza 1: Validare geometrică server-side (frontiera Moldovei)

- [x] Task 1: Adaugă poligonul de frontieră al Moldovei ca resursă a backend-ului
  - [x] **Files:** `src/TalentMap.Api/Data/moldova-border.geojson` (nou, copie identică din
    `src/talent-map-client/public/data/moldova-border.geojson`), `src/TalentMap.Api/TalentMap.Api.csproj`
  - [x] Copiază fișierul geojson (nu-l regenera manual — trebuie să fie byte-identic cu cel din
    client, ca validarea server-side să corespundă exact cu ce vede utilizatorul pe hartă).
  - [x] În `.csproj`, adaugă:
    ```xml
    <ItemGroup>
      <Content Include="Data\moldova-border.geojson">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      </Content>
    </ItemGroup>
    ```
  - [x] LOGGING: nu se aplică (task fără cod executabil).

- [x] Task 2: Creează serviciul `IMoldovaBorderValidator` / `MoldovaBorderValidator`
  - [x] **Files:** `src/TalentMap.Api/Services/IMoldovaBorderValidator.cs`,
    `src/TalentMap.Api/Services/MoldovaBorderValidator.cs`, `src/TalentMap.Api/Program.cs`
  - [x] **Depinde de:** Task 1
  - [x] `IMoldovaBorderValidator` expune `bool IsInside(double longitude, double latitude);`.
  - [x] `MoldovaBorderValidator` (înregistrat `Singleton`):
    - [x] În constructor, citește fișierul de la
      `Path.Combine(AppContext.BaseDirectory, "Data", "moldova-border.geojson")`, îl parsează cu
      `System.Text.Json` (`JsonDocument`) și extrage `geometry.coordinates[0]` (singurul ring)
      într-un `IReadOnlyList<(double Longitude, double Latitude)>` imutabil, păstrat în memorie
      pentru durata de viață a aplicației (nu se re-citește la fiecare request).
    - [x] Implementează `IsInside` cu algoritmul standard de point-in-polygon prin ray-casting
      (even-odd rule), echivalent semantic cu `booleanPointInPolygon` din Turf folosit pe client.
    - [x] Dacă fișierul lipsește sau nu poate fi parsat, aruncă excepție din constructor (fail-fast
      la pornirea aplicației — nu vrem un validator "mut" care acceptă orice coordonată).
  - [x] În `Program.cs`, înregistrează `builder.Services.AddSingleton<IMoldovaBorderValidator, MoldovaBorderValidator>();`
    înainte de `builder.Build()`.
  - [x] LOGGING (verbose):
    - [x] La constructor: `LogInformation` cu numărul de puncte din ring și calea fișierului încărcat.
    - [x] La eroare de încărcare/parsare: `LogError` cu excepția inclusă, înainte de a arunca mai
      departe.
    - [x] La fiecare apel `IsInside`: `LogDebug` cu longitude/latitude și rezultatul boolean.

### Faza 2: Endpoint de creare/modificare cu validare completă

<!-- Commit checkpoint: tasks 1-2 -->

- [x] Task 3: Creează DTO-ul de request pentru creare/actualizare
  - [x] **File:** `src/TalentMap.Api/Models/MapPointRequest.cs` (nou)
  - [x] Clasă publică `MapPointRequest` cu `Name` (`string`), `Description` (`string?`),
    `Longitude` (`double`), `Latitude` (`double`) — folosită identic pentru POST (creare) și PUT
    (actualizare), fără duplicare de tip.
  - [x] LOGGING: nu se aplică (DTO fără logică).

- [x] Task 4: Expune numele colecției Mongo ca o singură sursă de adevăr și creează Repository-ul
  - [x] **Files:** `src/TalentMap.Api/Models/MapPoint.cs`,
    `src/TalentMap.Api/Extensions/MongoStartupExtensions.cs`,
    `src/TalentMap.Api/Repositories/IMapPointRepository.cs` (nou),
    `src/TalentMap.Api/Repositories/MongoMapPointRepository.cs` (nou), `src/TalentMap.Api/Program.cs`
  - [x] În `MapPoint.cs`, adaugă `public const string CollectionName = "mapPoints";` la nivel de clasă.
  - [x] În `MongoStartupExtensions.cs`, elimină `private const string MapPointsCollectionName` și
    înlocuiește toate referințele cu `MapPoint.CollectionName`, ca să nu existe două constante
    care pot diverge.
  - [x] `IMapPointRepository`:
    - [x] `Task<MapPoint> InsertAsync(MapPoint point)` — inserează documentul, întoarce entitatea cu
      `Id`-ul generat de Mongo populat.
    - [x] `Task<bool> ReplaceAsync(string id, MapPoint point)` — înlocuiește documentul existent
      (`Builders<MapPoint>.Filter.Eq(p => p.Id, id)`); întoarce `false` dacă
      `ReplaceOneResult.MatchedCount == 0` (id inexistent).
    - [x] `Task<MapPoint?> FindByIdAsync(string id)` — adăugat față de planul inițial, necesar pentru
      Task 5 (citirea documentului existent înainte de `ReplaceAsync`, ca să păstrăm `CreatedAt`).
  - [x] `MongoMapPointRepository` implementează interfața folosind
    `IMongoDatabase.GetCollection<MapPoint>(MapPoint.CollectionName)`.
  - [x] În `Program.cs`, înregistrează `builder.Services.AddSingleton<IMapPointRepository, MongoMapPointRepository>();`.
  - [x] LOGGING (verbose):
    - [x] La `InsertAsync`: `LogInformation` cu id-ul rezultat și durata operației (`Stopwatch`,
      consecvent cu stilul din `MongoStartupExtensions`).
    - [x] La `ReplaceAsync`: `LogInformation` la succes cu id-ul și durata; `LogWarning` dacă
      documentul nu a fost găsit (`MatchedCount == 0`).

- [x] Task 5: Creează serviciul de business `MapPointService` cu validarea completă
  - [x] **Files:** `src/TalentMap.Api/Services/IMapPointService.cs`,
    `src/TalentMap.Api/Services/MapPointService.cs`,
    `src/TalentMap.Api/Models/MapPointValidationException.cs` (nou), `src/TalentMap.Api/Program.cs`
  - [x] **Depinde de:** Task 2, Task 3, Task 4
  - [x] `MapPointValidationException(IReadOnlyList<string> Errors)` — excepție dedicată ce poartă
    toate mesajele de eroare acumulate (nu doar prima), folosită de Controller pentru a construi
    un răspuns 400 cu toate problemele de validare deodată.
  - [x] `IMapPointService`:
    - [x] `Task<MapPoint> CreateAsync(MapPointRequest request)`
    - [x] `Task<MapPoint?> UpdateAsync(string id, MapPointRequest request)` — `null` dacă id-ul nu
      există în colecție **sau dacă `id` nu are un format valid de `ObjectId`**.
  - [x] **Validare format `id` (doar `UpdateAsync`, înainte de orice altă validare):** verifică
    `MongoDB.Bson.ObjectId.TryParse(id, out _)`; dacă e `false`, întoarce direct `null` fără a
    apela Repository-ul. Motiv: `MapPoint.Id` e `string` cu `[BsonRepresentation(BsonType.ObjectId)]`,
    deci un `id` din rută care nu e un `ObjectId` hex valid (ex. `"abc"`) ar arunca
    `System.FormatException` din driverul Mongo la construirea filtrului (`Builders<MapPoint>.Filter.Eq`),
    rezultând într-un 500 necontrolat în loc de 404 curat.
  - [x] Validări (acumulate toate înainte de a decide dacă se aruncă excepția — nu early-return la
    prima eroare):
    - [x] `Name` obligatoriu (`string.IsNullOrWhiteSpace(request.Name)` — nu doar `.Trim()`, ca să nu
      arunce `NullReferenceException` dacă `Name` ajunge `null` din deserializare).
    - [x] `Longitude` ∈ [-180, 180], `Latitude` ∈ [-90, 90] — verificat explicit **înainte** de a
      construi `GeoJson2DGeographicCoordinates`, ca să nu lăsăm excepția internă a
      `MongoDB.Driver` să iasă necontrolat din Service.
    - [x] Punctul `(Longitude, Latitude)` trebuie să fie în interiorul Moldovei — verificat cu
      `IMoldovaBorderValidator.IsInside(...)`; dacă `false`, adaugă eroarea
      `"Coordonatele trebuie să fie în interiorul Moldovei."`.
  - [x] Dacă lista de erori nu e goală → aruncă `MapPointValidationException`, fără a apela
    Repository-ul.
  - [x] La succes: construiește `MapPoint` cu
    `Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(request.Longitude, request.Latitude))`
    și apelează `InsertAsync` (creare) / citește + `ReplaceAsync` păstrând `CreatedAt` original
    (actualizare).
  - [x] Înregistrează `builder.Services.AddScoped<IMapPointService, MapPointService>();` în
    `Program.cs`.
  - [x] LOGGING (verbose):
    - [x] La intrare în `CreateAsync`/`UpdateAsync`: `LogInformation` cu Name și coordonatele primite.
    - [x] Când `id`-ul din `UpdateAsync` nu e un `ObjectId` valid: `LogWarning` cu valoarea primită.
    - [x] Când validarea eșuează: `LogWarning` cu lista completă de erori.
    - [x] La succes: `LogInformation` cu id-ul rezultat al documentului creat/actualizat.

- [x] Task 6: Creează `MapPointsController` cu endpoint-urile POST și PUT
  - [x] **File:** `src/TalentMap.Api/Controllers/MapPointsController.cs` (nou)
  - [x] **Depinde de:** Task 5
  - [x] `[ApiController] [Route("api/[controller]")]` (rută rezultată: `api/mappoints`).
  - [x] `[HttpPost]` → apelează `_mapPointService.CreateAsync(request)`; la succes întoarce
    `Created($"/api/mappoints/{result.Id}", result)` (201, fără a necesita o acțiune GET
    înregistrată — nu există GET în scopul acestui task).
  - [x] `[HttpPut("{id}")]` → apelează `_mapPointService.UpdateAsync(id, request)`; `404 NotFound()`
    dacă rezultatul e `null`; altfel `200 OK(result)`.
  - [x] `catch (MapPointValidationException ex)` → `400` prin `ValidationProblem(...)` sau
    `BadRequest(new { errors = ex.Errors })`, populând toate mesajele din excepție.
  - [x] LOGGING (verbose):
    - [x] La intrarea în fiecare acțiune: `LogInformation` cu numele acțiunii și route-ul.
    - [x] La 400 (validare eșuată): `LogWarning` cu erorile.
    - [x] La 404 (update pe id inexistent): `LogWarning` cu id-ul căutat.
    - [x] Excepțiile neașteptate (altele decât `MapPointValidationException`) NU se înghit — se lasă
      să propage către middleware-ul implicit de error handling, doar logate la nivel `LogError`
      înainte de re-throw dacă e nevoie de context suplimentar.

<!-- Commit checkpoint: tasks 3-6 -->

## Commit Plan

Plan cu 6 task-uri — două commit-uri, pe faze:

```
Commit 1 (după task-urile 1-2): feat(api): add server-side Moldova border point-in-polygon validator
Commit 2 (după task-urile 3-6): feat(api): add map point create/update endpoint with server-side validation
```
