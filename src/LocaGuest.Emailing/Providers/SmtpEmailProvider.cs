using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Providers.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace LocaGuest.Emailing.Providers;

internal sealed class SmtpEmailProvider : IEmailProvider
{
    private readonly BrevoOptions _opt;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(BrevoOptions opt, ILogger<SmtpEmailProvider> logger)
    {
        _opt = opt;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        SendResult? finalResult = null;
        string? failureReason = null;

        _logger.LogInformation(
            "EmailProvider.Smtp.Send ENTER to={ToEmail} hasHtml={HasHtml} hasText={HasText} attachments={AttachmentsCount}",
            request.ToEmail,
            !string.IsNullOrWhiteSpace(request.HtmlContent),
            !string.IsNullOrWhiteSpace(request.TextContent),
            request.Attachments.Count);

        try
        {
            if (!_opt.EnableSending)
            {
                finalResult = SendResult.Ok("disabled");
                return finalResult;
            }

            if (string.IsNullOrWhiteSpace(_opt.SmtpHost))
            {
                failureReason = "SMTP host is missing";
                finalResult = SendResult.Fail("SMTP host is missing", retryable: false);
                return finalResult;
            }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.SenderName, _opt.SenderEmail));
        message.To.Add(new MailboxAddress(request.ToName ?? request.ToEmail, request.ToEmail));
        message.Subject = request.Subject;

        var builder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(request.HtmlContent))
            builder.HtmlBody = request.HtmlContent;
        if (!string.IsNullOrWhiteSpace(request.TextContent))
            builder.TextBody = request.TextContent;

        if (request.Attachments.Count > 0)
        {
            foreach (var a in request.Attachments)
            {
                builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
            }
        }

        message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();

            // TLS control
            var socketOption = _opt.SmtpUseTls
                ? SecureSocketOptions.StartTlsWhenAvailable
                : SecureSocketOptions.None;

            await client.ConnectAsync(_opt.SmtpHost, _opt.SmtpPort, socketOption, ct);

            if (!string.IsNullOrWhiteSpace(_opt.SmtpUsername))
            {
                await client.AuthenticateAsync(_opt.SmtpUsername, _opt.SmtpPassword, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            // SMTP doesn't give a provider message-id; we use a local marker
            finalResult = SendResult.Ok("smtp-ok");
            return finalResult;
        }
        catch (Exception ex)
        {
            // SMTP transient errors are generally retryable
            failureReason = "SMTP send error";
            finalResult = SendResult.Fail($"SMTP send error: {ex.Message}", retryable: true);
            return finalResult;
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "EmailProvider.Smtp.Send EXIT to={ToEmail} success={Success} retryable={Retryable} reason={Reason} elapsedMs={ElapsedMs}",
                request.ToEmail,
                finalResult?.Success,
                finalResult?.Retryable,
                failureReason,
                sw.ElapsedMilliseconds);
        }
    }
}
