namespace Firefly.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendDocumentEmailAsync(
            List<string> recipientEmails,
            string subject,
            string body,
            byte[] pdfAttachment,
            string fileName
        );
    }
}