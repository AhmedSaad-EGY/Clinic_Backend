# 🏥 Clinic Management Backend

> Production-oriented clinic management backend built with **ASP.NET Core**, **SQL Server**, and **Clean Architecture** for scheduling, clinical records, cashier operations, payments, refunds, packages, and administrative workflows.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/)
[![EF Core](https://img.shields.io/badge/EF_Core-10.0.11-512BD4?style=flat-square&logo=nuget&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-Database-CC2927?style=flat-square&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![xUnit](https://img.shields.io/badge/Tests-xUnit-512BD4?style=flat-square)](https://xunit.net/)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

---

## 📌 Overview

**Clinic Management Backend** is a modular monolith designed to manage the day-to-day operations of a medical clinic.

The system goes beyond basic CRUD operations and models real clinic workflows such as:

- Doctor and resource scheduling
- Appointment conflict prevention
- Patient and clinical record management
- Cashier shifts and cash drawers
- Multi-target payments
- Cancellation approval workflows
- Refunds and financial reconciliation
- Treatment packages
- Prescriptions and follow-ups
- Audit logging
- Role-based access control

The project is built around **Clean Architecture boundaries**, explicit domain rules, transactional workflows, optimistic concurrency, and automated testing.

---

# ✨ Engineering Highlights

### 🏗️ Clean Architecture

The solution is separated into four projects:

```text
Clinic.Domain
Clinic.Application
Clinic.Infrastructure
Clinic.Api
```

with the following dependency direction:

```text
API ───────> Application <────── Infrastructure
                  │
                  ▼
                Domain
```

The key rule is simple:

> Dependencies point toward the business core.

`Clinic.Domain` has no project dependency on ASP.NET Core, Entity Framework Core, SQL Server, Identity, or any infrastructure framework.

---

### 🧩 Modular Monolith

The application is deployed as a single backend and SQL Server database while keeping business areas logically separated.

Current business areas include:

```text
Identity
Catalog
Scheduling
Patients
Appointments
Clinical Records
Cashier
Packages
Discounts
Reporting
Audit
```

This keeps deployment simple while maintaining boundaries that can evolve independently.

---

### 📅 Resource-Aware Appointment Scheduling

Appointments are not modeled as simple date/time records.

Scheduling considers multiple resources including:

- Doctors
- Rooms
- Clinic departments
- Services
- Medical devices
- Doctor schedules
- Schedule exceptions
- Department closures

The system protects against overlapping active reservations for constrained resources.

A typical booking flow validates:

```text
Requested Appointment
        ↓
Department availability
        ↓
Room availability
        ↓
Doctor eligibility
        ↓
Doctor schedule
        ↓
Required devices
        ↓
Conflict detection
        ↓
Transactional booking
```

Scheduling rules are revalidated during important state transitions rather than assuming previously loaded availability is still valid.

---

### ⚔️ Conflict Protection

The booking system prevents conflicting reservations across:

```text
Doctor
Room
Required Devices
```

and uses transactional database operations when multiple resources must be reserved together.

Appointments also preserve historical service information instead of recalculating older bookings whenever catalog prices or configuration change.

---

### 🔄 Optimistic Concurrency

Mutable business records use SQL Server:

```text
rowversion
```

where concurrent modification matters.

Examples include areas such as:

- Patients
- Catalog configuration
- Packages
- Discounts
- Clinical records
- Approval requests
- Appointments

Expected row versions are compared before committing changes.

A stale request results in:

```text
409 Conflict
```

instead of silently overwriting another user's changes.

---

### 💰 Financial Workflows

The cashier module models more than a simple `Payment` table.

It includes:

- Cash drawers
- Secretary shifts
- Opening balances
- Cash collections
- Electronic collections
- Payments
- Payment-method allocations
- Booking allocations
- Package allocations
- Refunds
- Cash withdrawals / expenses
- Shift reconciliation
- Approval workflows

Financial workflows are designed around explicit invariants.

For example:

```text
Payment Method Allocations
            ↓
      Payment Total
            ↑
Booking / Package Allocations
```

The allocated amounts must remain consistent with the recorded payment total.

---

### 💸 Refund & Cancellation Approval Flow

Paid appointment cancellation is treated differently from an ordinary unpaid cancellation.

A protected flow can require:

```text
Cancellation Request
        ↓
Admin Review
        ↓
Approval
        ↓
Appointment Cancellation
        ↓
Refund Execution
        ↓
Audit Record
```

Refunds reference the original payment and preserve traceability between:

- Original payment
- Original allocations
- Refund methods
- Refunded targets
- Administrative approval

This helps protect financial history from destructive updates.

---

### 💵 Cashier Shift Reconciliation

Secretary cash operations are associated with active cashier shifts.

The system tracks expected cash using the shift's financial activity.

Conceptually:

```text
Expected Cash
=
Opening Balance
+ Cash Collections
+ Cash Corrections
- Cash Refunds
- Cash Expenses / Withdrawals
```

Shift closing can compare expected and declared cash while preserving the financial history used to calculate the result.

---

### 🩺 Clinical Records

The clinical area supports patient medical information including:

- Treatment history
- Clinical records
- Prescriptions
- Revisions
- Follow-up reminders
- Completed appointment-service references

Clinical history is treated as business history rather than disposable CRUD data.

---

### 📦 Treatment Packages

The system includes treatment-package foundations covering:

- Package definitions
- Patient packages
- Sessions
- Package pricing
- Payment allocation support
- Package lifecycle rules

Package-related changes also participate in concurrency protection where applicable.

---

### 🏷️ Catalog & Pricing

Clinic administration includes structured management for:

- Departments
- Rooms
- Specializations
- Services
- Devices
- Service/device relationships
- Price history

Historical appointments preserve price snapshots so existing financial records are not recalculated when an administrator changes current prices.

---

# 🔐 Authentication & Authorization

The backend uses:

```text
ASP.NET Core Identity
+
Secure Cookie Authentication
+
Role / Policy Based Authorization
```

The primary roles currently include:

```text
Admin
Secretary
```

Authorization policies also enforce the required password-change state.

Examples include:

```text
AdminOnly
SecretaryOnly
PasswordChanged
```

---

## 🍪 Secure Authentication Cookie

The authentication cookie is configured with security-focused settings including:

- `HttpOnly`
- `Secure`
- `SameSite`
- Controlled expiration

The application does not expose the authentication session to client-side JavaScript.

---

# 🛡️ CSRF Protection

Because authentication is cookie-based, state-changing API requests are protected with antiforgery validation.

The client first requests a CSRF token and then sends it through:

```text
X-XSRF-TOKEN
```

for protected operations.

The antiforgery cookie is configured as:

```text
HttpOnly
SameSite=Strict
Secure
```

and state-changing controller requests are automatically validated.

---

# 👤 Bootstrap Administrator

The first administrator can be created through deployment configuration rather than hard-coded credentials.

Example configuration:

```text
DatabaseInitialization__ApplyMigrationsOnStartup=true

BootstrapAdmin__Enabled=true
BootstrapAdmin__UserName=<ADMIN_USERNAME>
BootstrapAdmin__Password=<TEMPORARY_STRONG_PASSWORD>
BootstrapAdmin__FullName=<ADMIN_FULL_NAME>
BootstrapAdmin__PhoneNumber=<ADMIN_PHONE>
BootstrapAdmin__Email=<OPTIONAL_EMAIL>
```

The bootstrap account is created with:

```text
MustChangePassword = true
```

so the temporary password must be changed before normal privileged operations.

After initial provisioning, bootstrap and automatic-migration flags should be disabled.

---

# 🚨 Error Handling

Expected business failures use a Result-based flow rather than exceptions for ordinary situations such as:

- Validation failures
- Missing resources
- Business conflicts
- Authorization outcomes
- Concurrency conflicts

Controllers convert these errors into standard HTTP responses using:

```text
ProblemDetails
```

Unexpected exceptions are handled by a global exception handler.

Typical API responses include:

| Status | Meaning |
|---|---|
| `200 OK` | Successful query or update |
| `201 Created` | Resource created |
| `204 No Content` | Successful command without response body |
| `400 Bad Request` | Validation or invalid business operation |
| `401 Unauthorized` | Authentication required |
| `403 Forbidden` | Insufficient permissions |
| `404 Not Found` | Resource not found |
| `409 Conflict` | State or concurrency conflict |
| `423 Locked` | Account-related lock condition |
| `500 Internal Server Error` | Unexpected server failure |

---

# 📝 Audit Logging

Important business operations generate audit records.

Audit information can include:

```text
Actor
Action
Entity Type
Entity ID
Timestamp
Reason
Additional Data
```

Audited workflows include operations around:

- Cashier activity
- Appointments
- Refunds
- Administrative configuration
- Approval decisions
- Financial operations

Financial, clinical, approval, and audit history is intentionally treated differently from disposable operational data.

---

# 🔗 Request Correlation

Requests pass through correlation middleware so operations can be traced across logs and error responses more easily.

This becomes especially useful when investigating:

- Failed transactions
- Concurrency conflicts
- Production errors
- Financial workflow issues

---

# ❤️ Health Checks

The API exposes health endpoints for deployment and monitoring scenarios.

```text
/health
/health/live
/health/ready
```

`/health/ready` includes a SQL Server readiness check.

This allows infrastructure to distinguish between:

```text
Application Process Is Running
```

and:

```text
Application Is Ready To Serve Requests
```

---

# 🔒 Security Headers

API and health responses include defensive HTTP headers such as:

```text
Content-Security-Policy
Permissions-Policy
Referrer-Policy
X-Content-Type-Options
X-Frame-Options
```

API responses are also configured to avoid inappropriate client/proxy caching of sensitive data.

---

# 🧠 Architectural Decisions

This project deliberately avoids adding patterns simply because they are popular.

## No Generic Repository Over EF Core

The application does **not** wrap every `DbSet<T>` inside a generic repository.

Entity Framework Core already provides repository-like access through:

```text
DbSet<T>
```

and unit-of-work behavior through:

```text
DbContext
```

Focused abstractions are introduced only when they represent meaningful application boundaries.

---

## CQRS-Lite

Commands and queries are separated at the application level without introducing unnecessary distributed infrastructure.

```text
Command
    → Changes State

Query
    → Reads State
```

The system does not require separate databases to gain the organizational benefits of command/query separation.

---

## Result Pattern

Expected business failures use:

```text
Result
Result<T>
```

while exceptions are reserved for unexpected failures.

This keeps business outcomes explicit and prevents exceptions from becoming ordinary control flow.

---

## Domain Factories & Invariants

Domain objects expose controlled creation and state transitions where business rules must remain valid.

The goal is to prevent invalid state rather than create invalid objects and repair them later.

---

## Strategy / Policy-Oriented Business Rules

Policy-style logic is used where behavior varies around areas such as:

- Pricing
- Discounts
- Scheduling
- Refunds

This keeps changing business rules isolated from HTTP and persistence concerns.

---

# 🛠️ Technology Stack

| Category | Technology |
|---|---|
| Language | C# |
| Runtime | .NET 10 |
| SDK | .NET SDK `10.0.400` |
| API | ASP.NET Core Web API |
| ORM | Entity Framework Core `10.0.11` |
| Database | Microsoft SQL Server |
| Identity | ASP.NET Core Identity |
| Authentication | Secure Cookie Authentication |
| Authorization | Roles + Policy-Based Authorization |
| CSRF | ASP.NET Core Antiforgery |
| API Docs | Swagger / Swashbuckle `10.2.3` |
| Error Format | RFC-style `ProblemDetails` |
| Testing | xUnit `2.9.3` |
| Integration Testing | `Microsoft.AspNetCore.Mvc.Testing` |
| Coverage | Coverlet |
| Architecture | Clean Architecture / Modular Monolith |

Package versions are managed centrally through:

```text
Directory.Packages.props
```

---

# 🏗️ Project Structure

```text
Clinic_Backend/
│
├── src/
│   │
│   ├── Clinic.Domain/
│   │   └── Core entities, value objects,
│   │       domain rules and business invariants
│   │
│   ├── Clinic.Application/
│   │   └── Use cases, commands, queries,
│   │       handlers, application contracts and results
│   │
│   ├── Clinic.Infrastructure/
│   │   └── EF Core, SQL Server, Identity,
│   │       persistence and external implementations
│   │
│   └── Clinic.Api/
│       └── Controllers, HTTP contracts,
│           authorization, error handling,
│           health checks and composition root
│
├── tests/
│   │
│   ├── Clinic.Domain.UnitTests/
│   ├── Clinic.Application.UnitTests/
│   ├── Clinic.ArchitectureTests/
│   └── Clinic.Api.IntegrationTests/
│
├── docs/
│   ├── client/
│   ├── erd/
│   ├── planning/
│   └── requirements/
│
├── ARCHITECTURE.md
├── Directory.Build.props
├── Directory.Packages.props
├── Clinic.slnx
├── global.json
├── LICENSE
└── README.md
```

---

# 🧱 Layer Responsibilities

## `Clinic.Domain`

Contains the business core.

Examples include:

- Appointments
- Patients
- Clinical records
- Scheduling concepts
- Cashier concepts
- Packages
- Discounts
- Audit entities
- Domain rules

The project contains no project references.

---

## `Clinic.Application`

Contains application use cases and contracts.

Responsibilities include:

- Commands
- Queries
- Handlers
- Validation
- Application models
- Result types
- Infrastructure ports

It depends on:

```text
Clinic.Domain
```

but not on Infrastructure.

---

## `Clinic.Infrastructure`

Implements application ports using infrastructure technologies.

Responsibilities include:

- EF Core
- SQL Server
- ASP.NET Core Identity
- Database configuration
- Migrations
- Business workflow persistence
- Infrastructure services

It depends on:

```text
Clinic.Application
Clinic.Domain
```

---

## `Clinic.Api`

Acts as the HTTP delivery layer and composition root.

Responsibilities include:

- Controllers
- Authentication pipeline
- Authorization
- Antiforgery protection
- ProblemDetails
- Swagger
- Health checks
- Security headers
- Request correlation
- Dependency composition

It references:

```text
Clinic.Application
Clinic.Infrastructure
```

---

# 🔄 Typical Request Flow

A typical application request follows:

```text
HTTP Request
      ↓
Authentication
      ↓
Authorization Policy
      ↓
Antiforgery Validation
      ↓
Controller
      ↓
Command / Query Handler
      ↓
Application Contract
      ↓
Infrastructure Implementation
      ↓
EF Core
      ↓
SQL Server
      ↓
Result<T>
      ↓
ProblemDetails / Response DTO
```

---

# 🧪 Testing Strategy

Testing is a first-class part of the solution.

The repository contains four dedicated test projects.

---

## 1. Domain Unit Tests

```text
Clinic.Domain.UnitTests
```

Domain tests cover business behavior across areas including:

- Appointments
- Scheduling
- Patients
- Clinical records
- Cashier
- Catalog
- Packages
- Discounts
- Auditing
- Shared domain behavior

These tests validate business invariants without requiring HTTP or SQL Server.

---

## 2. Application Unit Tests

```text
Clinic.Application.UnitTests
```

Application-level tests validate use-case behavior such as:

- Successful execution
- Validation
- Authorization rules
- Conflict paths
- Cancellation behavior

---

## 3. Architecture Tests

```text
Clinic.ArchitectureTests
```

Architecture tests automatically validate project dependency rules.

For example:

```text
Domain
    → must not reference Application,
      Infrastructure or API

Application
    → may reference Domain

Infrastructure
    → may reference Application + Domain

API
    → may reference Application + Infrastructure
```

This helps prevent architectural boundaries from silently degrading as the project grows.

---

## 4. API Integration Tests

```text
Clinic.Api.IntegrationTests
```

Integration coverage includes areas such as:

- Identity
- Catalog
- Scheduling
- Appointments
- Patients
- Clinical records
- Cashier
- Packages
- Health endpoints
- Production configuration validation

SQL Server-backed integration tests are used when database behavior, constraints, transactions, or concurrency need to be tested realistically.

---

# ✅ Running Tests

Restore dependencies:

```bash
dotnet restore Clinic.slnx
```

Build:

```bash
dotnet build Clinic.slnx --no-restore
```

Run the complete test suite:

```bash
dotnet test Clinic.slnx --no-build
```

> SQL Server is required for the SQL-backed API integration tests.

---

# 🗂️ Database Design

The repository includes an editable database ERD:

```text
docs/erd/Clinic_ERD_Final.drawio
```

Open it with:

```text
draw.io / diagrams.net
```

The schema models areas including:

- Identity
- Clinic catalog
- Scheduling
- Patients
- Appointments
- Clinical history
- Payments
- Refunds
- Cashier shifts
- Treatment packages
- Approvals
- Audit history

---

# 🚀 Getting Started

## Prerequisites

Install:

```text
.NET SDK 10.0.400
SQL Server
Git
EF Core CLI
```

Verify the .NET SDK:

```bash
dotnet --version
```

Install EF Core CLI if needed:

```bash
dotnet tool install --global dotnet-ef
```

---

## Clone the Repository

```bash
git clone https://github.com/AhmedSaad-EGY/Clinic_Backend.git
cd Clinic_Backend
```

---

## Restore Dependencies

```bash
dotnet restore Clinic.slnx
```

---

# ⚙️ Database Configuration

The checked-in development configuration uses:

```text
Server=localhost
Database=ClinicDb
Integrated Security=True
Encrypt=True
```

No database password is committed.

For another environment, override the connection string using configuration or environment variables.

Environment variable:

```text
ConnectionStrings__ClinicDatabase
```

Example:

```powershell
$env:ConnectionStrings__ClinicDatabase="<YOUR_SQL_SERVER_CONNECTION_STRING>"
```

---

# 🗃️ Apply Database Migrations

```powershell
dotnet ef database update `
  --project src/Clinic.Infrastructure/Clinic.Infrastructure.csproj `
  --startup-project src/Clinic.Api/Clinic.Api.csproj
```

---

# ▶️ Run the API

```bash
dotnet run --project src/Clinic.Api/Clinic.Api.csproj --launch-profile https
```

Using the checked-in HTTPS launch profile, Swagger is available at:

```text
https://localhost:7157/swagger
```

---

# 🔑 Initial Admin Setup

Initial administrative provisioning is disabled by default.

For the first controlled startup, configure:

```text
DatabaseInitialization__ApplyMigrationsOnStartup=true

BootstrapAdmin__Enabled=true

BootstrapAdmin__UserName=<ADMIN_USERNAME>

BootstrapAdmin__Password=<TEMPORARY_STRONG_PASSWORD>

BootstrapAdmin__FullName=<ADMIN_FULL_NAME>

BootstrapAdmin__PhoneNumber=<ADMIN_PHONE>

BootstrapAdmin__Email=<OPTIONAL_EMAIL>
```

After the first administrator has been created:

```text
DatabaseInitialization__ApplyMigrationsOnStartup=false

BootstrapAdmin__Enabled=false
```

should be restored.

The bootstrap user is required to change the temporary password.

---

# 🔐 Authentication Flow

Login establishes the secure authentication cookie.

For state-changing requests, retrieve a CSRF token first:

```text
GET /api/auth/csrf
```

Then include the returned token in:

```text
X-XSRF-TOKEN
```

while also sending the authentication cookie.

Conceptually:

```text
Login
  ↓
Secure Auth Cookie

GET /api/auth/csrf
  ↓
CSRF Token

POST / PUT / PATCH / DELETE
  ↓
Cookie + X-XSRF-TOKEN
```

---

# ❤️ Health Endpoints

Basic health:

```text
GET /health
```

Liveness:

```text
GET /health/live
```

Readiness:

```text
GET /health/ready
```

Readiness includes the SQL Server database check.

---

# 📖 API Documentation

Swagger UI is included for interactive API exploration.

```text
/swagger
```

The repository also includes additional design and planning documentation under:

```text
docs/
```

and architectural decisions are documented in:

[ARCHITECTURE.md](ARCHITECTURE.md)

---

# 🧭 Design Philosophy

The project follows a few deliberate engineering principles:

### Business rules first

Complex clinic rules belong in the domain/application layers, not inside controllers.

### Avoid unnecessary abstractions

An interface or pattern is introduced when it solves a real boundary, testing, or maintainability problem.

### Protect history

Financial, clinical, approval, and audit records should not disappear because a normal CRUD delete was called.

### Concurrency is a business problem

Where simultaneous edits matter, concurrency is handled explicitly instead of relying on last-write-wins behavior.

### Transactions protect workflows

If multiple state changes must either all succeed or all fail, they belong inside one transaction.

### Tests protect architecture too

Automated tests validate not only business behavior but also the intended dependency direction of the solution.

---

# ⚠️ Current Operational Notes

The project is actively evolving.

Current considerations include:

- Swagger is currently enabled regardless of environment and should normally be restricted before a hardened production deployment.
- SQL Server is required for parts of the integration suite.
- Deployment automation / CI is not currently represented by a committed workflow.
- Production database credentials should be provided through the deployment environment rather than committed configuration.
- Bootstrap administration should only be enabled during controlled initial provisioning.

---

# 🗺️ Possible Next Improvements

Potential future engineering work includes:

- CI build and automated test workflow
- Production deployment pipeline
- Structured centralized logging
- Metrics and tracing
- Extended reporting
- Broader test coverage for additional financial edge cases
- Backup / recovery automation
- Production secret-store integration
- Additional health and observability checks

---

# 📄 License

This project is licensed under the [MIT License](LICENSE).

---

# 👨‍💻 Author

**Ahmed Saad**

Backend .NET Developer

- GitHub: https://github.com/AhmedSaad-EGY
- LinkedIn: https://www.linkedin.com/in/ahmed-mohamed-saad-b57695356/

---

> Built as a backend engineering project focused on real business rules, maintainable architecture, transactional correctness, security, concurrency, and automated testing.
