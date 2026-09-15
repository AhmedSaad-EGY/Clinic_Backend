# Clinic Management Backend

Backend API for a single-branch medical clinic covering patient records, appointments,
clinic scheduling, cashier shifts, collections, refunds, prescriptions, and treatment
packages.

The solution is a modular monolith built with Clean Architecture. It targets .NET 10
and SQL Server and exposes documented REST APIs for Admin and Secretary roles.

## Implemented modules

- Identity, cookie authentication, CSRF protection, and role-based authorization
- Departments, rooms, specializations, services, devices, and price history
- Doctors, schedules, exceptions, and department closures
- Patients, treatment history, prescriptions, revisions, and follow-up reminders
- Appointments with doctor, room, and device conflict protection
- Cash drawers, shifts, collections, cancellation approvals, and refunds
- Package catalog and patient package/session foundations
- Audit logging, optimistic concurrency, and RFC 7807 error responses

## Technology

- ASP.NET Core 10
- Entity Framework Core 10
- SQL Server
- ASP.NET Core Identity
- OpenAPI / Swagger
- xUnit

## Repository structure

```text
src/
  Clinic.Domain/          Business rules and aggregates
  Clinic.Application/     Use cases and application contracts
  Clinic.Infrastructure/  EF Core, SQL Server, Identity, and implementations
  Clinic.Api/             HTTP API and composition root
tests/
  Clinic.Domain.UnitTests/
  Clinic.Application.UnitTests/
  Clinic.ArchitectureTests/
  Clinic.Api.IntegrationTests/
docs/
  erd/
    Clinic_ERD_Final.drawio
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for the dependency rules and design decisions.

## Prerequisites

- .NET SDK `10.0.400` or a compatible patch version
- SQL Server with Windows Authentication for the default local configuration
- `dotnet-ef` 10 for applying migrations

## Local setup

The committed settings use the local database `ClinicDb` and contain no credentials.
For another environment, provide the connection string through user secrets or the
`ConnectionStrings__ClinicDatabase` environment variable.

```powershell
dotnet restore Clinic.slnx

dotnet ef database update `
  --project src/Clinic.Infrastructure/Clinic.Infrastructure.csproj `
  --startup-project src/Clinic.Api/Clinic.Api.csproj

dotnet run --project src/Clinic.Api/Clinic.Api.csproj --launch-profile https
```

Swagger UI is available at `https://localhost:7157/swagger` when using the HTTPS
development profile.

## Initial admin

The first admin is created from deployment configuration; no password is stored in
source control. Configure these values for the first startup only:

```text
DatabaseInitialization__ApplyMigrationsOnStartup=true
BootstrapAdmin__Enabled=true
BootstrapAdmin__UserName=<admin user name>
BootstrapAdmin__Password=<temporary strong password>
BootstrapAdmin__FullName=<admin full name>
BootstrapAdmin__PhoneNumber=<admin phone>
BootstrapAdmin__Email=<optional email>
```

Disable both enable flags after initialization. The admin must change the temporary
password at first login.

## Authentication

The API uses an HTTP-only secure cookie. Before a state-changing request, clients call
`GET /api/auth/csrf` and send the returned token in the `X-XSRF-TOKEN` header while
including browser credentials.

## Verification

```powershell
dotnet restore Clinic.slnx
dotnet build Clinic.slnx --no-restore
dotnet test Clinic.slnx --no-build
```

SQL Server is required for the API integration tests.

## License

Licensed under the [MIT License](LICENSE).
