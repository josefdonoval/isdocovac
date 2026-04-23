using System.Collections.Immutable;
using System.Reflection;

namespace Isdocovac.Services.CodeLists;

public record TaxOffice(int CUfo, int KUfoVema, string Nazev, string Kraj);
public record TaxOfficeBranch(int CPracufo, int CUfo, string Nazev, string Kraj);
public record NaceSection(string Sekce, string NazevCz);

public interface ICodeListService
{
    IReadOnlyList<TaxOffice> TaxOffices { get; }
    IReadOnlyList<TaxOfficeBranch> TaxOfficeBranches { get; }
    IReadOnlyList<NaceSection> NaceSections { get; }
    IReadOnlyList<TaxOfficeBranch> GetBranchesFor(int? cUfo);
}

public class CodeListService : ICodeListService
{
    public IReadOnlyList<TaxOffice> TaxOffices { get; }
    public IReadOnlyList<TaxOfficeBranch> TaxOfficeBranches { get; }
    public IReadOnlyList<NaceSection> NaceSections { get; }

    private readonly ILookup<int, TaxOfficeBranch> _branchesByUfo;

    public CodeListService()
    {
        var asm = typeof(CodeListService).Assembly;

        TaxOffices = ParseCsv(asm, "Isdocovac.Data.CodeLists.c_ufo.csv",
                cols => new TaxOffice(
                    int.Parse(cols[0]),
                    int.Parse(cols[1]),
                    cols[2],
                    cols[3]))
            .OrderBy(x => x.CUfo == 13 ? 999 : x.KUfoVema)
            .ToImmutableArray();

        TaxOfficeBranches = ParseCsv(asm, "Isdocovac.Data.CodeLists.c_pracufo.csv",
                cols => new TaxOfficeBranch(
                    int.Parse(cols[0]),
                    int.Parse(cols[1]),
                    cols[3],
                    cols[4]))
            .OrderBy(x => x.Nazev)
            .ToImmutableArray();

        NaceSections = ParseCsv(asm, "Isdocovac.Data.CodeLists.cz_nace_2025_sekce.csv",
                cols => new NaceSection(cols[0], cols[1]))
            .ToImmutableArray();

        _branchesByUfo = TaxOfficeBranches.ToLookup(b => b.CUfo);
    }

    public IReadOnlyList<TaxOfficeBranch> GetBranchesFor(int? cUfo)
    {
        if (cUfo == null) return Array.Empty<TaxOfficeBranch>();
        return _branchesByUfo[cUfo.Value].ToList();
    }

    private static IEnumerable<T> ParseCsv<T>(Assembly asm, string resourceName, Func<string[], T> factory)
    {
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        string? line;
        var isHeader = true;
        while ((line = reader.ReadLine()) != null)
        {
            if (isHeader) { isHeader = false; continue; }
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split(';');
            yield return factory(cols);
        }
    }
}
