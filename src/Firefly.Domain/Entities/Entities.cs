using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Firefly.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class RolePermission
    {
        [Key]
        public int RolePermissionId { get; set; }
        public string RoleId { get; set; } = string.Empty;
        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }

    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string TIN { get; set; } = string.Empty;
        public int? LogoFileId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
        public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    public class CustomerContact
    {
        [Key]
        public int ContactId { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }

    public class ProductVariant
    {
        [Key]
        public int ProductVariantId { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public string SKU { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Quotation
    {
        [Key]
        public int QuotationId { get; set; }
        public string QuotationNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        // Made Nullable (int?) so quotations without formal contact records won't break foreign keys
        public int? ContactId { get; set; }
        public CustomerContact? Contact { get; set; }

        public string ContactNameSnapshot { get; set; } = string.Empty;
        public string ContactEmailSnapshot { get; set; } = string.Empty;
        public string ContactPositionSnapshot { get; set; } = string.Empty;

        public DateTime DateGenerated { get; set; } = DateTime.UtcNow;
        public DateTime ValidUntil { get; set; }
        public string VATType { get; set; } = "Inclusive";
        public string Status { get; set; } = "Created";

        // Made Nullable (string?) so notes are optional
        public string? NoteToCustomer { get; set; }
        public string PreparedByFK { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    }

    public class QuotationItem
    {
        [Key]
        public int QuotationItemId { get; set; }
        public int QuotationId { get; set; }
        public Quotation Quotation { get; set; } = null!;

        // Made Nullable (int?) to allow custom non-catalog items
        public int? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int? QuotationId { get; set; }
        public Quotation? Quotation { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        public int? ContactId { get; set; }
        public CustomerContact? Contact { get; set; }

        public string ContactNameSnapshot { get; set; } = string.Empty;
        public string ContactEmailSnapshot { get; set; } = string.Empty;
        public string ContactPositionSnapshot { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }
        public string VATType { get; set; } = "Inclusive";
        public string Status { get; set; } = "Unpaid"; // Unpaid, PartiallyPaid, Paid, Cancelled

        public decimal Subtotal { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal BalanceDue { get; set; }

        public string Notes { get; set; } = string.Empty;
        public string CreatedByFK { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class InvoiceItem
    {
        [Key]
        public int InvoiceItemId { get; set; }
        public int InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        // Made Nullable (int?) to support custom items
        public int? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }
        public int InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string RecordedByFK { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Expense
    {
        [Key]
        public int ExpenseId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Supplier { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string RecordedByFK { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CompanySettings
    {
        [Key]
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TIN { get; set; } = string.Empty;
        public int? LogoFileId { get; set; }
        public string PaymentOptions { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }
        public string UserIdFK { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string OldValues { get; set; } = string.Empty;
        public string NewValues { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}