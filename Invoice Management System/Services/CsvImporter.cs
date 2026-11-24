using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Invoice_Management_System
{
    /// <summary>Πρόβλημα σε μια γραμμή του CSV (η γραμμή παραλείπεται).</summary>
    public sealed record CsvProblem(int LineNumber, string Kind, string Value);

    public sealed class CsvImportResult
    {
        public int LinesRead { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        /// <summary>Μόνο τα πρώτα <see cref="CsvImporter.MaxProblems"/>, για να εμφανιστούν στον χρήστη.</summary>
        public List<CsvProblem> Problems { get; } = new();
    }

    /// <summary>
    /// Εισαγωγή CSV χωρίς UI, ώστε να τρέχει σε background thread και να δοκιμάζεται χωρίς την εφαρμογή.
    ///
    /// Μορφή: Προμηθευτής;Κωδικός;Όνομα;Απόθεμα;Τιμή[;Συνολική αξία]  (διαχωριστικό ; ή , ή Tab, αναγνωρίζεται αυτόματα)
    /// Αριθμοί: δέχεται και «48,41» και «48.41» (και «1.234,56», «1,234.56», «1'234.56», «€»).
    /// Ίδιο προϊόν = ίδιος Κωδικός + Όνομα: η ποσότητα προστίθεται και η τιμή γίνεται σταθμισμένος μέσος όρος.
    /// </summary>
    public static class CsvImporter
    {
        public const int MaxProblems = 50;

        public static CsvImportResult Import(string path, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            var result = new CsvImportResult();
            var groups = new Dictionary<(string Code, string Name), Group>();
            char delimiter = ';';
            int lineNo = 0;
            bool first = true;

            foreach (var raw in File.ReadLines(path, Encoding.UTF8))
            {
                ct.ThrowIfCancellationRequested();
                lineNo++;
                if (lineNo % 10_000 == 0) progress?.Report(lineNo);

                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (first)
                {
                    first = false;
                    delimiter = DetectDelimiter(raw);
                    // Γραμμή επικεφαλίδων; (η 4η και 5η στήλη δεν είναι αριθμοί)
                    var h = Split(raw, delimiter);
                    if (h.Count < 5 || !TryParseNumber(h[3], out _) || !TryParseNumber(h[4], out _))
                        continue;
                }

                result.LinesRead++;
                var v = Split(raw, delimiter);
                if (v.Count < 5) { Skip(result, lineNo, "columns", raw); continue; }

                string supplier = v[0].Trim(), code = v[1].Trim(), name = v[2].Trim();
                if (code.Length == 0 || name.Length == 0) { Skip(result, lineNo, "empty", raw); continue; }
                if (!TryParseNumber(v[3], out decimal qty)) { Skip(result, lineNo, "number", v[3]); continue; }
                if (!TryParseNumber(v[4], out decimal price)) { Skip(result, lineNo, "number", v[4]); continue; }
                if (qty < 0 || price < 0) { Skip(result, lineNo, "negative", raw); continue; }

                if (!groups.TryGetValue((code, name), out var g))
                    groups[(code, name)] = g = new Group();
                g.Supplier = supplier;          // ο τελευταίος προμηθευτής του αρχείου (όπως πριν)
                g.Quantity += qty;
                g.Value += qty * price;
                g.LastPrice = price;
            }
            progress?.Report(lineNo);

            // Μία εγγραφή στη βάση ανά προϊόν (όχι ανά γραμμή)
            using var db = new AppDbContext();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var existing = db.Products.AsTracking().ToList()
                             .GroupBy(p => (p.ProductCode ?? "", p.Name ?? ""))
                             .ToDictionary(x => x.Key, x => x.First());

            foreach (var ((code, name), g) in groups)
            {
                if (existing.TryGetValue((code, name), out var p))
                {
                    decimal totalQty = p.Stock + g.Quantity;
                    decimal totalValue = p.Stock * p.Price + g.Value;
                    p.Stock = totalQty;
                    p.Price = totalQty > 0 ? Math.Round(totalValue / totalQty, 4) : p.Price;
                    if (g.Supplier.Length > 0) p.Supplier = g.Supplier;
                    db.Entry(p).State = EntityState.Modified;
                    result.Updated++;
                }
                else
                {
                    db.Products.Add(new Product
                    {
                        Supplier = g.Supplier,
                        ProductCode = code,
                        Name = name,
                        Stock = g.Quantity,
                        Price = g.Quantity > 0 ? Math.Round(g.Value / g.Quantity, 4) : g.LastPrice
                    });
                    result.Added++;
                }
            }
            db.SaveChanges();
            return result;
        }

        private sealed class Group
        {
            public string Supplier = "";
            public decimal Quantity;
            public decimal Value;
            public decimal LastPrice;
        }

        private static void Skip(CsvImportResult r, int line, string kind, string value)
        {
            r.Skipped++;
            if (r.Problems.Count < MaxProblems)
                r.Problems.Add(new CsvProblem(line, kind, value.Length > 80 ? value[..80] + "…" : value));
        }

        private static char DetectDelimiter(string header)
        {
            int semi = header.Count(c => c == ';'), tab = header.Count(c => c == '\t'), comma = header.Count(c => c == ',');
            if (semi >= tab && semi >= comma && semi > 0) return ';';
            if (tab >= comma && tab > 0) return '\t';
            return comma > 0 ? ',' : ';';
        }

        /// <summary>Χωρίζει μια γραμμή, σεβόμενο τιμές σε εισαγωγικά ("Α;Β").</summary>
        public static List<string> Split(string line, char delimiter)
        {
            var parts = new List<string>(6);
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == delimiter && !quoted) { parts.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            parts.Add(sb.ToString());
            return parts;
        }

        /// <summary>
        /// Αριθμός με τελεία ή κόμμα ως υποδιαστολή, ανεξάρτητα από τη γλώσσα των Windows/της εφαρμογής.
        /// Αν υπάρχουν και τα δύο, το τελευταίο είναι η υποδιαστολή. Ένα μόνο σύμβολο, μία φορά = υποδιαστολή.
        /// Το ίδιο σύμβολο πολλές φορές = διαχωριστικό χιλιάδων.
        /// </summary>
        public static bool TryParseNumber(string? text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var s = new StringBuilder(text.Length);
            foreach (char c in text.Trim())
                if (c != '€' && c != ' ' && c != ' ' && c != ' ' && c != '\'' && c != '’') s.Append(c);
            string t = s.ToString();
            if (t.Length == 0) return false;

            int dots = t.Count(c => c == '.'), commas = t.Count(c => c == ',');
            if (dots > 0 && commas > 0)
            {
                char dec = t.LastIndexOf('.') > t.LastIndexOf(',') ? '.' : ',';
                char grp = dec == '.' ? ',' : '.';
                t = t.Replace(grp.ToString(), "").Replace(dec, '.');
            }
            else if (commas == 1) t = t.Replace(',', '.');
            else if (commas > 1) t = t.Replace(",", "");
            else if (dots > 1) t = t.Replace(".", "");

            return decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out value);
        }
    }
}
