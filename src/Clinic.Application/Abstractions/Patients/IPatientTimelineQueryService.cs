using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Patients;

public interface IPatientTimelineQueryService
{
    Task<Result<PatientTimelinePage>> GetAsync(long patientId,
        bool includeArchivedPatient, bool includeAdminOnlyNotes,
        PatientTimelineFilter filter, CancellationToken cancellationToken);
}
