using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocaGuest.Emailing.Abstractions;
using LocaGuest.Emailing.Internal;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Persistence;
using LocaGuest.Emailing.Providers;
using LocaGuest.Emailing.Providers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocaGuest.Emailing.Workers;

/// <summary>
/// Background worker that dispatches queued emails using configured Brevo provider (API or SMTP).
/// </summary>
public sealed class EmailDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDispatcherWorker> _logger;
    private readonly IOptions<EmailDispatcherOptions> _workerOptions;

    public EmailDispatcherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailDispatcherWorker> logger,
        IOptions<EmailDispatcherOptions> workerOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _workerOptions = workerOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatch(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email dispatcher crashed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_workerOptions.Value.PollSeconds), stoppingToken);
        }
    }

    private async Task DispatchBatch(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailingDbContext>();
        var brevo = scope.ServiceProvider.GetRequiredService<IOptions<BrevoOptions>>().Value;

        var now = DateTime.UtcNow;
        var lockUntil = now.AddMinutes(_workerOptions.Value.LockMinutes);

        var batch = await db.EmailMessages
            .Include(x => x.Attachments)
            .Where(x =>
                (x.Status == EmailStatus.Queued || x.Status == EmailStatus.Failed) && // Failed here = retry-scheduled by NextAttemptAtUtc
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(_workerOptions.Value.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        // Select provider
        var provider = CreateProvider(scope.ServiceProvider, brevo);

        // Lock
        foreach (var msg in batch)
        {
            msg.Status = EmailStatus.Sending;
            msg.LockedUntilUtc = lockUntil;
        }
        await db.SaveChangesAsync(ct);

        foreach (var msg in batch)
        {
            try
            {
                var contextTags = TagCatalog.ParseCsv(msg.ContextTagsCsv);
                var useCaseTags = TagCatalog.ParseCsv(msg.UseCaseTagsCsv);
                var allTags = contextTags.Concat(useCaseTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                object? templateParams = null;
                if (!string.IsNullOrWhiteSpace(msg.TemplateParamsJson))
                    templateParams = JsonSerializer.Deserialize<object>(msg.TemplateParamsJson);

                var req = new SendRequest
                {
                    ToEmail = msg.ToEmail,
                    ToName = msg.ToName,
                    Subject = msg.Subject,
                    HtmlContent = msg.HtmlContent,
                    TextContent = msg.TextContent,
                    TemplateId = msg.TemplateId,
                    TemplateParams = templateParams,
                    Attachments = msg.Attachments.Select(a => new SendAttachment
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        Content = a.Content
                    }).ToList(),
                    Tags = allTags
                };

                msg.AttemptCount++;

                var result = await provider.SendAsync(req, ct);

                if (result.Success)
                {
                    msg.ProviderMessageId = result.ProviderMessageId;
                    msg.Status = EmailStatus.Sent;
                    msg.LockedUntilUtc = null;
                    msg.NextAttemptAtUtc = null;
                    msg.LastError = null;
                    msg.LastEventAtUtc = DateTime.UtcNow;
                }
                else
                {
                    msg.LastError = result.Error;
                    msg.LockedUntilUtc = null;

                    if (!result.Retryable || msg.AttemptCount >= brevo.MaxRetries)
                    {
                        // Permanent failure
                        msg.Status = EmailStatus.Failed;
                        msg.NextAttemptAtUtc = null;
                    }
                    else
                    {
                        // Schedule retry (reuse Failed + NextAttemptAtUtc to avoid extra enum)
                        msg.Status = EmailStatus.Failed;
                        msg.NextAttemptAtUtc = DateTime.UtcNow.Add(RetryPolicy.ComputeDelay(msg.AttemptCount));
                    }
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                msg.Status = EmailStatus.Failed;
                msg.LockedUntilUtc = null;
                msg.LastError = ex.Message;
                msg.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(5);
                await db.SaveChangesAsync(ct);

                _logger.LogWarning(ex, "Email dispatch failed for {EmailId}", msg.Id);
            }
        }
    }

    private static IEmailProvider CreateProvider(IServiceProvider sp, BrevoOptions brevo)
    {
        if (string.Equals(brevo.Mode, "BREVO_SMTP", StringComparison.OrdinalIgnoreCase))
        {
            return new SmtpEmailProvider(brevo);
        }

        // Default: API
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var http = httpClientFactory.CreateClient("BrevoApi");
        return new BrevoApiEmailProvider(http, brevo);
    }
}
