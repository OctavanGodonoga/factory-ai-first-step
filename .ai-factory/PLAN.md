<!-- handoff:task:3d552e75-9258-47ed-bcb3-78e33111be94 -->

# Implementation Plan: MongoDB Connection

Branch: main
Created: 2026-09-09

## Settings
- [ ] Testing: no
- [ ] Logging: verbose
- [ ] Docs: no

## Context

Talent Map's backend (`src/TalentMap.Api`, .NET 10 / ASP.NET Core Web API) is currently a bare
scaffold: only `Program.cs` exists, registering `AddControllers()` and `AddOpenApi()`. No
`ConnectionStrings` section, no MongoDB package, and no `mongo` service in `compose.yml` exist yet.
Per `.ai-factory/ARCHITECTURE.md`, persistence was originally planned as flat JSON files under
`Database/`, but this task introduces a MongoDB connection instead, using the **default**
(unauthenticated) MongoDB instance already running in Docker Desktop.

**Scope note:** this plan only establishes the MongoDB connection infrastructure (driver, config,
DI registration, startup connectivity check, Docker networking). It does NOT migrate the
(not-yet-implemented) repository layer to Mongo — that is a separate follow-up once entities/
repositories exist.

**Docker networking assumption:** the `api` service runs inside a container (see `compose.yml`),
while MongoDB runs as a separate container/process managed by Docker Desktop on the host. From
inside the `api` container, `localhost:27017` refers to the container itself, not the host — so
the containerized api reaches Mongo via `host.docker.internal:27017`, while local `dotnet run`
(outside Docker) uses `localhost:27017`. This is handled via an environment-variable override in
`compose.yml` (task 5) on top of the `appsettings.json` default (task 2).

## Tasks

### Phase 1: Package & Configuration
- [x] Task 1: Add `MongoDB.Driver` NuGet package reference to `src/TalentMap.Api/TalentMap.Api.csproj` (latest stable 3.x, compatible with net10.0); restore and confirm no conflicts.
- [x] Task 2: Add `ConnectionStrings:MongoDb` (`mongodb://localhost:27017`) and `MongoDbSettings:DatabaseName` (`TalentMap`) to `src/TalentMap.Api/appsettings.json`.

### Phase 2: Connection Wiring
- [x] Task 3: Register `IMongoClient` and `IMongoDatabase` as singletons in `src/TalentMap.Api/Program.cs`, reading connection string/database name from configuration, failing fast with a clear exception if either is missing. Per the verbose logging setting, log (at Information level) the resolved Mongo host and database name on successful registration — never log credentials (not applicable here since the default connection is unauthenticated, but keep the log statement credential-safe for future-proofing). (depends on 1, 2)
- [x] Task 4: Add a startup connectivity check (Mongo `ping` command) in `Program.cs` (or a small `Extensions/MongoStartupExtensions.cs`), logging success/failure without crashing the app on failure. Per the verbose logging setting: on success, log the ping round-trip time in ms; on failure, log the full exception (type and message) plus the target host/database, at Warning level. (depends on 3)
<!-- Commit checkpoint: tasks 1-4 -->

### Phase 3: Docker Networking
- [x] Task 5: Update `compose.yml`'s `api` service with `extra_hosts: host.docker.internal:host-gateway` and an `ConnectionStrings__MongoDb=mongodb://host.docker.internal:27017` environment override so the containerized api reaches the host's Docker Desktop MongoDB. (depends on 2)
<!-- Commit checkpoint: task 5 -->

## Commit Plan
- [x] **Commit 1** (after tasks 1-4): "feat: add MongoDB driver, configuration, and connection registration with startup ping check"
- [ ] **Commit 2** (after task 5): "chore: route containerized api to host MongoDB via Docker Desktop networking"
