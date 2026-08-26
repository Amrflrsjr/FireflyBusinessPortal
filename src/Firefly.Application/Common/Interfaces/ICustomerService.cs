using Firefly.Application.Customers.Dtos;
using Firefly.Domain.Entities;

namespace Firefly.Application.Common.Interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<CustomerResponseDto>> GetAllCustomersAsync(string? search = null, string? sortBy = null, bool ascending = true);
        Task<CustomerResponseDto?> GetCustomerByIdAsync(int id);
        Task<CustomerResponseDto> CreateCustomerAsync(CreateCustomerDto dto);
        Task<bool> UpdateCustomerAsync(int id, UpdateCustomerDto dto);
        Task<ContactResponseDto?> AddContactAsync(int customerId, CreateContactDto dto);
        Task<bool> DeleteContactAsync(int customerId, int contactId);
        Task<bool> UpdateContactAsync(int customerId, int contactId, UpdateContactDto dto);
        Task<bool> DeleteCustomerAsync(int id);
        Task<IEnumerable<CustomerResponseDto>> GetDeletedCustomersAsync(string? search);
        Task<bool> RestoreCustomerAsync(int id);
        Task<bool> PermanentlyDeleteCustomerAsync(int id);

    }
}