using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Services;

public interface IInvoiceImportService
{
    Task<Invoice> ImportFromFakturoidAsync(Guid fakturoidInvoiceId, Guid userId);
    Task<IEnumerable<Invoice>> BulkImportFromFakturoidAsync(IEnumerable<Guid> fakturoidInvoiceIds, Guid userId);
    Task<Invoice> ImportFromParsedInvoiceAsync(Guid parsedInvoiceId, Guid userId);
    Task<Invoice> ResyncFromFakturoidAsync(Guid invoiceId);
}

public class InvoiceImportService : IInvoiceImportService
{
    private readonly IMainInvoiceProvider _invoiceProvider;
    private readonly IFakturoidInvoiceProvider _fakturoidInvoiceProvider;
    private readonly IParsedInvoiceProvider _parsedInvoiceProvider;
    private readonly IConfiguration _configuration;

    public InvoiceImportService(
        IMainInvoiceProvider invoiceProvider,
        IFakturoidInvoiceProvider fakturoidInvoiceProvider,
        IParsedInvoiceProvider parsedInvoiceProvider,
        IConfiguration configuration)
    {
        _invoiceProvider = invoiceProvider;
        _fakturoidInvoiceProvider = fakturoidInvoiceProvider;
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _configuration = configuration;
    }

    public async Task<Invoice> ImportFromFakturoidAsync(Guid fakturoidInvoiceId, Guid userId)
    {
        var fakturoidInvoice = await _fakturoidInvoiceProvider.GetByIdAsync(fakturoidInvoiceId);
        if (fakturoidInvoice == null)
        {
            throw new InvalidOperationException($"FakturoidInvoice with ID {fakturoidInvoiceId} not found");
        }

        // Check if already imported
        if (fakturoidInvoice.IsImported)
        {
            throw new InvalidOperationException($"FakturoidInvoice {fakturoidInvoiceId} has already been imported");
        }

        // Map FakturoidInvoice to Invoice
        var invoice = new Invoice
        {
            UserId = userId,
            Direction = InvoiceDirection.Outbound, // Fakturoid only handles issued invoices
            Source = InvoiceSource.Fakturoid,
            FakturoidInvoiceId = fakturoidInvoiceId,

            // Core fields
            CustomId = fakturoidInvoice.CustomId,
            Number = fakturoidInvoice.Number,
            DocumentType = fakturoidInvoice.DocumentType,
            Status = fakturoidInvoice.Status,
            Open = fakturoidInvoice.Open,
            Sent = fakturoidInvoice.Sent,
            Overdue = fakturoidInvoice.Overdue,
            Paid = fakturoidInvoice.Paid,
            Cancelled = fakturoidInvoice.Cancelled,

            // Dates
            IssuedOn = fakturoidInvoice.IssuedOn,
            SentAt = fakturoidInvoice.SentAt,
            PaidOn = fakturoidInvoice.PaidOn,
            DueOn = fakturoidInvoice.DueOn,
            CancelledAt = fakturoidInvoice.CancelledAt,
            Due = fakturoidInvoice.Due,

            // Financial
            Subtotal = fakturoidInvoice.Subtotal,
            Total = fakturoidInvoice.Total,
            RemainingAmount = fakturoidInvoice.RemainingAmount,
            Currency = fakturoidInvoice.Currency,
            ExchangeRate = fakturoidInvoice.ExchangeRate,
            VatPriceMode = fakturoidInvoice.VatPriceMode,

            // Client info
            ClientName = fakturoidInvoice.ClientName,
            ClientStreet = fakturoidInvoice.ClientStreet,
            ClientCity = fakturoidInvoice.ClientCity,
            ClientZip = fakturoidInvoice.ClientZip,
            ClientCountry = fakturoidInvoice.ClientCountry,
            ClientRegistrationNo = fakturoidInvoice.ClientRegistrationNo,
            ClientVatNo = fakturoidInvoice.ClientVatNo,
            ClientHasDeliveryAddress = fakturoidInvoice.ClientHasDeliveryAddress,
            ClientDeliveryName = fakturoidInvoice.ClientDeliveryName,
            ClientDeliveryStreet = fakturoidInvoice.ClientDeliveryStreet,
            ClientDeliveryCity = fakturoidInvoice.ClientDeliveryCity,
            ClientDeliveryZip = fakturoidInvoice.ClientDeliveryZip,
            ClientDeliveryCountry = fakturoidInvoice.ClientDeliveryCountry,

            // Supplier info (our company)
            SupplierName = fakturoidInvoice.YourName,
            SupplierStreet = fakturoidInvoice.YourStreet,
            SupplierCity = fakturoidInvoice.YourCity,
            SupplierZip = fakturoidInvoice.YourZip,
            SupplierCountry = fakturoidInvoice.YourCountry,
            SupplierRegistrationNo = fakturoidInvoice.YourRegistrationNo,
            SupplierVatNo = fakturoidInvoice.YourVatNo,

            // Payment info
            VariableSymbol = fakturoidInvoice.VariableSymbol,
            ConstantSymbol = fakturoidInvoice.ConstantSymbol,
            SpecificSymbol = fakturoidInvoice.SpecificSymbol,
            BankAccount = fakturoidInvoice.BankAccount,
            Iban = fakturoidInvoice.Iban,
            SwiftBic = fakturoidInvoice.SwiftBic,

            // Notes
            Note = fakturoidInvoice.Note,
            FooterNote = fakturoidInvoice.FooterNote,
            PrivateNote = fakturoidInvoice.PrivateNote,

            // JSON fields
            Tags = fakturoidInvoice.Tags,
            VatRatesSummary = fakturoidInvoice.VatRatesSummary,
        };

        // Map invoice lines
        foreach (var line in fakturoidInvoice.Lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                LineOrder = line.LineOrder,
                Name = line.Name,
                Quantity = line.Quantity,
                UnitName = line.UnitName,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                UnitPriceWithoutVat = line.UnitPriceWithoutVat,
                UnitPriceWithVat = line.UnitPriceWithVat,
                TotalPriceWithoutVat = line.TotalPriceWithoutVat,
                TotalVat = line.TotalVat,
                TotalPriceWithVat = line.TotalPriceWithVat,
                Sku = line.Sku
            });
        }

        // Map payments
        foreach (var payment in fakturoidInvoice.Payments)
        {
            invoice.Payments.Add(new InvoicePayment
            {
                PaidOn = payment.PaidOn,
                Currency = payment.Currency,
                Amount = payment.Amount,
                VariableSymbol = payment.VariableSymbol,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Map attachments
        foreach (var attachment in fakturoidInvoice.Attachments)
        {
            invoice.Attachments.Add(new InvoiceAttachment
            {
                Filename = attachment.Filename,
                ContentType = attachment.ContentType,
                ExternalUrl = attachment.DownloadUrl,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Create the invoice
        var createdInvoice = await _invoiceProvider.CreateAsync(invoice);

        // Mark Fakturoid invoice as imported
        await _fakturoidInvoiceProvider.MarkAsImportedAsync(fakturoidInvoiceId, createdInvoice.Id);

        return createdInvoice;
    }

    public async Task<IEnumerable<Invoice>> BulkImportFromFakturoidAsync(IEnumerable<Guid> fakturoidInvoiceIds, Guid userId)
    {
        var invoices = new List<Invoice>();
        foreach (var fakturoidInvoiceId in fakturoidInvoiceIds)
        {
            var invoice = await ImportFromFakturoidAsync(fakturoidInvoiceId, userId);
            invoices.Add(invoice);
        }
        return invoices;
    }

    public async Task<Invoice> ImportFromParsedInvoiceAsync(Guid parsedInvoiceId, Guid userId)
    {
        var parsedInvoice = await _parsedInvoiceProvider.GetByIdAsync(parsedInvoiceId);
        if (parsedInvoice == null)
        {
            throw new InvalidOperationException($"ParsedInvoice with ID {parsedInvoiceId} not found");
        }

        // Check if already imported
        if (parsedInvoice.Status == ParsedInvoiceStatus.Imported)
        {
            throw new InvalidOperationException($"ParsedInvoice {parsedInvoiceId} has already been imported");
        }

        // Validate that the invoice is ready to import
        if (parsedInvoice.Status != ParsedInvoiceStatus.Parsed && parsedInvoice.Status != ParsedInvoiceStatus.ReadyToImport)
        {
            throw new InvalidOperationException($"ParsedInvoice {parsedInvoiceId} is not ready to import (current status: {parsedInvoice.Status})");
        }

        if (!parsedInvoice.IsValid)
        {
            throw new InvalidOperationException($"ParsedInvoice {parsedInvoiceId} has validation errors and cannot be imported");
        }

        // Determine direction based on supplier/customer VAT numbers
        var direction = DetermineInvoiceDirection(parsedInvoice);

        // Map ParsedInvoice to Invoice
        var invoice = new Invoice
        {
            UserId = userId,
            Direction = direction,
            Source = InvoiceSource.ISDOC,
            ParsedInvoiceId = parsedInvoiceId,

            // Core fields
            CustomId = parsedInvoice.CustomId,
            Number = parsedInvoice.InvoiceNumber ?? "UNKNOWN",
            DocumentType = parsedInvoice.DocumentType ?? "invoice",
            Status = parsedInvoice.IsValid ? "open" : "draft",
            Open = true,
            Sent = false,
            Overdue = false,
            Paid = false,
            Cancelled = false,

            // Dates
            IssuedOn = parsedInvoice.IssuedOn,
            DueOn = parsedInvoice.DueOn,

            // Financial
            Subtotal = parsedInvoice.Subtotal ?? 0,
            Total = parsedInvoice.Total ?? 0,
            RemainingAmount = parsedInvoice.Total ?? 0,
            Currency = parsedInvoice.Currency ?? "CZK",
            VatPriceMode = parsedInvoice.VatPriceMode,

            // Client/Supplier info (depends on direction)
            ClientName = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerName : parsedInvoice.SupplierName,
            ClientStreet = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerStreet : parsedInvoice.SupplierStreet,
            ClientCity = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerCity : parsedInvoice.SupplierCity,
            ClientZip = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerZip : parsedInvoice.SupplierZip,
            ClientCountry = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerCountry : parsedInvoice.SupplierCountry,
            ClientRegistrationNo = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerRegistrationNo : parsedInvoice.SupplierRegistrationNo,
            ClientVatNo = direction == InvoiceDirection.Outbound ? parsedInvoice.CustomerVatNo : parsedInvoice.SupplierVatNo,
            ClientHasDeliveryAddress = false,

            SupplierName = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierName : parsedInvoice.CustomerName,
            SupplierStreet = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierStreet : parsedInvoice.CustomerStreet,
            SupplierCity = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierCity : parsedInvoice.CustomerCity,
            SupplierZip = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierZip : parsedInvoice.CustomerZip,
            SupplierCountry = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierCountry : parsedInvoice.CustomerCountry,
            SupplierRegistrationNo = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierRegistrationNo : parsedInvoice.CustomerRegistrationNo,
            SupplierVatNo = direction == InvoiceDirection.Outbound ? parsedInvoice.SupplierVatNo : parsedInvoice.CustomerVatNo,

            // Payment info
            VariableSymbol = parsedInvoice.VariableSymbol,
            ConstantSymbol = parsedInvoice.ConstantSymbol,
            SpecificSymbol = parsedInvoice.SpecificSymbol,
            BankAccount = parsedInvoice.BankAccount,
            Iban = parsedInvoice.Iban,

            // Notes
            Note = parsedInvoice.Note,

            // JSON fields
            VatRatesSummary = parsedInvoice.VatRatesSummary,
        };

        // Create the invoice
        var createdInvoice = await _invoiceProvider.CreateAsync(invoice);

        // Mark ParsedInvoice as imported
        await _parsedInvoiceProvider.MarkAsImportedAsync(parsedInvoiceId, createdInvoice.Id);

        return createdInvoice;
    }

    public async Task<Invoice> ResyncFromFakturoidAsync(Guid invoiceId)
    {
        var invoice = await _invoiceProvider.GetWithDetailsAsync(invoiceId);
        if (invoice == null)
        {
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        }

        if (invoice.Source != InvoiceSource.Fakturoid || invoice.FakturoidInvoiceId == null)
        {
            throw new InvalidOperationException($"Invoice {invoiceId} is not from Fakturoid and cannot be resynced");
        }

        var fakturoidInvoice = await _fakturoidInvoiceProvider.GetByIdAsync(invoice.FakturoidInvoiceId.Value);
        if (fakturoidInvoice == null)
        {
            throw new InvalidOperationException($"Source FakturoidInvoice not found for Invoice {invoiceId}");
        }

        // Update invoice fields from Fakturoid
        invoice.Status = fakturoidInvoice.Status;
        invoice.Open = fakturoidInvoice.Open;
        invoice.Sent = fakturoidInvoice.Sent;
        invoice.Overdue = fakturoidInvoice.Overdue;
        invoice.Paid = fakturoidInvoice.Paid;
        invoice.Cancelled = fakturoidInvoice.Cancelled;
        invoice.SentAt = fakturoidInvoice.SentAt;
        invoice.PaidOn = fakturoidInvoice.PaidOn;
        invoice.CancelledAt = fakturoidInvoice.CancelledAt;
        invoice.RemainingAmount = fakturoidInvoice.RemainingAmount;

        await _invoiceProvider.UpdateAsync(invoice);

        return invoice;
    }

    private InvoiceDirection DetermineInvoiceDirection(ParsedInvoice parsedInvoice)
    {
        // Get our company VAT number from configuration
        var ourVatNo = _configuration["Company:VatNo"];

        if (string.IsNullOrEmpty(ourVatNo))
        {
            // Default to inbound if we don't have company VAT configured
            return InvoiceDirection.Inbound;
        }

        // If we are the supplier, it's an outbound invoice
        if (!string.IsNullOrEmpty(parsedInvoice.SupplierVatNo) &&
            parsedInvoice.SupplierVatNo.Equals(ourVatNo, StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceDirection.Outbound;
        }

        // If we are the customer, it's an inbound invoice
        if (!string.IsNullOrEmpty(parsedInvoice.CustomerVatNo) &&
            parsedInvoice.CustomerVatNo.Equals(ourVatNo, StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceDirection.Inbound;
        }

        // Default to inbound if we can't determine
        return InvoiceDirection.Inbound;
    }
}
