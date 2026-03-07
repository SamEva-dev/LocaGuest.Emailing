using System.Threading;
using System.Threading.Tasks;

namespace LocaGuest.Emailing.Abstractions;

/// <summary>
/// Public entry point for your services to queue emails with use-case tags.
/// Context tags are added automatically from configuration.
/// </summary>
public interface IEmailingService
{
    Task<System.Guid> QueueHtmlAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string? textContent,
        System.Collections.Generic.IReadOnlyCollection<EmailAttachment>? attachments,
        EmailUseCaseTags tags,
        CancellationToken cancellationToken);

    Task<System.Guid> QueueTemplateAsync(
        string toEmail,
        int templateId,
        object templateParams,
        System.Collections.Generic.IReadOnlyCollection<EmailAttachment>? attachments,
        EmailUseCaseTags tags,
        CancellationToken cancellationToken);
}
