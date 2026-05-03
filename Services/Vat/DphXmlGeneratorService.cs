using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Isdocovac.Models;
using Isdocovac.Models.Vat;

namespace Isdocovac.Services.Vat;

public interface IDphXmlGeneratorService
{
    string Generate(VatMonthlyReport report);
}

public class DphXmlGeneratorService : IDphXmlGeneratorService
{
    private const string VerzePis = "03.01.03";

    public string Generate(VatMonthlyReport r)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("Pisemnost",
                new XAttribute("nazevSW", "Isdocovac"),
                new XElement("DPHDP3",
                    new XAttribute("verzePis", VerzePis),
                    BuildVetaD(r),
                    BuildVetaP(r.OwnCompany),
                    BuildVeta1(r),
                    BuildVeta2(),
                    BuildVeta4(r),
                    BuildVeta6(r))));

        return Serialize(doc);
    }

    private static XElement BuildVetaD(VatMonthlyReport r)
    {
        return new XElement("VetaD",
            new XAttribute("dokument", "DP3"),
            new XAttribute("k_uladis", "DPH"),
            new XAttribute("dapdph_forma", "B"),
            new XAttribute("typ_platce", "P"),
            new XAttribute("trans", "A"),
            new XAttribute("c_okec", r.OwnCompany.OkecCode ?? string.Empty),
            new XAttribute("d_poddp", VatXmlFormat.Date(DateTime.UtcNow)),
            new XAttribute("rok", r.Year.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("mesic", r.Month.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildVetaP(Company c)
    {
        var element = new XElement("VetaP",
            new XAttribute("c_pracufo", c.TaxOfficeBranchCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            new XAttribute("c_ufo", c.TaxOfficeCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            new XAttribute("dic", c.Dic ?? string.Empty),
            new XAttribute("email", c.Email ?? string.Empty),
            new XAttribute("c_telef", c.Phone ?? string.Empty),
            new XAttribute("naz_obce", c.City ?? string.Empty),
            new XAttribute("psc", c.Zip ?? string.Empty),
            new XAttribute("stat", CountryName(c.CountryCode)),
            new XAttribute("c_pop", c.HouseNumber ?? string.Empty),
            new XAttribute("c_orient", c.OrientNumber ?? string.Empty));

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

    private static XElement BuildVeta1(VatMonthlyReport r)
    {
        return new XElement("Veta1",
            new XAttribute("obrat23", VatXmlFormat.DecimalWholeCzk(r.Obrat23)),
            new XAttribute("dan23", VatXmlFormat.DecimalWholeCzk(r.Dan23)),
            new XAttribute("obrat5", VatXmlFormat.DecimalWholeCzk(r.Obrat5)),
            new XAttribute("dan5", VatXmlFormat.DecimalWholeCzk(r.Dan5)),
            new XAttribute("p_sl23_e", VatXmlFormat.DecimalWholeCzk(r.PSl23Eu)),
            new XAttribute("dan_psl23_e", VatXmlFormat.DecimalWholeCzk(r.DanPSl23Eu)),
            new XAttribute("p_sl5_e", VatXmlFormat.DecimalWholeCzk(r.PSl5Eu)),
            new XAttribute("dan_psl5_e", VatXmlFormat.DecimalWholeCzk(r.DanPSl5Eu)),
            new XAttribute("p_sl23_z", VatXmlFormat.DecimalWholeCzk(r.PSl23NonEu)),
            new XAttribute("dan_psl23_z", VatXmlFormat.DecimalWholeCzk(r.DanPSl23NonEu)),
            new XAttribute("p_sl5_z", VatXmlFormat.DecimalWholeCzk(r.PSl5NonEu)),
            new XAttribute("dan_psl5_z", VatXmlFormat.DecimalWholeCzk(r.DanPSl5NonEu)),
            new XAttribute("rez_pren23", "0.0"),
            new XAttribute("dan_rpren23", "0.0"),
            new XAttribute("rez_pren5", "0.0"),
            new XAttribute("dan_rpren5", "0.0"));
    }

    private static XElement BuildVeta2()
    {
        return new XElement("Veta2",
            new XAttribute("dod_zb", "0.0"),
            new XAttribute("pln_sluzby", "0.0"),
            new XAttribute("pln_rez_pren", "0.0"),
            new XAttribute("pln_zaslani", "0.0"),
            new XAttribute("pln_ost", "0.0"));
    }

    private static XElement BuildVeta4(VatMonthlyReport r)
    {
        return new XElement("Veta4",
            new XAttribute("pln23", VatXmlFormat.DecimalWholeCzk(r.Pln23)),
            new XAttribute("odp_tuz23_nar", VatXmlFormat.DecimalWholeCzk(r.OdpTuz23)),
            new XAttribute("pln5", VatXmlFormat.DecimalWholeCzk(r.Pln5)),
            new XAttribute("odp_tuz5_nar", VatXmlFormat.DecimalWholeCzk(r.OdpTuz5)),
            new XAttribute("nar_zdp23", VatXmlFormat.DecimalWholeCzk(r.NarZdp23)),
            new XAttribute("od_zdp23", VatXmlFormat.DecimalWholeCzk(r.OdZdp23)),
            new XAttribute("nar_zdp5", VatXmlFormat.DecimalWholeCzk(r.NarZdp5)),
            new XAttribute("od_zdp5", VatXmlFormat.DecimalWholeCzk(r.OdZdp5)),
            new XAttribute("odp_sum_kr", "0"),
            new XAttribute("odp_sum_nar", VatXmlFormat.DecimalWholeCzk(r.OdpSumNar)));
    }

    private static XElement BuildVeta6(VatMonthlyReport r)
    {
        return new XElement("Veta6",
            new XAttribute("dano", "0"),
            new XAttribute("dano_no", VatXmlFormat.DecimalWholeCzk(r.DanoNo)),
            new XAttribute("dano_da", VatXmlFormat.DecimalWholeCzk(r.DanoDa)),
            new XAttribute("dan_zocelk", VatXmlFormat.DecimalWholeCzk(r.DanZocelk)),
            new XAttribute("odp_zocelk", VatXmlFormat.DecimalWholeCzk(r.OdpZocelk)));
    }

    private static string CountryName(string code) => code switch
    {
        "CZ" => "ČESKÁ REPUBLIKA",
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
