using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Appointments;

public interface IAppointmentService
{
    Task<Result<AppointmentAvailability>> CheckAvailabilityAsync(long actorUserId,
        AppointmentInput input, long? excludedAppointmentId,
        CancellationToken cancellationToken);
    Task<Result<AppointmentModel>> CreateAsync(long actorUserId, AppointmentInput input,
        CancellationToken cancellationToken);
    Task<Result<AppointmentModel>> UpdateAsync(long actorUserId, long appointmentId,
        AppointmentInput input, byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result> CancelAsync(long actorUserId, long appointmentId, string? reason,
        byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result> CompleteAsync(long actorUserId, long appointmentId, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result> MarkNoShowAsync(long actorUserId, long appointmentId, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<AppointmentModel>> GetAsync(long appointmentId,
        CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<AppointmentModel>>> CalendarAsync(DateOnly clinicDate,
        long? departmentId, CancellationToken cancellationToken);
    Task<Result<AppointmentPage>> SearchAsync(AppointmentSearch search,
        CancellationToken cancellationToken);
    Task<Result<int>> RevalidateSuspendedAsync(long? actorUserId, long? departmentId,
        CancellationToken cancellationToken);
    Task<Result<AppointmentModel>> TransferDoctorAsync(long actorUserId,
        long appointmentId, long appointmentServiceId, long doctorId, string reason,
        byte[] rowVersion, CancellationToken cancellationToken);
}
