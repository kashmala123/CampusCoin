using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CampusCoin.Services
{
    public class EmailSettings
    {
        public bool Enabled { get; set; } = true;
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromName { get; set; } = "CampusCoin";
        public string FromEmail { get; set; } = string.Empty;
        public bool UseStartTls { get; set; } = true;
    }

    public interface IEmailService
    {
        Task<(bool Success, string? Error)> SendAsync(string toEmail, string subject, string htmlBody, string? replyTo = null);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<(bool Success, string? Error)> SendAsync(string toEmail, string subject, string htmlBody, string? replyTo = null)
        {
            if (!_settings.Enabled)
            {
                _logger.LogWarning("Email is disabled in configuration. Skipping send.");
                return (false, "Email sending is disabled.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                string.IsNullOrWhiteSpace(_settings.UserName) ||
                string.IsNullOrWhiteSpace(_settings.Password) ||
                string.IsNullOrWhiteSpace(_settings.FromEmail))
            {
                _logger.LogError("Email settings are incomplete.");
                return (false, "Email is not configured correctly.");
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(new MailAddress(toEmail));
                if (!string.IsNullOrWhiteSpace(replyTo))
                {
                    message.ReplyToList.Add(new MailAddress(replyTo));
                }

                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.UseStartTls,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully (recipient redacted). Subject length={Len}", subject?.Length ?? 0);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email (recipient redacted).");
                return (false, "Email delivery failed.");
            }
        }
    }
}
