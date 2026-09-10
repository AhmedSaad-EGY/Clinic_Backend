using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Persistence;

internal static class TransactionalResourceLock
{
    public static Task AcquireDepartmentAsync(
        ClinicDbContext dbContext,
        long departmentId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:department:{departmentId}", cancellationToken);

    public static Task AcquireDoctorAsync(
        ClinicDbContext dbContext,
        long doctorId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:doctor:{doctorId}", cancellationToken);

    public static Task AcquireServiceAsync(
        ClinicDbContext dbContext,
        long serviceId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:service:{serviceId}", cancellationToken);

    public static Task AcquirePackageAsync(
        ClinicDbContext dbContext,
        long packageId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:package:{packageId}", cancellationToken);

    public static Task AcquirePatientPackageAsync(
        ClinicDbContext dbContext,
        long patientPackageId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:patient-package:{patientPackageId}", cancellationToken);

    public static Task AcquirePatientPackageRegistrationAsync(
        ClinicDbContext dbContext,
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:patient-package-registration:{idempotencyKey:N}",
            cancellationToken);

    public static Task AcquirePatientAsync(
        ClinicDbContext dbContext,
        long patientId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:patient:{patientId}", cancellationToken);

    public static Task AcquireSecretaryAsync(
        ClinicDbContext dbContext,
        long secretaryUserId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:secretary:{secretaryUserId}", cancellationToken);

    public static Task AcquireShiftAsync(
        ClinicDbContext dbContext,
        long shiftId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:shift:{shiftId}", cancellationToken);

    public static Task AcquireAppointmentAsync(ClinicDbContext dbContext,
        long appointmentId, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:appointment:{appointmentId}",
            cancellationToken);

    public static Task AcquirePackageAppointmentRequestAsync(ClinicDbContext dbContext,
        Guid idempotencyKey, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:package-appointment:{idempotencyKey:N}",
            cancellationToken);

    public static Task AcquirePaymentAsync(ClinicDbContext dbContext,
        long paymentId, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:payment:{paymentId}", cancellationToken);

    public static Task AcquireApprovalRequestAsync(ClinicDbContext dbContext,
        long requestId, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:approval-request:{requestId}",
            cancellationToken);

    public static Task AcquireCashWithdrawalAsync(ClinicDbContext dbContext,
        long withdrawalId, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:cash-withdrawal:{withdrawalId}",
            cancellationToken);

    public static Task AcquirePrescriptionAsync(ClinicDbContext dbContext,
        long prescriptionId, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:prescription:{prescriptionId}",
            cancellationToken);

    public static Task AcquireFollowUpAsync(ClinicDbContext dbContext,
        long followUpId, CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:follow-up:{followUpId}",
            cancellationToken);

    public static Task AcquireRoomAsync(ClinicDbContext dbContext, long roomId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:room:{roomId}", cancellationToken);

    public static Task AcquireDeviceAsync(ClinicDbContext dbContext, long deviceId,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, $"clinic:device:{deviceId}", cancellationToken);

    public static Task AcquireDiscountScheduleReadAsync(ClinicDbContext dbContext,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, "clinic:discount-schedule", "Shared", cancellationToken);

    public static Task AcquireDiscountScheduleWriteAsync(ClinicDbContext dbContext,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, "clinic:discount-schedule", "Exclusive", cancellationToken);

    private static async Task AcquireAsync(
        ClinicDbContext dbContext,
        string resource,
        CancellationToken cancellationToken)
        => await AcquireAsync(dbContext, resource, "Exclusive", cancellationToken);

    private static async Task AcquireAsync(ClinicDbContext dbContext, string resource,
        string mode, CancellationToken cancellationToken)
    {
        try
        {
            _ = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = {mode},
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                IF @lockResult < 0
                    THROW 51000, 'Could not acquire the transactional resource lock.', 1;
                """, cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 51000)
        {
            throw new DbUpdateConcurrencyException(
                "The transactional resource lock could not be acquired.",
                exception);
        }
    }
}
