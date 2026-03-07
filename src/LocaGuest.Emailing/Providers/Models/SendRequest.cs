using System.Collections.Generic;

namespace LocaGuest.Emailing.Providers.Models;

internal sealed class SendRequest
{
    public string ToEmail { get; init; } = default!;
    public string? ToName { get; init; }

    public string Subject { get; init; } = default!;

    public string? HtmlContent { get; init; }
    public string? TextContent { get; init; }

    public int? TemplateId { get; init; }
    public object? TemplateParams { get; init; }

    public List<SendAttachment> Attachments { get; init; } = new();

    public List<string> Tags { get; init; } = new();
}
