using System;

namespace LocaGuest.Emailing.Persistence.Entities;

public sealed class EmailAttachmentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmailMessageId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public EmailMessageEntity? EmailMessage { get; set; }
}
