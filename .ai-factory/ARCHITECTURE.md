# Arhitectură: Layered Architecture

## Prezentare generală
Talent Map folosește o arhitectură pe straturi orizontale (Layered Architecture): fiecare
strat depinde exclusiv de stratul de sub el. Este cel mai simplu pattern arhitectural —
ușor de înțeles, ușor de configurat și suficient pentru o aplicație CRUD cu logică de
business directă (gestionarea angajaților, competențelor și a maprii lor).

## Motivul deciziei
- **Tip de proiect:** aplicație web CRUD — mapare talente/competențe într-o organizație
  (vezi `.ai-factory/DESCRIPTION.md`)
- **Stack tehnologic:** backend .NET (ASP.NET Core Web API), frontend Angular (SPA),
  persistență pe fișiere `.json` (fără server de bază de date, fără ORM)
- **Factor cheie:** echipă mică, domeniu de business simplu (fără reguli complexe
  cross-entitate încă identificate), scală redusă — Layered Architecture oferă viteza
  de dezvoltare cea mai mare pentru acest context, fără complexitatea unei arhitecturi
  modulare sau DDD care nu s-ar justifica momentan.

## Structura de foldere

```
tallent_map_md/
├── TalentMap.slnx                         # Soluția .NET (format .slnx, implicit în SDK .NET 10)
├── src/
│   ├── TalentMap.Api/                     # Proiect ASP.NET Core Web API
│   │   ├── Controllers/                   # Handlere HTTP, validare request
│   │   │   ├── EmployeesController.cs
│   │   │   └── SkillsController.cs
│   │   ├── Services/                      # Logică de business
│   │   │   ├── EmployeeService.cs
│   │   │   └── SkillService.cs
│   │   ├── Repositories/                  # Acces la date — SINGURUL strat care
│   │   │   │                              # citește/scrie fișierele din Database/
│   │   │   ├── IEmployeeRepository.cs
│   │   │   ├── JsonEmployeeRepository.cs
│   │   │   ├── ISkillRepository.cs
│   │   │   └── JsonSkillRepository.cs
│   │   ├── Models/                        # Modele de domeniu / DTO-uri
│   │   │   ├── Employee.cs
│   │   │   └── Skill.cs
│   │   ├── Middleware/                    # Auth, error handling, logging
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── talent-map-client/                 # Aplicație Angular (SPA)
│       └── src/app/
│           ├── employees/                 # Feature module: angajați
│           ├── skills/                    # Feature module: competențe
│           └── core/                      # Servicii HTTP, interceptori, modele TS
│
├── Database/                              # "Tabele" — câte un fișier .json per entitate
│   ├── employees.json
│   └── skills.json
│
└── .ai-factory/
```

## Reguli de dependențe

```
Routes/Endpoints → Controllers → Services → Repositories → Database/*.json
        ↓                                                ↑
  Middleware (cross-cutting)                   Models (structuri de date comune)
```

- ✅ Controllers apelează Services; Services apelează Repositories; doar Repositories
  ating fișierele din `Database/`.
- ✅ Angular comunică exclusiv cu API-ul .NET prin HTTP/JSON — nu accesează niciodată
  direct fișierele din `Database/`.
- ❌ Controllers nu apelează direct Repositories (sărirea peste stratul Services).
- ❌ Services sau Repositories nu importă/depind de Controllers (dependință inversă interzisă).
- ❌ Niciun cod din afara `Repositories/` nu citește sau scrie direct în `Database/`.

## Comunicarea între straturi

- **Controllers → Services:** apel direct, prin injectare de dependințe în constructor
  (`IEmployeeService` injectat în `EmployeesController`).
- **Services → Repositories:** Services depind de interfețe (`IEmployeeRepository`),
  nu de implementarea concretă (`JsonEmployeeRepository`) — înregistrate în DI container
  din `Program.cs`.
- **Repositories → Database/:** fiecare implementare de repository citește/deserializează
  și scrie/serializează exclusiv propriul fișier `.json`, folosind `System.Text.Json`.
- **Angular → API:** `HttpClient` + servicii Angular dedicate per feature (ex: `EmployeeApiService`),
  cu un interceptor HTTP central pentru gestionarea erorilor.

## Principii cheie

1. **Dependințe strict descendente:** fiecare strat depinde doar de stratul imediat inferior;
   nu se sar straturi, nu există import-uri ascendente.
2. **Responsabilitate unică per strat:** Controllers gestionează request/response HTTP,
   Services conțin logica de business, Repositories gestionează accesul la fișierele JSON.
3. **Controllers subțiri:** un Controller validează input-ul, apelează una-două metode din
   Service și formatează răspunsul — nicio logică de business în Controller.
4. **Services fără stare:** Services nu păstrează stare între request-uri; primesc date ca
   parametri și returnează rezultate.
5. **Repository = grănița exclusivă către `Database/`:** doar clasele din `Repositories/`
   citesc/scriu fișierele `.json`. Fiecare repository serializează scrierile pe propriul
   fișier (ex. `SemaphoreSlim` per fișier) pentru a evita coruperea datelor la scrieri
   concurente — vezi nota de concurență din `.ai-factory/DESCRIPTION.md`.

## Code Examples

### Controller subțire → Service → Repository (C#)
```csharp
// Controllers/EmployeesController.cs
[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
        => _employeeService = employeeService;

    [HttpGet("{id}")]
    public async Task<ActionResult<EmployeeDto>> GetById(string id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        return employee is null ? NotFound() : Ok(employee);
    }
}

// Services/EmployeeService.cs
public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repository;

    public EmployeeService(IEmployeeRepository repository)
        => _repository = repository;

    public async Task<EmployeeDto?> GetByIdAsync(string id)
    {
        var employee = await _repository.FindByIdAsync(id);
        return employee?.ToDto();
    }
}
```

### Repository — singurul strat care atinge `Database/*.json`
```csharp
// Repositories/JsonEmployeeRepository.cs
public class JsonEmployeeRepository : IEmployeeRepository
{
    private readonly string _filePath; // ex: Path.Combine(rootPath, "Database", "employees.json")
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public async Task<Employee?> FindByIdAsync(string id)
    {
        var employees = await ReadAllAsync();
        return employees.FirstOrDefault(e => e.Id == id);
    }

    public async Task SaveAsync(Employee employee)
    {
        await FileLock.WaitAsync();
        try
        {
            var employees = await ReadAllAsync();
            employees.RemoveAll(e => e.Id == employee.Id);
            employees.Add(employee);
            var json = JsonSerializer.Serialize(employees);
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<Employee>> ReadAllAsync()
    {
        if (!File.Exists(_filePath)) return new List<Employee>();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<Employee>>(json) ?? new List<Employee>();
    }
}
```

## Anti-patterns
- ❌ **God Controller:** un Controller care conține logică de business, citește direct
  fișiere JSON și formatează răspunsul, toate într-o singură metodă.
- ❌ **Sărirea peste straturi:** Controllers apelând direct Repositories, sau Angular
  citind/scriind direct fișiere din `Database/` printr-un MCP/endpoint generic de fișiere.
- ❌ **Dependințe ascendente:** Services care importă Controllers, sau Repositories care
  importă Services.
- ❌ **Acces necontrolat la fișiere:** cod în afara `Repositories/` care deschide direct
  fișiere din `Database/` (bypass al stratului de acces la date, risc de race condition
  și de path traversal dacă path-ul e construit din input utilizator).
- ❌ **Model anemic dus la extrem:** entități care sunt doar "saci de date" fără nicio
  validare proprie — regulile simple de validare (ex. câmpuri obligatorii) pot rămâne în
  Model, restul logicii de orchestrare stă în Service.
