using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace Invoice_Management_System
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<License> Licenses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DataLocation.DatabasePath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.Entity<License>().ToTable("Licenses");
        }
    }

    /// <summary>
    /// Πού αποθηκεύονται τα δεδομένα του χρήστη.
    ///
    /// Μέχρι την 1.0.20 η βάση ήταν δίπλα στο .exe. Ο Updater αποσυμπιέζει κάθε ενημέρωση πάνω σε εκείνο
    /// τον φάκελο, οπότε μια ενημέρωση μπορούσε να αντικαταστήσει τη βάση του χρήστη. Από την 1.1.0 η βάση ζει στο
    /// %LocalAppData%\Netronium\Invoice Manager\, που οι ενημερώσεις δεν αγγίζουν ποτέ.
    /// </summary>
    public static class DataLocation
    {
        public const string DatabaseFileName = "InvoiceDB.db";

        /// <summary>
        /// %LocalAppData%\Netronium\Invoice Manager, εκτός αν οριστεί η μεταβλητή περιβάλλοντος
        /// INVOICEMANAGER_DATA_DIR (για δοκιμές, ώστε να μην αγγίζονται τα πραγματικά δεδομένα).
        /// </summary>
        public static string DataFolder { get; } =
            Environment.GetEnvironmentVariable("INVOICEMANAGER_DATA_DIR") is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Netronium", "Invoice Manager");

        /// <summary>Η παλιά θέση (φάκελος εγκατάστασης).</summary>
        public static string LegacyDatabasePath { get; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);

        private static string? _databasePath;

        public static string DatabasePath => _databasePath ??= Resolve();

        /// <summary>
        /// Την πρώτη φορά μετά την ενημέρωση αντιγράφει την παλιά βάση στο AppData.
        /// Η παλιά μένει στη θέση της ως αντίγραφο ασφαλείας και δεν χρησιμοποιείται πια.
        /// Αν η αντιγραφή αποτύχει, η εφαρμογή συνεχίζει με την παλιά θέση ώστε να μη «χαθούν» δεδομένα.
        /// </summary>
        private static string Resolve()
        {
            string newPath = Path.Combine(DataFolder, DatabaseFileName);
            try
            {
                Directory.CreateDirectory(DataFolder);

                if (!File.Exists(newPath) && File.Exists(LegacyDatabasePath))
                {
                    // Αντιγραφή σε προσωρινό όνομα και μετά μετονομασία: δεν μένει ποτέ μισή βάση
                    string temp = newPath + ".copying";
                    File.Copy(LegacyDatabasePath, temp, overwrite: true);
                    File.Move(temp, newPath);

                    // Σημάδι ώστε να φαίνεται ότι η παλιά βάση έχει μεταφερθεί
                    try
                    {
                        File.WriteAllText(Path.Combine(DataFolder, "migrated-from.txt"),
                            $"{LegacyDatabasePath}{Environment.NewLine}{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    }
                    catch { /* μόνο πληροφοριακό */ }
                }
                return newPath;
            }
            catch
            {
                return File.Exists(LegacyDatabasePath) ? LegacyDatabasePath : newPath;
            }
        }
    }
}
