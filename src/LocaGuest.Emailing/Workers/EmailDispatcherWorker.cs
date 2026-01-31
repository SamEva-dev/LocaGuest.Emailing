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
        var startedAt = DateTime.UtcNow;
        int? batchCount = null;
        string? providerMode = null;

        _logger.LogInformation("EmailDispatcher.DispatchBatch ENTER");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailingDbContext>();
        var brevo = scope.ServiceProvider.GetRequiredService<IOptions<BrevoOptions>>().Value;
        providerMode = brevo.Mode;

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

        batchCount = batch.Count;

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
            var msgStartedAt = DateTime.UtcNow;
            bool? success = null;
            bool? retryable = null;
            string? providerMessageId = null;
            string? error = null;

            _logger.LogInformation(
                "EmailDispatcher.ProcessMessage ENTER emailId={EmailId} to={ToEmail} templateId={TemplateId} attempt={Attempt} attachments={AttachmentsCount}",
                msg.Id,
                msg.ToEmail,
                msg.TemplateId,
                msg.AttemptCount,
                msg.Attachments.Count);

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

                success = result.Success;
                retryable = result.Retryable;
                providerMessageId = result.ProviderMessageId;
                error = result.Error;

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

                success = false;
                retryable = true;
                error = ex.Message;

                _logger.LogWarning(ex, "Email dispatch failed for {EmailId}", msg.Id);
            }
            finally
            {
                var elapsedMs = (long)(DateTime.UtcNow - msgStartedAt).TotalMilliseconds;
                _logger.LogInformation(
                    "EmailDispatcher.ProcessMessage EXIT emailId={EmailId} success={Success} retryable={Retryable} providerMessageId={ProviderMessageId} error={Error} elapsedMs={ElapsedMs}",
                    msg.Id,
                    success,
                    retryable,
                    providerMessageId,
                    error,
                    elapsedMs);
            }
        }

        _logger.LogInformation(
            "EmailDispatcher.DispatchBatch EXIT batchCount={BatchCount} providerMode={ProviderMode} elapsedMs={ElapsedMs}",
            batchCount,
            providerMode,
            (long)(DateTime.UtcNow - startedAt).TotalMilliseconds);
    }

    private static IEmailProvider CreateProvider(IServiceProvider sp, BrevoOptions brevo)
    {
        if (string.Equals(brevo.Mode, "BREVO_SMTP", StringComparison.OrdinalIgnoreCase))
        {
            var logger = sp.GetRequiredService<ILogger<SmtpEmailProvider>>();
            return new SmtpEmailProvider(brevo, logger);
        }

        // Default: API
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var http = httpClientFactory.CreateClient("BrevoApi");
        var loggerApi = sp.GetRequiredService<ILogger<BrevoApiEmailProvider>>();
        return new BrevoApiEmailProvider(http, brevo, loggerApi);
    }
}
