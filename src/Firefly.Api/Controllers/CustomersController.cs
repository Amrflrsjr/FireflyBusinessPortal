using Firefly.Application.Common.Interfaces;
using Firefly.Application.Customers.Dtos;
using Firefly.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var customers = await _customerService.GetAllCustomersAsync();
            return Ok(customers);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null) return NotFound();
            return Ok(customer);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
        {
            var customer = await _customerService.CreateCustomerAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId }, customer);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto)
        {
            var updated = await _customerService.UpdateCustomerAsync(id, dto);
            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _customerService.DeleteCustomerAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpPost("{id:int}/contacts")]
        public async Task<IActionResult> AddContact(int id, [FromBody] CreateContactDto dto)
        {
            var contact = await _customerService.AddContactAsync(id, dto);
            if (contact == null) return NotFound();
            return Ok(contact);
        }

        [HttpPut("{customerId}/contacts/{contactId}")]
        public async Task<IActionResult> UpdateContact(
            int customerId,
            int contactId,
            [FromBody] UpdateContactDto dto)
        {
            var success = await _customerService.UpdateContactAsync(customerId, contactId, dto);
            if (!success)
                return NotFound("Customer or contact not found.");

            return NoContent();
        }

        [HttpDelete("{customerId}/contacts/{contactId}")]
        public async Task<IActionResult> DeleteContact(int customerId, int contactId)
        {
            var result = await _customerService.DeleteContactAsync(customerId, contactId);
            if (!result) return NotFound("Contact or Customer not found");
            return NoContent();
        }

        
    }
}