namespace Firefly.Application.Customers.Dtos
{
    public record CreateCustomerDto(
        string CustomerType, // "Business" or "Individual"
        string CompanyName,
        string CompanyAddress,
        string TIN,
        string Notes,
        List<CreateContactDto>? InitialContacts
    );

    public record UpdateCustomerDto(
        string CompanyName,
        string CompanyAddress,
        string TIN,
        string Notes
    );

    public record CustomerResponseDto(
        int CustomerId,
        string CustomerType, // "Business" or "Individual"
        string CompanyName,
        string CompanyAddress,
        string TIN,
        string Notes,
        DateTime CreatedAt,
        List<ContactResponseDto> Contacts
    );

    public record CreateContactDto(
        string Name,
        string Department,
        string Position,
        string Email,
        string Phone,
        bool IsPrimary
    );
    public record UpdateContactDto(
        string Name,
        string Department,
        string Position,
        string Email,
        string Phone,
        bool IsPrimary,
        bool IsActive
    );
    public record ContactResponseDto(
        int ContactId,
        int CustomerId,
        string Name,
        string Department,
        string Position,
        string Email,
        string Phone,
        bool IsPrimary,
        bool IsActive
    );
}