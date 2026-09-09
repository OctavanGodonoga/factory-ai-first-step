# Plan de implementare: Configurare Docker Compose pentru backend + frontend

Branch: feature/docker-compose-setup
Created: 2026-09-09

## Cerere originală
configurează un docker-compose pentru a rula ambele proiecte în docker desktop. backend pe port-ul 13466 și frontendul pe 13467

## Setări
- Teste: nu
- Logging: standard
- Documentație: da — checkpoint obligatoriu la finalizare (`/aif-docs`)

## Commit Plan
- **Commit 1** (după task-urile 1-2): "build: add Dockerfiles for API and Angular client"
- **Commit 2** (după task-urile 3-5): "build: add docker compose setup (api:13466, client:13467)"

## Tasks

### Faza 1: Dockerfiles per proiect
- [x] Task 1: Dockerfile + .dockerignore pentru backend (TalentMap.Api) — vezi TaskList #10
- [x] Task 2: Dockerfile + .dockerignore pentru frontend (talent-map-client) — vezi TaskList #11
<!-- Commit checkpoint: tasks 1-2 -->

### Faza 2: Orchestrare și verificare
- [x] Task 3: compose.yml la rădăcina proiectului (depinde de 1, 2) — vezi TaskList #12
- [x] Task 4: Build și verificare rulare în Docker Desktop (depinde de 3) — vezi TaskList #13
- [x] Task 5: Actualizează AGENTS.md și ARCHITECTURE.md cu setup-ul Docker (depinde de 4) — vezi TaskList #14
<!-- Commit checkpoint: tasks 3-5 -->
