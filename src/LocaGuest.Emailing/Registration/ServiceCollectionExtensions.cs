using System;
using LocaGuest.Emailing.Abstractions;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Persistence;
using LocaGuest.Emailing.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocaGuest.Emailing.Registration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register LocaGuest.Emailing services + EF Core DbContext.
    /// The host can choose the database provider via the builder callback (recommended).
    /// </summary>
    public static IServiceCollection AddLocaGuestEmailing(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<EmailingDbContextBuilder> db)
    {
        services.Configure<BrevoOptions>(configuration.GetSection("Brevo"));
        services.Configure<EmailDispatcherOptions>(configuration.GetSection("EmailDispatcher"));

        // DbContext configuration
        var builder = new EmailingDbContextBuilder(services);
        db(builder);

        services.AddHttpClient("BrevoApi", (sp, c) =>
        {
            var opt = sp.GetRequiredService<IOptions<BrevoOptions>>().Value;
            c.BaseAddress = new Uri(opt.ApiBaseUrl);
        });

        services.AddScoped<IEmailingService, EmailingService>();

        return services;
    }
}

public sealed class EmailingDbContextBuilder
{
    private readonly IServiceCollection _services;

    internal EmailingDbContextBuilder(IServiceCollection services) => _services = services;

    public void UsePostgres(string connectionString)
    {
        _services.AddDbContext<EmailingDbContext>(opt => opt.UseNpgsql(connectionString));
    }

    public void UsePostgres(string connectionString, string? migrationsAssembly)
    {
        _services.AddDbContext<EmailingDbContext>(opt =>
            opt.UseNpgsql(connectionString, npgsql =>
            {
                if (!string.IsNullOrWhiteSpace(migrationsAssembly))
                    npgsql.MigrationsAssembly(migrationsAssembly);
            }));
    }

    public void UseSqlite(string connectionString)
    {
        _services.AddDbContext<EmailingDbContext>(opt => opt.UseSqlite(connectionString));
    }

    public void UseSqlite(string connectionString, string? migrationsAssembly)
    {
        _services.AddDbContext<EmailingDbContext>(opt =>
            opt.UseSqlite(connectionString, sqlite =>
            {
                if (!string.IsNullOrWhiteSpace(migrationsAssembly))
                    sqlite.MigrationsAssembly(migrationsAssembly);
            }));
    }
}
