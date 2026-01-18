using System.ComponentModel.DataAnnotations;
using Isdocovac.Models.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace Isdocovac.Models.Forms;

public class InvoiceFormModel
{
    // Essential Fields
    [Required(ErrorMessage = "Invoice number is required")]
    [MaxLength(100, ErrorMessage = "Invoice number cannot exceed 100 characters")]
    public string Number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Direction is required")]
    public InvoiceDirection Direction { get; set; } = InvoiceDirection.Inbound;

    [Required(ErrorMessage = "Document type is required")]
    [MaxLength(50, ErrorMessage = "Document type cannot exceed 50 characters")]
    public string DocumentType { get; set; } = "invoice";

    [Required(ErrorMessage = "Issue date is required")]
    public DateTime IssuedOn { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "VAT date is required")]
    public DateTime TaxableSupplyDate { get; set; } = DateTime.Today;

    public DateTime? DueOn { get; set; }

    [Required(ErrorMessage = "Currency is required")]
    [MaxLength(10, ErrorMessage = "Currency cannot exceed 10 characters")]
    public string Currency { get; set; } = "CZK";

    [Required(ErrorMessage = "Client name is required")]
    [MaxLength(500, ErrorMessage = "Client name cannot exceed 500 characters")]
    public string ClientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Supplier name is required")]
    [MaxLength(500, ErrorMessage = "Supplier name cannot exceed 500 characters")]
    public string SupplierName { get; set; } = string.Empty;

    // Line Items
    public List<InvoiceLineFormModel> Lines { get; set; } = new();

    // Optional PDF File
    public IBrowserFile? PdfFile { get; set; }

    // Financial Totals (calculated)
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public decimal RemainingAmount { get; set; }

    // Advanced/Optional Fields - Client Details
    [MaxLength(500)]
    public string? ClientStreet { get; set; }

    [MaxLength(255)]
    public string? ClientCity { get; set; }

    [MaxLength(20)]
    public string? ClientZip { get; set; }

    [MaxLength(100)]
    public string? ClientCountry { get; set; }

    [MaxLength(100)]
    public string? ClientRegistrationNo { get; set; }

    [MaxLength(100)]
    public string? ClientVatNo { get; set; }

    // Advanced/Optional Fields - Supplier Details
    [MaxLength(500)]
    public string? SupplierStreet { get; set; }

    [MaxLength(255)]
    public string? SupplierCity { get; set; }

    [MaxLength(20)]
    public string? SupplierZip { get; set; }

    [MaxLength(100)]
    public string? SupplierCountry { get; set; }

    [MaxLength(100)]
    public string? SupplierRegistrationNo { get; set; }

    [MaxLength(100)]
    public string? SupplierVatNo { get; set; }

    // Advanced/Optional Fields - Payment Details
    [MaxLength(50)]
    public string? VariableSymbol { get; set; }

    [MaxLength(50)]
    public string? ConstantSymbol { get; set; }

    [MaxLength(50)]
    public string? SpecificSymbol { get; set; }

    [MaxLength(100)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? Iban { get; set; }

    [MaxLength(100)]
    public string? SwiftBic { get; set; }

    // Advanced/Optional Fields - Notes
    public string? Note { get; set; }
    public string? FooterNote { get; set; }
    public string? PrivateNote { get; set; }

    /// <summary>
    /// Recalculates all line items and totals
    /// </summary>
    public void RecalculateTotals()
    {
        // Recalculate each line
        foreach (var line in Lines)
        {
            line.RecalculatePrices();
        }

        // Calculate invoice totals
        Subtotal = Lines.Sum(l => l.TotalPriceWithoutVat);
        Total = Lines.Sum(l => l.TotalPriceWithVat);
        RemainingAmount = Total; // Initially unpaid
    }

    /// <summary>
    /// Validates the form model
    /// </summary>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();

        // Validate that we have at least one line item
        if (Lines == null || Lines.Count == 0)
        {
            errors.Add("At least one line item is required");
        }

        // Validate date logic
        if (DueOn.HasValue && DueOn.Value < IssuedOn)
        {
            errors.Add("Due date cannot be earlier than issue date");
        }

        // Validate line items
        if (Lines != null)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                if (string.IsNullOrWhiteSpace(line.Name))
                {
                    errors.Add($"Line {i + 1}: Description is required");
                }
                if (line.Quantity <= 0)
                {
                    errors.Add($"Line {i + 1}: Quantity must be greater than 0");
                }
                if (line.UnitPrice < 0)
                {
                    errors.Add($"Line {i + 1}: Unit price cannot be negative");
                }
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Converts this form model to an Invoice entity
    /// </summary>
    public Invoice ToEntity(Guid userId)
    {
        RecalculateTotals();

        // Generate a new ID that will be used for both the invoice and its lines
        var invoiceId = Guid.NewGuid();

        var invoice = new Invoice
        {
            Id = invoiceId,
            UserId = userId,
            Direction = Direction,
            Source = InvoiceSource.Manual, // Will be set by service, but set here for completeness
            Number = Number,
            DocumentType = DocumentType,
            Status = "open",
            Open = true,
            Sent = false,
            Overdue = false,
            Paid = false,
            Cancelled = false,
            IssuedOn = DateTime.SpecifyKind(IssuedOn, DateTimeKind.Utc),
            TaxableSupplyDate = DateTime.SpecifyKind(TaxableSupplyDate, DateTimeKind.Utc),
            DueOn = DueOn.HasValue && DueOn.Value.Date != DateTime.MinValue.Date
                ? DateTime.SpecifyKind(DueOn.Value, DateTimeKind.Utc)
                : null,
            Subtotal = Subtotal,
            Total = Total,
            RemainingAmount = RemainingAmount,
            Currency = Currency,
            ClientName = ClientName,
            ClientStreet = ClientStreet,
            ClientCity = ClientCity,
            ClientZip = ClientZip,
            ClientCountry = ClientCountry,
            ClientRegistrationNo = ClientRegistrationNo,
            ClientVatNo = ClientVatNo,
            ClientHasDeliveryAddress = false,
            SupplierName = SupplierName,
            SupplierStreet = SupplierStreet,
            SupplierCity = SupplierCity,
            SupplierZip = SupplierZip,
            SupplierCountry = SupplierCountry,
            SupplierRegistrationNo = SupplierRegistrationNo,
            SupplierVatNo = SupplierVatNo,
            VariableSymbol = VariableSymbol,
            ConstantSymbol = ConstantSymbol,
            SpecificSymbol = SpecificSymbol,
            BankAccount = BankAccount,
            Iban = Iban,
            SwiftBic = SwiftBic,
            Note = Note,
            FooterNote = FooterNote,
            PrivateNote = PrivateNote,
            ImportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Convert line items
        for (int i = 0; i < Lines.Count; i++)
        {
            Lines[i].LineOrder = i + 1;
            invoice.Lines.Add(Lines[i].ToEntity(invoiceId));
        }

        return invoice;
    }

    /// <summary>
    /// Creates a form model from an existing Invoice entity
    /// </summary>
    public static InvoiceFormModel FromEntity(Invoice invoice)
    {
        var formModel = new InvoiceFormModel
        {
            Number = invoice.Number,
            Direction = invoice.Direction,
            DocumentType = invoice.DocumentType,
            IssuedOn = invoice.IssuedOn ?? DateTime.Today,
            TaxableSupplyDate = invoice.TaxableSupplyDate ?? DateTime.Today,
            DueOn = invoice.DueOn,
            Currency = invoice.Currency,
            ClientName = invoice.ClientName ?? string.Empty,
            ClientStreet = invoice.ClientStreet,
            ClientCity = invoice.ClientCity,
            ClientZip = invoice.ClientZip,
            ClientCountry = invoice.ClientCountry,
            ClientRegistrationNo = invoice.ClientRegistrationNo,
            ClientVatNo = invoice.ClientVatNo,
            SupplierName = invoice.SupplierName ?? string.Empty,
            SupplierStreet = invoice.SupplierStreet,
            SupplierCity = invoice.SupplierCity,
            SupplierZip = invoice.SupplierZip,
            SupplierCountry = invoice.SupplierCountry,
            SupplierRegistrationNo = invoice.SupplierRegistrationNo,
            SupplierVatNo = invoice.SupplierVatNo,
            VariableSymbol = invoice.VariableSymbol,
            ConstantSymbol = invoice.ConstantSymbol,
            SpecificSymbol = invoice.SpecificSymbol,
            BankAccount = invoice.BankAccount,
            Iban = invoice.Iban,
            SwiftBic = invoice.SwiftBic,
            Note = invoice.Note,
            FooterNote = invoice.FooterNote,
            PrivateNote = invoice.PrivateNote,
            Subtotal = invoice.Subtotal,
            Total = invoice.Total,
            RemainingAmount = invoice.RemainingAmount
        };

        // Convert line items
        if (invoice.Lines != null && invoice.Lines.Any())
        {
            formModel.Lines = invoice.Lines
                .OrderBy(l => l.LineOrder)
                .Select(InvoiceLineFormModel.FromEntity)
                .ToList();
        }

        return formModel;
    }
}
