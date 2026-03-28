using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isdocovac.Models.Enums;
using Isdocovac.Models.OpenAI;

namespace Isdocovac.Services.OpenAI;

public interface IOpenAIInvoiceParsingService
{
    Task<string> UploadPdfToOpenAIAsync(Stream pdfStream, string filename);
    Task<InvoiceExtractionResult> ExtractInvoiceDataAsync(string fileId, InvoiceLineMode lineMode);
    Task DeleteFileFromOpenAIAsync(string fileId);
}

public class OpenAIInvoiceParsingService : IOpenAIInvoiceParsingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIInvoiceParsingService> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxRetries;

    public OpenAIInvoiceParsingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIInvoiceParsingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _configuration = configuration;
        _logger = logger;

        _apiKey = _configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured");
        _model = _configuration["OpenAI:Model"] ?? "gpt-4o";
        _maxRetries = int.Parse(_configuration["OpenAI:MaxRetries"] ?? "3");

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(int.Parse(_configuration["OpenAI:TimeoutSeconds"] ?? "120"));
    }

    public async Task<string> UploadPdfToOpenAIAsync(Stream pdfStream, string filename)
    {
        _logger.LogInformation("Uploading PDF to OpenAI: {Filename}", filename);

        try
        {
            using var content = new MultipartFormDataContent();

            // Add file
            var streamContent = new StreamContent(pdfStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(streamContent, "file", filename);

            // Add purpose
            content.Add(new StringContent("assistants"), "purpose");

            var response = await _httpClient.PostAsync("files", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to upload PDF to OpenAI: {StatusCode} - {Response}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to upload PDF to OpenAI: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<OpenAIFileUploadResponse>(responseContent);
            if (result?.Id == null)
            {
                throw new InvalidOperationException("OpenAI file upload response did not contain file ID");
            }

            _logger.LogInformation("PDF uploaded to OpenAI successfully. File ID: {FileId}", result.Id);
            return result.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading PDF to OpenAI: {Filename}", filename);
            throw;
        }
    }

    public async Task<InvoiceExtractionResult> ExtractInvoiceDataAsync(string fileId, InvoiceLineMode lineMode)
    {
        _logger.LogInformation("Extracting invoice data from OpenAI file: {FileId}, Mode: {LineMode}", fileId, lineMode);

        var prompt = BuildPrompt(lineMode);
        var retryCount = 0;

        while (retryCount <= _maxRetries)
        {
            try
            {
                var requestPayload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new { type = "input_file", input_file = new { file_id = fileId } }
                            }
                        }
                    },
                    temperature = double.Parse(_configuration["OpenAI:Temperature"] ?? "0.1"),
                    max_tokens = int.Parse(_configuration["OpenAI:MaxTokens"] ?? "4000"),
                    response_format = new { type = "json_object" }
                };

                var requestJson = JsonSerializer.Serialize(requestPayload);
                var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("chat/completions", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && retryCount < _maxRetries)
                    {
                        var delay = (int)Math.Pow(2, retryCount) * 1000; // Exponential backoff: 1s, 2s, 4s
                        _logger.LogWarning("OpenAI rate limit hit. Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", delay, retryCount + 1, _maxRetries);
                        await Task.Delay(delay);
                        retryCount++;
                        continue;
                    }

                    _logger.LogError("OpenAI API request failed: {StatusCode} - {Response}", response.StatusCode, responseContent);
                    throw new HttpRequestException($"OpenAI API request failed: {response.StatusCode}");
                }

                var apiResponse = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseContent);
                if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
                {
                    throw new InvalidOperationException("OpenAI response did not contain any choices");
                }

                var messageContent = apiResponse.Choices[0].Message?.Content;
                if (string.IsNullOrEmpty(messageContent))
                {
                    throw new InvalidOperationException("OpenAI response message content is empty");
                }

                // Parse the JSON response into InvoiceExtractionResult
                var extractionResult = ParseExtractionResponse(messageContent, apiResponse.Usage);

                _logger.LogInformation("Invoice data extracted successfully. Invoice: {InvoiceNumber}, Lines: {LineCount}, Tokens: {PromptTokens}/{CompletionTokens}",
                    extractionResult.InvoiceNumber, extractionResult.Lines.Count, extractionResult.PromptTokens, extractionResult.CompletionTokens);

                return extractionResult;
            }
            catch (HttpRequestException) when (retryCount < _maxRetries)
            {
                var delay = (int)Math.Pow(2, retryCount) * 1000;
                _logger.LogWarning("Network error calling OpenAI API. Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", delay, retryCount + 1, _maxRetries);
                await Task.Delay(delay);
                retryCount++;
            }
        }

        throw new InvalidOperationException($"Failed to extract invoice data after {_maxRetries} retries");
    }

    public async Task DeleteFileFromOpenAIAsync(string fileId)
    {
        _logger.LogInformation("Deleting file from OpenAI: {FileId}", fileId);

        try
        {
            var response = await _httpClient.DeleteAsync($"files/{fileId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("File deleted from OpenAI successfully: {FileId}", fileId);
            }
            else
            {
                _logger.LogWarning("Failed to delete file from OpenAI: {FileId} - {StatusCode}", fileId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting file from OpenAI: {FileId}", fileId);
            // Don't throw - file deletion is best effort
        }
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

    private InvoiceExtractionResult ParseExtractionResponse(string jsonContent, OpenAIUsage? usage)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var result = JsonSerializer.Deserialize<InvoiceExtractionResult>(jsonContent, options);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize OpenAI response");
            }

            // Add token usage
            if (usage != null)
            {
                result.PromptTokens = usage.PromptTokens;
                result.CompletionTokens = usage.CompletionTokens;
            }

            result.Model = _model;
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing OpenAI extraction response: {JsonContent}", jsonContent);

            return new InvoiceExtractionResult
            {
                Success = false,
                ErrorMessage = $"Failed to parse extraction response: {ex.Message}",
                Model = _model
            };
        }
    }

    // OpenAI API response models
    private class OpenAIFileUploadResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class OpenAIChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public OpenAIChoice[]? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}
