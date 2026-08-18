using System.Net;
using System.Net.Mail;
using Firefly.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Firefly.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendDocumentEmailAsync(
            List<string> recipientEmails,
            string subject,
            string body,
            byte[] pdfAttachment,
            string fileName)
        {
            // Updated to "SmtpSettings" to match your appsettings.json
            var smtpHost = _config["SmtpSettings:Host"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_config["SmtpSettings:Port"] ?? "587");
            var senderEmail = _config["SmtpSettings:SenderEmail"] ?? _config["SmtpSettings:Username"] ?? "";
            var senderPassword = _config["SmtpSettings:Password"] ?? "";
            var senderName = _config["SmtpSettings:SenderName"] ?? "Firefly Business Portal";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            foreach (var email in recipientEmails)
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    message.To.Add(email.Trim());
                }
            }

            using var stream = new MemoryStream(pdfAttachment);
            message.Attachments.Add(new Attachment(stream, fileName, "application/pdf"));

            await client.SendMailAsync(message);
        }
    }
}