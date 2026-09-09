using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Domain.Common;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Patients;

public sealed class PatientAdministrationService : IPatientAdministrationService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PatientAdministrationService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PatientDetails>> CreatePatientAsync(long actorUserId,
        PatientInput input, CancellationToken cancellationToken)
    {
        if (await _dbContext.Patients.AnyAsync(
            item => item.PrimaryPhoneNumber == input.PrimaryPhoneNumber, cancellationToken))
        {
            return Result.Failure<PatientDetails>(PatientErrors.DuplicatePrimaryPhone);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateOnly today = PatientInfrastructureSupport.ClinicDate(now);
        Patient patient;
        try
        {
            patient = CreatePatient(input, actorUserId, today, now);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PatientDetails>(PatientErrors.Validation(exception.Message));
        }

        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                _dbContext.Patients.Add(patient);
                await _dbContext.SaveChangesAsync(cancellationToken);
                PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    PatientAuditActions.PatientCreated, nameof(Patient), patient.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(PatientMapper.Details(patient, today));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PatientDetails>(
                PatientInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<PatientDetails>> UpdatePatientAsync(long actorUserId,
        long patientId, PatientInput input, byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Patient? patient = await _dbContext.Patients.SingleOrDefaultAsync(
            item => item.Id == patientId && !item.IsArchived, cancellationToken);
        if (patient is null)
        {
            return Result.Failure<PatientDetails>(PatientErrors.PatientNotFound);
        }

        if (!PatientInfrastructureSupport.MatchesVersion(patient.RowVersion, expectedRowVersion))
        {
            return Result.Failure<PatientDetails>(PatientErrors.ConcurrencyConflict);
        }

        if (await _dbContext.Patients.AnyAsync(item => item.Id != patientId &&
            item.PrimaryPhoneNumber == input.PrimaryPhoneNumber, cancellationToken))
        {
            return Result.Failure<PatientDetails>(PatientErrors.DuplicatePrimaryPhone);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateOnly today = PatientInfrastructureSupport.ClinicDate(now);
        try
        {
            patient.Update(input.FullName, input.PrimaryPhoneNumber,
                input.SecondaryPhoneNumber, input.BirthDate, input.Age, input.Gender,
                input.Area, input.Address, input.Email, input.GuardianName,
                input.GuardianPhoneNumber, actorUserId, today, now);
            PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                PatientAuditActions.PatientUpdated, nameof(Patient), patient.Id, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(PatientMapper.Details(patient, today));
        }
        catch (DomainException exception)
        {
            return Result.Failure<PatientDetails>(PatientErrors.Validation(exception.Message));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PatientDetails>(
                PatientInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> ArchivePatientAsync(long actorUserId, long patientId,
        string reason, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquirePatientAsync(
                    _dbContext, patientId, cancellationToken);
                Patient? patient = await _dbContext.Patients.SingleOrDefaultAsync(
                    item => item.Id == patientId && !item.IsArchived, cancellationToken);
                if (patient is null)
                {
                    return Result.Failure(PatientErrors.PatientNotFound);
                }

                if (!PatientInfrastructureSupport.MatchesVersion(
                        patient.RowVersion, expectedRowVersion))
                {
                    return Result.Failure(PatientErrors.ConcurrencyConflict);
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();
                patient.Archive(actorUserId, now);
                PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    PatientAuditActions.PatientArchived, nameof(Patient), patient.Id, now, reason);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure(PatientInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<SensitiveNoteReceipt>> CreateNoteAsync(long actorUserId,
        long patientId, string noteText, PatientNoteVisibility visibility,
        CancellationToken cancellationToken)
    {
        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquirePatientAsync(
                    _dbContext, patientId, cancellationToken);
                bool patientIsActive = await _dbContext.Patients.AnyAsync(
                    item => item.Id == patientId && !item.IsArchived, cancellationToken);
                if (!patientIsActive)
                {
                    return Result.Failure<SensitiveNoteReceipt>(PatientErrors.PatientNotFound);
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();
                PatientNote note = PatientNote.Create(
                    patientId, noteText, visibility, actorUserId, now);
                _dbContext.PatientNotes.Add(note);
                await _dbContext.SaveChangesAsync(cancellationToken);
                PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    PatientAuditActions.NoteCreated, nameof(PatientNote), note.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(new SensitiveNoteReceipt(note.Id, note.CreatedAt));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<SensitiveNoteReceipt>(
                PatientInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> ArchiveNoteAsync(long actorUserId, long noteId, string reason,
        byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        PatientNote? note = await _dbContext.PatientNotes.SingleOrDefaultAsync(
            item => item.Id == noteId && !item.IsArchived, cancellationToken);
        if (note is null)
        {
            return Result.Failure(PatientErrors.NoteNotFound);
        }

        if (!PatientInfrastructureSupport.MatchesVersion(note.RowVersion, expectedRowVersion))
        {
            return Result.Failure(PatientErrors.ConcurrencyConflict);
        }

        note.Archive();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
            PatientAuditActions.NoteArchived, nameof(PatientNote), note.Id, now, reason);
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result<TreatmentHistoryModel>> CreateTreatmentHistoryAsync(
        long actorUserId, long patientId, DateOnly eventDate, string description,
        CancellationToken cancellationToken)
    {
        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquirePatientAsync(
                    _dbContext, patientId, cancellationToken);
                bool patientIsActive = await _dbContext.Patients.AnyAsync(
                    item => item.Id == patientId && !item.IsArchived, cancellationToken);
                if (!patientIsActive)
                {
                    return Result.Failure<TreatmentHistoryModel>(PatientErrors.PatientNotFound);
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();
                DateOnly today = PatientInfrastructureSupport.ClinicDate(now);
                TreatmentHistory item = TreatmentHistory.Create(patientId, eventDate,
                    description, actorUserId, today, now);
                _dbContext.TreatmentHistory.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken);
                PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    PatientAuditActions.TreatmentHistoryCreated, nameof(TreatmentHistory),
                    item.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(PatientMapper.Treatment(item));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<TreatmentHistoryModel>(
                PatientInfrastructureSupport.MapDatabaseFailure(exception));
        }
        catch (DomainException exception)
        {
            return Result.Failure<TreatmentHistoryModel>(
                PatientErrors.Validation(exception.Message));
        }
    }

    public async Task<Result> ArchiveTreatmentHistoryAsync(long actorUserId,
        long treatmentHistoryId, string reason, byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        TreatmentHistory? item = await _dbContext.TreatmentHistory.SingleOrDefaultAsync(
            value => value.Id == treatmentHistoryId && !value.IsArchived, cancellationToken);
        if (item is null)
        {
            return Result.Failure(PatientErrors.TreatmentHistoryNotFound);
        }

        if (!PatientInfrastructureSupport.MatchesVersion(item.RowVersion, expectedRowVersion))
        {
            return Result.Failure(PatientErrors.ConcurrencyConflict);
        }

        item.Archive();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        PatientInfrastructureSupport.AddAudit(_dbContext, actorUserId,
            PatientAuditActions.TreatmentHistoryArchived, nameof(TreatmentHistory),
            item.Id, now, reason);
        return await SaveAsync(cancellationToken);
    }

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure(PatientInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    private static Patient CreatePatient(PatientInput input, long actorUserId,
        DateOnly today, DateTimeOffset now) => Patient.Create(input.FullName,
        input.PrimaryPhoneNumber, input.SecondaryPhoneNumber, input.BirthDate, input.Age,
        input.Gender, input.Area, input.Address, input.Email, input.GuardianName,
        input.GuardianPhoneNumber, actorUserId, today, now);
}
