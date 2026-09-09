using System.Globalization;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Identity;

public sealed class SecretaryAccountService : ISecretaryAccountService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly UserManager<ApplicationUser> _userManager;

    public SecretaryAccountService(
        ClinicDbContext dbContext,
        TimeProvider timeProvider,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _userManager = userManager;
    }

    public async Task<Result<SecretarySummary>> CreateAsync(
        long adminUserId,
        string fullName,
        string phoneNumber,
        string? email,
        string userName,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        Result adminCheck = await EnsureAdminAsync(adminUserId);
        if (adminCheck.IsFailure)
        {
            return Result.Failure<SecretarySummary>(adminCheck.Error);
        }

        string? normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null)
        {
            return Result.Failure<SecretarySummary>(IdentityErrors.Validation(
                "رقم الهاتف غير صحيح."));
        }

        bool phoneExists = await _dbContext.Users.AnyAsync(
            user => user.NormalizedPhoneNumber == normalizedPhone,
            cancellationToken);

        if (phoneExists)
        {
            return Result.Failure<SecretarySummary>(IdentityErrors.DuplicatePhone);
        }

        if (email is not null)
        {
            string? normalizedEmail = _userManager.NormalizeEmail(email);
            bool emailExists = await _dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken);

            if (emailExists)
            {
                return Result.Failure<SecretarySummary>(IdentityErrors.DuplicateEmail);
            }
        }

        DateTimeOffset createdAt = _timeProvider.GetUtcNow();
        ApplicationUser secretary = new()
        {
            FullName = fullName,
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = normalizedPhone,
            Email = email,
            UserName = userName,
            MustChangePassword = true,
            CreatedByAdminUserId = adminUserId,
            CreatedAt = createdAt
        };

        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            IdentityResult creationResult = await _userManager.CreateAsync(
                secretary,
                temporaryPassword);

            if (!creationResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<SecretarySummary>(MapCreationError(creationResult));
            }

            IdentityResult roleResult = await _userManager.AddToRoleAsync(
                secretary,
                RoleNames.Secretary);

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<SecretarySummary>(IdentityErrors.OperationFailed(
                    "تعذر إسناد دور السكرتارية للحساب."));
            }

            CashDrawer drawer = CashDrawer.Create(
                secretary.Id,
                secretary.FullName,
                createdAt);
            _dbContext.CashDrawers.Add(drawer);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.AuditLogs.Add(AuditLog.CreateForUser(
                adminUserId,
                IdentityAuditActions.SecretaryCreated,
                nameof(ApplicationUser),
                secretary.Id.ToString(CultureInfo.InvariantCulture),
                createdAt));

            _dbContext.AuditLogs.Add(AuditLog.CreateForUser(
                adminUserId,
                "cashier.cash_drawer_created",
                nameof(CashDrawer),
                drawer.Id.ToString(CultureInfo.InvariantCulture),
                createdAt));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success(MapSummary(secretary, createdAt));
        });
    }

    public async Task<Result<PagedResult<SecretarySummary>>> ListAsync(
        long adminUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Result adminCheck = await EnsureAdminAsync(adminUserId);
        if (adminCheck.IsFailure)
        {
            return Result.Failure<PagedResult<SecretarySummary>>(adminCheck.Error);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        IQueryable<ApplicationUser> query =
            from user in _dbContext.Users.AsNoTracking()
            join userRole in _dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == RoleNames.Secretary && !user.IsArchived
            orderby user.FullName, user.Id
            select user;

        int totalCount = await query.CountAsync(cancellationToken);
        List<SecretarySummary> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new SecretarySummary(
                user.Id,
                user.FullName,
                user.UserName ?? string.Empty,
                user.PhoneNumber ?? string.Empty,
                user.Email,
                user.IsDisabled,
                user.LockoutEnd != null && user.LockoutEnd > now,
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SecretarySummary>(
            items,
            pageNumber,
            pageSize,
            totalCount));
    }

    public Task<Result> DisableAsync(
        long adminUserId,
        long secretaryUserId,
        string reason,
        CancellationToken cancellationToken) =>
        UpdateSecretaryAsync(
            adminUserId,
            secretaryUserId,
            IdentityAuditActions.SecretaryDisabled,
            reason,
            user => user.IsDisabled = true,
            updateSecurityStamp: true,
            cancellationToken);

    public Task<Result> EnableAsync(
        long adminUserId,
        long secretaryUserId,
        CancellationToken cancellationToken) =>
        UpdateSecretaryAsync(
            adminUserId,
            secretaryUserId,
            IdentityAuditActions.SecretaryEnabled,
            reason: null,
            user => user.IsDisabled = false,
            updateSecurityStamp: true,
            cancellationToken);

    public Task<Result> UnlockAsync(
        long adminUserId,
        long secretaryUserId,
        CancellationToken cancellationToken) =>
        UpdateSecretaryAsync(
            adminUserId,
            secretaryUserId,
            IdentityAuditActions.SecretaryUnlocked,
            reason: null,
            user =>
            {
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
            },
            updateSecurityStamp: false,
            cancellationToken);

    public async Task<Result> ResetPasswordAsync(
        long adminUserId,
        long secretaryUserId,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            Result adminCheck = await EnsureAdminAsync(adminUserId);
            if (adminCheck.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return adminCheck;
            }

            Result<ApplicationUser> targetResult = await FindSecretaryAsync(secretaryUserId);
            if (targetResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(targetResult.Error);
            }

            ApplicationUser secretary = targetResult.Value;
            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(secretary);
            IdentityResult resetResult = await _userManager.ResetPasswordAsync(
                secretary,
                resetToken,
                temporaryPassword);

            if (!resetResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(CreatePasswordError());
            }

            DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
            secretary.MustChangePassword = true;
            secretary.UpdatedAt = occurredAt;
            IdentityResult stampResult = await _userManager.UpdateSecurityStampAsync(secretary);

            if (!stampResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(IdentityErrors.OperationFailed(
                    "تعذر تأمين الحساب بعد إعادة تعيين كلمة المرور."));
            }

            AddAuditLog(
                adminUserId,
                secretaryUserId,
                IdentityAuditActions.SecretaryPasswordReset,
                occurredAt,
                reason: null);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success();
        });
    }

    private async Task<Result> UpdateSecretaryAsync(
        long adminUserId,
        long secretaryUserId,
        string auditAction,
        string? reason,
        Action<ApplicationUser> update,
        bool updateSecurityStamp,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            Result adminCheck = await EnsureAdminAsync(adminUserId);
            if (adminCheck.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return adminCheck;
            }

            Result<ApplicationUser> targetResult = await FindSecretaryAsync(secretaryUserId);
            if (targetResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(targetResult.Error);
            }

            ApplicationUser secretary = targetResult.Value;
            DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
            update(secretary);
            secretary.UpdatedAt = occurredAt;

            IdentityResult updateResult = updateSecurityStamp
                ? await _userManager.UpdateSecurityStampAsync(secretary)
                : await _userManager.UpdateAsync(secretary);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(IdentityErrors.OperationFailed(
                    "تعذر تحديث حساب السكرتارية."));
            }

            AddAuditLog(
                adminUserId,
                secretaryUserId,
                auditAction,
                occurredAt,
                reason);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success();
        });
    }

    private async Task<Result> EnsureAdminAsync(long adminUserId)
    {
        ApplicationUser? admin = await _userManager.FindByIdAsync(
            adminUserId.ToString(CultureInfo.InvariantCulture));

        if (admin is null || admin.IsDisabled || admin.IsArchived)
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        bool isAdmin = await _userManager.IsInRoleAsync(admin, RoleNames.Admin);
        return isAdmin
            ? Result.Success()
            : Result.Failure(IdentityErrors.Forbidden);
    }

    private async Task<Result<ApplicationUser>> FindSecretaryAsync(long secretaryUserId)
    {
        ApplicationUser? secretary = await _userManager.FindByIdAsync(
            secretaryUserId.ToString(CultureInfo.InvariantCulture));

        if (secretary is null || secretary.IsArchived)
        {
            return Result.Failure<ApplicationUser>(IdentityErrors.UserNotFound);
        }

        bool isSecretary = await _userManager.IsInRoleAsync(secretary, RoleNames.Secretary);
        return isSecretary
            ? Result.Success(secretary)
            : Result.Failure<ApplicationUser>(IdentityErrors.UserNotFound);
    }

    private void AddAuditLog(
        long adminUserId,
        long secretaryUserId,
        string action,
        DateTimeOffset occurredAt,
        string? reason)
    {
        _dbContext.AuditLogs.Add(AuditLog.CreateForUser(
            adminUserId,
            action,
            nameof(ApplicationUser),
            secretaryUserId.ToString(CultureInfo.InvariantCulture),
            occurredAt,
            reason));
    }

    private static SecretarySummary MapSummary(
        ApplicationUser user,
        DateTimeOffset now) => new(
            user.Id,
            user.FullName,
            user.UserName ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.Email,
            user.IsDisabled,
            user.LockoutEnd != null && user.LockoutEnd > now,
            user.CreatedAt);

    private static ResultError MapCreationError(IdentityResult result)
    {
        if (result.Errors.Any(error =>
            string.Equals(error.Code, "DuplicateUserName", StringComparison.Ordinal)))
        {
            return IdentityErrors.DuplicateUserName;
        }

        if (result.Errors.Any(error =>
            string.Equals(error.Code, "DuplicateEmail", StringComparison.Ordinal)))
        {
            return IdentityErrors.DuplicateEmail;
        }

        return CreatePasswordError();
    }

    private static ResultError CreatePasswordError() => IdentityErrors.Validation(
        "كلمة المرور يجب أن تكون 10 أحرف على الأقل وتحتوي على حرف كبير وحرف صغير ورقم.");
}
