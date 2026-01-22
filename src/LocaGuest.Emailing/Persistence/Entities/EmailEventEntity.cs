using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocaGuest.Emailing.Persistence.Entities;

public sealed class EmailEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? EmailMessageId { get; set; }
    public EmailMessageEntity? EmailMessage { get; set; }

    public string ProviderMessageId { get; set; } = default!;
    public string Event { get; set; } = default!;
    public long TsEvent { get; set; }

    public string? Email { get; set; }
    public string? Reason { get; set; }
    public string? Link { get; set; }

    [Column(TypeName = "text")]
    public string RawPayloadJson { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
