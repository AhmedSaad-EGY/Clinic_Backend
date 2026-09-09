using System.Security.Cryptography;
using System.Text;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Domain.Common;
using Clinic.Domain.Packages;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Packages;

public sealed class PatientPackageCommandService : IPatientPackageCommandService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PatientPackageCommandService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<Result<PatientPackageRegistrationResult>> RegisterAsync(long actorUserId,
        long patientId, long packageId, byte[] expectedPackageRowVersion, Guid idempotencyKey,
        CancellationToken cancellationToken) => ExecuteRegistrationAsync(async () =>
    {
        string fingerprint = Fingerprint(actorUserId, patientId, packageId,
            expectedPackageRowVersion);
        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await TransactionalResourceLock.AcquirePatientPackageRegistrationAsync(_dbContext,
            idempotencyKey, cancellationToken);

        PatientPackage? existing = await FullQuery().SingleOrDefaultAsync(
            item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint,
                    StringComparison.Ordinal))
            {
                return Result.Failure<PatientPackageRegistrationResult>(
                    PackageErrors.IdempotencyConflict);
            }

            DateTimeOffset replayedAt = _timeProvider.GetUtcNow();
            bool stopped = await IsDepartmentStoppedAsync(existing.DepartmentId,
                replayedAt, cancellationToken);
            PatientPackageModel replay = PatientPackageInfrastructureSupport.Map(existing,
                stopped, replayedAt);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new PatientPackageRegistrationResult(replay, true));
        }

        long? departmentId = await _dbContext.Packages.AsNoTracking()
            .Where(item => item.Id == packageId).Select(item => (long?)item.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.DefinitionUnavailable);
        }

        await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext, departmentId.Value,
            cancellationToken);
        await TransactionalResourceLock.AcquirePatientAsync(_dbContext, patientId,
            cancellationToken);
        await TransactionalResourceLock.AcquirePackageAsync(_dbContext, packageId,
            cancellationToken);

        Patient? patient = await _dbContext.Patients.SingleOrDefaultAsync(
            item => item.Id == patientId && !item.IsArchived, cancellationToken);
        if (patient is null)
        {
            return Result.Failure<PatientPackageRegistrationResult>(PackageErrors.PatientNotFound);
        }

        Package? package = await _dbContext.Packages.Include(item => item.Department)
            .Include(item => item.Services).ThenInclude(item => item.Service)
                .ThenInclude(item => item.Specialization)
            .AsSplitQuery().SingleOrDefaultAsync(item => item.Id == packageId,
                cancellationToken);
        if (package is null || package.IsArchived || !package.IsActive ||
            package.Department.IsArchived || package.Services.All(item => !item.IsActive))
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.DefinitionUnavailable);
        }

        if (!package.RowVersion.AsSpan().SequenceEqual(expectedPackageRowVersion))
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.ConcurrencyConflict);
        }

        if (package.ActivationGraceDays is null || package.UsageDurationDays is null)
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.DefinitionIncomplete);
        }

        long[] serviceIds = package.Services.Where(item => item.IsActive)
            .Select(item => item.ServiceId).Order().ToArray();
        foreach (long serviceId in serviceIds)
        {
            await TransactionalResourceLock.AcquireServiceAsync(_dbContext, serviceId,
                cancellationToken);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool unavailable = package.Services.Where(item => item.IsActive).Any(item =>
                item.Service.IsArchived || !item.Service.IsActive ||
                item.Service.PricingMode != Domain.Catalog.PricingMode.Fixed) ||
            await IsDepartmentStoppedAsync(departmentId.Value, now, cancellationToken);
        if (unavailable)
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.DefinitionUnavailable);
        }

        PatientPackage patientPackage = PatientPackage.Register(patient, package,
            idempotencyKey, fingerprint, actorUserId, now);
        _dbContext.PatientPackages.Add(patientPackage);
        await _dbContext.SaveChangesAsync(cancellationToken);
        PatientPackageInfrastructureSupport.AddAudit(_dbContext, actorUserId,
            PatientPackageAuditActions.Registered, patientPackage.Id, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        PatientPackageModel response = PatientPackageInfrastructureSupport.Map(patientPackage,
            false, now);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new PatientPackageRegistrationResult(response, false));
    });

    public Task<Result<PatientPackageModel>> ExtendAsync(long adminUserId, long patientPackageId,
        PatientPackageExtensionType extensionType, DateTimeOffset newDeadline, string reason,
        byte[] expectedRowVersion, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            long? departmentId = await _dbContext.PatientPackages.AsNoTracking()
                .Where(item => item.Id == patientPackageId)
                .Select(item => (long?)item.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            if (departmentId is null)
            {
                return Result.Failure<PatientPackageModel>(
                    PackageErrors.PatientPackageNotFound);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext,
                departmentId.Value, cancellationToken);
            await TransactionalResourceLock.AcquirePatientPackageAsync(_dbContext,
                patientPackageId, cancellationToken);
            PatientPackage? patientPackage = await FullQuery().SingleOrDefaultAsync(
                item => item.Id == patientPackageId, cancellationToken);
            if (patientPackage is null)
            {
                return Result.Failure<PatientPackageModel>(
                    PackageErrors.PatientPackageNotFound);
            }

            if (!patientPackage.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
            {
                return Result.Failure<PatientPackageModel>(PackageErrors.ConcurrencyConflict);
            }

            DateTimeOffset oldDeadline = extensionType ==
                PatientPackageExtensionType.ActivationDeadline
                ? patientPackage.ActivationDeadlineAt ?? default
                : patientPackage.ExpiresAt ?? default;
            DateTimeOffset now = _timeProvider.GetUtcNow();
            patientPackage.Extend(extensionType, newDeadline, adminUserId, now);
            PatientPackageInfrastructureSupport.AddAudit(_dbContext, adminUserId,
                PatientPackageAuditActions.Extended, patientPackage.Id, now,
                new { ExtensionType = extensionType.ToString(), OldDeadline = oldDeadline,
                    NewDeadline = newDeadline, Reason = reason });
            await _dbContext.SaveChangesAsync(cancellationToken);
            bool stopped = await IsDepartmentStoppedAsync(patientPackage.DepartmentId, now,
                cancellationToken);
            PatientPackageModel response = PatientPackageInfrastructureSupport.Map(patientPackage,
                stopped, now);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        });

    private IQueryable<PatientPackage> FullQuery() => _dbContext.PatientPackages
        .Include(item => item.Patient)
        .Include(item => item.Package).ThenInclude(item => item.Department)
        .Include(item => item.Services).ThenInclude(item => item.SourcePackageService)
            .ThenInclude(item => item.Service)
        .Include(item => item.Services).ThenInclude(item => item.Sessions)
        .AsSplitQuery();

    private Task<bool> IsDepartmentStoppedAsync(long departmentId, DateTimeOffset now,
        CancellationToken cancellationToken) => _dbContext.DepartmentClosures.AsNoTracking()
        .AnyAsync(item => item.DepartmentId == departmentId && item.CancelledAt == null &&
            item.StartAt <= now && now < item.EndAt, cancellationToken);

    private static string Fingerprint(long actorUserId, long patientId, long packageId,
        byte[] packageRowVersion)
    {
        string input = $"{actorUserId}|{patientId}|{packageId}|" +
            Convert.ToBase64String(packageRowVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private async Task<Result<PatientPackageRegistrationResult>> ExecuteRegistrationAsync(
        Func<Task<Result<PatientPackageRegistrationResult>>> operation)
    {
        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                return await operation();
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.ConcurrencyConflict);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.Validation(exception.Message));
        }
    }

    private async Task<Result<PatientPackageModel>> ExecuteAsync(
        Func<Task<Result<PatientPackageModel>>> operation)
    {
        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                return await operation();
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PatientPackageModel>(PackageErrors.ConcurrencyConflict);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PatientPackageModel>(
                PackageErrors.Validation(exception.Message));
        }
    }
}
