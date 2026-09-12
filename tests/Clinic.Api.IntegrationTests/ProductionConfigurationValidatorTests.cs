using Clinic.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Clinic.Api.IntegrationTests;

public sealed class ProductionConfigurationValidatorTests
{
    private const string RemoteConnection =
        "Server=sql.monsterasp.net;Database=ClinicDb;User Id=clinic;" +
        "Password=secret;Encrypt=True;TrustServerCertificate=False";

    [Fact]
    public void DevelopmentAllowsLocalDefaults()
    {
        IConfiguration configuration = BuildConfiguration(
            "Server=localhost;Database=ClinicDb;Integrated Security=True;Encrypt=False",
            "*");

        ProductionConfigurationValidator.Validate(configuration, isProduction: false);
    }

    [Fact]
    public void ProductionAcceptsExplicitRemoteConfiguration()
    {
        IConfiguration configuration = BuildConfiguration(
            RemoteConnection,
            "clinic-api.runasp.net");

        ProductionConfigurationValidator.Validate(configuration, isProduction: true);
    }

    [Fact]
    public void ProductionRejectsWildcardAllowedHosts()
    {
        IConfiguration configuration = BuildConfiguration(RemoteConnection, "*");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, isProduction: true));

        Assert.Contains("AllowedHosts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRejectsLocalDatabase()
    {
        IConfiguration configuration = BuildConfiguration(
            "Server=localhost;Database=ClinicDb;User Id=clinic;Password=secret;Encrypt=True",
            "clinic-api.runasp.net");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, isProduction: true));

        Assert.Contains("remote SQL Server", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRejectsUnencryptedDatabaseConnection()
    {
        IConfiguration configuration = BuildConfiguration(
            "Server=sql.monsterasp.net;Database=ClinicDb;User Id=clinic;" +
            "Password=secret;Encrypt=False",
            "clinic-api.runasp.net");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, isProduction: true));

        Assert.Contains("encryption", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRejectsUntrustedDatabaseCertificate()
    {
        IConfiguration configuration = BuildConfiguration(
            "Server=sql.monsterasp.net;Database=ClinicDb;User Id=clinic;" +
            "Password=secret;Encrypt=True;TrustServerCertificate=True",
            "clinic-api.runasp.net");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, isProduction: true));

        Assert.Contains("certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionAllowsUntrustedDatabaseCertificateWhenExplicitlyApproved()
    {
        IConfiguration configuration = BuildConfiguration(
            "Server=sql.monsterasp.net;Database=ClinicDb;User Id=clinic;" +
            "Password=secret;Encrypt=True;TrustServerCertificate=True",
            "clinic-api.runasp.net",
            allowUntrustedServerCertificate: true);

        ProductionConfigurationValidator.Validate(configuration, isProduction: true);
    }

    [Fact]
    public void ProductionRejectsSspiDatabaseAuthentication()
    {
        IConfiguration configuration = BuildConfiguration(
            "Server=sql.monsterasp.net;Database=ClinicDb;Integrated Security=SSPI;" +
            "Encrypt=True",
            "clinic-api.runasp.net");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, isProduction: true));

        Assert.Contains("integrated authentication", exception.Message,
            StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(
        string connectionString,
        string allowedHosts,
        bool allowUntrustedServerCertificate = false)
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:ClinicDatabase"] = connectionString,
            ["AllowedHosts"] = allowedHosts
        };

        if (allowUntrustedServerCertificate)
        {
            values["DatabaseSecurity:AllowUntrustedServerCertificate"] = "true";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
