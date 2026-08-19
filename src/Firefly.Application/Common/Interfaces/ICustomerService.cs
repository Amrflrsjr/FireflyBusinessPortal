using Firefly.Application.Customers.Dtos;
using Firefly.Domain.Entities;

namespace Firefly.Application.Common.Interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<CustomerResponseDto>> GetAllCustomersAsync();
        Task<CustomerResponseDto?> GetCustomerByIdAsync(int id);
        Task<CustomerResponseDto> CreateCustomerAsync(CreateCustomerDto dto);
        Task<bool> UpdateCustomerAsync(int id, UpdateCustomerDto dto);
        Task<ContactResponseDto?> AddContactAsync(int customerId, CreateContactDto dto);
        Task<bool> UpdateContactAsync(int customerId, int contactId, UpdateContactDto dto);
        Task<bool> DeleteContactAsync(int customerId, int contactId);
        Task<bool> DeleteCustomerAsync(int id);
    }
}