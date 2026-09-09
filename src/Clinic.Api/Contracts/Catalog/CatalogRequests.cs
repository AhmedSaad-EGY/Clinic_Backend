using Clinic.Domain.Catalog;

namespace Clinic.Api.Contracts.Catalog;

public sealed record CreateDepartmentRequest(
    string Name,
    string? Description,
    string RoomName);

public sealed record UpdateDepartmentRequest(
    string Name,
    string? Description,
    string RoomName,
    string RowVersion);

public sealed record RowVersionRequest(string RowVersion);

public sealed record CreateSpecializationRequest(string Name);

public sealed record UpdateSpecializationRequest(string Name, string RowVersion);

public sealed record CreateDeviceRequest(string Name, string? Identifier);

public sealed record UpdateDeviceRequest(
    string Name,
    string? Identifier,
    string RowVersion);

public sealed record CreateServiceRequest(
    long DepartmentId,
    long SpecializationId,
    string Name,
    ServiceType ServiceType,
    int DurationMinutes,
    PricingMode PricingMode,
    decimal UnitPrice);

public sealed record UpdateServiceRequest(
    string Name,
    ServiceType ServiceType,
    int DurationMinutes,
    PricingMode PricingMode,
    string RowVersion);

public sealed record ChangeServicePriceRequest(decimal UnitPrice, string RowVersion);

public sealed record ServiceDeviceRequest(long DeviceId, bool IsRequired);

public sealed record ReplaceServiceDevicesRequest(
    IReadOnlyCollection<ServiceDeviceRequest> Devices,
    string RowVersion);
