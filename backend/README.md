# Clinic Backend

ASP.NET Core backend using .NET 10, SQL Server, Clean Architecture, and a modular
monolith deployment model.

## Projects

```text
src/
  Clinic.Domain/          Enterprise rules, aggregates, value objects, domain events
  Clinic.Application/     Use cases, ports, commands, queries, validation contracts
  Clinic.Infrastructure/  EF Core, SQL Server, identity, external implementations
  Clinic.Api/             HTTP endpoints, authentication pipeline, composition root
tests/
  Clinic.Domain.UnitTests/
  Clinic.Application.UnitTests/
  Clinic.ArchitectureTests/
  Clinic.Api.IntegrationTests/
ERD/
  Clinic_ERD_Final.drawio
```

## Commands

```powershell
dotnet restore Clinic.slnx
dotnet build Clinic.slnx --no-restore
dotnet test Clinic.slnx --no-build
```

The checked-in connection string contains no password and requires certificate
validation. Use user secrets or environment variables for machine-specific values.

## Identity bootstrap

The initial migration creates the `Admin` and `Secretary` roles. The first admin is
created from deployment secrets; no password is stored in source control. Configure
these environment variables for the first startup only:

```text
DatabaseInitialization__ApplyMigrationsOnStartup=true
BootstrapAdmin__Enabled=true
BootstrapAdmin__UserName=<admin user name>
BootstrapAdmin__Password=<temporary strong password>
BootstrapAdmin__FullName=<admin full name>
BootstrapAdmin__PhoneNumber=<admin phone>
BootstrapAdmin__Email=<optional email>
```

After the database and admin are created, set both enable flags to `false`. The admin
must change the temporary password at first login.

For a local SQL Server database, migrations can instead be applied explicitly:

```powershell
dotnet ef database update `
  --project src/Clinic.Infrastructure/Clinic.Infrastructure.csproj `
  --startup-project src/Clinic.Api/Clinic.Api.csproj
```

## Browser authentication

Authentication uses an HTTP-only secure cookie. Before every state-changing request,
the frontend first calls `GET /api/auth/csrf`, then sends the returned token in the
`X-XSRF-TOKEN` header. Requests must include browser credentials. The planned Vercel
rewrite keeps frontend and API requests under the same public origin.
