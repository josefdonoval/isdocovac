using System.Text.Json;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Models.Extraction;
using Isdocovac.Models.Forms;
using Isdocovac.Providers;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Services;

public interface IInvoiceImportService
{
    Task<Invoice> ImportFromFakturoidAsync(Guid fakturoidInvoiceId, Guid companyId);
    Task<IEnumerable<Invoice>> BulkImportFromFakturoidAsync(IEnumerable<Guid> fakturoidInvoiceIds, Guid companyId);
    Task<Invoice> ImportFromParsedInvoiceAsync(Guid parsedInvoiceId, Guid companyId);
    Task<Invoice> ImportFromEditedFormAsync(Guid parsedInvoiceId, Guid companyId, InvoiceFormModel editedModel, Guid contactId);
    Task<Invoice> ResyncFromFakturoidAsync(Guid invoiceId);

    /// <summary>
    /// Resolves whether a parsed invoice was issued by us (Outbound) or received by us (Inbound)
    /// based on the configured company VAT number.
    /// </summary>
    InvoiceDirection DetermineInvoiceDirection(ParsedInvoice parsedInvoice);

    /// <summary>
    /// Looks up (or creates) the contact representing the counterparty on a parsed invoice and
    /// returns its id, so callers can pre-select it in a contact picker.
    /// </summary>
    Task<Guid?> EnsureContactForCounterpartyAsync(ParsedInvoice parsed, InvoiceDirection direction, Guid companyId);
}

public class InvoiceImportService : IInvoiceImportService
{
    private readonly IMainInvoiceProvider _invoiceProvider;
    private readonly IFakturoidInvoiceProvider _fakturoidInvoiceProvider;
    private readonly IParsedInvoiceProvider _parsedInvoiceProvider;
    private readonly IContactProvider _contactProvider;
    private readonly IInvoiceManagementService _invoiceManagement;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InvoiceImportService> _logger;

    public InvoiceImportService(
        IMainInvoiceProvider invoiceProvider,
        IFakturoidInvoiceProvider fakturoidInvoiceProvider,
        IParsedInvoiceProvider parsedInvoiceProvider,
        IContactProvider contactProvider,
        IInvoiceManagementService invoiceManagement,
        IConfiguration configuration,
        ILogger<InvoiceImportService> logger)
    {
        _invoiceProvider = invoiceProvider;
        _fakturoidInvoiceProvider = fakturoidInvoiceProvider;
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _contactProvider = contactProvider;
        _invoiceManagement = invoiceManagement;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Invoice> ImportFromFakturoidAsync(Guid fakturoidInvoiceId, Guid companyId)
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

        var vatDate = fakturoidInvoice.TaxableSupplyDate ?? fakturoidInvoice.IssuedOn ?? DateTime.UtcNow;
        var internalNumber = await _invoiceManagement.GenerateInternalNumberAsync(companyId, vatDate);

        // Map FakturoidInvoice to Invoice
        var invoice = new Invoice
        {
            CompanyId = companyId,
            Direction = InvoiceDirection.Outbound, // Fakturoid only handles issued invoices
            Source = InvoiceSource.Fakturoid,
            FakturoidInvoiceId = fakturoidInvoiceId,

            // Core fields
            CustomId = fakturoidInvoice.CustomId,
            InternalNumber = internalNumber,
            OriginalDocumentNumber = fakturoidInvoice.Number,
            DocumentType = fakturoidInvoice.DocumentType,
            Status = fakturoidInvoice.Status,
            Open = fakturoidInvoice.Open,
            Sent = fakturoidInvoice.Sent,
            Overdue = fakturoidInvoice.Overdue,
            Paid = fakturoidInvoice.Paid,
            Cancelled = fakturoidInvoice.Cancelled,

            // Dates
            IssuedOn = fakturoidInvoice.IssuedOn,
            TaxableSupplyDate = fakturoidInvoice.TaxableSupplyDate ?? fakturoidInvoice.IssuedOn,
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

        // Create the invoice (with retry on InternalNumber unique-constraint clash)
        var createdInvoice = await CreateWithCollisionRetryAsync(invoice);

        // Mark Fakturoid invoice as imported
        await _fakturoidInvoiceProvider.MarkAsImportedAsync(fakturoidInvoiceId, createdInvoice.Id);

        return createdInvoice;
    }

    public async Task<IEnumerable<Invoice>> BulkImportFromFakturoidAsync(IEnumerable<Guid> fakturoidInvoiceIds, Guid companyId)
    {
        var invoices = new List<Invoice>();
        foreach (var fakturoidInvoiceId in fakturoidInvoiceIds)
        {
            var invoice = await ImportFromFakturoidAsync(fakturoidInvoiceId, companyId);
            invoices.Add(invoice);
        }
        return invoices;
    }

    public async Task<Invoice> ImportFromParsedInvoiceAsync(Guid parsedInvoiceId, Guid companyId)
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

        await EnsureContactForCounterpartyAsync(parsedInvoice, direction, companyId);

        var vatDate = parsedInvoice.TaxableSupplyDate ?? parsedInvoice.IssuedOn ?? DateTime.UtcNow;
        var internalNumber = await _invoiceManagement.GenerateInternalNumberAsync(companyId, vatDate);

        // Map ParsedInvoice to Invoice
        var invoice = new Invoice
        {
            CompanyId = companyId,
            Direction = direction,
            Source = InvoiceSource.ISDOC,
            ParsedInvoiceId = parsedInvoiceId,

            // Core fields
            CustomId = parsedInvoice.CustomId,
            InternalNumber = internalNumber,
            OriginalDocumentNumber = parsedInvoice.InvoiceNumber,
            DocumentType = parsedInvoice.DocumentType ?? "invoice",
            Status = parsedInvoice.IsValid ? "open" : "draft",
            Open = true,
            Sent = false,
            Overdue = false,
            Paid = false,
            Cancelled = false,

            // Dates
            IssuedOn = parsedInvoice.IssuedOn,
            TaxableSupplyDate = parsedInvoice.TaxableSupplyDate,
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

        // Map invoice lines from LinesJson
        if (!string.IsNullOrEmpty(parsedInvoice.LinesJson))
        {
            var lineItems = JsonSerializer.Deserialize<List<InvoiceLineItem>>(parsedInvoice.LinesJson);
            if (lineItems != null)
            {
                foreach (var line in lineItems)
                {
                    invoice.Lines.Add(new InvoiceLine
                    {
                        LineOrder = line.LineNumber,
                        Name = line.Name,
                        Quantity = line.Quantity,
                        UnitName = line.UnitName,
                        UnitPrice = line.UnitPrice,
                        VatRate = line.VatRate,
                        UnitPriceWithoutVat = line.UnitPrice,
                        UnitPriceWithVat = line.VatRate > 0
                            ? Math.Round(line.UnitPrice * (1 + line.VatRate / 100), 2)
                            : line.UnitPrice,
                        TotalPriceWithoutVat = line.TotalPriceWithoutVat,
                        TotalVat = line.TotalVat,
                        TotalPriceWithVat = line.TotalPriceWithVat,
                        Sku = line.Sku
                    });
                }
            }
        }

        // Create the invoice
        var createdInvoice = await CreateWithCollisionRetryAsync(invoice);

        // Mark ParsedInvoice as imported
        await _parsedInvoiceProvider.MarkAsImportedAsync(parsedInvoiceId, createdInvoice.Id);

        return createdInvoice;
    }

    public async Task<Invoice> ImportFromEditedFormAsync(
        Guid parsedInvoiceId,
        Guid companyId,
        InvoiceFormModel editedModel,
        Guid contactId)
    {
        var parsedInvoice = await _parsedInvoiceProvider.GetByIdAsync(parsedInvoiceId);
        if (parsedInvoice == null)
        {
            throw new InvalidOperationException($"ParsedInvoice with ID {parsedInvoiceId} not found");
        }

        if (parsedInvoice.Status == ParsedInvoiceStatus.Imported)
        {
            throw new InvalidOperationException($"ParsedInvoice {parsedInvoiceId} has already been imported");
        }

        if (parsedInvoice.Status != ParsedInvoiceStatus.Parsed && parsedInvoice.Status != ParsedInvoiceStatus.ReadyToImport)
        {
            throw new InvalidOperationException($"ParsedInvoice {parsedInvoiceId} is not ready to import (current status: {parsedInvoice.Status})");
        }

        if (string.IsNullOrWhiteSpace(editedModel.InternalNumber))
        {
            var vatDate = editedModel.TaxableSupplyDate;
            editedModel.InternalNumber = await _invoiceManagement.GenerateInternalNumberAsync(companyId, vatDate);
        }

        var invoice = editedModel.ToEntity(companyId);
        invoice.Source = InvoiceSource.ISDOC;
        invoice.ParsedInvoiceId = parsedInvoiceId;

        var createdInvoice = await CreateWithCollisionRetryAsync(invoice);

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

    public InvoiceDirection DetermineInvoiceDirection(ParsedInvoice parsedInvoice)
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

    public async Task<Guid?> EnsureContactForCounterpartyAsync(ParsedInvoice parsed, InvoiceDirection direction, Guid companyId)
    {
        var kind = direction == InvoiceDirection.Inbound ? ContactKind.Supplier : ContactKind.Client;

        string? name, ico, dic, street, city, zip, country;
        if (direction == InvoiceDirection.Inbound)
        {
            name = parsed.SupplierName;
            ico = parsed.SupplierRegistrationNo;
            dic = parsed.SupplierVatNo;
            street = parsed.SupplierStreet;
            city = parsed.SupplierCity;
            zip = parsed.SupplierZip;
            country = parsed.SupplierCountry;
        }
        else
        {
            name = parsed.CustomerName;
            ico = parsed.CustomerRegistrationNo;
            dic = parsed.CustomerVatNo;
            street = parsed.CustomerStreet;
            city = parsed.CustomerCity;
            zip = parsed.CustomerZip;
            country = parsed.CustomerCountry;
        }

        if (string.IsNullOrWhiteSpace(ico) && string.IsNullOrWhiteSpace(dic) && string.IsNullOrWhiteSpace(name))
        {
            _logger.LogInformation("Skipping contact upsert: no identifying data on parsed invoice {Id}", parsed.Id);
            return null;
        }

        var existing = await _contactProvider.FindByIdentifiersAsync(companyId, ico, dic, name);
        if (existing != null)
        {
            if (existing.Kind != kind && existing.Kind != ContactKind.Both)
            {
                existing.Kind = ContactKind.Both;
                await _contactProvider.UpdateAsync(existing);
                _logger.LogInformation("Upgraded contact {Id} kind to Both", existing.Id);
            }
            else
            {
                _logger.LogInformation("Matched existing contact {Id} ({Kind})", existing.Id, existing.Kind);
            }
            return existing.Id;
        }

        var countryCode = !string.IsNullOrWhiteSpace(country) && country.Trim().Length == 2
            ? country.Trim().ToUpperInvariant()
            : "CZ";

        var contact = new Contact
        {
            CompanyId = companyId,
            Kind = kind,
            CompanyName = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Ico = string.IsNullOrWhiteSpace(ico) ? null : ico.Trim(),
            Dic = string.IsNullOrWhiteSpace(dic) ? null : dic.Trim(),
            Street = string.IsNullOrWhiteSpace(street) ? null : street.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            Zip = string.IsNullOrWhiteSpace(zip) ? null : zip.Trim(),
            CountryCode = countryCode,
            IsLegalEntity = true,
            IsActive = true,
            VatPeriod = VatPeriod.Monthly,
        };

        if (contact.Id == Guid.Empty) contact.Id = Guid.NewGuid();
        await _contactProvider.AddAsync(contact);
        _logger.LogInformation("Created contact {Name} ({Kind}) from import", contact.CompanyName, contact.Kind);
        return contact.Id;
    }

    private async Task<Invoice> CreateWithCollisionRetryAsync(Invoice invoice)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _invoiceProvider.CreateAsync(invoice);
            }
            catch (DbUpdateException ex) when (IsInternalNumberUniqueViolation(ex) && attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "InternalNumber collision for {InternalNumber}, regenerating (attempt {Attempt}/{MaxAttempts})",
                    invoice.InternalNumber, attempt, maxAttempts);

                var vatDate = invoice.TaxableSupplyDate ?? invoice.IssuedOn ?? DateTime.UtcNow;
                invoice.InternalNumber = await _invoiceManagement.GenerateInternalNumberAsync(invoice.CompanyId, vatDate);
                invoice.Id = Guid.Empty;
            }
        }

        throw new InvalidOperationException("Failed to assign a unique InternalNumber after multiple attempts.");
    }

    private static bool IsInternalNumberUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("internal_number", StringComparison.OrdinalIgnoreCase) == true;
    }
}
