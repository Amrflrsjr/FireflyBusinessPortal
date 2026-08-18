namespace Firefly.Application.Quotations.Dtos
{
    public record DocumentEmailPreviewDto(
        int DocumentId,
        string DocumentNumber,
        List<string> Recipients,
        string Subject,
        string Body,
        string CustomerName,
        string ContactName,
        decimal TotalAmount,
        string AttachmentFileName // 9th parameter added here
    );

    public record SendEmailRequestDto(
        List<string> RecipientEmails,
        string Subject,
        string Body
    );
}