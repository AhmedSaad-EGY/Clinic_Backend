using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Patients;

public sealed class PatientQueryService : IPatientQueryService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PatientQueryService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PatientPage<PatientSummary>>> ListPatientsAsync(string? search,
        string? normalizedPhone, long? fileNumber, PatientGender? gender, string? area,
        bool includeArchived, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Patient> query = _dbContext.Patients.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(item => !item.IsArchived);
        }

        if (search is not null)
        {
            query = query.Where(item => item.FullName.Contains(search) ||
                (normalizedPhone != null && item.PrimaryPhoneNumber == normalizedPhone) ||
                (normalizedPhone != null && item.SecondaryPhoneNumber == normalizedPhone) ||
                (fileNumber != null && item.FileNumber == fileNumber));
        }

        if (gender is not null)
        {
            query = query.Where(item => item.Gender == gender);
        }

        if (area is not null)
        {
            query = query.Where(item => item.Area != null && item.Area.Contains(area));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int skip = checked((pageNumber - 1) * pageSize);
        Patient[] patients = await query.OrderBy(item => item.FullName).ThenBy(item => item.Id)
            .Skip(skip).Take(pageSize).ToArrayAsync(cancellationToken);
        DateOnly today = PatientInfrastructureSupport.ClinicDate(_timeProvider.GetUtcNow());
        return Result.Success(new PatientPage<PatientSummary>(
            patients.Select(item => PatientMapper.Summary(item, today)).ToArray(),
            pageNumber, pageSize, totalCount));
    }

    public async Task<Result<PatientDetails>> GetPatientAsync(long patientId,
        bool includeArchived, CancellationToken cancellationToken)
    {
        Patient? patient = await _dbContext.Patients.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == patientId && (includeArchived || !item.IsArchived),
            cancellationToken);
        if (patient is null)
        {
            return Result.Failure<PatientDetails>(PatientErrors.PatientNotFound);
        }

        DateOnly today = PatientInfrastructureSupport.ClinicDate(_timeProvider.GetUtcNow());
        return Result.Success(PatientMapper.Details(patient, today));
    }

    public Task<Result<PatientPage<PatientNoteModel>>> ListStaffNotesAsync(long patientId,
        bool includeArchived, int pageNumber, int pageSize,
        CancellationToken cancellationToken) => ListNotesAsync(patientId,
            includeAdminOnly: false, includeArchived, pageNumber, pageSize, cancellationToken);

    public Task<Result<PatientPage<PatientNoteModel>>> ListAllNotesAsync(long patientId,
        bool includeArchived, int pageNumber, int pageSize,
        CancellationToken cancellationToken) => ListNotesAsync(patientId,
            includeAdminOnly: true, includeArchived, pageNumber, pageSize, cancellationToken);

    public async Task<Result<PatientPage<TreatmentHistoryModel>>> ListTreatmentHistoryAsync(
        long patientId, bool includeArchived, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await PatientExistsAsync(patientId, includeArchived, cancellationToken))
        {
            return Result.Failure<PatientPage<TreatmentHistoryModel>>(
                PatientErrors.PatientNotFound);
        }

        IQueryable<TreatmentHistory> query = _dbContext.TreatmentHistory.AsNoTracking()
            .Where(item => item.PatientId == patientId);
        if (!includeArchived)
        {
            query = query.Where(item => !item.IsArchived);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        TreatmentHistory[] items = await query.OrderByDescending(item => item.EventDate)
            .ThenByDescending(item => item.Id).Skip((pageNumber - 1) * pageSize)
            .Take(pageSize).ToArrayAsync(cancellationToken);
        return Result.Success(new PatientPage<TreatmentHistoryModel>(
            items.Select(PatientMapper.Treatment).ToArray(), pageNumber, pageSize, totalCount));
    }

    private async Task<Result<PatientPage<PatientNoteModel>>> ListNotesAsync(long patientId,
        bool includeAdminOnly, bool includeArchived, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await PatientExistsAsync(patientId, includeArchived, cancellationToken))
        {
            return Result.Failure<PatientPage<PatientNoteModel>>(PatientErrors.PatientNotFound);
        }

        IQueryable<PatientNote> query = _dbContext.PatientNotes.AsNoTracking()
            .Where(item => item.PatientId == patientId);
        if (!includeAdminOnly)
        {
            query = query.Where(item => item.Visibility == PatientNoteVisibility.Staff);
        }

        if (!includeArchived)
        {
            query = query.Where(item => !item.IsArchived);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        PatientNote[] notes = await query.OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id).Skip((pageNumber - 1) * pageSize)
            .Take(pageSize).ToArrayAsync(cancellationToken);
        return Result.Success(new PatientPage<PatientNoteModel>(
            notes.Select(PatientMapper.Note).ToArray(), pageNumber, pageSize, totalCount));
    }

    private Task<bool> PatientExistsAsync(long patientId, bool includeArchived,
        CancellationToken cancellationToken) => _dbContext.Patients.AsNoTracking().AnyAsync(
            item => item.Id == patientId && (includeArchived || !item.IsArchived),
            cancellationToken);
}
