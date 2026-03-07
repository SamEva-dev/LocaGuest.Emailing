namespace LocaGuest.Emailing.Providers.Models;

internal sealed class SendAttachment
{
    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = "application/octet-stream";

    public byte[] Content { get; init; } = System.Array.Empty<byte>();
}
