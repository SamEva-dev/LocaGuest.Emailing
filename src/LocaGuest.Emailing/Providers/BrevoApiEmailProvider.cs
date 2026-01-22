using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Providers.Models;
using LocaGuest.Emailing.Internal;
using System.Net.Http.Json;

namespace LocaGuest.Emailing.Providers;

internal sealed class BrevoApiEmailProvider : IEmailProvider
{
    private readonly HttpClient _http;
    private readonly BrevoOptions _opt;

    private sealed class BrevoSendResponse
    {
        public string? MessageId { get; set; }
        public string[]? MessageIds { get; set; }
    }

    public BrevoApiEmailProvider(HttpClient http, BrevoOptions opt)
    {
        _http = http;
        _opt = opt;
    }

    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken ct)
    {
        if (!_opt.EnableSending)
            return SendResult.Ok("disabled");

        // Brevo rejects "tags" if blank -> only include when non-empty
        var tags = request.Tags.Count > 0 ? request.Tags : null;

        // Brevo expects base64 attachments
        object? attachments = request.Attachments.Count > 0
            ? request.Attachments.Select(a => new
            {
                name = a.FileName,
                content = Convert.ToBase64String(a.Content)
            }).ToList()
            : null;

        object payload = request.TemplateId is not null
            ? new
            {
                sender = new { name = _opt.SenderName, email = _opt.SenderEmail },
                to = new[] { new { email = request.ToEmail, name = request.ToName } },
                templateId = request.TemplateId,
                @params = request.TemplateParams,
                tags = tags,
                headers = _opt.Sandbox ? new System.Collections.Generic.Dictionary<string, string> { ["X-Sib-Sandbox"] = "drop" } : null
            }
            : new
            {
                sender = new { name = _opt.SenderName, email = _opt.SenderEmail },
                to = new[] { new { email = request.ToEmail, name = request.ToName } },
                subject = request.Subject,
                htmlContent = request.HtmlContent,
                textContent = request.TextContent,
                tags = tags,
                attachment = attachments,
                headers = _opt.Sandbox ? new System.Collections.Generic.Dictionary<string, string> { ["X-Sib-Sandbox"] = "drop" } : null
            };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v3/smtp/email")
        {
            Content = JsonContent.Create(payload)
        };

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (string.IsNullOrWhiteSpace(_opt.ApiKey))
            return SendResult.Fail("Brevo ApiKey is missing", retryable: false);

        req.Headers.TryAddWithoutValidation("api-key", _opt.ApiKey);

        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (res.IsSuccessStatusCode)
            {
                var dto = JsonSerializer.Deserialize<BrevoSendResponse>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var id = dto?.MessageId ?? (dto?.MessageIds?.Length > 0 ? dto?.MessageIds?[0] : null);
                return SendResult.Ok(id);
            }

            // Retryable classification (best practice)
            var retryable = res.StatusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError;

            // 400 errors are usually payload errors -> not retryable
            if ((int)res.StatusCode >= 400 && (int)res.StatusCode < 500 && res.StatusCode != HttpStatusCode.TooManyRequests)
                retryable = false;

            return SendResult.Fail($"Brevo API error {(int)res.StatusCode}: {body}", retryable);
        }
        catch (TaskCanceledException ex)
        {
            return SendResult.Fail($"Brevo API timeout: {ex.Message}", retryable: true);
        }
        catch (Exception ex)
        {
            return SendResult.Fail($"Brevo API exception: {ex.Message}", retryable: true);
        }
    }
}