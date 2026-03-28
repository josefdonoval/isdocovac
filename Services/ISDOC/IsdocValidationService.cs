using System.Xml;
using System.Xml.Schema;

namespace Isdocovac.Services.ISDOC;

public interface IIsdocValidationService
{
    Task<(bool isValid, List<string> errors)> ValidateAgainstXsdAsync(string xmlContent);
}

public class IsdocValidationService : IIsdocValidationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IsdocValidationService> _logger;
    private readonly HttpClient _httpClient;
    private XmlSchemaSet? _cachedSchemaSet;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    public IsdocValidationService(
        IConfiguration configuration,
        ILogger<IsdocValidationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<(bool isValid, List<string> errors)> ValidateAgainstXsdAsync(string xmlContent)
    {
        var errors = new List<string>();

        try
        {
            // Ensure schema is loaded
            var schemaSet = await GetOrLoadSchemaAsync();

            // Parse XML document
            var settings = new XmlReaderSettings
            {
                Schemas = schemaSet,
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ProcessSchemaLocation |
                                  XmlSchemaValidationFlags.ReportValidationWarnings
            };

            // Collect validation errors
            settings.ValidationEventHandler += (sender, args) =>
            {
                var message = $"Line {args.Exception?.LineNumber}, Position {args.Exception?.LinePosition}: {args.Message}";
                errors.Add(message);

                if (args.Severity == XmlSeverityType.Warning)
                {
                    _logger.LogWarning("ISDOC validation warning: {Message}", message);
                }
                else
                {
                    _logger.LogError("ISDOC validation error: {Message}", message);
                }
            };

            // Validate XML
            using var stringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            while (xmlReader.Read())
            {
                // Read through entire document to trigger validation
            }

            var isValid = errors.Count == 0;

            if (isValid)
            {
                _logger.LogInformation("ISDOC XML validation successful");
            }
            else
            {
                _logger.LogWarning("ISDOC XML validation failed with {ErrorCount} errors", errors.Count);
            }

            return (isValid, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ISDOC XML");
            errors.Add($"Validation exception: {ex.Message}");
            return (false, errors);
        }
    }

    private async Task<XmlSchemaSet> GetOrLoadSchemaAsync()
    {
        if (_cachedSchemaSet != null)
        {
            return _cachedSchemaSet;
        }

        await _schemaLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedSchemaSet != null)
            {
                return _cachedSchemaSet;
            }

            var schemaUrl = _configuration["ISDOC:XsdSchemaUrl"] ?? "https://isdoc.cz/6.0.2/xsd/isdoc-invoice-6.0.2.xsd";
            var cachePath = _configuration["ISDOC:XsdCachePath"] ?? "./Data/isdoc-schema.xsd";

            string schemaContent;

            // Try to load from cache first
            if (File.Exists(cachePath))
            {
                _logger.LogInformation("Loading ISDOC XSD schema from cache: {CachePath}", cachePath);
                schemaContent = await File.ReadAllTextAsync(cachePath);
            }
            else
            {
                // Download schema from URL
                _logger.LogInformation("Downloading ISDOC XSD schema from: {SchemaUrl}", schemaUrl);

                try
                {
                    schemaContent = await _httpClient.GetStringAsync(schemaUrl);

                    // Cache the schema for future use
                    var cacheDirectory = Path.GetDirectoryName(cachePath);
                    if (!string.IsNullOrEmpty(cacheDirectory) && !Directory.Exists(cacheDirectory))
                    {
                        Directory.CreateDirectory(cacheDirectory);
                    }

                    await File.WriteAllTextAsync(cachePath, schemaContent);
                    _logger.LogInformation("ISDOC XSD schema cached to: {CachePath}", cachePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download ISDOC XSD schema from {SchemaUrl}", schemaUrl);
                    throw new InvalidOperationException($"Failed to download ISDOC schema: {ex.Message}", ex);
                }
            }

            // Create schema set
            var schemaSet = new XmlSchemaSet();
            using var schemaReader = new StringReader(schemaContent);
            using var xmlReader = XmlReader.Create(schemaReader);

            schemaSet.Add("http://isdoc.cz/namespace/2013", xmlReader);
            schemaSet.Compile();

            _cachedSchemaSet = schemaSet;
            _logger.LogInformation("ISDOC XSD schema loaded and compiled successfully");

            return _cachedSchemaSet;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
