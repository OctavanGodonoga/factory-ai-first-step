[← Getting Started](getting-started.md) · [Back to README](../README.md) · [Deployment →](deployment.md)

# Architecture

## Pattern: Layered Architecture

Fiecare strat depinde exclusiv de stratul de sub el:

```
Controllers → Services → Repositories → Database/*.json
```

Ales pentru simplitate și viteză de dezvoltare, potrivit unei aplicații CRUD cu o
echipă mică și logică de business încă neconturată. Detalii complete, exemple de
cod și anti-pattern-uri: [.ai-factory/ARCHITECTURE.md](../.ai-factory/ARCHITECTURE.md).

## Structura proiectului

```
tallent_map_md/
├── TalentMap.slnx              # Soluția .NET
├── compose.yml                 # Docker Compose (api + client)
└── src/
    ├── TalentMap.Api/          # Backend ASP.NET Core Web API (.NET 10)
    │   ├── Controllers/        # Handlere HTTP
    │   ├── Services/           # Logică de business
    │   ├── Repositories/       # Acces la date (viitor: fișiere Database/*.json)
    │   ├── Models/             # Modele de domeniu / DTO-uri
    │   ├── Middleware/         # Auth, erori, logging
    │   ├── Program.cs
    │   └── Dockerfile
    └── talent-map-client/      # Frontend Angular (SPA)
        ├── src/app/            # Componenta rădăcină + rutare
        └── Dockerfile
```

## Persistență date

Nu există (încă) un server de bază de date sau ORM. Planul este ca fiecare
"tabelă" să fie un fișier `.json` propriu în folderul `Database/` (nu există încă
în cod — va apărea odată cu primele entități de business). Accesul la aceste
fișiere va fi izolat exclusiv în stratul `Repositories/`.

## Regula de dependențe

- ✅ Controllers → Services → Repositories
- ✅ Angular comunică cu API-ul .NET exclusiv prin HTTP/JSON
- ❌ Niciun cod din afara `Repositories/` nu citește/scrie direct în `Database/`
- ❌ Controllers nu apelează direct Repositories (sărirea peste Services)

## See Also

- [Getting Started](getting-started.md) — instalare și rulare locală
- [Deployment](deployment.md) — rularea containerizată cu Docker Compose
