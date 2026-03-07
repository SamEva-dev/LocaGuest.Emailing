# Emailing

A reusable .NET 9 library for **transactional email delivery** with:

- Persistent **queue** (EF Core)
- **Background worker** (HostedService) for dispatch + retries
- Brevo providers: **API** and **SMTP**
- **2-level tags** (Context tags auto + Use-case tags by caller)
- **Webhook** event ingestion (delivered/open/click/bounce/spam) with idempotency
- Local testing support (SMTP localhost:1025 for Mailpit/Mailhog)

## Quick install

```bash
dotnet add package Emailing
```

## Configuration (appsettings.json)

Minimal (Brevo API):

```json
{
  "Brevo": {
    "Mode": "BREVO_API",
    "ApiKey": "YOUR_API_KEY",
    "ApiBaseUrl": "https://api.brevo.com",
    "SenderName": "AuthGate",
    "SenderEmail": "noreply@authgate.com",
    "Sandbox": false,
    "ContextTagsCsv": "locaguest-prod,svc-authgate",
    "WebhookToken": "YOUR_WEBHOOK_TOKEN",
    "EnableSending": true,
    "MaxRetries": 7
  }
}
```

Local SMTP test (Mailpit/Mailhog):

```json
{
  "Brevo": {
    "Mode": "BREVO_SMTP",
    "SenderName": "AuthGate",
    "SenderEmail": "noreply@authgate.com",
    "SmtpHost": "localhost",
    "SmtpPort": 1025,
    "SmtpUsername": "",
    "SmtpPassword": "",
    "SmtpUseTls": false,
    "ContextTagsCsv": "locaguest-staging,svc-authgate",
    "EnableSending": true,
    "MaxRetries": 5
  }
}
```

### Environment variables (Docker)

You typically inject these in docker-compose:

```yaml
environment:
  Brevo__Mode: ${EMAIL_PROVIDER_PROD} # BREVO_API or BREVO_SMTP
  Brevo__ApiKey: ${BREVO_API_KEY_AUTHGATE_PROD}
  Brevo__SenderName: AuthGate
  Brevo__SenderEmail: noreply@authgate.com
  Brevo__ContextTagsCsv: ${BREVO_CONTEXT_TAGS_AUTHGATE_PROD} # "locaguest-prod,svc-authgate"
  Brevo__WebhookToken: ${BREVO_WEBHOOK_TOKEN_AUTHGATE_PROD}
```

## Register services (Program.cs)

```csharp
builder.Services.AddLocaGuestEmailing(builder.Configuration, opt =>
{
    opt.UsePostgres(builder.Configuration.GetConnectionString("Default")!);
});

builder.Services.AddHostedService<EmailDispatcherWorker>();
```

## Queue an email (2-level tags)

```csharp
await emailing.QueueHtmlAsync(
    toEmail: "user@email.com",
    subject: "Reset your password",
    htmlContent: "<p>...</p>",
    textContent: "Reset your password",
    tags: EmailUseCaseTags.AuthResetPassword,
    cancellationToken);
```

Your final tags in Brevo will be:

- Context (auto): from `Brevo:ContextTagsCsv`
- Use-case: from `EmailUseCaseTags` flags

Example: `locaguest-prod,svc-authgate` + `auth-reset-password`.

## Ingest Brevo webhooks (Minimal API)

```csharp
app.MapBrevoTransactionalWebhook("/api/webhooks/brevo/transactional");
```

Brevo UI (Webhook) authentication method: **Token**  
Token value must match `Brevo:WebhookToken`.

## Retry strategy

- Retries only for **transient** failures (timeouts, 429, 5xx, SMTP connect issues).
- Non-retryable: 400 payload errors, invalid credentials, etc.
- Backoff schedule: 1m, 5m, 15m, 1h, 6h, 24h (configurable in code).

## NuGet packaging

From repo root:

```bash
dotnet pack -c Release
```

The `.nupkg` will be generated in `bin/Release`.

---

## Tag catalog (use-case)

See `EmailUseCaseTags` in the source. You can combine tags using `|`:

```csharp
EmailUseCaseTags.BillingInvoiceSent | EmailUseCaseTags.BillingPaymentReminder
```