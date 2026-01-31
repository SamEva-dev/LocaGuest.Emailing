using System;
using System.Linq;
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
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LocaGuest.Emailing.Providers;

internal sealed class BrevoApiEmailProvider : IEmailProvider
{
    private readonly HttpClient _http;
    private readonly BrevoOptions _opt;
    private readonly ILogger<BrevoApiEmailProvider> _logger;

    private sealed class BrevoSendResponse
    {
        public string? MessageId { get; set; }
        public string[]? MessageIds { get; set; }
    }

    public BrevoApiEmailProvider(HttpClient http, BrevoOptions opt, ILogger<BrevoApiEmailProvider> logger)
    {
        _http = http;
        _opt = opt;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        SendResult? finalResult = null;
        string? failureReason = null;
        int? statusCode = null;

        _logger.LogInformation(
            "EmailProvider.BrevoApi.Send ENTER to={ToEmail} templateId={TemplateId} hasHtml={HasHtml} hasText={HasText} attachments={AttachmentsCount} tags={TagsCount} sandbox={Sandbox}",
            request.ToEmail,
            request.TemplateId,
            !string.IsNullOrWhiteSpace(request.HtmlContent),
            !string.IsNullOrWhiteSpace(request.TextContent),
            request.Attachments.Count,
            request.Tags.Count,
            _opt.Sandbox);

        try
        {
            if (!_opt.EnableSending)
            {
                finalResult = SendResult.Ok("disabled");
                return finalResult;
            }

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
            {
                finalResult = SendResult.Fail("Brevo ApiKey is missing", retryable: false);
                return finalResult;
            }

        req.Headers.TryAddWithoutValidation("api-key", _opt.ApiKey);

            using var res = await _http.SendAsync(req, ct);
            statusCode = (int)res.StatusCode;
            var body = await res.Content.ReadAsStringAsync(ct);

            if (res.IsSuccessStatusCode)
            {
                var dto = JsonSerializer.Deserialize<BrevoSendResponse>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var id = dto?.MessageId ?? (dto?.MessageIds?.Length > 0 ? dto?.MessageIds?[0] : null);
                finalResult = SendResult.Ok(id);
                return finalResult;
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

            // Do not log raw body here (can contain payload details). Keep it in DB error only.
            failureReason = $"Brevo API error {(int)res.StatusCode}";
            finalResult = SendResult.Fail($"Brevo API error {(int)res.StatusCode}: {body}", retryable);
            return finalResult;
        }
        catch (TaskCanceledException ex)
        {
            failureReason = "Brevo API timeout";
            finalResult = SendResult.Fail($"Brevo API timeout: {ex.Message}", retryable: true);
            return finalResult;
        }
        catch (Exception ex)
        {
            failureReason = "Brevo API exception";
            finalResult = SendResult.Fail($"Brevo API exception: {ex.Message}", retryable: true);
            return finalResult;
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "EmailProvider.BrevoApi.Send EXIT to={ToEmail} success={Success} providerMessageId={ProviderMessageId} statusCode={StatusCode} retryable={Retryable} reason={Reason} elapsedMs={ElapsedMs}",
                request.ToEmail,
                finalResult?.Success,
                finalResult?.ProviderMessageId,
                statusCode,
                finalResult?.Retryable,
                failureReason,
                sw.ElapsedMilliseconds);
        }
    }
}