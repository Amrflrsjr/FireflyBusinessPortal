using Firefly.Application.Common.Interfaces;
using Firefly.Application.Customers.Dtos;
using Firefly.Domain.Entities;
using Firefly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Infrastructure.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;

        public CustomerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CustomerResponseDto>> GetAllCustomersAsync()
        {
            return await _context.Customers
                .Include(c => c.Contacts)
                .Select(c => new CustomerResponseDto(
                    c.CustomerId,
                    c.CompanyName,
                    c.CompanyAddress,
                    c.TIN,
                    c.Notes,
                    c.CreatedAt,
                    c.Contacts.Select(ct => new ContactResponseDto(
                        ct.ContactId,
                        ct.CustomerId,
                        ct.Name,
                        ct.Department,
                        ct.Position,
                        ct.Email,
                        ct.Phone,
                        ct.IsPrimary,
                        ct.IsActive
                    )).ToList()
                ))
                .ToListAsync();
        }

        public async Task<CustomerResponseDto?> GetCustomerByIdAsync(int id)
        {
            var c = await _context.Customers
                .Include(x => x.Contacts)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (c == null) return null;

            return new CustomerResponseDto(
                c.CustomerId,
                c.CompanyName,
                c.CompanyAddress,
                c.TIN,
                c.Notes,
                c.CreatedAt,
                c.Contacts.Select(ct => new ContactResponseDto(
                    ct.ContactId,
                    ct.CustomerId,
                    ct.Name,
                    ct.Department,
                    ct.Position,
                    ct.Email,
                    ct.Phone,
                    ct.IsPrimary,
                    ct.IsActive
                )).ToList()
            );
        }

        public async Task<CustomerResponseDto> CreateCustomerAsync(CreateCustomerDto dto)
        {
            var customer = new Customer
            {
                CompanyName = dto.CompanyName,
                CompanyAddress = dto.CompanyAddress,
                TIN = dto.TIN,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.InitialContacts != null)
            {
                foreach (var ct in dto.InitialContacts)
                {
                    customer.Contacts.Add(new CustomerContact
                    {
                        Name = ct.Name,
                        Department = ct.Department,
                        Position = ct.Position,
                        Email = ct.Email,
                        Phone = ct.Phone,
                        IsPrimary = ct.IsPrimary,
                        IsActive = true
                    });
                }
            }

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return (await GetCustomerByIdAsync(customer.CustomerId))!;
        }

        public async Task<bool> UpdateCustomerAsync(int id, UpdateCustomerDto dto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return false;

            customer.CompanyName = dto.CompanyName;
            customer.CompanyAddress = dto.CompanyAddress;
            customer.TIN = dto.TIN;
            customer.Notes = dto.Notes;
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ContactResponseDto?> AddContactAsync(int customerId, CreateContactDto dto)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return null;

            var contact = new CustomerContact
            {
                CustomerId = customerId,
                Name = dto.Name,
                Department = dto.Department,
                Position = dto.Position,
                Email = dto.Email,
                Phone = dto.Phone,
                IsPrimary = dto.IsPrimary,
                IsActive = true
            };

            _context.CustomerContacts.Add(contact);
            await _context.SaveChangesAsync();

            return new ContactResponseDto(
                contact.ContactId,
                contact.CustomerId,
                contact.Name,
                contact.Department,
                contact.Position,
                contact.Email,
                contact.Phone,
                contact.IsPrimary,
                contact.IsActive
            );
        }
        public async Task<bool> DeleteContactAsync(int customerId, int contactId)
        {
            var contact = await _context.CustomerContacts
                .FirstOrDefaultAsync(c => c.ContactId == contactId && c.CustomerId == customerId);

            if (contact == null)
                return false;

            // Soft delete by marking the contact inactive
            contact.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateContactAsync(int customerId, int contactId, UpdateContactDto dto)
        {
            var contact = await _context.CustomerContacts
                .FirstOrDefaultAsync(c => c.ContactId == contactId && c.CustomerId == customerId);

            if (contact == null)
                return false;

            contact.Name = dto.Name;
            contact.Department = dto.Department;
            contact.Position = dto.Position;
            contact.Email = dto.Email;
            contact.Phone = dto.Phone;
            contact.IsPrimary = dto.IsPrimary;
            contact.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}