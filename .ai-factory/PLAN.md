<!-- handoff:task:ac3d3f74-2b9a-4115-9f4c-6561c1e895f9 -->
# Implementation Plan: Selectarea punctelor existente și încărcarea detaliilor

Branch: main
Created: 2026-09-10

## Settings
- Testing: no
- Logging: verbose
- Docs: no

## Context / Findings
- **Nu există încă endpoint GET-by-id.** Backend-ul e ASP.NET Core + MongoDB (nu EF Core).
  `Controllers/MapPointsController.cs` are rută `[Route("api/[controller]")]` → `api/mappoints`,
  cu `[Authorize]` la nivel de clasă, și expune deja `POST` (Create) și `PUT {id}` (Update), plus
  `GET tile/{z}/{x}/{y}` (`[AllowAnonymous]`, citire publică). **Nu există un `GET {id}`.**
  Task description-ul menționează `GET /api/points/{id}`, dar ruta reală corectă — consecventă
  cu restul controller-ului și fără a introduce un al doilea prefix de API paralel — este
  **`GET api/mappoints/{id}`**. Frontend-ul trebuie să apeleze exact această rută.
- **Repository-ul are deja tot ce trebuie:** `IMapPointRepository.FindByIdAsync(string id)` e
  implementat în `Repositories/MongoMapPointRepository.cs`, dar nu e apelat de niciun serviciu/
  controller încă. `Services/IMapPointService.cs` / `Services/MapPointService.cs` trebuie extinse
  cu o metodă nouă `GetByIdAsync`, urmând exact tiparul deja folosit în `UpdateAsync` (validare
  `ObjectId.TryParse` pe id-ul primit, apoi mapare `MapPoint` → `MapPointDto` prin helper-ul
  privat `ToDto()` deja existent în `MapPointService`).
- **`MapPointDto` (`Models/MapPointDto.cs`) expune deja `Id`, `Name`, `Description`, `Longitude`,
  `Latitude`** — acestea sunt exact "datele complete" cerute de task (dincolo de `id`/`type`/
  `status`, care sunt deja disponibile din feature-ul MVT clic-uit, fără roundtrip suplimentar).
  `Type`/`Status` există pe entitatea Mongo (`Models/MapPoint.cs`) dar nu sunt expuse pe DTO —
  nu e nevoie să fie adăugate, pentru că frontend-ul le are deja din proprietățile feature-ului
  MVT selectat (`type`, `status`), evitând duplicare inutilă în răspunsul JSON.
- **CORS e deja acoperit.** Politica `"map-client"` din `Program.cs` e globală (per
  origine+metodă, nu per-rută) și restricționează doar la `GET` — o rută nouă `GET
  api/mappoints/{id}` e automat acoperită, fără nicio modificare de configurare CORS.
- **Endpoint-ul nou trebuie să fie public (`[AllowAnonymous]`)**, la fel ca `GetByTile`, pentru
  că class-level `[Authorize]` de pe `MapPointsController` ar bloca altfel citirea anonimă din
  browser — scrierile rămân protejate, doar acest `GET` nou devine excepție explicită, consecvent
  cu `GetByTile`.
- **Frontend: `HttpClient` nu e configurat deloc încă.** `app.config.ts` nu are
  `provideHttpClient(...)` în `providers`, și nu există niciun `*.service.ts` sau folder
  `services/` în `src/app` — acesta va fi primul serviciu HTTP din proiect. Singurul precedent de
  bază URL este constanta hardcodată `mapPointsApiBaseUrl = 'http://localhost:5205'` din
  `map/map.ts` (nu există `environment.ts`, convenția e hardcodare locală per component/serviciu,
  stabilită într-un plan anterior) — noul serviciu va replica aceeași constantă/valoare, cu
  același comentariu explicativ, mai degrabă decât să introducă o abstracție de configurare nouă.
- **Nu există niciun precedent de UI pentru "selecție"/"highlight"/popup.** Singurul precedent
  vizual e `temporaryMarker` din `map.ts` (`showTemporaryMarker`): un singur `maplibregl.Marker`,
  păstrat într-un field, `.remove()` înainte de fiecare înlocuire. Acest plan urmează exact
  același tipar pentru un nou field `selectedMarker`, cu o culoare distinctă, pentru evidențierea
  punctului selectat — nu introduce `feature-state`/paint expressions noi (ar fi un tipar
  suplimentar fără precedent, în afara scopului minim al acestui task).
- **`handleMapClick` are deja logica de gating pe granița Moldovei** (verifică
  `booleanPointInPolygon` înainte de a plasa `temporaryMarker`). Noua logică de selecție a unui
  punct existent trebuie inserată **înaintea** acelui gating (un click pe un punct existent poate
  fi oriunde vizibil pe hartă, nu doar în interiorul granițelor Moldovei — punctele existente pot
  fi lângă graniță), folosind `this.map.queryRenderedFeatures(event.point, { layers:
  ['mappoints-circle'] })` direct în `handleMapClick`, ca sursă unică de adevăr (nu un listener
  separat `map.on('click', 'mappoints-circle', ...)`, care ar rula în paralel cu handler-ul
  general și ar duplica logica de branching).
- **`map.html` conține azi doar `<div #mapContainer>`, fără nicio zonă de UI pentru detalii.**
  Pentru a verifica vizual că datele complete s-au încărcat (cerință explicită a task-ului:
  "încarcă datele complete"), acest plan adaugă un panou minimal de detalii (nume, descriere,
  tip, status), populat dintr-un `signal` nou pe component — nu un `maplibregl.Popup` (ar
  introduce un tipar UI suplimentar peste marker-ul de highlight, fără beneficiu clar pentru
  scopul minim al task-ului).

## Tasks

### Phase 1: Backend — endpoint GET api/mappoints/{id}

- [ ] **Task 1: Adaugă `GetByIdAsync` în serviciu + acțiune `GetById` în controller**
  Fișiere: `src/TalentMap.Api/Services/IMapPointService.cs`,
  `src/TalentMap.Api/Services/MapPointService.cs`,
  `src/TalentMap.Api/Controllers/MapPointsController.cs`

  - [ ] În `IMapPointService`, adaugă `Task<MapPointDto?> GetByIdAsync(string id);`.
  - [ ] În `MapPointService`, implementează `GetByIdAsync`: validează `id` cu
    `ObjectId.TryParse` (exact tiparul folosit deja în `UpdateAsync` — dacă parsing-ul eșuează,
    întoarce `null` fără a lovi baza de date), apoi apelează
    `await _repository.FindByIdAsync(id)`; dacă rezultatul e `null`, întoarce `null`; altfel
    mapează cu helper-ul privat existent `ToDto()` și întoarce `MapPointDto`.
  - [ ] În `MapPointsController`, adaugă:
    ```csharp
    [HttpGet("{id}")]
    [AllowAnonymous]
    [EnableRateLimiting("mappoints-read")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _mapPointService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }
    ```
    plasată lângă acțiunea `Update` existentă, cu `[AllowAnonymous]` explicit pentru a suprascrie
    `[Authorize]`-ul de la nivel de clasă (consecvent cu `GetByTile` din `MapTilesController`) și
    `[EnableRateLimiting("mappoints-read")]` (aceeași politică de rate limiting ca celelalte
    citiri publice, nu `"mappoints-write"`).
  - [ ] Nu adăuga `Type`/`Status` pe `MapPointDto` — rămân în afara scopului acestui task (deja
    disponibile din feature-ul MVT pe partea de client, per Context).

  LOGGING REQUIREMENTS:
  - [ ] La intrare în acțiunea `GetById`: `_logger.LogInformation("GetById action called. Route:
    GET api/mappoints/{Id}", id);` — consecvent cu stilul de logging existent al
    `MapPointsController` (verifică formatul exact folosit de `Create`/`Update` și replică-l).
  - [ ] Dacă `id` nu e un `ObjectId` valid sau punctul nu există: `_logger.LogWarning("GetById:
    point not found or invalid id {Id}", id);` în serviciu, înainte de a întoarce `null`.
  - [ ] Dacă punctul e găsit: `_logger.LogDebug("GetById: point {Id} found", id);` în serviciu.

### Phase 2: Frontend — HttpClient, serviciu, selecție + highlight + panou de detalii

- [ ] **Task 2: Activează `HttpClient` și creează `MapPointsService`**
  Fișiere: `src/talent-map-client/src/app/app.config.ts`,
  `src/talent-map-client/src/app/map/map-points.service.ts` (nou)
  (depinde de Task 1 — fără endpoint-ul backend, serviciul n-are ce apela)

  - [ ] În `app.config.ts`, adaugă `provideHttpClient()` (import din `@angular/common/http`) în
    array-ul `providers`, alături de `provideBrowserGlobalErrorListeners()` și `provideRouter(routes)`.
  - [ ] Creează `map-points.service.ts` ca `@Injectable({ providedIn: 'root' })`, cu:
    - [ ] Constanta `private readonly apiBaseUrl = 'http://localhost:5205';`, cu același
      comentariu explicativ ca `mapPointsApiBaseUrl` din `map.ts` (nu există `environment.ts`,
      hardcodare locală e convenția stabilită).
    - [ ] Un `interface MapPointDetails { id: string; name: string; description: string | null;
      longitude: number; latitude: number; }` exportat, care oglindește exact câmpurile din
      `MapPointDto` backend (Task 1).
    - [ ] O metodă `getById(id: string): Observable<MapPointDetails>` care face
      `this.http.get<MapPointDetails>(`${this.apiBaseUrl}/api/mappoints/${id}`)` (ruta corectă
      identificată în Context, nu `/api/points/{id}`).
    - [ ] `HttpClient` injectat prin `private readonly http = inject(HttpClient);`.

  LOGGING REQUIREMENTS:
  - [ ] `console.debug('[MapPointsService] fetching point details', { id })` la începutul
    `getById`, consecvent cu stilul `[ComponentName] message {data}` deja folosit în `map.ts`.

- [ ] **Task 3: Detectează click pe un punct existent, evidențiază-l și încarcă detaliile**
  Fișiere: `src/talent-map-client/src/app/map/map.ts`
  (depinde de Task 2)

  - [ ] Injectează `MapPointsService` (`private readonly mapPointsService =
    inject(MapPointsService);`) și importă `MapPointDetails`.
  - [ ] Adaugă un signal nou `readonly selectedPoint = signal<MapPointDetails | null>(null);`
    (import `signal` din `@angular/core`) și un field nou `private selectedMarker:
    maplibregl.Marker | undefined;` (oglindește exact tiparul `temporaryMarker`).
  - [ ] În `handleMapClick`, **înaintea** verificării `insideMoldova`/`showTemporaryMarker`,
    adaugă: `const hits = this.map?.queryRenderedFeatures(event.point, { layers:
    ['mappoints-circle'] }) ?? [];`. Dacă `hits.length > 0`, extrage primul feature, extrage
    `id` din `feature.properties['id']` (convertit explicit la `string` cu `String(...)`, pentru
    siguranță față de encodarea MVT), apelează o metodă nouă `selectExistingPoint(id,
    feature.geometry)` și **`return` imediat** (nu continua spre logica de `temporaryMarker` —
    un click pe un punct existent nu trebuie să plaseze și un marker temporar de creare).
  - [ ] Creează metoda privată `private selectExistingPoint(id: string, geometry:
    GeoJSON.Geometry): void`:
    - [ ] Ghid pentru coordonate: geometry-ul feature-ului din `queryRenderedFeatures` e deja în
      coordonate ecran-proiectate corect de MapLibre pentru un `Point` — extrage `lngLat` din
      `(geometry as GeoJSON.Point).coordinates` ca `[number, number]`.
    - [ ] Evidențiază punctul: `this.selectedMarker?.remove(); this.selectedMarker = new
      maplibregl.Marker({ color: '#2563eb' }).setLngLat(lngLat).addTo(this.map!);` (culoare
      distinctă albastră, diferită de galbenul `temporaryMarker`-ului, pentru a nu fi confundate
      vizual).
    - [ ] Apelează `this.mapPointsService.getById(id)` (RxJS `subscribe`, cu
      `takeUntilDestroyed(this.destroyRef)` — import din `@angular/core/rxjs-interop` — pentru
      curățare automată la distrugerea componentului) și, la succes, `this.selectedPoint.set(details)`;
      la eroare, `this.selectedPoint.set(null)` și loghează eroarea.
  - [ ] În `destroyRef.onDestroy`, adaugă și curățarea noului marker:
    `this.selectedMarker?.remove(); this.selectedMarker = undefined;` — oglindește exact
    curățarea existentă pentru `temporaryMarker`.
  - [ ] Nu modifica stilizarea `mappoints-circle`/`mappoints-label` din `addMapPointsLayer` —
    highlight-ul se face exclusiv prin marker-ul nou, nu prin `feature-state`/paint expressions.

  LOGGING REQUIREMENTS:
  - [ ] `console.debug('[MapComponent] existing point clicked', { id })` imediat ce un feature e
    detectat prin `queryRenderedFeatures`.
  - [ ] `console.debug('[MapComponent] point details loaded', { id, details })` la succesul
    apelului `getById`.
  - [ ] `console.error('[MapComponent] failed to load point details', { id, error })` la eroare.

- [ ] **Task 4: Panou minimal de detalii legat de `selectedPoint`**
  Fișiere: `src/talent-map-client/src/app/map/map.html`, `src/talent-map-client/src/app/map/map.css`
  (depinde de Task 3)

  - [ ] În `map.html`, adaugă sub `<div #mapContainer>` un bloc condiționat de `@if
    (selectedPoint(); as point)`, care afișează `point.name`, `point.description` (fallback text
    dacă e `null`), `point.longitude`, `point.latitude` într-un container poziționat absolut
    peste hartă (ex. colț din dreapta-sus).
  - [ ] În `map.css`, adaugă stiluri minime pentru acest panou (poziționare absolută, fundal alb
    semi-opac, padding, `z-index` peste canvas-ul hărții) — consecvent cu stilul minimalist deja
    prezent în `map.css` (doar dimensionare container, fără framework CSS suplimentar).
  - [ ] Nu adăuga buton de închidere/logica de deselectare — în afara scopului acestui task (un
    nou click pe alt punct înlocuiește deja selecția curentă, prin `.set(...)`; comportamentul de
    deselectare explicită poate fi adăugat într-un task viitor).

  LOGGING REQUIREMENTS:
  - [ ] Niciuna suplimentară — acest task e strict template/CSS, fără logică nouă (logging-ul
    relevant e deja acoperit de Task 3, la sursa datelor).

## Commit Plan
Sub pragul de 5 task-uri — un singur commit la final, ex.: `feat(map): select existing points on click and load full details via GET /api/mappoints/{id}`.
