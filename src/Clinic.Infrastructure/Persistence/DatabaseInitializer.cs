namespace Clinic.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly UserManager<ApplicationUser> _userManager;

    public DatabaseInitializer(
        IConfiguration configuration,
        ClinicDbContext dbContext,
        TimeProvider timeProvider,
        UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _userManager = userManager;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        bool applyMigrations = _configuration.GetValue<bool>(
            "DatabaseInitialization:ApplyMigrationsOnStartup");
        bool createAdmin = _configuration.GetValue<bool>("BootstrapAdmin:Enabled");

        if (!applyMigrations && !createAdmin)
        {
            return;
        }

        if (applyMigrations)
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }

        if (!createAdmin)
        {
            return;
        }

        string userName = GetRequiredSetting("BootstrapAdmin:UserName");
        string password = GetRequiredSetting("BootstrapAdmin:Password");
        string fullName = GetRequiredSetting("BootstrapAdmin:FullName");
        string phoneNumber = GetRequiredSetting("BootstrapAdmin:PhoneNumber");
        string? normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);

        if (normalizedPhone is null)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:PhoneNumber is not a valid phone number.");
        }

        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            ApplicationUser? existingAdmin = await _userManager.FindByNameAsync(userName);
            if (existingAdmin is not null)
            {
                if (!await _userManager.IsInRoleAsync(existingAdmin, RoleNames.Admin))
                {
                    throw new InvalidOperationException(
                        "Bootstrap admin username already belongs to a non-admin account.");
                }

                return;
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            DateTimeOffset createdAt = _timeProvider.GetUtcNow();
            ApplicationUser admin = new()
            {
                FullName = fullName,
                PhoneNumber = phoneNumber,
                NormalizedPhoneNumber = normalizedPhone,
                Email = _configuration["BootstrapAdmin:Email"],
                UserName = userName,
                MustChangePassword = true,
                CreatedAt = createdAt
            };

            IdentityResult creationResult = await _userManager.CreateAsync(admin, password);
            ThrowIfFailed(creationResult, "create the bootstrap admin");

            IdentityResult addRoleResult = await _userManager.AddToRoleAsync(admin, RoleNames.Admin);
            ThrowIfFailed(addRoleResult, "assign the admin role");

            _dbContext.AuditLogs.Add(AuditLog.CreateForSystem(
                "identity.bootstrap_admin_created",
                nameof(ApplicationUser),
                admin.Id.ToString(CultureInfo.InvariantCulture),
                createdAt));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private string GetRequiredSetting(string key) =>
        string.IsNullOrWhiteSpace(_configuration[key])
            ? throw new InvalidOperationException($"Configuration '{key}' is required.")
            : _configuration[key]!;

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to {operation}.");
        }
    }
}
