using System.Data.Common;

namespace Clinic.Api.Infrastructure.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!isProduction)
        {
            return;
        }

        ValidateAllowedHosts(configuration["AllowedHosts"]);
        ValidateConnectionString(
            configuration.GetConnectionString("ClinicDatabase"),
            configuration.GetValue<bool>("DatabaseSecurity:AllowUntrustedServerCertificate"));
    }

    private static void ValidateAllowedHosts(string? configuredHosts)
    {
        string[] hosts = configuredHosts?.Split(
            ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (hosts.Length == 0 || hosts.Any(host => host.Contains('*')))
        {
            throw new InvalidOperationException(
                "Production requires explicit AllowedHosts values without wildcards.");
        }

        if (hosts.Any(host => host.Contains("://", StringComparison.Ordinal) ||
                              host.Contains('/', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "AllowedHosts must contain host names only, without schemes or paths.");
        }
    }

    private static void ValidateConnectionString(
        string? connectionString,
        bool allowUntrustedServerCertificate)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Production connection string 'ClinicDatabase' is not configured.");
        }

        DbConnectionStringBuilder builder = new();

        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Production connection string 'ClinicDatabase' is invalid.", exception);
        }

        string? dataSource = GetValue(builder, "Server", "Data Source", "Address",
            "Addr", "Network Address");

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new InvalidOperationException(
                "Production connection string must specify a SQL Server host.");
        }

        string serverName = NormalizeServerName(dataSource);
        string[] localServers = [".", "(local)", "localhost", "127.0.0.1", "::1"];

        if (localServers.Contains(serverName, StringComparer.OrdinalIgnoreCase) ||
            serverName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
            dataSource.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production database must use a remote SQL Server host.");
        }

        string? integratedSecurity = GetValue(builder, "Integrated Security",
            "Trusted_Connection");
        if (RepresentsEnabled(integratedSecurity) ||
            string.Equals(integratedSecurity, "SSPI", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production database must not use Windows integrated authentication.");
        }

        string? encrypt = GetValue(builder, "Encrypt");
        if (RepresentsDisabledEncryption(encrypt))
        {
            throw new InvalidOperationException(
                "Production database connection must enable encryption.");
        }

        if (RepresentsEnabled(GetValue(builder, "TrustServerCertificate")) &&
            !allowUntrustedServerCertificate)
        {
            throw new InvalidOperationException(
                "Production database connection must validate the server certificate unless " +
                "DatabaseSecurity:AllowUntrustedServerCertificate is explicitly enabled.");
        }
    }

    private static bool RepresentsEnabled(string? value) =>
        bool.TryParse(value, out bool enabled)
            ? enabled
            : string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static bool RepresentsDisabledEncryption(string? value) =>
        string.Equals(value, bool.FalseString, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "optional", StringComparison.OrdinalIgnoreCase);

    private static string? GetValue(
        DbConnectionStringBuilder builder,
        params string[] keys)
    {
        foreach (string key in keys)
        {
            if (builder.TryGetValue(key, out object? value))
            {
                return value?.ToString();
            }
        }

        return null;
    }

    private static string NormalizeServerName(string dataSource)
    {
        string value = dataSource.Trim();
        if (value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..];
        }

        int separatorIndex = value.IndexOfAny(['\\', ',']);
        return separatorIndex >= 0 ? value[..separatorIndex].Trim() : value;
    }
}
