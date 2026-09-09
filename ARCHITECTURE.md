# Architecture decisions

## Chosen style

Use a modular monolith with Clean Architecture boundaries. Deploy one API and one
SQL Server database initially; separate services only when real operational scaling
or ownership requires it.

Dependency direction:

```text
API -> Application <- Infrastructure
          |
          v
        Domain
```

- Domain has no project or framework dependency.
- Application knows Domain and defines ports needed by use cases.
- Infrastructure implements Application ports using EF Core and SQL Server.
- API composes the application and exposes HTTP contracts.

## Internal organization

Organize Application code by business feature and use case. Keep request, handler,
validator, response, and tests close together. The initial modules are Identity,
Catalog, Scheduling, Patients, ClinicalRecords, Cashier, Packages, Discounts,
Reporting, and Audit.

## Patterns to use deliberately

- Aggregate Root and Value Object for business invariants.
- CQRS-lite: separate command handlers from query handlers without separate databases.
- Result for expected validation/business failures; exceptions for unexpected faults.
- Strategy/Policy for pricing, discounts, refunds, and scheduling conflict rules.
- Decorator for validation, authorization, logging, and transactions around handlers.
- Factory methods on aggregates to guarantee valid creation and state transitions.
- Optimistic concurrency with SQL Server `rowversion` where concurrent edits matter.

## Patterns intentionally avoided at the start

- No generic repository over EF Core; `DbContext` already supplies repository and
  unit-of-work behavior. Add focused aggregate repositories only where they improve
  a real use case.
- No microservices, event bus, event sourcing, or distributed cache in the initial
  release.
- No framework-heavy mediator dependency until the number of handlers proves it is
  valuable. The application owns small command/query contracts instead.
- No abstractions around every class. Introduce interfaces only at boundaries or
  when multiple implementations/testing require them.

These choices preserve SOLID while avoiding ceremony that increases delivery time
without improving the clinic's business behavior.
