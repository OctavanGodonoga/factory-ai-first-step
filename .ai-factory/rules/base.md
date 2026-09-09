# Reguli de bază ale proiectului

> Proiect nou, fără cod existent — nu există convenții de detectat automat.
> Regulile de mai jos sunt convenții implicite recomandate pentru stack-ul
> confirmat (.NET + Angular, persistență pe fișiere JSON). Editează-le pe
> măsură ce apar decizii concrete de echipă.

## Convenții de denumire

- **Backend (.NET/C#):**
  - Fișiere și clase: `PascalCase` (ex: `EmployeeService.cs`, `SkillsController.cs`)
  - Metode și proprietăți publice: `PascalCase`
  - Variabile locale și parametri: `camelCase`
  - Interfețe: prefix `I` (ex: `IEmployeeRepository`)
- **Frontend (Angular/TypeScript):**
  - Fișiere: `kebab-case` (ex: `employee-list.component.ts`)
  - Clase (componente, servicii): `PascalCase`
  - Variabile și metode: `camelCase`
  - Selectori de componente: `kebab-case` cu prefix de aplicație (ex: `app-employee-list`)

## Structura modulelor

- **Backend:** separare pe straturi — `Controllers/` (API), `Services/` (logică de business),
  `Repositories/` (acces la fișierele `.json` din `Database/`), `Models/`/`Dtos/` (contracte).
- **Date:** folderul `Database/` din rădăcina proiectului conține fișierele `.json`
  care servesc drept "tabele". Accesul la aceste fișiere se face exclusiv prin
  stratul `Repositories/`, niciodată direct din `Controllers/` sau din frontend.
- **Frontend:** organizare pe module/feature (ex: `employees/`, `skills/`), fiecare
  cu propriile componente, servicii și modele.

## Gestionarea erorilor

- Backend: excepții capturate central (ex. middleware/filter global de excepții în
  ASP.NET Core), răspunsuri de eroare JSON structurate cu coduri HTTP corecte.
- Frontend: erorile de la API sunt tratate central (ex. interceptor HTTP Angular),
  nu ad-hoc în fiecare componentă.

## Control Flow

- Prefer flat, readable control flow over deeply nested conditionals. Use guard clauses, early `return`/`continue`, small named helper methods, or explicit classification logic when they make the code easier to follow. Handle edge cases and irrelevant branches early so the main path stays visible.

## Logging

- Backend: `ILogger<T>` din `Microsoft.Extensions.Logging`, niveluri configurabile din
  `appsettings.json` (`Logging:LogLevel`).
- Frontend: logare minimă în consolă doar în mediul de dezvoltare; erorile către
  utilizator se afișează prin UI, nu prin `console.log`.
