namespace Clinic.Infrastructure.Identity;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly ClinicDbContext _dbContext;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TimeProvider _timeProvider;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthenticationService(
        ClinicDbContext dbContext,
        SignInManager<ApplicationUser> signInManager,
        TimeProvider timeProvider,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _signInManager = signInManager;
        _timeProvider = timeProvider;
        _userManager = userManager;
    }

    public async Task<Result<UserProfile>> SignInAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.FindByNameAsync(userName);

        if (user is null)
        {
            return Result.Failure<UserProfile>(IdentityErrors.InvalidCredentials);
        }

        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        Result<SignInOperation> operationResult = await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            DateTimeOffset occurredAt = _timeProvider.GetUtcNow();

            if (user.IsDisabled || user.IsArchived)
            {
                AddSystemAuthenticationAudit(
                    user.Id,
                    IdentityAuditActions.LoginRejectedDisabled,
                    occurredAt);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Failure<SignInOperation>(IdentityErrors.AccountDisabled);
            }

            bool wasLocked = await _userManager.IsLockedOutAsync(user);
            SignInResult signInResult = await _signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);

            if (signInResult.IsLockedOut)
            {
                string action = wasLocked
                    ? IdentityAuditActions.LoginRejectedLocked
                    : IdentityAuditActions.AccountLocked;
                AddSystemAuthenticationAudit(user.Id, action, occurredAt);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Failure<SignInOperation>(IdentityErrors.AccountLocked);
            }

            if (!signInResult.Succeeded)
            {
                AddSystemAuthenticationAudit(
                    user.Id,
                    IdentityAuditActions.LoginFailed,
                    occurredAt);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Failure<SignInOperation>(IdentityErrors.InvalidCredentials);
            }

            user.LastLoginAt = occurredAt;
            user.UpdatedAt = occurredAt;

            _dbContext.AuditLogs.Add(AuditLog.CreateForUser(
                user.Id,
                IdentityAuditActions.LoginSucceeded,
                nameof(ApplicationUser),
                user.Id.ToString(CultureInfo.InvariantCulture),
                occurredAt));

            await _dbContext.SaveChangesAsync(cancellationToken);
            Result<UserProfile> profileResult = await CreateProfileAsync(user);

            if (profileResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<SignInOperation>(profileResult.Error);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new SignInOperation(user, profileResult.Value));
        });

        if (operationResult.IsFailure)
        {
            return Result.Failure<UserProfile>(operationResult.Error);
        }

        await _signInManager.SignInAsync(operationResult.Value.User, isPersistent: false);
        return Result.Success(operationResult.Value.Profile);
    }

    public Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _signInManager.SignOutAsync();
    }

    public async Task<Result<UserProfile>> GetUserAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserProfile>(IdentityErrors.UserNotFound);
        }

        if (user.IsDisabled || user.IsArchived)
        {
            return Result.Failure<UserProfile>(IdentityErrors.AccountDisabled);
        }

        return await CreateProfileAsync(user);
    }

    public async Task<Result<UserProfile>> ChangePasswordAsync(
        long userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        Result<ApplicationUser> operationResult = await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            ApplicationUser? user = await _userManager.FindByIdAsync(
                userId.ToString(CultureInfo.InvariantCulture));

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<ApplicationUser>(IdentityErrors.UserNotFound);
            }

            if (user.IsDisabled || user.IsArchived)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<ApplicationUser>(IdentityErrors.AccountDisabled);
            }

            IdentityResult passwordResult = await _userManager.ChangePasswordAsync(
                user,
                currentPassword,
                newPassword);

            if (!passwordResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                ResultError error = passwordResult.Errors.Any(candidate =>
                    string.Equals(candidate.Code, "PasswordMismatch", StringComparison.Ordinal))
                    ? IdentityErrors.CurrentPasswordIncorrect
                    : CreatePasswordError();

                return Result.Failure<ApplicationUser>(error);
            }

            DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
            user.MustChangePassword = false;
            user.UpdatedAt = occurredAt;

            _dbContext.AuditLogs.Add(AuditLog.CreateForUser(
                user.Id,
                IdentityAuditActions.PasswordChanged,
                nameof(ApplicationUser),
                user.Id.ToString(CultureInfo.InvariantCulture),
                occurredAt));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success(user);
        });

        if (operationResult.IsFailure)
        {
            return Result.Failure<UserProfile>(operationResult.Error);
        }

        await _signInManager.RefreshSignInAsync(operationResult.Value);
        return await CreateProfileAsync(operationResult.Value);
    }

    private async Task<Result<UserProfile>> CreateProfileAsync(ApplicationUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);
        string? role = roles.FirstOrDefault();

        if (role is null)
        {
            return Result.Failure<UserProfile>(IdentityErrors.Forbidden);
        }

        return Result.Success(new UserProfile(
            user.Id,
            user.FullName,
            user.UserName ?? string.Empty,
            role,
            user.MustChangePassword));
    }

    private static ResultError CreatePasswordError() => IdentityErrors.Validation(
        "كلمة المرور يجب أن تكون 10 أحرف على الأقل وتحتوي على حرف كبير وحرف صغير ورقم.");

    private void AddSystemAuthenticationAudit(
        long userId,
        string action,
        DateTimeOffset occurredAt)
    {
        _dbContext.AuditLogs.Add(AuditLog.CreateForSystem(
            action,
            nameof(ApplicationUser),
            userId.ToString(CultureInfo.InvariantCulture),
            occurredAt));
    }

    private sealed record SignInOperation(ApplicationUser User, UserProfile Profile);
}
