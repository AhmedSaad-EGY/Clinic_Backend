# Application features

Organize use cases by business capability, not by technical file type:

```text
Features/
  Appointments/
    CreateAppointment/
      CreateAppointmentCommand.cs
      CreateAppointmentHandler.cs
      CreateAppointmentValidator.cs
      CreateAppointmentResponse.cs
```

Planned capabilities: Identity, Catalog, Scheduling, Patients, ClinicalRecords,
Cashier, Packages, Discounts, Reporting, and Audit.

Commands change state. Queries only read. Each handler represents one use case.
