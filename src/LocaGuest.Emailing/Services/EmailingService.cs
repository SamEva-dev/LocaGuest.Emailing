using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocaGuest.Emailing.Abstractions;
using LocaGuest.Emailing.Internal;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Persistence;
using LocaGuest.Emailing.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocaGuest.Emailing.Services;

internal sealed class EmailingService : IEmailingService
{
    private readonly EmailingDbContext _db;
    private readonly BrevoOptions _brevo;
    private readonly ILogger<EmailingService> _logger;

    public EmailingService(EmailingDbContext db, IOptions<BrevoOptions> brevo, ILogger<EmailingService> logger)
    {
        _db = db;
        _brevo = brevo.Value;
        _logger = logger;
    }

    public async Task<Guid> QueueHtmlAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string? textContent,
        System.Collections.Generic.IReadOnlyCollection<EmailAttachment>? attachments,
        EmailUseCaseTags tags,
        CancellationToken cancellationToken)
    {
        Guid? emailId = null;
        string? failureReason = null;

        _logger.LogInformation(
            "EmailingService.QueueHtml ENTER to={ToEmail} subject={Subject} attachments={AttachmentsCount} tags={Tags}",
            toEmail,
            subject,
            attachments?.Count ?? 0,
            tags);

        try
        {
            var useCaseTags = TagCatalog.ResolveUseCaseTags(tags);
            var contextTags = TagCatalog.ParseCsv(_brevo.ContextTagsCsv);

        var entity = new EmailMessageEntity
        {
            ToEmail = toEmail,
            Subject = subject,
            HtmlContent = htmlContent,
            TextContent = textContent,
            ContextTagsCsv = string.Join(',', contextTags),
            UseCaseTagsCsv = string.Join(',', useCaseTags),
            Status = EmailStatus.Queued,
            NextAttemptAtUtc = DateTime.UtcNow
        };

        if (attachments is not null)
        {
            foreach (var a in attachments)
            {
                entity.Attachments.Add(new EmailAttachmentEntity
                {
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    Content = a.Content
                });
            }
        }

        _db.EmailMessages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
            emailId = entity.Id;
            return entity.Id;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            throw;
        }
        finally
        {
            _logger.LogInformation(
                "EmailingService.QueueHtml EXIT to={ToEmail} emailId={EmailId} success={Success} reason={Reason}",
                toEmail,
                emailId,
                emailId.HasValue,
                failureReason);
        }
    }

    public async Task<Guid> QueueTemplateAsync(
        string toEmail,
        int templateId,
        object templateParams,
        System.Collections.Generic.IReadOnlyCollection<EmailAttachment>? attachments,
        EmailUseCaseTags tags,
        CancellationToken cancellationToken)
    {
        Guid? emailId = null;
        string? failureReason = null;

        _logger.LogInformation(
            "EmailingService.QueueTemplate ENTER to={ToEmail} templateId={TemplateId} attachments={AttachmentsCount} tags={Tags}",
            toEmail,
            templateId,
            attachments?.Count ?? 0,
            tags);

        try
        {
            var useCaseTags = TagCatalog.ResolveUseCaseTags(tags);
            var contextTags = TagCatalog.ParseCsv(_brevo.ContextTagsCsv);

        var entity = new EmailMessageEntity
        {
            ToEmail = toEmail,
            Subject = $"Template:{templateId}",
            TemplateId = templateId,
            TemplateParamsJson = JsonSerializer.Serialize(templateParams),
            ContextTagsCsv = string.Join(',', contextTags),
            UseCaseTagsCsv = string.Join(',', useCaseTags),
            Status = EmailStatus.Queued,
            NextAttemptAtUtc = DateTime.UtcNow
        };

        if (attachments is not null)
        {
            foreach (var a in attachments)
            {
                entity.Attachments.Add(new EmailAttachmentEntity
                {
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    Content = a.Content
                });
            }
        }

        _db.EmailMessages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
            emailId = entity.Id;
            return entity.Id;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            throw;
        }
        finally
        {
            _logger.LogInformation(
                "EmailingService.QueueTemplate EXIT to={ToEmail} emailId={EmailId} success={Success} reason={Reason}",
                toEmail,
                emailId,
                emailId.HasValue,
                failureReason);
        }
    }
}
