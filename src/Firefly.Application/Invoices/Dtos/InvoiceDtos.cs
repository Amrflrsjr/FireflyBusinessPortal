namespace Firefly.Application.Invoices.Dtos
{
    public record CreateInvoiceFromQuotationDto(
        int QuotationId,
        DateTime DueDate,
        string Notes
    );

    public record RecordPaymentDto(
        decimal AmountPaid,
        DateTime PaymentDate,
        string PaymentMethod, // "Metrobank", "GCash", "Cash", "Check"
        string ReferenceNumber,
        string Notes
    );

    public record PaymentResponseDto(
        int PaymentId,
        int InvoiceId,
        decimal AmountPaid,
        DateTime PaymentDate,
        string PaymentMethod,
        string ReferenceNumber,
        string Notes,
        DateTime CreatedAt
    );

    public record InvoiceResponseDto(
        int InvoiceId,
        string InvoiceNumber,
        int QuotationId,
        string QuotationNumber,
        int CustomerId,
        string CompanyName,
        string ContactNameSnapshot,
        string ContactEmailSnapshot,
        DateTime IssueDate,
        DateTime DueDate,
        string VATType,
        string Status, // "Unpaid", "PartiallyPaid", "Paid", "Cancelled"
        decimal Subtotal,
        decimal VATAmount,
        decimal TotalAmount,
        decimal TotalPaid,
        decimal BalanceDue,
        string Notes,
        DateTime CreatedAt,
        List<PaymentResponseDto> Payments
    );
}