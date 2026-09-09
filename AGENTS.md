# Clinic Project Working Agreement

This file defines the mandatory engineering rules for every contributor and coding
agent working in this repository. Its scope is the entire `F:\Clinic` workspace.

## 1. Workspace boundary

- Work only inside `F:\Clinic`. Never create, move, or modify project artifacts
  outside this directory.
- Preserve existing user changes. Do not overwrite unrelated work.
- Use absolute paths when reporting or opening project files.
- Do not commit, push, rewrite history, or create branches unless the user asks.
- Never store passwords, tokens, production connection strings, patient secrets, or
  private keys in source control.

## 2. Architecture

The backend is a modular monolith using Clean Architecture and .NET 10.

```text
Clinic.Api -> Clinic.Application <- Clinic.Infrastructure
                       |
                       v
                  Clinic.Domain
```

Allowed production project references:

- `Clinic.Domain`: no project references.
- `Clinic.Application`: `Clinic.Domain` only.
- `Clinic.Infrastructure`: `Clinic.Application` and `Clinic.Domain`.
- `Clinic.Api`: `Clinic.Application` and `Clinic.Infrastructure`.

Never bypass these boundaries. The architecture tests must remain green.

## 3. Module and feature organization

Business modules currently include:

- Identity and Access
- Catalog
- Scheduling and Appointments
- Patients
- Clinical Records and Prescriptions
- Cashier, Shifts, Payments, and Refunds
- Packages
- Discounts
- Reporting
- Approvals and Audit

Inside `Clinic.Application`, organize code by feature and use case, not by a single
global folder for all commands, handlers, validators, or DTOs.

```text
Features/Appointments/CreateAppointment/
  CreateAppointmentCommand.cs
  CreateAppointmentHandler.cs
  CreateAppointmentValidator.cs
  CreateAppointmentResponse.cs
```

Keep each use case focused. Do not create a large service class that contains many
unrelated operations.

## 4. Domain rules

- Keep `Clinic.Domain` free from EF Core, ASP.NET Core, SQL Server, HTTP, file system,
  and external service dependencies.
- Put business invariants and state transitions inside aggregates and value objects.
- Prefer private/protected setters and factory methods that cannot create invalid
  entities.
- Use domain events only for meaningful completed business events.
- Use `DomainException` only for domain rule violations that should never be ignored.
- Use enums or value objects for finite business states; avoid magic strings.
- Store monetary values as `decimal`; never use `float` or `double` for money.
- Store timestamps as UTC `DateTimeOffset` unless a deliberate local clinic date or
  time value is required.
- Add `rowversion` concurrency control to records that may be edited concurrently.
- Operational deletion is soft delete/archive. Financial, clinical, approval, and
  audit history is append-only.

## 5. Application rules

- Application contains use cases and interfaces (ports), not database or HTTP code.
- Commands change state; queries do not change state.
- A command handler owns one transaction boundary for its use case.
- Use `Result`/`Result<T>` for expected validation and business failures.
- Do not use exceptions for ordinary not-found, conflict, or validation outcomes.
- Validate input at the application boundary and re-check critical invariants inside
  the domain.
- Pass `CancellationToken` through every asynchronous call.
- Do not expose EF Core entities or `IQueryable` outside Infrastructure.
- Do not return API-specific models from Application.
- Interfaces are introduced at real boundaries or where multiple implementations are
  useful. Do not create an interface for every class.

## 6. Infrastructure and EF Core rules

- `ClinicDbContext` is the default Unit of Work. Do not add a generic repository over
  EF Core.
- Add focused aggregate repositories or query services only when a use case benefits
  from them.
- Create one `IEntityTypeConfiguration<TEntity>` per persisted entity.
- Configure table names, column sizes, Unicode, decimal precision, indexes, unique
  keys, check constraints, foreign keys, delete behavior, and concurrency explicitly.
- Use `decimal(18,2)` for ordinary currency unless a documented requirement needs
  different precision.
- Do not use cascade delete for financial, clinical, approval, or audit records.
- Avoid lazy loading. Load the exact graph required by the use case.
- Use projections and `AsNoTracking()` for read-only queries.
- Prevent N+1 queries and unbounded list queries. Paginate large results.
- Generate migrations only after the model builds and its constraints are reviewed.
- Never edit an already-applied production migration. Add a new migration.
- Database transactions must protect booking conflicts, payment allocation totals,
  refunds, package-session consumption, and other cross-row invariants.

## 7. API rules

- `Clinic.Api` is the HTTP boundary and composition root; it contains no business
  rules.
- Keep endpoints thin: map the request, call one use case, and map the result.
- Use explicit request/response contracts. Never expose persistence entities.
- Return consistent RFC 7807 Problem Details for errors.
- Use correct HTTP status codes and do not leak exception details or stack traces.
- Validate authorization on every protected operation, including admin-only fields.
- Version public APIs before introducing breaking changes.
- Keep OpenAPI accurate when endpoints change.

## 8. Security and privacy

- Hash passwords with an established ASP.NET Core Identity/password-hashing facility.
  Never implement custom cryptography.
- Store refresh tokens only as hashes and support revocation.
- Lock an account temporarily after five failed login attempts; an admin may unlock
  it earlier.
- Use parameterized EF Core queries. Never concatenate user input into SQL.
- Use encrypted database connections and validate certificates outside explicit,
  isolated local development decisions.
- Apply least privilege: one Admin role and Secretary accounts with limited actions.
- Admin-only patient notes must never appear in secretary responses or offline data.
- Do not log passwords, tokens, sensitive clinical notes, or full patient payloads.
- Audit authentication, authorization-sensitive changes, confirmed cancellations,
  refunds, cash movements, prescription revisions, and admin actions.

## 9. Critical business invariants

- One clinic branch and one current room per department.
- A multi-service appointment uses services from the appointment room's department.
- The assigned doctor must be eligible for the exact service.
- Required devices must be configured for the exact service and department.
- Room, doctor, and device time conflicts are checked atomically.
- Booking prices and discounts are immutable snapshots after creation.
- A secretary may cancel an unpaid booking. Cancelling a confirmed/paid booking
  requires admin approval and a recorded reason.
- Posting a full payment and confirming its booking happens atomically.
- A payment's booking/package targets must belong to the same patient.
- Payment-method allocations and target allocations must each equal the payment total.
- Every refund requires admin approval and references one original payment.
- Refund method and target allocations must belong to that original payment and must
  not exceed their refundable balances.
- Financial revenue belongs to the shift/date of collection, not booking creation.
- One secretary cannot have overlapping shifts; different secretaries may overlap
  because they have independent cash drawers.
- An automatically opened shift cannot collect money until its opening balance is
  recorded.
- Closed shifts cannot be edited by secretaries. Corrections/reversals are new,
  traceable records with reasons.
- Purchased package composition is snapshotted in `PatientPackageService`.
- Package sessions reserve on booking and consume only when the visit completes.
- Patient packages support full payment only; package sessions are never collected individually.
- Prescription corrections create new revisions; previous revisions are immutable.
- A prescription return date creates a follow-up reminder, not an appointment.
- Offline mode is read-only and never queues offline writes.

The approved source of truth for entities and relationships is:
`F:\Clinic\backend\ERD\Clinic_ERD_Final.drawio`.

## 10. Patterns and restraint

Use patterns only when they solve a current problem:

- Aggregate Root and Value Object for invariants.
- CQRS-lite for command/query separation.
- Strategy/Policy for pricing, discounts, refunds, and scheduling decisions.
- Decorator for validation, authorization, logging, and transaction behaviors.
- Factory methods for valid aggregate creation.
- Optimistic concurrency for competing edits.

Do not introduce microservices, event sourcing, an event bus, distributed caching,
or a framework-heavy mediator without an approved requirement. Prefer clear code over
pattern ceremony.

## 11. Clean-code standard

- Use descriptive names based on clinic terminology.
- Keep methods small and at one level of abstraction.
- Prefer guard clauses over deeply nested conditionals.
- Remove dead code, generated samples, unused usings, and commented-out code.
- Avoid boolean parameters when an enum or separate method communicates intent.
- Avoid primitive obsession for money, phone numbers, file numbers, time ranges, and
  other concepts with important validation.
- Do not duplicate business rules across API, Application, and Domain.
- Comments explain why, constraints, or non-obvious tradeoffs—not what the code says.
- All warnings are errors. Do not suppress analyzers without a written justification.
- Follow `F:\Clinic\.editorconfig` and central package versions.

## 12. Testing requirements

- Domain behavior requires fast unit tests.
- Application handlers require unit tests for success, validation, authorization,
  conflict, and cancellation paths.
- Database constraints and transaction behavior require SQL Server integration tests.
- API contracts, authentication, authorization, and error responses require API
  integration tests.
- Add a regression test before or with every bug fix.
- Do not delete or weaken an existing test merely to make a change pass.
- Keep architecture tests updated when an explicitly approved boundary changes.

Mandatory verification from `F:\Clinic\backend`:

```powershell
dotnet restore Clinic.slnx
dotnet build Clinic.slnx --no-restore
dotnet test Clinic.slnx --no-build
```

## 13. Definition of Done

A feature is complete only when:

- Its acceptance rules and authorization are implemented.
- Domain and application invariants are enforced.
- Persistence mapping, indexes, constraints, and migration are reviewed.
- Sensitive data and audit behavior are verified.
- Unit/integration tests cover important success and failure paths.
- Build succeeds with zero warnings and zero errors.
- All tests pass.
- OpenAPI and relevant documentation are updated.
- No secrets, temporary files, sample code, or unrelated changes remain.

If a requested shortcut conflicts with patient privacy, financial integrity, audit
history, or an approved business invariant, stop and surface the conflict before
implementing it.
