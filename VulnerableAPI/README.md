# Identity Migrations & Database Update — Notes

Purpose
- Quick reference for creating/applying EF Core migrations for the `AppIdentityDbContext` in this solution.

Important files
- Migration generated: `VulnerableAPI\Migrations\Identity\20260220125219_App_Identity_Context.cs`
- DbContext: `VulnerableAPI\Data\AppIdentityDbContext.cs`
- Project that contains the DbContext: `VulnerableAPI` (ensure PMC Default Project or CLI `--project` points here)

Correct commands

1) Add a migration
- dotnet CLI (from project folder or supply --project / --startup-project):
  `dotnet ef migrations add Identity_First --context AppIdentityDbContext --output-dir Migrations/Identity`

- Visual Studio Package Manager Console (set Default Project to the project that contains the DbContext):
  `Add-Migration Identity_First -Context AppIdentityDbContext -OutputDir "Migrations\Identity"`

Notes:
- Do NOT use a leading slash in `--output-dir` / `-OutputDir`. Use `Migrations\Identity` (Windows-style in PMC) or `Migrations/Identity` (CLI).
- `--output-dir` / `-OutputDir` is only for `Add-Migration`, not for `Update-Database`.

2) Apply migrations to the database (Update-Database)
- Package Manager Console:
  `Update-Database -Context AppIdentityDbContext`

- dotnet CLI:
  `dotnet ef database update --context AppIdentityDbContext`
  If your DbContext project differs from the startup project, add:
  `--project <ProjectContainingDbContext> --startup-project <StartupProject>`

Common pitfalls & troubleshooting
- Wrong context name: `AppIdentityDb` is incorrect. Use `AppIdentityDbContext`.
- Ensure `Microsoft.EntityFrameworkCore.Design` is referenced in the project and `dotnet-ef` is installed for CLI: `dotnet tool install --global dotnet-ef` (if needed).
- In PMC, set the Default Project to the project containing the DbContext before running `Add-Migration` / `Update-Database`.
- Confirm the connection string in `appsettings.json` and that PostgreSQL is reachable.
- If you need to apply a specific migration by name or timestamp:
  - PMC: `Update-Database -Migration 20260220125219_App_Identity_Context -Context AppIdentityDbContext`
  - CLI: `dotnet ef database update 20260220125219_App_Identity_Context --context AppIdentityDbContext`

Commit guidance
- Add the generated migration files under `Migrations\Identity` to source control and commit with a message like:
  `Add identity migration: Identity_First (AppIdentityDbContext)`

Security reminder
- This project intentionally contains vulnerabilities for training. Do not deploy this configuration to production.
