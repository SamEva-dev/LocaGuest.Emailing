namespace LocaGuest.Emailing.Options;

/// <summary>
/// Binds to configuration section "Brevo".
/// Supports both API and SMTP modes for local/prod.
/// </summary>
public sealed class BrevoOptions
{
    public string Mode { get; set; } = "BREVO_API";

    // API
    public string ApiKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.brevo.com";
    public bool Sandbox { get; set; } = false;

    // Sender
    public string SenderName { get; set; } = "LocaGuest";
    public string SenderEmail { get; set; } = "no-reply@locaguest.com";

    // SMTP (used when Mode == BREVO_SMTP)
    public string SmtpHost { get; set; } = "smtp-relay.brevo.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool SmtpUseTls { get; set; } = true;

    // Webhook token (Brevo UI: "Token" auth method)
    public string WebhookToken { get; set; } = string.Empty;

    // 2-level tags: context tags (level 1) provided by config
    public string ContextTagsCsv { get; set; } = string.Empty;

    // Controls
    public bool EnableSending { get; set; } = true;
    public int MaxRetries { get; set; } = 7;
}
