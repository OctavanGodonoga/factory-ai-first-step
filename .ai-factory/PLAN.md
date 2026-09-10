<!-- handoff:task:12e5f93a-5216-46e7-a990-d1bc513af637 -->
# Implementation Plan: Consumă MVT în MapLibre și stilizează punctele

Branch: main
Created: 2026-09-10

## Settings
- [x] Testing: no
- [x] Logging: verbose
- [x] Docs: no

## Context / Findings
- [x] Frontend nu e greenfield: `src/talent-map-client/src/app/map/map.ts` (+`map.html`/`map.css`)
  e deja un component standalone MapLibre GL (`app-map`, rutat lazy din `app.routes.ts`) care
  randează harta centrată pe Moldova, maschează exteriorul țării (din
  `public/data/moldova-border.geojson`) și plasează un marker temporar la click. Nu consumă
  încă niciun endpoint din API — nu există sursă de date, nici layer de puncte.
- [x] Endpoint-ul de tile-uri MVT există deja (implementat într-un plan anterior):
  `GET http://localhost:13466/api/map/points/{z}/{x}/{y}.pbf`
  (`MapTilesController`, `[AllowAnonymous]`, rate-limited, Content-Type
  `application/vnd.mapbox-vector-tile`). Layer-ul MVT se numește `mappoints` și conține
  **exact trei proprietăți per feature**: `id`, `type` (implicit `"generic"`), `status`
  (implicit `"active"`) — fără `name`/`description` (excluse explicit din encoder pentru
  minimalism). Nu există încă niciun flux de creare care să populeze `type`/`status` cu alte
  valori decât cele implicite, deci stilizarea trebuie să funcționeze corect și cu un singur
  tip/status prezent în date, cu fallback vizual clar pentru valori necunoscute viitoare.
- [x] Endpoint-ul JSON existent (`GET /api/mappoints/tile/{z}/{x}/{y}`, `MapPointDto`) **nu**
  expune `type`/`status` — doar `Id`/`Name`/`Description`/`Longitude`/`Latitude`. Nu e utilizabil
  pentru cerința de stilizare pe proprietăți; acest plan folosește exclusiv sursa vector (MVT),
  nu adaugă un flux paralel JSON.
- [x] **Gap blocant identificat:** API-ul (`src/TalentMap.Api/Program.cs`) nu are CORS configurat,
  iar clientul Angular (`ng serve` pe `http://localhost:4200`, sau containerul nginx pe
  `http://localhost:13467` din `compose.yml`) rulează pe o origine diferită de API
  (`http://localhost:13466`). Fără CORS, orice cerere de tile din browser către `.pbf` va fi
  blocată de same-origin policy — sursa vector nu se va încărca deloc. Acest plan adaugă o
  politică CORS minimă (doar GET, origini explicite din configurare) ca precondiție, altfel
  restul task-ului e imposibil de verificat funcțional.
- [x] Proiectul Angular nu are încă un sistem de `environment.ts`/`fileReplacements` (verificat în
  `angular.json`) — constantele de configurare existente (ex. stilul OpenFreeMap din `map.ts`)
  sunt deja hardcodate ca `private readonly` fields direct în component. Acest plan urmează
  aceeași convenție pentru URL-ul de bază al API-ului (nu introduce o abstracție nouă de
  environment, în afara scopului acestui task).
- [x] Ordinea layerelor contează: `moldova-mask-fill` și `moldova-border-line` sunt adăugate
  async, după ce se încarcă `moldova-border.geojson`. Layerele noi de puncte trebuie adăugate
  **după** finalizarea acelui flux, ca să se randeze deasupra măștii/graniței (MapLibre
  randează layerele în ordinea adăugării, cel mai recent adăugat deasupra).

## Tasks

### Phase 1: Precondiție — CORS pe API

- [x] **Task 1: Activează CORS pe API pentru originile clientului Angular**
  Fișiere: `src/TalentMap.Api/Program.cs`, `src/TalentMap.Api/appsettings.json`,
  `src/TalentMap.Api/appsettings.Development.json`

  - [x] În `appsettings.json`, adaugă o secțiune nouă `"Cors": { "AllowedOrigins": [
    "http://localhost:13467" ] }` (originea clientului din `compose.yml`).
  - [x] În `appsettings.Development.json`, adaugă `"Cors": { "AllowedOrigins": [
    "http://localhost:4200" ] }` (originea implicită `ng serve`), pentru a nu amesteca
    originea de dev cu cea de producție/docker.
  - [x] În `Program.cs`, înregistrează o politică CORS numită `"map-client"` cu
    `builder.Services.AddCors(options => options.AddPolicy("map-client", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
    [])
    .WithMethods("GET")
    .AllowAnyHeader()));` — doar GET, deoarece endpoint-urile publice consumate din browser
    (tile MVT + JSON) sunt exclusiv de citire; scrierile (`POST`/`PUT`) rămân autenticate prin
    API key și nu sunt apelate din acest component de hartă.
  - [x] Adaugă `app.UseCors("map-client");` în pipeline, imediat după `app.UseHttpsRedirection();`
    și înainte de `app.UseRateLimiter();` (ordinea recomandată de ASP.NET Core: CORS înaintea
    rate limiting/auth pentru ca preflight-urile `OPTIONS` să nu fie blocate de politicile de
    autentificare/rate-limit).
  - [x] Nu extinde politica la `AllowAnyOrigin`/`AllowCredentials` — nu e nevoie de credențiale
    pentru citire anonimă, iar o listă explicită de origini e mai sigură.

  LOGGING REQUIREMENTS:
  - [x] Nicio cerință de logging suplimentară — este o modificare de configurare/pipeline fără
    logică de business proprie.

### Phase 2: Consumare MVT + stilizare în MapLibre

- [x] **Task 2: Adaugă sursa vector `mappoints`, un circle layer și un symbol layer stilizate
  după `type`/`status`, în `map.ts`**
  Fișiere: `src/talent-map-client/src/app/map/map.ts`
  (depinde de Task 1 — fără CORS, cererile de tile eșuează silențios cu eroare de rețea)

  - [x] Adaugă un câmp nou `private readonly mapPointsApiBaseUrl = 'http://localhost:5205';`
    lângă celelalte constante existente (`style`, `center`, etc.), cu un comentariu scurt care
    explică hardcodarea (consistent cu restul componentului, care nu folosește încă
    `environment.ts`).
    **[REWORK 2026-09-10]** Valoarea inițială (`13466`) era portul mapat de `compose.yml` pentru
    scenariul docker-compose (client nginx pe `13467` → api pe `13466`), nu portul real al API-ului
    rulat local prin `dotnet run` (`5205`/`7004`, per `launchSettings.json`). Cum fluxul de dev
    descris în Context (`ng serve` pe `4200`) rulează API-ul local, nu prin docker, s-a corectat la
    `5205` ca să corespundă cu CORS-ul deja configurat în `appsettings.Development.json` (`4200`).
    Constanta nu poate acoperi ambele scenarii simultan fără `environment.ts` (în afara scopului) —
    scenariul docker-compose (`13466`/`13467`, auto-consistent între `compose.yml` și
    `appsettings.json`) rămâne o limitare cunoscută dacă frontend-ul e build-uit și rulat prin
    containerul nginx.
  - [x] Creează o metodă privată nouă `addMapPointsLayer(): void` care, dacă `this.map` există:
    - [x] Adaugă sursa: `this.map.addSource('mappoints', { type: 'vector', tiles: [
      `${this.mapPointsApiBaseUrl}/api/map/points/{z}/{x}/{y}.pbf`], minzoom: this.minZoom,
      maxzoom: this.maxZoom });`
    - [x] Adaugă un circle layer `'mappoints-circle'`, `source: 'mappoints'`,
      `'source-layer': 'mappoints'`, cu styling data-driven:
      - [x] `circle-radius`: `['interpolate', ['linear'], ['zoom'], 6, 3, 14, 7, 18, 10]`
      - [x] `circle-color`: `['match', ['get', 'status'], 'active', '#16a34a', 'inactive',
        '#9ca3af', /* fallback pentru statusuri necunoscute */ '#f59e0b']`
      - [x] `circle-stroke-width`: `2`
      - [x] `circle-stroke-color`: `['match', ['get', 'type'], 'generic', '#ffffff', /* fallback
        pentru tipuri necunoscute — folosește aceeași culoare albă până apar tipuri reale */
        '#ffffff']` — expresia `match` e scrisă explicit pregătită pentru extindere (adăugare
        de culori noi per tip), chiar dacă azi toate punctele au `type: "generic"`.
    - [x] Adaugă un symbol layer `'mappoints-label'`, aceeași sursă/`source-layer`, vizibil doar
      la zoom mai mare (`minzoom: 13`, pentru a evita aglomerarea vizuală la zoom redus):
      - [x] `layout`: `'text-field': ['get', 'type']`, `'text-size': 12, 'text-offset': [0, 1.4],
        'text-anchor': 'top'`
      - [x] `paint`: `'text-color': '#111827', 'text-halo-color': '#ffffff', 'text-halo-width': 1.2`
    - [x] Log `console.debug('[MapComponent] mappoints source + circle/symbol layers added', {
      tilesUrl })` la final.
  - [x] În `ngAfterViewInit`, în handler-ul `this.map.on('load', ...)`, înlănțuie apelul după ce
    granița se încarcă: schimbă `void this.loadMoldovaBorder();` în
    `void this.loadMoldovaBorder().then(() => this.addMapPointsLayer());` — garantează ordinea
    de randare (puncte deasupra măștii/graniței) conform găsirii din Context.
  - [x] Nu modifica `handleMapClick`/`showTemporaryMarker` — interacțiunea de plasare marker
    temporar rămâne neschimbată, în afara scopului acestui task.

  LOGGING REQUIREMENTS:
  - [x] `console.debug` la adăugarea sursei și fiecărui layer (poate fi un singur log combinat,
    ca mai sus), consecvent cu stilul de logging existent în `map.ts`
    (`console.debug('[MapComponent] ...')`).
  - [x] `console.error('[MapComponent] failed to add mappoints layer', error)` dacă
    `addSource`/`addLayer` aruncă excepție (ex. sursă/layer deja existent la un re-render) —
    încadrează corpul metodei într-un `try/catch`, consecvent cu `addMoldovaMaskLayer`.
