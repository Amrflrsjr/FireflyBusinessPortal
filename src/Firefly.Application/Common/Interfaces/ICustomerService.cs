using Firefly.Application.Customers.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<CustomerResponseDto>> GetAllCustomersAsync();
        Task<CustomerResponseDto?> GetCustomerByIdAsync(int id);
        Task<CustomerResponseDto> CreateCustomerAsync(CreateCustomerDto dto);
        Task<bool> UpdateCustomerAsync(int id, UpdateCustomerDto dto);
        Task<ContactResponseDto?> AddContactAsync(int customerId, CreateContactDto dto);
    }
}