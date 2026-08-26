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

        public async Task<IEnumerable<CustomerResponseDto>> GetAllCustomersAsync(string? search = null, string? sortBy = null, bool ascending = true)
        {
            var query = _context.Customers
                .Include(c => c.Contacts)
                .Where(c => c.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.CustomerId.ToString() == search ||
                    c.CompanyName.ToLower().Contains(search) ||
                    (!string.IsNullOrEmpty(c.TIN) && c.TIN.ToLower().Contains(search))
                );
            }

            // Apply backend sorting
            query = sortBy?.ToLower() switch
            {
                "tin" => ascending ? query.OrderBy(c => c.TIN) : query.OrderByDescending(c => c.TIN),
                "createdat" => ascending ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt),
                "companyname" or _ => ascending ? query.OrderBy(c => c.CompanyName) : query.OrderByDescending(c => c.CompanyName),
            };

            return await query
                .Select(c => new CustomerResponseDto(
                    c.CustomerId,
                    c.CustomerType,
                    c.CompanyName,
                    c.CompanyAddress,
                    c.TIN,
                    c.Notes,
                    c.CreatedAt,
                    c.Contacts
                        .Where(ct => ct.IsActive)
                        .Select(ct => new ContactResponseDto(
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
                .FirstOrDefaultAsync(x => x.CustomerId == id && x.IsActive);

            if (c == null) return null;

            return new CustomerResponseDto(
                c.CustomerId,
                c.CustomerType,
                c.CompanyName,
                c.CompanyAddress,
                c.TIN,
                c.Notes,
                c.CreatedAt,
                c.Contacts
                    .Where(ct => ct.IsActive)
                    .Select(ct => new ContactResponseDto(
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
            string resolvedCompanyName = string.IsNullOrWhiteSpace(dto.CompanyName)
                ? (dto.InitialContacts?.FirstOrDefault()?.Name ?? "Personal Account")
                : dto.CompanyName;

            var customer = new Customer
            {
                CustomerType = string.IsNullOrWhiteSpace(dto.CustomerType) ? "Business" : dto.CustomerType,
                CompanyName = resolvedCompanyName,
                CompanyAddress = dto.CompanyAddress ?? string.Empty,
                TIN = dto.TIN ?? string.Empty,
                Notes = dto.Notes ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.InitialContacts != null && dto.InitialContacts.Any())
            {
                bool primaryAssigned = false;
                foreach (var ct in dto.InitialContacts)
                {
                    bool makePrimary = ct.IsPrimary && !primaryAssigned;
                    if (makePrimary) primaryAssigned = true;

                    customer.Contacts.Add(new CustomerContact
                    {
                        Name = ct.Name,
                        Department = ct.Department ?? string.Empty,
                        Position = ct.Position ?? string.Empty,
                        Email = ct.Email ?? string.Empty,
                        Phone = ct.Phone ?? string.Empty,
                        IsPrimary = makePrimary,
                        IsActive = true
                    });
                }

                if (!primaryAssigned && customer.Contacts.Any())
                {
                    customer.Contacts.First().IsPrimary = true;
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

            customer.CompanyName = string.IsNullOrWhiteSpace(dto.CompanyName) ? customer.CompanyName : dto.CompanyName;
            customer.CompanyAddress = dto.CompanyAddress ?? string.Empty;
            customer.TIN = dto.TIN ?? string.Empty;
            customer.Notes = dto.Notes ?? string.Empty;
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCustomerAsync(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.CustomerId == id && c.IsActive);

            if (customer == null) return false;

            customer.IsActive = false;
            foreach (var contact in customer.Contacts)
            {
                contact.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ContactResponseDto?> AddContactAsync(int customerId, CreateContactDto dto)
        {
            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);

            if (customer == null) return null;

            bool isPrimary = dto.IsPrimary;

            if (isPrimary)
            {
                foreach (var existing in customer.Contacts)
                {
                    existing.IsPrimary = false;
                }
            }
            else if (!customer.Contacts.Any(c => c.IsActive && c.IsPrimary))
            {
                isPrimary = true;
            }

            var contact = new CustomerContact
            {
                CustomerId = customerId,
                Name = dto.Name,
                Department = dto.Department ?? string.Empty,
                Position = dto.Position ?? string.Empty,
                Email = dto.Email ?? string.Empty,
                Phone = dto.Phone ?? string.Empty,
                IsPrimary = isPrimary,
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

            contact.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateContactAsync(int customerId, int contactId, UpdateContactDto dto)
        {
            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);

            if (customer == null) return false;

            var contact = customer.Contacts.FirstOrDefault(c => c.ContactId == contactId);
            if (contact == null)
                return false;

            bool isPrimary = dto.IsPrimary;
            bool hasOtherPrimary = customer.Contacts.Any(c => c.ContactId != contactId && c.IsActive && c.IsPrimary);

            if (isPrimary)
            {
                foreach (var otherContact in customer.Contacts)
                {
                    if (otherContact.ContactId != contactId)
                    {
                        otherContact.IsPrimary = false;
                    }
                }
            }
            else if (!hasOtherPrimary && contact.IsPrimary)
            {
                isPrimary = true;
            }

            contact.Name = dto.Name;
            contact.Department = dto.Department ?? string.Empty;
            contact.Position = dto.Position ?? string.Empty;
            contact.Email = dto.Email ?? string.Empty;
            contact.Phone = dto.Phone ?? string.Empty;
            contact.IsPrimary = isPrimary;
            contact.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<CustomerResponseDto>> GetDeletedCustomersAsync(string? search = null)
        {
            var query = _context.Customers
                .Include(c => c.Contacts)
                .Where(c => !c.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.CompanyName.ToLower().Contains(search) ||
                    (!string.IsNullOrEmpty(c.CompanyAddress) && c.CompanyAddress.ToLower().Contains(search)) ||
                    (!string.IsNullOrEmpty(c.TIN) && c.TIN.ToLower().Contains(search)) ||
                    c.Contacts.Any(ct =>
                        ct.Name.ToLower().Contains(search) ||
                        (!string.IsNullOrEmpty(ct.Email) && ct.Email.ToLower().Contains(search))
                    )
                );
            }

            return await query
                .Select(c => new CustomerResponseDto(
                    c.CustomerId,
                    c.CustomerType,
                    c.CompanyName,
                    c.CompanyAddress,
                    c.TIN,
                    c.Notes,
                    c.CreatedAt,
                    c.Contacts
                        .Select(ct => new ContactResponseDto(
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

        public async Task<bool> RestoreCustomerAsync(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return false;

            customer.IsActive = true;
            foreach (var contact in customer.Contacts)
            {
                contact.IsActive = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PermanentlyDeleteCustomerAsync(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return false;

            var contactIds = customer.Contacts.Select(c => c.ContactId).ToList();

            if (contactIds.Any())
            {
                var relatedQuotations = await _context.Quotations
                    .Where(q => q.ContactId.HasValue && contactIds.Contains(q.ContactId.Value))
                    .ToListAsync();

                foreach (var quotation in relatedQuotations)
                {
                    quotation.ContactId = null;
                }
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}