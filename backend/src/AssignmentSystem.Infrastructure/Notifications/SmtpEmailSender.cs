using System.Net;
using System.Net.Mail;
using AssignmentSystem.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Notifications;

/// <summary>
/// Sends mail over SMTP, or logs it when no host is configured.
///
/// The two behaviours live in one class rather than two registrations because the choice is
/// made from configuration at runtime, not at wiring time: a fresh checkout has no SMTP
/// credentials and must still run, and an evaluator needs to see what <i>would</i> have been
/// sent. The log line is the fallback delivery channel, not a silent no-op.
///
/// Uses the in-box <see cref="SmtpClient"/> deliberately — one less dependency for a feature
/// that sends short plain-text messages, and it speaks STARTTLS to every provider this needs
/// to reach.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            // Treated as delivered: with no mail server there is nothing to retry against, so
            // failing would only fill the outbox with rows no retry could ever clear.
            _logger.LogInformation(
                "Email delivery is not configured — notification logged instead.\n" +
                "  To      : {ToName} <{ToEmail}>\n  Subject : {Subject}\n{Body}",
                message.ToName, message.ToEmail, message.Subject, message.Body);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Timeout = _options.TimeoutSeconds * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        // Anonymous relays (a local MailHog, an internal smarthost) take no credentials —
        // sending empty ones would fail an otherwise working setup.
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.EffectiveFromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
        };
        mail.To.Add(new MailAddress(message.ToEmail, message.ToName));

        await client.SendMailAsync(mail, ct);

        _logger.LogInformation(
            "Notification email sent to {ToEmail} — {Subject}", message.ToEmail, message.Subject);
    }
}
