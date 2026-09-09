using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Api.IntegrationTests.Identity;

public sealed class IdentitySqlServerFixture : IAsyncLifetime
{
    public const string InitialAdminPassword = "InitialAdmin1";
    public const string UpdatedAdminPassword = "UpdatedAdmin2";

    private readonly string _databaseName = $"ClinicIdentityTests_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program>? _factory;
    private bool _started;

    public AdjustableTimeProvider Clock { get; } = new();

    public IServiceProvider Services => Factory.Services;

    public Task InitializeAsync()
    {
        string connectionString =
            $"Server=localhost;Database={_databaseName};Integrated Security=True;" +
            "Encrypt=False;TrustServerCertificate=True";
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:ClinicDatabase"] = connectionString,
            ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "true",
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:UserName"] = "integration.admin",
            ["BootstrapAdmin:Password"] = InitialAdminPassword,
            ["BootstrapAdmin:FullName"] = "مدير اختبار التكامل",
            ["BootstrapAdmin:PhoneNumber"] = "01099999999",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
        };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ClinicDbContext>>();
                services.RemoveAll<ClinicDbContext>();
                services.AddDbContext<ClinicDbContext>(options =>
                    options.UseSqlServer(
                        connectionString,
                        sqlOptions => sqlOptions.EnableRetryOnFailure()));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        });

        _ = _factory.Services;
        _started = true;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is null)
        {
            return;
        }

        if (_started)
        {
            await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
            ClinicDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        await _factory.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private WebApplicationFactory<Program> Factory => _factory ??
        throw new InvalidOperationException("The SQL Server fixture is not initialized.");
}

public sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset? _fixedUtcNow;
    private readonly Queue<DateTimeOffset> _utcNowSequence = new();

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNowSequence.TryDequeue(out DateTimeOffset value)
            ? value
            : _fixedUtcNow ?? DateTimeOffset.UtcNow;
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        _utcNowSequence.Clear();
        _fixedUtcNow = value.ToUniversalTime();
    }

    public void SetUtcNowSequence(params DateTimeOffset[] values)
    {
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);
        _utcNowSequence.Clear();
        foreach (DateTimeOffset value in values)
        {
            _utcNowSequence.Enqueue(value.ToUniversalTime());
        }

        _fixedUtcNow = values[^1].ToUniversalTime();
    }
}
