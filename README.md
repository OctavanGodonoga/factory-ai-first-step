# Talent Map

> Aplicație web pentru maparea talentelor și competențelor dintr-o organizație.

Backend .NET 10 (ASP.NET Core Web API) + client Angular (SPA), cu persistență
plănuită pe fișiere `.json` (fără server de bază de date). Proiect aflat în fază
de scaffolding — vezi [Arhitectură](docs/architecture.md) pentru detalii.

## Pornire rapidă

Cel mai simplu mod de a rula ambele proiecte, cu Docker Desktop pornit:

```bash
docker compose up -d
```

- Backend: http://localhost:13466
- Frontend: http://localhost:13467

Vezi [Getting Started](docs/getting-started.md) pentru rularea locală (fără Docker).

## Ce există acum

- **Backend:** `src/TalentMap.Api` — ASP.NET Core Web API pe .NET 10, template
  Controllers-based, foldere pregătite pentru arhitectura Layered (`Controllers/`,
  `Services/`, `Repositories/`, `Models/`, `Middleware/`) — încă goale, fără
  funcționalități de business implementate.
- **Frontend:** `src/talent-map-client` — aplicație Angular (SPA) cu routing activat.
- **Docker:** `compose.yml` rulează ambele servicii local, containerizate.
- **Fără încă:** endpoint-uri API, teste, CI/CD, autentificare.

## Exemplu

```bash
docker compose up -d
curl -i http://localhost:13467/     # → 200, index.html Angular
curl -i http://localhost:13466/     # → 404 (server pornit, fără controllere încă)
docker compose down
```

---

## Documentație

| Ghid | Descriere |
|------|-----------|
| [Getting Started](docs/getting-started.md) | Instalare, rulare locală și via Docker |
| [Architecture](docs/architecture.md) | Structura proiectului, pattern Layered |
| [Deployment](docs/deployment.md) | Rularea cu Docker Compose |
