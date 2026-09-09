using System.Data;
using System.Globalization;
using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Common;
using Clinic.Application.Features.ClinicalRecords;
using Clinic.Domain.Auditing;
using Clinic.Domain.ClinicalRecords;
using Clinic.Domain.Common;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.ClinicalRecords;

public sealed class FollowUpCommandService(ClinicDbContext dbContext,
    TimeProvider timeProvider, IClinicalRecordQueryService queryService)
    : IFollowUpCommandService
{
    public async Task<Result<FollowUpModel>> CancelFollowUpAsync(long actorUserId,
        long followUpId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        long? patientId = await dbContext.FollowUps.AsNoTracking()
            .Where(item => item.Id == followUpId)
            .Select(item => (long?)item.PatientId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!patientId.HasValue)
            return Result.Failure<FollowUpModel>(ClinicalRecordErrors.FollowUpNotFound);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquirePatientAsync(dbContext,
                    patientId.Value, cancellationToken);
                await TransactionalResourceLock.AcquireFollowUpAsync(dbContext,
                    followUpId, cancellationToken);
                FollowUp? followUp = await dbContext.FollowUps
                    .Include(item => item.Prescription).ThenInclude(item => item.AppointmentService)
                        .ThenInclude(item => item.Appointment).ThenInclude(item => item.Patient)
                    .SingleOrDefaultAsync(item => item.Id == followUpId &&
                        !item.Prescription.AppointmentService.Appointment.Patient.IsArchived,
                        cancellationToken);
                if (followUp is null)
                    return Result.Failure<FollowUpModel>(
                        ClinicalRecordErrors.FollowUpNotFound);
                if (!followUp.RowVersion.AsSpan().SequenceEqual(rowVersion))
                    return Result.Failure<FollowUpModel>(
                        ClinicalRecordErrors.ConcurrencyConflict);

                DateTimeOffset now = timeProvider.GetUtcNow();
                followUp.Cancel(actorUserId, now, reason);
                dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId,
                    "clinical.follow_up_cancelled", nameof(FollowUp),
                    followUp.Id.ToString(CultureInfo.InvariantCulture), now, reason));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Result<FollowUpModel> model = await queryService.GetFollowUpAsync(
                    followUpId, includeArchivedPatient: false, cancellationToken);
                return model.IsSuccess
                    ? model
                    : throw new InvalidOperationException("Saved follow-up was not found.");
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<FollowUpModel>(
                ClinicalRecordErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<FollowUpModel>(ClinicalRecordErrors.ConcurrencyConflict);
        }
    }
}
