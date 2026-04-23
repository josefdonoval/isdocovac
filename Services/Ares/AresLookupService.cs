using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Isdocovac.Services.Ares;

public record AresContact(
    string Ico,
    string? Dic,
    string? Name,
    bool IsLegalEntity,
    string? Street,
    string? HouseNumber,
    string? OrientNumber,
    string? City,
    string? Zip,
    string CountryCode);

public interface IAresLookupService
{
    Task<AresContact?> LookupByIcoAsync(string ico, CancellationToken ct = default);
}

public class AresLookupService : IAresLookupService
{
    private static readonly HashSet<string> IndividualLegalForms = new()
    {
        "101", "102", "103", "104", "105", "107"
    };

    private readonly HttpClient _http;
    private readonly ILogger<AresLookupService> _logger;

    public AresLookupService(IHttpClientFactory httpFactory, ILogger<AresLookupService> logger)
    {
        _http = httpFactory.CreateClient("Ares");
        _logger = logger;
    }

    public async Task<AresContact?> LookupByIcoAsync(string ico, CancellationToken ct = default)
    {
        ico = (ico ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ico) || !ico.All(char.IsDigit))
        {
            return null;
        }

        var url = $"https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/ekonomicke-subjekty/{ico}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        var dto = JsonSerializer.Deserialize<AresResponse>(json);
        if (dto == null) return null;

        var sidlo = dto.Sidlo;
        var isIndividual = dto.PravniForma != null && IndividualLegalForms.Contains(dto.PravniForma);

        string? zip = sidlo?.Psc != null
            ? sidlo.Psc.Value.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)
            : null;

        return new AresContact(
            Ico: dto.Ico ?? ico,
            Dic: string.IsNullOrWhiteSpace(dto.Dic) ? null : dto.Dic,
            Name: dto.ObchodniJmeno,
            IsLegalEntity: !isIndividual,
            Street: sidlo?.NazevUlice,
            HouseNumber: sidlo?.CisloDomovni?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            OrientNumber: sidlo?.CisloOrientacni?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            City: sidlo?.NazevObce,
            Zip: zip,
            CountryCode: sidlo?.KodStatu ?? "CZ");
    }

    private sealed class AresResponse
    {
        [JsonPropertyName("ico")] public string? Ico { get; set; }
        [JsonPropertyName("dic")] public string? Dic { get; set; }
        [JsonPropertyName("obchodniJmeno")] public string? ObchodniJmeno { get; set; }
        [JsonPropertyName("pravniForma")] public string? PravniForma { get; set; }
        [JsonPropertyName("sidlo")] public AresSidlo? Sidlo { get; set; }
    }

    private sealed class AresSidlo
    {
        [JsonPropertyName("kodStatu")] public string? KodStatu { get; set; }
        [JsonPropertyName("nazevObce")] public string? NazevObce { get; set; }
        [JsonPropertyName("nazevUlice")] public string? NazevUlice { get; set; }
        [JsonPropertyName("cisloDomovni")] public int? CisloDomovni { get; set; }
        [JsonPropertyName("cisloOrientacni")] public int? CisloOrientacni { get; set; }
        [JsonPropertyName("psc")] public int? Psc { get; set; }
    }
}
