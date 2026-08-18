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
            var smtpHost = _config["Smtp:Host"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
            var senderEmail = _config["Smtp:Email"] ?? "fireflycraftscebu@gmail.com";
            var senderPassword = _config["Smtp:Password"] ?? "";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, "Firefly Crafts PH"),
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