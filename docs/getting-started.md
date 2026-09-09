[Back to README](../README.md) · [Architecture →](architecture.md)

# Getting Started

## Cerințe

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — pentru backend
- [Node.js](https://nodejs.org/) 22 LTS (sau mai nou) + npm — pentru frontend
- (opțional) [Docker Desktop](https://www.docker.com/products/docker-desktop/) — pentru rularea containerizată, vezi [Deployment](deployment.md)

## Rulare cu Docker (recomandat)

Cel mai rapid mod de a porni ambele proiecte deodată — vezi
[Deployment](deployment.md) pentru detalii complete.

```bash
docker compose up -d
```

## Rulare locală, fără Docker

### Backend (`src/TalentMap.Api`)

```bash
cd src/TalentMap.Api
dotnet restore
dotnet run
```

Implicit pornește pe un port aleatoriu configurat în
`Properties/launchSettings.json` (verifică output-ul consolei pentru URL-ul exact).
Nu există încă niciun endpoint (`Controllers/` e gol) — un request către rădăcină
va răspunde `404`, ceea ce confirmă doar că serverul rulează corect.

### Frontend (`src/talent-map-client`)

```bash
cd src/talent-map-client
npm install
npm start
```

Pornește serverul de dezvoltare Angular (implicit pe `http://localhost:4200`).

## Verificare

- Backend: orice răspuns HTTP (inclusiv `404`) la request către rădăcină confirmă
  că serverul ascultă.
- Frontend: `http://localhost:4200` (local) sau `http://localhost:13467` (Docker)
  trebuie să afișeze pagina implicită Angular ("TalentMapClient").

## Pași următori

- [Architecture](architecture.md) — structura proiectului și convențiile de cod
- [Deployment](deployment.md) — rularea containerizată cu Docker Compose

## See Also

- [Architecture](architecture.md) — pattern-ul Layered și structura de foldere
- [Deployment](deployment.md) — configurarea Docker Compose
