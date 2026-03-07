using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LocaGuest.Emailing.Abstractions;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Persistence;
using LocaGuest.Emailing.Persistence.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocaGuest.Emailing.Webhooks;

public static class BrevoWebhookEndpointExtensions
{
    /// <summary>
    /// Minimal API endpoint to ingest Brevo transactional webhooks.
    /// Configure Brevo UI authentication method = Token.
    /// </summary>
    public static IEndpointConventionBuilder MapBrevoTransactionalWebhook(
        this IEndpointRouteBuilder app,
        string pattern = "/api/webhooks/brevo/transactional")
    {
        return app.MapPost(pattern, async (HttpRequest request, IServiceProvider sp) =>
        {
            var opt = sp.GetRequiredService<IOptions<BrevoOptions>>().Value;
            if (!ValidateBearerToken(request, opt.WebhookToken))
                return Results.Unauthorized();

            using var reader = new StreamReader(request.Body);
            var raw = await reader.ReadToEndAsync();

            // Accept single payload or array payload
            if (raw.TrimStart().StartsWith("["))
            {
                var payloads = JsonSerializer.Deserialize<BrevoWebhookPayload[]>(raw);
                if (payloads is not null)
                {
                    foreach (var p in payloads)
                        await HandleOne(p, raw, sp);
                }
            }
            else
            {
                var payload = JsonSerializer.Deserialize<BrevoWebhookPayload>(raw);
                if (payload is not null)
                    await HandleOne(payload, raw, sp);
            }

            return Results.Ok("ok");
        });
    }

    private static async Task HandleOne(BrevoWebhookPayload payload, string rawJson, IServiceProvider sp)
    {
        if (payload.MessageId is null || payload.Event is null || payload.TsEvent is null)
            return;

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailingDbContext>();

        // Insert event (idempotent)
        db.EmailEvents.Add(new EmailEventEntity
        {
            ProviderMessageId = payload.MessageId,
            Event = payload.Event,
            TsEvent = payload.TsEvent.Value,
            Email = payload.Email,
            Reason = payload.Reason,
            Link = payload.Link,
            RawPayloadJson = rawJson
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            // Duplicate event -> ignore
            db.ChangeTracker.Clear();
            return;
        }

        // Update current status
        var msg = await db.EmailMessages.FirstOrDefaultAsync(x => x.ProviderMessageId == payload.MessageId);
        if (msg is null) return;

        ApplyStatusTransition(msg, payload.Event, payload.Reason);
        msg.LastEventAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static void ApplyStatusTransition(EmailMessageEntity msg, string brevoEvent, string? reason)
    {
        var key = NormalizeEvent(brevoEvent);

        switch (key)
        {
            case "request":
            case "sent":
                if (msg.Status < EmailStatus.Sent)
                    msg.Status = EmailStatus.Sent;
                break;

            case "delivered":
                if (msg.Status != EmailStatus.SpamComplaint)
                    msg.Status = EmailStatus.Delivered;
                break;

            case "deferred":
            case "softbounce":
                if (msg.Status < EmailStatus.Delivered)
                    msg.Status = EmailStatus.Deferred;
                msg.LastError = reason ?? brevoEvent;
                break;

            case "hardbounce":
            case "blocked":
            case "invalidemail":
            case "invalid":
            case "error":
                msg.Status = EmailStatus.Failed;
                msg.LastError = reason ?? brevoEvent;
                break;

            case "spam":
                msg.Status = EmailStatus.SpamComplaint;
                msg.LastError = "spam";
                break;

            // opened / click: keep as events only
            case "opened":
            case "uniqueopened":
            case "click":
                break;
        }
    }

    private static string NormalizeEvent(string e)
    {
        var s = new string(e.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        return s;
    }

    private static bool ValidateBearerToken(HttpRequest request, string? expectedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
            return false;

        var auth = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var received = auth["Bearer ".Length..].Trim();

        var ba = Encoding.UTF8.GetBytes(received);
        var bb = Encoding.UTF8.GetBytes(expectedToken);

        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private sealed class BrevoWebhookPayload
    {
        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("ts_event")]
        public long? TsEvent { get; set; }

        [JsonPropertyName("message-id")]
        public string? MessageId { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }
    }
}
