namespace LocaGuest.Emailing.Providers.Models;

internal sealed class SendResult
{
    public bool Success { get; init; }
    public bool Retryable { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? Error { get; init; }

    public static SendResult Ok(string? providerMessageId) => new()
    {
        Success = true,
        Retryable = false,
        ProviderMessageId = providerMessageId
    };

    public static SendResult Fail(string error, bool retryable) => new()
    {
        Success = false,
        Retryable = retryable,
        Error = error
    };
}
