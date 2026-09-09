using Clinic.Domain.Common;

namespace Clinic.Domain.Packages;

public sealed class PatientPackageService : Entity
{
    private readonly List<PackageSession> _sessions = [];

    private PatientPackageService()
    {
    }

    internal PatientPackageService(PatientPackage patientPackage, PackageService source)
    {
        PatientPackage = patientPackage;
        PatientPackageId = patientPackage.Id;
        DepartmentId = patientPackage.DepartmentId;
        SourcePackageService = source;
        SourcePackageServiceId = source.Id;
        SourcePackageId = source.PackageId;
        ServiceId = source.ServiceId;
        ServiceNameSnapshot = source.Service.Name;
        SpecializationIdSnapshot = source.Service.SpecializationId;
        SpecializationNameSnapshot = source.Service.Specialization.Name;
        SessionsPurchased = source.SessionsIncluded;
        UnitPriceSnapshot = source.UnitPriceAtDefinition;

        for (int sequence = 1; sequence <= SessionsPurchased; sequence++)
        {
            _sessions.Add(new PackageSession(this, sequence));
        }
    }

    public long PatientPackageId { get; private set; }
    public PatientPackage PatientPackage { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public long SourcePackageServiceId { get; private set; }
    public long SourcePackageId { get; private set; }
    public PackageService SourcePackageService { get; private set; } = null!;
    public long ServiceId { get; private set; }
    public string ServiceNameSnapshot { get; private set; } = string.Empty;
    public long SpecializationIdSnapshot { get; private set; }
    public string SpecializationNameSnapshot { get; private set; } = string.Empty;
    public int SessionsPurchased { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }
    public IReadOnlyCollection<PackageSession> Sessions => _sessions;
}
