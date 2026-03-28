using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isdocovac.Models.Enums;
using Isdocovac.Models.OpenAI;
using Isdocovac.Services.OpenAI;

namespace Isdocovac.Services.Claude;

public class ClaudeInvoiceParsingService : IOpenAIInvoiceParsingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeInvoiceParsingService> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxRetries;

    public ClaudeInvoiceParsingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ClaudeInvoiceParsingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Claude");
        _configuration = configuration;
        _logger = logger;

        _apiKey = _configuration["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude API key is not configured");
        _model = _configuration["Claude:Model"] ?? "claude-sonnet-4-20250514";
        _maxRetries = int.Parse(_configuration["Claude:MaxRetries"] ?? "3");

        _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _httpClient.Timeout = TimeSpan.FromSeconds(int.Parse(_configuration["Claude:TimeoutSeconds"] ?? "120"));
    }

    /// <summary>
    /// Claude doesn't need a file upload step — PDFs are sent inline as base64.
    /// This method reads the PDF into memory and returns the base64 string as the "file ID".
    /// </summary>
    public async Task<string> UploadPdfToOpenAIAsync(Stream pdfStream, string filename)
    {
        _logger.LogInformation("Encoding PDF for Claude: {Filename}", filename);

        using var memoryStream = new MemoryStream();
        await pdfStream.CopyToAsync(memoryStream);
        var base64 = Convert.ToBase64String(memoryStream.ToArray());

        _logger.LogInformation("PDF encoded for Claude: {Filename} ({Size} bytes)", filename, memoryStream.Length);
        return base64;
    }

    /// <summary>
    /// Extracts invoice data using the Claude Messages API with PDF support.
    /// The fileId parameter is actually the base64-encoded PDF content.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExtractInvoiceDataAsync(string fileId, InvoiceLineMode lineMode)
    {
        _logger.LogInformation("Extracting invoice data with Claude, Mode: {LineMode}", lineMode);

        var prompt = BuildPrompt(lineMode);
        var retryCount = 0;

        while (retryCount <= _maxRetries)
        {
            try
            {
                var requestPayload = new
                {
                    model = _model,
                    max_tokens = int.Parse(_configuration["Claude:MaxTokens"] ?? "4096"),
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "document",
                                    source = new
                                    {
                                        type = "base64",
                                        media_type = "application/pdf",
                                        data = fileId
                                    }
                                },
                                new
                                {
                                    type = (string)"text",
                                    source = (object?)null,
                                    text = (string?)prompt
                                }
                            }
                        }
                    }
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                var requestJson = JsonSerializer.Serialize(requestPayload, jsonOptions);
                var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("v1/messages", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429 && retryCount < _maxRetries)
                    {
                        var delay = (int)Math.Pow(2, retryCount) * 1000;
                        _logger.LogWarning("Claude rate limit hit. Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", delay, retryCount + 1, _maxRetries);
                        await Task.Delay(delay);
                        retryCount++;
                        continue;
                    }

                    if ((int)response.StatusCode == 529 && retryCount < _maxRetries)
                    {
                        var delay = (int)Math.Pow(2, retryCount) * 2000;
                        _logger.LogWarning("Claude overloaded. Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", delay, retryCount + 1, _maxRetries);
                        await Task.Delay(delay);
                        retryCount++;
                        continue;
                    }

                    _logger.LogError("Claude API request failed: {StatusCode} - {Response}", response.StatusCode, responseContent);
                    throw new HttpRequestException($"Claude API request failed: {response.StatusCode}");
                }

                var apiResponse = JsonSerializer.Deserialize<ClaudeMessageResponse>(responseContent);
                if (apiResponse?.Content == null || apiResponse.Content.Length == 0)
                {
                    throw new InvalidOperationException("Claude response did not contain any content");
                }

                var textContent = apiResponse.Content.FirstOrDefault(c => c.Type == "text");
                if (textContent == null || string.IsNullOrEmpty(textContent.Text))
                {
                    throw new InvalidOperationException("Claude response text content is empty");
                }

                var extractionResult = ParseExtractionResponse(textContent.Text, apiResponse.Usage);

                _logger.LogInformation("Invoice data extracted successfully. Invoice: {InvoiceNumber}, Lines: {LineCount}, Tokens: {InputTokens}/{OutputTokens}",
                    extractionResult.InvoiceNumber, extractionResult.Lines.Count, extractionResult.PromptTokens, extractionResult.CompletionTokens);

                return extractionResult;
            }
            catch (HttpRequestException) when (retryCount < _maxRetries)
            {
                var delay = (int)Math.Pow(2, retryCount) * 1000;
                _logger.LogWarning("Network error calling Claude API. Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", delay, retryCount + 1, _maxRetries);
                await Task.Delay(delay);
                retryCount++;
            }
        }

        throw new InvalidOperationException($"Failed to extract invoice data after {_maxRetries} retries");
    }

    /// <summary>
    /// No-op for Claude — there are no uploaded files to clean up.
    /// </summary>
    public Task DeleteFileFromOpenAIAsync(string fileId)
    {
        _logger.LogDebug("Claude does not require file cleanup (PDF was sent inline)");
        return Task.CompletedTask;
    }

    private string BuildPrompt(InvoiceLineMode lineMode)
    {
        if (lineMode == InvoiceLineMode.Detailed)
        {
            return @"You are an expert Czech invoice data extraction system. Extract ALL information from this invoice PDF.

CRITICAL REQUIREMENTS:
1. Extract EVERY line item with exact prices, quantities, units, and VAT rates
2. Identify supplier (seller/dodavatel) and customer (buyer/odběratel) with full address and registration numbers
3. Extract dates:
   - Issue date (datum vystavení)
   - Due date (datum splatnosti)
   - Taxable supply date (DUZP - datum uskutečnění zdanitelného plnění)
4. Extract payment symbols:
   - Variabilní symbol (VS)
   - Konstantní symbol (KS)
   - Specifický symbol (SS)
5. Extract bank account: číslo účtu, IBAN, bank code
6. Calculate VAT totals per rate (must match invoice totals exactly)
7. Extract document number and type

Return ONLY valid JSON with this exact structure:
{
  ""InvoiceNumber"": ""string"",
  ""DocumentType"": ""1"",
  ""IssuedOn"": ""2024-01-15"",
  ""DueOn"": ""2024-02-15"",
  ""TaxableSupplyDate"": ""2024-01-15"",
  ""Supplier"": {
    ""Name"": ""string"",
    ""Street"": ""string"",
    ""City"": ""string"",
    ""Zip"": ""string"",
    ""Country"": ""CZ"",
    ""RegistrationNo"": ""string"",
    ""VatNo"": ""string""
  },
  ""Customer"": {
    ""Name"": ""string"",
    ""Street"": ""string"",
    ""City"": ""string"",
    ""Zip"": ""string"",
    ""Country"": ""CZ"",
    ""RegistrationNo"": ""string"",
    ""VatNo"": ""string""
  },
  ""Lines"": [
    {
      ""LineNumber"": 1,
      ""Name"": ""Product/Service description"",
      ""Quantity"": 10.0,
      ""UnitName"": ""ks"",
      ""UnitPrice"": 100.0,
      ""VatRate"": 21.0,
      ""TotalPriceWithoutVat"": 1000.0,
      ""TotalVat"": 210.0,
      ""TotalPriceWithVat"": 1210.0,
      ""Sku"": ""optional""
    }
  ],
  ""Subtotal"": 1000.0,
  ""Total"": 1210.0,
  ""Currency"": ""CZK"",
  ""VatPriceMode"": ""without_vat"",
  ""VatRates"": [
    {
      ""VatRate"": 21.0,
      ""Base"": 1000.0,
      ""Vat"": 210.0
    }
  ],
  ""VariableSymbol"": ""string"",
  ""ConstantSymbol"": ""string"",
  ""SpecificSymbol"": ""string"",
  ""BankAccount"": ""123456/0800"",
  ""Iban"": ""CZ65 0800 0000 1920 0014 5399"",
  ""Note"": ""optional note""
}";
        }
        else // Overall mode
        {
            return @"You are an expert Czech invoice data extraction system. Extract summary information from this invoice PDF.

CRITICAL REQUIREMENTS:
1. Create ONE summary line item with total invoice amount
2. Identify supplier (seller/dodavatel) and customer (buyer/odběratel) with full details
3. Extract all dates, payment details, and symbols
4. Calculate VAT summary
5. All amounts must match invoice totals exactly

Return ONLY valid JSON with this exact structure:
{
  ""InvoiceNumber"": ""string"",
  ""DocumentType"": ""1"",
  ""IssuedOn"": ""2024-01-15"",
  ""DueOn"": ""2024-02-15"",
  ""TaxableSupplyDate"": ""2024-01-15"",
  ""Supplier"": {
    ""Name"": ""string"",
    ""Street"": ""string"",
    ""City"": ""string"",
    ""Zip"": ""string"",
    ""Country"": ""CZ"",
    ""RegistrationNo"": ""string"",
    ""VatNo"": ""string""
  },
  ""Customer"": {
    ""Name"": ""string"",
    ""Street"": ""string"",
    ""City"": ""string"",
    ""Zip"": ""string"",
    ""Country"": ""CZ"",
    ""RegistrationNo"": ""string"",
    ""VatNo"": ""string""
  },
  ""Lines"": [
    {
      ""LineNumber"": 1,
      ""Name"": ""Invoice total"",
      ""Quantity"": 1.0,
      ""UnitName"": ""ks"",
      ""UnitPrice"": 1000.0,
      ""VatRate"": 21.0,
      ""TotalPriceWithoutVat"": 1000.0,
      ""TotalVat"": 210.0,
      ""TotalPriceWithVat"": 1210.0
    }
  ],
  ""Subtotal"": 1000.0,
  ""Total"": 1210.0,
  ""Currency"": ""CZK"",
  ""VatPriceMode"": ""without_vat"",
  ""VatRates"": [
    {
      ""VatRate"": 21.0,
      ""Base"": 1000.0,
      ""Vat"": 210.0
    }
  ],
  ""VariableSymbol"": ""string"",
  ""ConstantSymbol"": ""string"",
  ""SpecificSymbol"": ""string"",
  ""BankAccount"": ""123456/0800"",
  ""Iban"": ""CZ65 0800 0000 1920 0014 5399"",
  ""Note"": ""optional note""
}";
        }
    }

    private InvoiceExtractionResult ParseExtractionResponse(string textContent, ClaudeUsage? usage)
    {
        try
        {
            // Claude may wrap JSON in markdown code blocks — strip them
            var jsonContent = textContent.Trim();
            if (jsonContent.StartsWith("```"))
            {
                var firstNewline = jsonContent.IndexOf('\n');
                if (firstNewline >= 0)
                    jsonContent = jsonContent[(firstNewline + 1)..];
                if (jsonContent.EndsWith("```"))
                    jsonContent = jsonContent[..^3].Trim();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var result = JsonSerializer.Deserialize<InvoiceExtractionResult>(jsonContent, options);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize Claude response");
            }

            if (usage != null)
            {
                result.PromptTokens = usage.InputTokens;
                result.CompletionTokens = usage.OutputTokens;
            }

            result.Model = _model;
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Claude extraction response: {TextContent}", textContent);

            return new InvoiceExtractionResult
            {
                Success = false,
                ErrorMessage = $"Failed to parse extraction response: {ex.Message}",
                Model = _model
            };
        }
    }

    // Claude API response models
    private class ClaudeMessageResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("content")]
        public ClaudeContentBlock[]? Content { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
    }

    private class ClaudeContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}
