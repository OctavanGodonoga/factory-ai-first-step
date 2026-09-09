[← Architecture](architecture.md) · [Back to README](../README.md)

# Deployment

## Docker Compose (local, Docker Desktop)

Ambele proiecte rulează containerizat prin `compose.yml` la rădăcina proiectului.

| Serviciu | Build context | Port host | Port intern |
|----------|---------------|-----------|--------------|
| `api` | `src/TalentMap.Api` | `13466` | `8080` |
| `client` | `src/talent-map-client` | `13467` | `80` |

### Pornire

```bash
docker compose up -d
```

### Verificare

```bash
docker compose ps                    # ambele servicii trebuie să fie "Up"
curl -i http://localhost:13466/      # backend — răspunde (404 e OK, nu există endpoint-uri încă)
curl -i http://localhost:13467/      # frontend — 200, index.html Angular
```

### Loguri

```bash
docker compose logs api
docker compose logs client
```

### Oprire

```bash
docker compose down
```

## Build-uri

- **`api`** — build multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` (restore +
  publish) → `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime). Imaginile oficiale
  .NET 8+ setează implicit `ASPNETCORE_HTTP_PORTS=8080` în container.
- **`client`** — build multi-stage: `node:22-alpine` (`npm ci` + `npm run build`)
  → `nginx:alpine` (servire statică din `dist/talent-map-client/browser/`).

## Ce nu există încă

- Fără bază de date sau volume montate — persistența pe fișiere JSON (`Database/`)
  nu e implementată încă în cod.
- Fără configurare de producție separată (compose override, secrete, HTTPS) —
  setup-ul actual e gândit pentru dezvoltare locală în Docker Desktop.
- Fără CI/CD.

## See Also

- [Getting Started](getting-started.md) — rularea locală, fără Docker
- [Architecture](architecture.md) — structura proiectului
