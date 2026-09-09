using Clinic.Domain.Catalog;

namespace Clinic.Application.Abstractions.Catalog;

public sealed record DepartmentModel(
    long Id,
    string Name,
    string? Description,
    DepartmentStatus Status,
    bool IsArchived,
    long RoomId,
    string RoomName,
    string RowVersion);

public sealed record SpecializationModel(
    long Id,
    long DepartmentId,
    string Name,
    bool IsActive,
    bool IsArchived,
    string RowVersion);

public sealed record DeviceModel(
    long Id,
    long DepartmentId,
    string Name,
    string? Identifier,
    bool IsActive,
    bool IsArchived,
    string RowVersion);

public sealed record ServiceDeviceModel(
    long DeviceId,
    string DeviceName,
    string? DeviceIdentifier,
    bool IsRequired);

public sealed record ServiceModel(
    long Id,
    long DepartmentId,
    long SpecializationId,
    string Name,
    ServiceType ServiceType,
    int DurationMinutes,
    PricingMode PricingMode,
    decimal CurrentUnitPrice,
    bool IsActive,
    bool IsArchived,
    IReadOnlyCollection<ServiceDeviceModel> Devices,
    string RowVersion);

public sealed record ServicePriceHistoryModel(
    long Id,
    decimal UnitPrice,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    long ChangedByUserId,
    DateTimeOffset ChangedAt);

public sealed record ServiceDeviceInput(long DeviceId, bool IsRequired);

public sealed record CatalogPage<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
