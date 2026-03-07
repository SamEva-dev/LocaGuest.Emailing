using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LocaGuest.Emailing.Persistence;

public sealed class EmailingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<EmailingDbContext>
{
    public EmailingDbContext CreateDbContext(string[] args)
    {
        // Required for `dotnet ef` when no host app is available.
        // Prefer EMAILING_DATABASE_URL, fallback to DATABASE_URL, then to a local default.
        var databaseUrl = Environment.GetEnvironmentVariable("EMAILING_DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        var connectionString = !string.IsNullOrWhiteSpace(databaseUrl)
            ? ToNpgsqlConnectionString(databaseUrl)
            : "Host=localhost;Port=5432;Database=locaguest;Username=postgres;Password=postgres;";

        var opt = new DbContextOptionsBuilder<EmailingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EmailingDbContext(opt);
    }

    private static string ToNpgsqlConnectionString(string databaseUrl)
    {
        // Accept either a full Npgsql connection string, or a postgres:// URI.
        if (databaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return databaseUrl;

        var uri = new Uri(databaseUrl.Split('?')[0]);
        var userInfo = uri.UserInfo.Split(':');

        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};";
    }
}
