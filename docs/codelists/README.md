# Číselníky Finanční správy ČR + CZ-NACE

## c_ufo.csv — Finanční úřady (15 položek)

Hlavní číselník používaný v EPO / XML podáních. Jedna položka na kraj + Specializovaný FÚ.

Sloupce:
- `c_ufo` — technický kód pro XML (např. 451 pro FÚ pro hl. m. Prahu, 13 pro SFÚ)
- `k_ufo_vema` — kód ÚFO VEMA (4místný, zobrazovaný ve formuláři EPO)
- `nazev` — oficiální název
- `kraj`, `kraj_nuts` — kraj a NUTS3 kód
- `matrika_uctu` — matriková část bankovního účtu FÚ u ČNB
- `cnb_banka` — kód ČNB (vždy 0710)

Skladba celého BÚ = `<předčíslí dle typu daně>-<matrika>/0710`.

## c_pracufo.csv — Územní pracoviště FÚ (~200 položek)

Sloupce:
- `c_pracufo` — 4místný kód územního pracoviště pro XML (např. 2001 = Praha 1, 2120 = Příbram, 4000 = SFÚ)
- `c_ufo` — odkaz na mateřský FÚ v c_ufo.csv
- `k_ufo_vema` — kód mateřského FÚ ve VEMA číselníku
- `nazev`, `kraj`

V XML EPO je c_pracufo nepovinné (podstatné je c_ufo), ale pokud se uvádí, 
pro SFÚ je to vždy `4000`.

**Pozor**: Finanční správa od 2023 postupně optimalizuje síť (vyhl. 189/2023 Sb.).
Některá pracoviště mají omezený provoz, ale KÓDY zůstávají platné pro elektronická podání.
Autoritativní zdroj: https://financnisprava.gov.cz/cs/financni-sprava/financni-sprava-cr/organizacni-struktura/organy-financni-spravy/uzemni-pracoviste

## cz_nace_2025_sekce.csv — CZ-NACE 2025, úroveň 1 (sekce, 22 položek)

### Co je CZ-NACE

CZ-NACE = česká verze evropské statistické klasifikace ekonomických činností 
(NACE = *Nomenclature statistique des Activités économiques dans la Communauté Européenne*).
V ČR platí od 1. 1. 2008, nahradila OKEČ. Správcem je ČSÚ.

Používá se pro:
- statistická šetření (Registr ekonomických subjektů – RES)
- daňové účely (FÚ eviduje hlavní ekonomickou činnost)
- živnostenské oprávnění a předmět podnikání
- veřejné zakázky a dotace (GBER, kategorie veřejné podpory)
- bankovnictví, AML, rizikové skóring

### Struktura (5 úrovní)

| Úroveň | Název | Formát kódu | Počet |
|--------|-------|-------------|-------|
| 1 | Sekce | písmeno (A–V) | 22 |
| 2 | Oddíl | XX (2 číslice) | ~88 |
| 3 | Skupina | XX.X | ~272 |
| 4 | Třída | XX.XX (mezinárodní NACE) | ~629 |
| 5 | Podtřída | XX.XX.X (česká úroveň) | ~99 (2025) |

Sekce NENÍ součástí číselného kódu — kód 62.01 je software engineering, 
sekce se dopočítá z oddílu.

### CZ-NACE 2008 vs 2025 (hlavní změny)

- Sekce J (Informační a komunikační činnosti) se rozdělila na:
  - **J** — Vydavatelské činnosti, vysílání, tvorba obsahu
  - **K** — Telekomunikace, programování, IT infrastruktura a ostatní informační činnosti
- Všechny původní sekce K–U se posunuly o jedno písmeno dolů (K→L, L→M, … U→V)
- Oddíl 45 (prodej/opravy motorových vozidel) byl zrušen — činnosti přesunuty do 46, 47 a 95

Duální kódování v RES bude udržováno cca do konce 2028.

### Kde stáhnout kompletní 5-úrovňový číselník

- **ČSÚ (oficiální XLSX, doporučeno):** 
  https://csu.gov.cz/klasifikace-ekonomickych-cinnosti-cz-nace-platna-od-1-1-2025
- **Eurostat RAMON / Europa (mezinárodní NACE Rev. 2.1):** 
  https://ec.europa.eu/eurostat/ramon/
- **Otevřená data ČSÚ (JSON-LD, SKOS):** 
  https://data.gov.cz — hledat „CZ-NACE“
- **ARES (API):** https://ares.gov.cz/stranky/vyvojar-info — API vrací CZ-NACE kód v odpovědích

## Formát CSV

- Kódování: **UTF-8 bez BOM**
- Oddělovač: **středník (;)** — kompatibilní s Excelem v české lokalizaci
- Řádky: LF

## Zdroje

- Finanční správa – podpora EPO: https://podpora.mojedane.gov.cz/cs/seznam-okruhu/rozhrani-pro-treti-strany/informace-k-ciselniku-ufo-platnem-od-1-1-4382
- Zákon č. 456/2011 Sb., o Finanční správě ČR
- Vyhláška č. 189/2023 Sb., o územních pracovištích FÚ
- Sdělení ČSÚ č. 400/2024 Sb. (CZ-NACE 2025)
- Nařízení Komise v přenesené pravomoci (EU) 2023/137 (NACE Rev. 2.1)
