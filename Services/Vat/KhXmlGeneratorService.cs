using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Isdocovac.Models;
using Isdocovac.Models.Vat;

namespace Isdocovac.Services.Vat;

public interface IKhXmlGeneratorService
{
    string Generate(VatMonthlyReport report);
}

public class KhXmlGeneratorService : IKhXmlGeneratorService
{
    private const string VerzePis = "03.01.14";

    public string Generate(VatMonthlyReport r)
    {
        var dphkh = new XElement("DPHKH1",
            new XAttribute("verzePis", VerzePis),
            BuildVetaD(r),
            BuildVetaP(r.OwnCompany));

        foreach (var row in r.A2Rows)
        {
            dphkh.Add(BuildVetaA2(row));
        }
        foreach (var row in r.A4Rows)
        {
            dphkh.Add(BuildVetaA4(row));
        }
        // VetaA5 is always written (even with zeros, matches Fakturoid sample).
        dphkh.Add(BuildVetaA5(r.A5Aggregate));

        foreach (var row in r.B2Rows)
        {
            dphkh.Add(BuildVetaB2(row));
        }
        if (r.B3Aggregate.HasAnyValue)
        {
            dphkh.Add(BuildVetaB3(r.B3Aggregate));
        }

        dphkh.Add(BuildVetaC(r));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("Pisemnost",
                new XAttribute("nazevSW", "Isdocovac"),
                dphkh));

        return Serialize(doc);
    }

    private static XElement BuildVetaD(VatMonthlyReport r)
    {
        return new XElement("VetaD",
            new XAttribute("dokument", "KH1"),
            new XAttribute("k_uladis", "DPH"),
            new XAttribute("khdph_forma", "B"),
            new XAttribute("d_poddp", VatXmlFormat.Date(DateTime.UtcNow)),
            new XAttribute("rok", r.Year.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("mesic", r.Month.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildVetaP(Contact c)
    {
        var element = new XElement("VetaP",
            new XAttribute("c_orient", c.OrientNumber ?? string.Empty),
            new XAttribute("c_pop", c.HouseNumber ?? string.Empty),
            new XAttribute("c_pracufo", c.TaxOfficeBranchCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            new XAttribute("c_ufo", c.TaxOfficeCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            new XAttribute("dic", c.Dic ?? string.Empty),
            new XAttribute("c_telef", c.Phone ?? string.Empty),
            new XAttribute("email", c.Email ?? string.Empty),
            new XAttribute("id_dats", c.DataBoxId ?? string.Empty),
            new XAttribute("naz_obce", c.City ?? string.Empty),
            new XAttribute("psc", c.Zip ?? string.Empty),
            new XAttribute("stat", CountryName(c.CountryCode)));

        if (!string.IsNullOrWhiteSpace(c.Street))
        {
            element.Add(new XAttribute("ulice", c.Street));
        }

        if (c.IsLegalEntity)
        {
            element.Add(new XAttribute("zkrobchjm", c.CompanyName ?? string.Empty));
            element.Add(new XAttribute("typ_ds", "P"));
        }
        else
        {
            element.Add(new XAttribute("jmeno", c.FirstName ?? string.Empty));
            element.Add(new XAttribute("prijmeni", c.LastName ?? string.Empty));
            element.Add(new XAttribute("titul", c.Titul ?? string.Empty));
            element.Add(new XAttribute("typ_ds", "F"));
        }

        return element;
    }

    private static XElement BuildVetaA2(KhA2Row row)
    {
        return new XElement("VetaA2",
            new XAttribute("c_evid_dd", row.EvidenceNumber),
            new XAttribute("dan1", VatXmlFormat.Decimal(row.Dan23)),
            new XAttribute("dan2", VatXmlFormat.Decimal(row.Dan5)),
            new XAttribute("dppd", VatXmlFormat.Date(row.TaxableSupplyDate)),
            new XAttribute("k_stat", row.CountryCode),
            new XAttribute("vatid_dod", row.VatId ?? string.Empty),
            new XAttribute("zakl_dane1", VatXmlFormat.Decimal(row.Base23)),
            new XAttribute("zakl_dane2", VatXmlFormat.Decimal(row.Base5)));
    }

    private static XElement BuildVetaA4(KhA4Row row)
    {
        return new XElement("VetaA4",
            new XAttribute("c_evid_dd", row.EvidenceNumber),
            new XAttribute("dan1", VatXmlFormat.Decimal(row.Dan23)),
            new XAttribute("dan2", VatXmlFormat.Decimal(row.Dan5)),
            new XAttribute("dic_odb", row.ClientDic ?? string.Empty),
            new XAttribute("dppd", VatXmlFormat.Date(row.TaxableSupplyDate)),
            new XAttribute("kod_rezim_pl", "0"),
            new XAttribute("zakl_dane1", VatXmlFormat.Decimal(row.Base23)),
            new XAttribute("zakl_dane2", VatXmlFormat.Decimal(row.Base5)),
            new XAttribute("zdph_44", "N"));
    }

    private static XElement BuildVetaA5(KhAggregateRow row)
    {
        return new XElement("VetaA5",
            new XAttribute("dan1", VatXmlFormat.Decimal(row.Dan23)),
            new XAttribute("dan2", VatXmlFormat.Decimal(row.Dan5)),
            new XAttribute("dan3", "0.0"),
            new XAttribute("zakl_dane1", VatXmlFormat.Decimal(row.Base23)),
            new XAttribute("zakl_dane2", VatXmlFormat.Decimal(row.Base5)),
            new XAttribute("zakl_dane3", "0.0"));
    }

    private static XElement BuildVetaB2(KhB2Row row)
    {
        return new XElement("VetaB2",
            new XAttribute("c_evid_dd", row.EvidenceNumber),
            new XAttribute("dan1", VatXmlFormat.Decimal(row.Dan23)),
            new XAttribute("dan2", VatXmlFormat.Decimal(row.Dan5)),
            new XAttribute("dic_dod", row.SupplierDic ?? string.Empty),
            new XAttribute("dppd", VatXmlFormat.Date(row.TaxableSupplyDate)),
            new XAttribute("zakl_dane1", VatXmlFormat.Decimal(row.Base23)),
            new XAttribute("zakl_dane2", VatXmlFormat.Decimal(row.Base5)),
            new XAttribute("zdph_44", "N"));
    }

    private static XElement BuildVetaB3(KhAggregateRow row)
    {
        return new XElement("VetaB3",
            new XAttribute("dan1", VatXmlFormat.Decimal(row.Dan23)),
            new XAttribute("dan2", VatXmlFormat.Decimal(row.Dan5)),
            new XAttribute("dan3", "0.0"),
            new XAttribute("zakl_dane1", VatXmlFormat.Decimal(row.Base23)),
            new XAttribute("zakl_dane2", VatXmlFormat.Decimal(row.Base5)),
            new XAttribute("zakl_dane3", "0.0"));
    }

    private static XElement BuildVetaC(VatMonthlyReport r)
    {
        return new XElement("VetaC",
            new XAttribute("celk_zd_a2", VatXmlFormat.Decimal(r.CelkZdA2)),
            new XAttribute("obrat23", VatXmlFormat.Decimal(r.KhObrat23)),
            new XAttribute("obrat5", VatXmlFormat.Decimal(r.KhObrat5)),
            new XAttribute("pln23", VatXmlFormat.Decimal(r.KhPln23)),
            new XAttribute("pln5", VatXmlFormat.Decimal(r.KhPln5)),
            new XAttribute("pln_rez_pren", "0"),
            new XAttribute("rez_pren23", "0"),
            new XAttribute("rez_pren5", "0"));
    }

    private static string CountryName(string code) => code switch
    {
        "CZ" => "Česká republika",
        _ => code,
    };

    private static string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
        };

        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
