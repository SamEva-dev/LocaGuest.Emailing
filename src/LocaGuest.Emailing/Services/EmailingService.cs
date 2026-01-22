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
using Microsoft.Extensions.Options;

namespace LocaGuest.Emailing.Services;

internal sealed class EmailingService : IEmailingService
{
    private readonly EmailingDbContext _db;
    private readonly BrevoOptions _brevo;

    public EmailingService(EmailingDbContext db, IOptions<BrevoOptions> brevo)
    {
        _db = db;
        _brevo = brevo.Value;
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
        return entity.Id;
    }

    public async Task<Guid> QueueTemplateAsync(
        string toEmail,
        int templateId,
        object templateParams,
        System.Collections.Generic.IReadOnlyCollection<EmailAttachment>? attachments,
        EmailUseCaseTags tags,
        CancellationToken cancellationToken)
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
        return entity.Id;
    }
}
