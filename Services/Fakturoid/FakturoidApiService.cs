using System.Net.Http.Headers;
using System.Text.Json;
using Isdocovac.Models;
using Isdocovac.Providers;

namespace Isdocovac.Services.Fakturoid;

public interface IFakturoidApiService
{
    Task<string> GetAccountSlugAsync(string accessToken);
    Task<List<FakturoidInvoice>> FetchInvoicesAsync(Guid connectionId, int page = 1, DateTime? updatedSince = null);
    Task<FakturoidInvoice> FetchInvoiceByIdAsync(Guid connectionId, int fakturoidInvoiceId);
}

public class FakturoidApiService : IFakturoidApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FakturoidApiService> _logger;
    private readonly IFakturoidConnectionProvider _connectionProvider;
    private readonly IFakturoidOAuthService _oauthService;

    public FakturoidApiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FakturoidApiService> logger,
        IFakturoidConnectionProvider connectionProvider,
        IFakturoidOAuthService oauthService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _connectionProvider = connectionProvider;
        _oauthService = oauthService;
    }

    public async Task<string> GetAccountSlugAsync(string accessToken)
    {
        var httpClient = CreateHttpClient(accessToken);

        // Fetch accounts to get the slug
        var response = await httpClient.GetAsync("https://app.fakturoid.cz/api/v3/accounts.json");

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch account: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var accounts = JsonSerializer.Deserialize<List<FakturoidAccount>>(json);

        if (accounts == null || accounts.Count == 0)
            throw new InvalidOperationException("No Fakturoid account found");

        return accounts[0].slug;
    }

    public async Task<List<FakturoidInvoice>> FetchInvoicesAsync(
        Guid connectionId,
        int page = 1,
        DateTime? updatedSince = null)
    {
        var connection = await _connectionProvider.GetByIdAsync(connectionId);
        if (connection == null)
            throw new InvalidOperationException("Fakturoid connection not found");

        // Check if token needs refresh
        if (connection.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            await RefreshConnectionTokenAsync(connection);
            connection = await _connectionProvider.GetByIdAsync(connectionId);
        }

        var httpClient = CreateHttpClient(connection!.AccessToken);

        var url = $"https://app.fakturoid.cz/api/v3/accounts/{connection.AccountSlug}/invoices.json?page={page}";

        if (updatedSince.HasValue)
        {
            url += $"&updated_since={updatedSince.Value:yyyy-MM-ddTHH:mm:ssZ}";
        }

        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to fetch invoices: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to fetch invoices: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var invoices = MapJsonToInvoices(json);

        return invoices;
    }

    public async Task<FakturoidInvoice> FetchInvoiceByIdAsync(Guid connectionId, int fakturoidInvoiceId)
    {
        var connection = await _connectionProvider.GetByIdAsync(connectionId);
        if (connection == null)
            throw new InvalidOperationException("Fakturoid connection not found");

        if (connection.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            await RefreshConnectionTokenAsync(connection);
            connection = await _connectionProvider.GetByIdAsync(connectionId);
        }

        var httpClient = CreateHttpClient(connection!.AccessToken);

        var url = $"https://app.fakturoid.cz/api/v3/accounts/{connection.AccountSlug}/invoices/{fakturoidInvoiceId}.json";
        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch invoice: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var invoices = MapJsonToInvoices($"[{json}]"); // Wrap single invoice in array for consistent parsing

        return invoices.First();
    }

    private HttpClient CreateHttpClient(string accessToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userAgent = _configuration["Fakturoid:UserAgent"] ?? "IsdocovacApp (admin@isdocovac.com)";
        httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

        return httpClient;
    }

    private async Task RefreshConnectionTokenAsync(FakturoidConnection connection)
    {
        var (newAccessToken, newRefreshToken, expiresIn) =
            await _oauthService.RefreshAccessTokenAsync(connection.RefreshToken);

        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        await _connectionProvider.UpdateTokensAsync(
            connection.Id,
            newAccessToken,
            newRefreshToken,
            expiresAt);

        _logger.LogInformation("Refreshed Fakturoid access token for connection {ConnectionId}", connection.Id);
    }

    private List<FakturoidInvoice> MapJsonToInvoices(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var apiInvoices = JsonSerializer.Deserialize<List<FakturoidApiInvoice>>(json, options);

        if (apiInvoices == null)
            return new List<FakturoidInvoice>();

        return apiInvoices.Select(MapApiInvoiceToModel).ToList();
    }

    private FakturoidInvoice MapApiInvoiceToModel(FakturoidApiInvoice api)
    {
        var invoice = new FakturoidInvoice
        {
            FakturoidId = api.id,
            CustomId = api.custom_id,
            Number = api.number ?? string.Empty,
            Token = api.token,
            DocumentType = api.document_type ?? string.Empty,
            Status = api.status ?? string.Empty,

            // Status booleans
            Open = api.open,
            Sent = api.sent,
            Overdue = api.overdue,
            Paid = api.paid,
            Cancelled = api.cancelled,
            Uncollectible = api.uncollectible,

            // Dates
            IssuedOn = api.issued_on,
            SentAt = api.sent_at,
            PaidOn = api.paid_on,
            DueOn = api.due_on,
            LockedAt = api.locked_at,
            CancelledAt = api.cancelled_at,
            UncollectibleAt = api.uncollectible_at,
            CreatedAt = api.created_at,
            UpdatedAt = api.updated_at,
            FakturoidUpdatedAt = api.updated_at,

            // Financial
            Subtotal = api.subtotal,
            Total = api.total,
            RemainingAmount = api.remaining_amount,
            Currency = api.currency ?? string.Empty,
            ExchangeRate = api.exchange_rate,
            NativeSubtotal = api.native_subtotal,
            NativeTotal = api.native_total,
            NativeRemainingAmount = api.native_remaining_amount,
            VatPriceMode = api.vat_price_mode,
            Due = api.due,

            // Client info
            SubjectId = api.subject_id,
            SubjectCustomId = api.subject_custom_id,
            ClientName = api.client_name,
            ClientStreet = api.client_street,
            ClientCity = api.client_city,
            ClientZip = api.client_zip,
            ClientCountry = api.client_country,
            ClientRegistrationNo = api.client_registration_no,
            ClientVatNo = api.client_vat_no,
            ClientLocalVatNo = api.client_local_vat_no,

            // Delivery address
            ClientHasDeliveryAddress = api.client_has_delivery_address,
            ClientDeliveryName = api.client_delivery_name,
            ClientDeliveryStreet = api.client_delivery_street,
            ClientDeliveryCity = api.client_delivery_city,
            ClientDeliveryZip = api.client_delivery_zip,
            ClientDeliveryCountry = api.client_delivery_country,

            // Your company
            YourName = api.your_name,
            YourStreet = api.your_street,
            YourCity = api.your_city,
            YourZip = api.your_zip,
            YourCountry = api.your_country,
            YourRegistrationNo = api.your_registration_no,
            YourVatNo = api.your_vat_no,
            YourLocalVatNo = api.your_local_vat_no,

            // Additional fields
            VariableSymbol = api.variable_symbol,
            ConstantSymbol = api.constant_symbol,
            SpecificSymbol = api.specific_symbol,
            NumberFormatId = api.number_format_id,
            GeneratorId = api.generator_id,
            RelatedId = api.related_id,
            CorrectionId = api.correction_id,
            ProformaFollowupDocument = api.proforma_followup_document,
            Paypal = api.paypal,
            Gopay = api.gopay,
            Note = api.note,
            FooterNote = api.footer_note,
            PrivateNote = api.private_note,
            BankAccountId = api.bank_account_id,
            BankAccount = api.bank_account,
            Iban = api.iban,
            SwiftBic = api.swift_bic,

            // JSON fields
            Tags = api.tags != null ? JsonSerializer.Serialize(api.tags) : null,
            VatRatesSummary = api.vat_rates_summary != null ? JsonSerializer.Serialize(api.vat_rates_summary) : null,
            NativeVatRatesSummary = api.native_vat_rates_summary != null ? JsonSerializer.Serialize(api.native_vat_rates_summary) : null,
            PaidAdvances = api.paid_advances != null ? JsonSerializer.Serialize(api.paid_advances) : null,

            // URLs
            HtmlUrl = api.html_url,
            PublicHtmlUrl = api.public_html_url,

            // Map line items
            Lines = api.lines?.Select((line, index) => new FakturoidInvoiceLine
            {
                Id = Guid.NewGuid(),
                LineOrder = index,
                Name = line.name ?? string.Empty,
                Quantity = line.quantity,
                UnitName = line.unit_name,
                UnitPrice = line.unit_price,
                VatRate = line.vat_rate,
                UnitPriceWithoutVat = line.unit_price_without_vat,
                UnitPriceWithVat = line.unit_price_with_vat,
                TotalPriceWithoutVat = line.total_price_without_vat,
                TotalVat = line.total_vat,
                TotalPriceWithVat = line.total_price_with_vat,
                InventoryItemId = line.inventory_item_id,
                Sku = line.sku
            }).ToList() ?? new List<FakturoidInvoiceLine>(),

            // Map payments
            Payments = new List<FakturoidInvoicePayment>(),

            // Map attachments
            Attachments = new List<FakturoidInvoiceAttachment>()
        };

        return invoice;
    }

    // DTOs for API deserialization
    private class FakturoidAccount
    {
        public string slug { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
    }

    private class FakturoidApiInvoice
    {
        public int id { get; set; }
        public string? custom_id { get; set; }
        public string? number { get; set; }
        public string? token { get; set; }
        public string? document_type { get; set; }
        public string? status { get; set; }
        public bool open { get; set; }
        public bool sent { get; set; }
        public bool overdue { get; set; }
        public bool paid { get; set; }
        public bool cancelled { get; set; }
        public bool uncollectible { get; set; }
        public DateTime? issued_on { get; set; }
        public DateTime? sent_at { get; set; }
        public DateTime? paid_on { get; set; }
        public DateTime? due_on { get; set; }
        public DateTime? locked_at { get; set; }
        public DateTime? cancelled_at { get; set; }
        public DateTime? uncollectible_at { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public decimal subtotal { get; set; }
        public decimal total { get; set; }
        public decimal remaining_amount { get; set; }
        public string? currency { get; set; }
        public decimal? exchange_rate { get; set; }
        public decimal? native_subtotal { get; set; }
        public decimal? native_total { get; set; }
        public decimal? native_remaining_amount { get; set; }
        public string? vat_price_mode { get; set; }
        public int? due { get; set; }
        public int? subject_id { get; set; }
        public string? subject_custom_id { get; set; }
        public string? client_name { get; set; }
        public string? client_street { get; set; }
        public string? client_city { get; set; }
        public string? client_zip { get; set; }
        public string? client_country { get; set; }
        public string? client_registration_no { get; set; }
        public string? client_vat_no { get; set; }
        public string? client_local_vat_no { get; set; }
        public bool client_has_delivery_address { get; set; }
        public string? client_delivery_name { get; set; }
        public string? client_delivery_street { get; set; }
        public string? client_delivery_city { get; set; }
        public string? client_delivery_zip { get; set; }
        public string? client_delivery_country { get; set; }
        public string? your_name { get; set; }
        public string? your_street { get; set; }
        public string? your_city { get; set; }
        public string? your_zip { get; set; }
        public string? your_country { get; set; }
        public string? your_registration_no { get; set; }
        public string? your_vat_no { get; set; }
        public string? your_local_vat_no { get; set; }
        public string? variable_symbol { get; set; }
        public string? constant_symbol { get; set; }
        public string? specific_symbol { get; set; }
        public int? number_format_id { get; set; }
        public int? generator_id { get; set; }
        public int? related_id { get; set; }
        public int? correction_id { get; set; }
        public string? proforma_followup_document { get; set; }
        public string? paypal { get; set; }
        public string? gopay { get; set; }
        public string? note { get; set; }
        public string? footer_note { get; set; }
        public string? private_note { get; set; }
        public int? bank_account_id { get; set; }
        public string? bank_account { get; set; }
        public string? iban { get; set; }
        public string? swift_bic { get; set; }
        public List<string>? tags { get; set; }
        public object? vat_rates_summary { get; set; }
        public object? native_vat_rates_summary { get; set; }
        public object? paid_advances { get; set; }
        public string? html_url { get; set; }
        public string? public_html_url { get; set; }
        public List<FakturoidApiLine>? lines { get; set; }
    }

    private class FakturoidApiLine
    {
        public string? name { get; set; }
        public decimal quantity { get; set; }
        public string? unit_name { get; set; }
        public decimal unit_price { get; set; }
        public decimal vat_rate { get; set; }
        public decimal unit_price_without_vat { get; set; }
        public decimal unit_price_with_vat { get; set; }
        public decimal total_price_without_vat { get; set; }
        public decimal total_vat { get; set; }
        public decimal total_price_with_vat { get; set; }
        public int? inventory_item_id { get; set; }
        public string? sku { get; set; }
    }
}
