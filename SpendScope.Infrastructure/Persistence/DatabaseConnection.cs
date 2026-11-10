using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SpendScope.Infrastructure.Persistence;

/// <summary>
/// Resolves the PostgreSQL connection string. <c>ConnectionStrings:Default</c> wins; otherwise a
/// <c>DATABASE_URL</c> in URL form (<c>postgresql://user:pass@host:5432/db</c>, as Render and most
/// platforms provide it) is converted to the key/value form Npgsql expects.
/// </summary>
public static class DatabaseConnection
{
    public const string UrlVariable = "DATABASE_URL";

    public static string? Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var url = configuration[UrlVariable];
        return string.IsNullOrWhiteSpace(url) ? null : FromUrl(url);
    }

    public static string FromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
        {
            throw new InvalidOperationException($"{UrlVariable} must look like postgresql://user:password@host:port/database.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,

            // Hosted PostgreSQL does not use Kerberos; skip the GSS probe, which otherwise logs
            // "Cannot load library libgssapi_krb5.so.2" in slim container images.
            GssEncryptionMode = GssEncryptionMode.Disable,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && parts.Length == 2
                && Enum.TryParse<SslMode>(Uri.UnescapeDataString(parts[1]), ignoreCase: true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }

        return builder.ConnectionString;
    }
}
