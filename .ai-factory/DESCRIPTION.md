# Talent Map

## Prezentare generală
Talent Map este o aplicație web pentru maparea și vizualizarea talentelor, competențelor
și rolurilor din cadrul unei organizații. Aplicația expune un backend .NET (ASP.NET Core
Web API) consumat de un client Angular (SPA), iar persistența datelor se face prin
fișiere JSON de pe disc, fără un motor de bază de date tradițional.

## Funcționalități principale
- Gestionarea profilelor de angajați/persoane (date de bază, roluri, departamente)
- Definirea și atribuirea competențelor/skill-urilor per persoană
- Vizualizarea hărții de talente (căutare, filtrare, agregare pe echipe/departamente)
- Operații CRUD complete expuse prin API-ul .NET, consumate de clientul Angular

> Notă: lista de mai sus este un punct de plecare dedus din numele proiectului și din
> stack-ul confirmat. Detaliază/ajustează funcționalitățile concrete cu `/aif-plan`
> sau `/aif-improve` pe măsură ce cerințele se clarifică.

## Stack tehnologic
- **Backend:** .NET (ASP.NET Core Web API)
- **Frontend:** Angular (SPA)
- **Persistență date:** Sistem de fișiere — fără server de bază de date. Fiecare
  "tabelă" este un fișier `.json` propriu, stocat în folderul `Database/` din
  rădăcina proiectului (ex: `Database/employees.json`, `Database/skills.json`).
- **ORM:** Nu se aplică — accesul la date se face prin citire/scriere directă a
  fișierelor JSON (serializare/deserializare `System.Text.Json` recomandată în
  backend), nu printr-un ORM relațional (EF Core/Dapper).
- **Integrări:** Niciuna confirmată încă.

## Note de arhitectură
- Separare clară client/server: Angular consumă exclusiv API-ul .NET prin HTTP/JSON,
  fără acces direct la fișierele din `Database/`.
- Stratul de acces la date din backend trebuie izolat într-un layer dedicat
  (ex. "repository" per entitate) care citește/scrie fișierele `.json`, astfel încât
  o eventuală migrare ulterioară către o bază de date reală să afecteze un singur strat.
- Concurența la scriere pe fișiere JSON este un risc cunoscut (race conditions la
  scrieri simultane) — se recomandă serializarea scrierilor (ex. lock per fișier) încă
  din arhitectura inițială.
- Detalii concrete de structură (foldere, pattern de acces la date, convenții API)
  sunt definite în `.ai-factory/ARCHITECTURE.md`.

## Arhitectură
Ghidul complet de arhitectură (structură de foldere, reguli de dependențe, exemple de cod)
este definit în `.ai-factory/ARCHITECTURE.md`.
Pattern: Layered Architecture

## Cerințe non-funcționale
- **Logging:** Configurabil, nivel implicit ajustabil (ex. `Logging:LogLevel` din
  `appsettings.json` pe backend).
- **Gestionarea erorilor:** Răspunsuri de eroare structurate din API (format JSON
  consecvent, coduri HTTP corecte).
- **Securitate:** Validarea datelor de intrare pe API, evitarea path traversal la
  citirea/scrierea fișierelor din `Database/`, evitarea expunerii directe a
  fișierelor JSON către client.
