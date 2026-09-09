using Clinic.Domain.Common;

namespace Clinic.Domain.Packages;

public sealed class PackageSession : Entity
{
    private PackageSession()
    {
    }

    internal PackageSession(PatientPackageService service, int sequenceNumber)
    {
        PatientPackageService = service;
        PatientPackageServiceId = service.Id;
        PatientPackageId = service.PatientPackageId;
        ServiceId = service.ServiceId;
        SequenceNumber = sequenceNumber;
        UnitPriceSnapshot = service.UnitPriceSnapshot;
        Status = PackageSessionStatus.Available;
    }

    public long PatientPackageServiceId { get; private set; }
    public PatientPackageService PatientPackageService { get; private set; } = null!;
    public long PatientPackageId { get; private set; }
    public long ServiceId { get; private set; }
    public int SequenceNumber { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }
    public PackageSessionStatus Status { get; private set; }
    public DateTimeOffset? ReservedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
