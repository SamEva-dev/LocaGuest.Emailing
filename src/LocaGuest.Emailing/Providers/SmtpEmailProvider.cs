using System;
using System.Threading;
using System.Threading.Tasks;
using LocaGuest.Emailing.Options;
using LocaGuest.Emailing.Providers.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LocaGuest.Emailing.Providers;

internal sealed class SmtpEmailProvider : IEmailProvider
{
    private readonly BrevoOptions _opt;

    public SmtpEmailProvider(BrevoOptions opt)
    {
        _opt = opt;
    }

    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken ct)
    {
        if (!_opt.EnableSending)
            return SendResult.Ok("disabled");

        if (string.IsNullOrWhiteSpace(_opt.SmtpHost))
            return SendResult.Fail("SMTP host is missing", retryable: false);

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

        try
        {
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
            return SendResult.Ok("smtp-ok");
        }
        catch (Exception ex)
        {
            // SMTP transient errors are generally retryable
            return SendResult.Fail($"SMTP send error: {ex.Message}", retryable: true);
        }
    }
}
