# AGENTS.md

> Acest fișier este întreținut de skill-urile AI Factory (`/aif`, `/aif-docs`). Actualizează-l
> manual doar pentru corecturi punctuale; structura generată automat va fi resincronizată la
> rulările viitoare ale acestor skill-uri.

## Prezentare generală a proiectului
Talent Map — aplicație web pentru maparea talentelor/competențelor dintr-o organizație, cu
backend .NET (ASP.NET Core Web API), client Angular (SPA) și persistență pe fișiere `.json`
(fără server de bază de date). Detalii complete în `.ai-factory/DESCRIPTION.md`.

## Stack tehnologic
- **Backend:** .NET (ASP.NET Core Web API)
- **Frontend:** Angular (SPA)
- **Persistență date:** Fișiere `.json` în folderul `Database/` (nu există încă un ORM sau
  server de bază de date — vezi `.ai-factory/DESCRIPTION.md` și `.ai-factory/ARCHITECTURE.md`)

## Structura proiectului

Scaffolding-ul inițial al celor două proiecte (backend .NET 10 Web API și client Angular) a
fost creat. Foldere precum `Repositories/`, `Services/`, `Middleware/` și `Models/` există
deja, dar sunt încă goale — funcționalitățile de business (ex. entitățile Employee/Skill,
folderul `Database/`) urmează să fie adăugate prin planuri viitoare, conform
`.ai-factory/ARCHITECTURE.md`.

```
tallent_map_md/
├── .ai-factory/           # Specificații AI Factory: DESCRIPTION.md, ARCHITECTURE.md, config.yaml, rules/
├── .claude/                # Skill-uri și agenți Claude Code instalate în proiect (aif-*, dotnet-webapi, angular-developer)
├── .mcp.json               # Configurare servere MCP la nivel de proiect (filesystem, chromeDevtools, playwright)
├── .ai-factory.json        # Manifestul intern al framework-ului AI Factory (skill-uri/agenți gestionate)
├── skills-lock.json        # Lock file pentru skill-urile instalate din skills.sh
├── TalentMap.slnx          # Soluția .NET (format .slnx, implicit în SDK .NET 10)
└── src/
    ├── TalentMap.Api/       # Backend ASP.NET Core Web API (.NET 10, Controllers-based)
    │   ├── Controllers/     # Handlere HTTP (gol deocamdată)
    │   ├── Services/        # Logică de business (gol deocamdată)
    │   ├── Repositories/    # Acces la fișierele din viitorul Database/ (gol deocamdată)
    │   ├── Models/           # Modele de domeniu / DTO-uri (gol deocamdată)
    │   ├── Middleware/       # Cross-cutting: auth, erori, logging (gol deocamdată)
    │   └── Program.cs        # Bootstrap-ul aplicației
    └── talent-map-client/    # Frontend Angular (SPA, ultima versiune stabilă)
        ├── angular.json      # Configurare Angular CLI
        └── src/
            ├── main.ts       # Bootstrap-ul aplicației Angular
            └── app/          # Componenta rădăcină + rutare (feature module-urile vor fi adăugate ulterior)
```

## Puncte de intrare cheie

| Fișier | Scop |
|--------|------|
| `.ai-factory/config.yaml` | Configurare AI Factory pentru acest proiect (limbă, căi, git, reguli) |
| `.ai-factory/DESCRIPTION.md` | Specificația proiectului: funcționalități, stack, note de arhitectură |
| `.mcp.json` | Servere MCP disponibile agenților AI în acest proiect |
| `TalentMap.slnx` | Soluția .NET care leagă proiectul backend |
| `src/TalentMap.Api/Program.cs` | Punctul de bootstrap al backend-ului ASP.NET Core Web API |
| `src/talent-map-client/src/main.ts` | Punctul de bootstrap al aplicației Angular |

## Documentație

| Document | Cale | Descriere |
|----------|------|-----------|
| README | — | Nu există încă. Rulează `/aif-docs` pentru a genera un README și documentație detaliată. |

## Fișiere de context AI

| Fișier | Scop |
|--------|------|
| AGENTS.md | Harta structurală a proiectului pentru agenți AI (acest fișier) |
| .ai-factory/DESCRIPTION.md | Specificația proiectului (funcționalități, stack, arhitectură la nivel înalt) |
| .ai-factory/ARCHITECTURE.md | Pattern de arhitectură, structura de foldere, reguli de dependențe |
| .ai-factory/rules/base.md | Convenții de bază (denumire, structură module, erori, logging) |
| ~/.claude/CLAUDE.md | Instrucțiuni globale ale utilizatorului, valabile pentru toate proiectele |

## Reguli pentru agenți

- Descompune comenzile shell înlănțuite (`&&`, `;`) în pași separați, astfel încât fiecare
  comandă și rezultatul ei să poată fi verificate individual înainte de a continua.
  - Exemplu incorect (combinat): `dotnet build && dotnet test`
  - Exemplu corect (descompus): mai întâi `dotnet build`, apoi, doar dacă reușește, `dotnet test`
- Proiectul nu folosește încă git (`git.enabled: false` în `.ai-factory/config.yaml`) — skill-urile
  care presupun un branch de bază (`/aif-plan full`, `/aif-review`, `/aif-verify`) rulează în
  modul fără git, fără creare de branch-uri.
