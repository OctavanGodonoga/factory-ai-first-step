<!-- handoff:task:f4b673d2-ca71-46ab-b454-e783ae9214a4 -->
# Plan: Implementează selectarea coordonatelor doar în Moldova

**Mode:** Fast
**Created:** 2026-09-10

## Description

Integrează Turf.js (deja instalat ca dependință — `@turf/turf@^7.4.0`), validează click-ul
utilizatorului pe hartă printr-un test point-in-polygon față de granița Moldovei, și afișează
un punct/marker temporar pe hartă doar dacă coordonata click-uită este în interiorul poligonului
Moldovei. Click-urile în afara graniței nu trebuie să producă niciun marker vizibil.

## Settings

- [x] **Testing:** No — nu se scriu teste în acest plan
- [x] **Logging:** Verbose — urmează convenția existentă din `map.ts` (`console.debug` pentru flux
  normal, `console.error` pentru erori), consecvent cu restul componentei
- [x] **Docs:** No — warn-only, fără checkpoint obligatoriu de documentare

## Context relevant din codebase

- [x] Componenta hărții: `src/talent-map-client/src/app/map/map.ts` (MapLibre GL + `@turf/turf`)
- [x] Harta e inițializată în `ngAfterViewInit()`; granița Moldovei se încarcă asincron în
  `loadMoldovaBorder()` (`map.on('load', ...)`) și e stocată în proprietatea privată
  `moldovaBorder: Feature<Polygon | MultiPolygon> | undefined`.
- [x] Nu există încă niciun handler de click pe hartă (`map.on('click', ...)` nu există momentan).
- [x] `@turf/turf` e deja importat parțial (`bboxPolygon`, `mask`); trebuie adăugat și
  `booleanPointInPolygon` (și `point`, pentru a construi feature-ul de test) din același pachet.
- [x] Nu există niciun model de "coordonată"/"locație" (lat/lng) definit altundeva în aplicație —
  această funcționalitate e complet auto-conținută în `MapComponent`.

## Tasks

### Task 1: Adaugă handler de click pe hartă cu validare point-in-polygon

**File:** `src/talent-map-client/src/app/map/map.ts`

- [x] Înregistrează un handler `this.map.on('click', (event) => { ... })` în `ngAfterViewInit()`,
  imediat lângă handler-ele existente `load`/`error` (după linia 51).
- [x] În handler:
  - [x] Extrage coordonatele clic-ului din `event.lngLat` (`{ lng, lat }`).
  - [x] Dacă `this.moldovaBorder` nu este încă încărcat (`undefined`), loghează un warning
    (`console.warn('[MapComponent] click ignored: Moldova border not loaded yet')`) și
    returnează fără alt efect.
  - [x] Construiește un punct Turf cu `point([lng, lat])` din `@turf/turf` și verifică
    apartenența la poligon cu `booleanPointInPolygon(clickPoint, this.moldovaBorder)`.
  - [x] Loghează rezultatul verificării la nivel `console.debug`, incluzând coordonatele și
    rezultatul boolean (ex: `console.debug('[MapComponent] click point-in-polygon check', { lng, lat, insideMoldova })`).
  - [x] Dacă rezultatul e `false`, nu face nimic altceva (nu afișează niciun punct) — comportamentul
    implicit e "ignoră click-ul".
  - [x] Dacă rezultatul e `true`, apelează `this.showTemporaryMarker(event.lngLat)` (metoda din Task 2),
    pasând direct `event.lngLat` (tip `maplibregl.LngLat`, compatibil cu `LngLatLike`) — nu e nevoie
    să reconstruiești un obiect `{ lng, lat }` separat.
- [x] Importă `booleanPointInPolygon` și `point` din `@turf/turf` alături de `bboxPolygon`, `mask`
  existente (linia 4).

### Task 2: Afișează/actualizează un marker temporar pentru click-uri valide

**File:** `src/talent-map-client/src/app/map/map.ts`

**Depinde de:** Task 1

- [x] Adaugă o proprietate privată `private temporaryMarker: maplibregl.Marker | undefined;` lângă
  celelalte proprietăți private ale clasei (lângă `map`, `moldovaBorder`).
- [x] Adaugă o metodă privată `showTemporaryMarker(lngLat: maplibregl.LngLatLike): void` care:
  - [x] Elimină marker-ul anterior dacă există (`this.temporaryMarker?.remove()`), astfel încât să
    existe cel mult un singur punct temporar afișat odată.
  - [x] Creează un nou `new maplibregl.Marker({ color: '#ffcc00' }).setLngLat(lngLat).addTo(this.map)`.
  - [x] Salvează noua instanță în `this.temporaryMarker`.
  - [x] Loghează la `console.debug` crearea marker-ului cu coordonatele (ex:
    `console.debug('[MapComponent] temporary marker placed', { lngLat })`).
- [x] Apelează această metodă din handler-ul de click (Task 1) doar în ramura `insideMoldova === true`.
- [x] În callback-ul existent `destroyRef.onDestroy(...)` (liniile 53-57), adaugă și eliminarea
  marker-ului temporar (`this.temporaryMarker?.remove(); this.temporaryMarker = undefined;`)
  pentru a evita referințe orfane la distrugerea componentei.

## Commit Plan

Plan mic (2 task-uri) — un singur commit la finalul implementării:

```
feat(map): restrict temporary point selection to inside Moldova border
```
