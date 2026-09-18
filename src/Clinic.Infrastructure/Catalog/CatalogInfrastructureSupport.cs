namespace Clinic.Infrastructure.Catalog;

internal static class CatalogInfrastructureSupport
{
    public static bool MatchesVersion(byte[] actual, byte[] expected) =>
        actual.AsSpan().SequenceEqual(expected);

    public static void AddAudit(
        ClinicDbContext dbContext,
        long actorUserId,
        string action,
        string entityType,
        long entityId,
        DateTimeOffset occurredAt,
        string? dataJson = null)
    {
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(
            actorUserId,
            action,
            entityType,
            entityId.ToString(CultureInfo.InvariantCulture),
            occurredAt,
            dataJson: dataJson));
    }

    public static ResultError MapDatabaseFailure(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return CatalogErrors.ConcurrencyConflict;
        }

        if (exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            return sqlException.Message.Contains(
                "UX_Devices_Identifier_Active",
                StringComparison.Ordinal)
                ? CatalogErrors.DuplicateIdentifier
                : CatalogErrors.DuplicateName;
        }

        throw new InvalidOperationException("Catalog persistence failed.", exception);
    }
}

internal static class CatalogMapper
{
    public static DepartmentModel Map(Department department, DepartmentStatus status) => new(
        department.Id,
        department.Name,
        department.Description,
        status,
        department.IsArchived,
        department.Room.Id,
        department.Room.Name,
        Convert.ToBase64String(department.RowVersion));

    public static SpecializationModel Map(Specialization specialization) => new(
        specialization.Id,
        specialization.DepartmentId,
        specialization.Name,
        specialization.IsActive,
        specialization.IsArchived,
        Convert.ToBase64String(specialization.RowVersion));

    public static DeviceModel Map(Device device) => new(
        device.Id,
        device.DepartmentId,
        device.Name,
        device.Identifier,
        device.IsActive,
        device.IsArchived,
        Convert.ToBase64String(device.RowVersion));

    public static ServiceModel Map(Service service) => new(
        service.Id,
        service.DepartmentId,
        service.SpecializationId,
        service.Name,
        service.ServiceType,
        service.DurationMinutes,
        service.PricingMode,
        service.CurrentUnitPrice,
        service.IsActive,
        service.IsArchived,
        service.DeviceAssignments
            .Where(item => item.IsActive)
            .OrderBy(item => item.Device.Name)
            .Select(item => new ServiceDeviceModel(
                item.DeviceId,
                item.Device.Name,
                item.Device.Identifier,
                item.IsRequired))
            .ToArray(),
        Convert.ToBase64String(service.RowVersion));
}

internal static class CatalogAuditActions
{
    public const string DepartmentCreated = "catalog.department.created";
    public const string DepartmentUpdated = "catalog.department.updated";
    public const string DepartmentArchived = "catalog.department.archived";
    public const string SpecializationCreated = "catalog.specialization.created";
    public const string SpecializationUpdated = "catalog.specialization.updated";
    public const string SpecializationArchived = "catalog.specialization.archived";
    public const string DeviceCreated = "catalog.device.created";
    public const string DeviceUpdated = "catalog.device.updated";
    public const string DeviceArchived = "catalog.device.archived";
    public const string ServiceCreated = "catalog.service.created";
    public const string ServiceUpdated = "catalog.service.updated";
    public const string ServicePriceChanged = "catalog.service.price_changed";
    public const string ServiceDevicesReplaced = "catalog.service.devices_replaced";
    public const string ServiceArchived = "catalog.service.archived";
}
