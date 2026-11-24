using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Markup;

namespace Invoice_Management_System.Localization
{
    /// <summary>
    /// Μεταφράσεις της εφαρμογής (Ελληνικά / English / Deutsch).
    /// XAML:  Text="{l:Tr Key}"      Κώδικας:  Loc.T("Key") ή Loc.T("Key", arg0, arg1…)
    /// Η αλλαγή γλώσσας ενημερώνει αμέσως όλα τα bindings και αποθηκεύεται για την επόμενη εκκίνηση.
    /// </summary>
    public sealed class Loc : INotifyPropertyChanged
    {
        // Lazy: δημιουργείται αφού έχουν αρχικοποιηθεί τα static πεδία παρακάτω (Codes, Strings)
        private static Loc? _instance;
        public static Loc Instance => _instance ??= new Loc();

        private static readonly string[] Codes = { "el", "en", "de" };
        private static readonly string[] CultureNames = { "el-GR", "en-GB", "de-CH" };

        private static readonly string SettingsFile = Path.Combine(DataLocation.DataFolder, "language.txt");

        private int _index;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? LanguageChanged;

        public string Code => Codes[_index];
        public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("el-GR");

        private Loc()
        {
            Apply(ReadSaved() ?? "el");
        }

        public string this[string key] => Strings.TryGetValue(key, out var t) ? t[_index] : key;

        public static string T(string key) => Instance[key];
        public static string T(string key, params object?[] args) => string.Format(Instance.Culture, Instance[key], args);

        public void SetLanguage(string code)
        {
            if (code == Code || Array.IndexOf(Codes, code) < 0) return;

            Apply(code);
            Save(code);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Apply(string code)
        {
            int i = Array.IndexOf(Codes, code);
            _index = i < 0 ? 0 : i;
            Culture = CultureInfo.GetCultureInfo(CultureNames[_index]);

            CultureInfo.CurrentCulture = Culture;
            CultureInfo.CurrentUICulture = Culture;
            CultureInfo.DefaultThreadCurrentCulture = Culture;
            CultureInfo.DefaultThreadCurrentUICulture = Culture;
        }

        private static string? ReadSaved()
        {
            try { return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).Trim() : null; }
            catch { return null; }
        }

        private static void Save(string code)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
                File.WriteAllText(SettingsFile, code);
            }
            catch { /* Η γλώσσα απλώς δεν θα θυμάται στην επόμενη εκκίνηση */ }
        }

        // ============================ Κείμενα: { Ελληνικά, English, Deutsch } ============================
        private static readonly Dictionary<string, string[]> Strings = new(StringComparer.Ordinal)
        {
            // ---- Sidebar ----
            ["Nav_Products"]       = new[] { "Προϊόντα", "Products", "Produkte" },
            ["Nav_Reports"]        = new[] { "Αναφορές", "Reports", "Berichte" },
            ["Nav_Print"]          = new[] { "Εκτύπωση", "Print", "Drucken" },
            ["Nav_SectionData"]    = new[] { "ΔΕΔΟΜΕΝΑ", "DATA", "DATEN" },
            ["Nav_ImportCsv"]      = new[] { "Εισαγωγή CSV", "Import CSV", "CSV importieren" },
            ["Nav_ExportCsv"]      = new[] { "Εξαγωγή CSV", "Export CSV", "CSV exportieren" },
            ["Nav_SectionSystem"]  = new[] { "ΣΥΣΤΗΜΑ", "SYSTEM", "SYSTEM" },
            ["Nav_Settings"]       = new[] { "Ρυθμίσεις", "Settings", "Einstellungen" },
            ["Nav_Updates"]        = new[] { "Ενημερώσεις", "Updates", "Updates" },
            ["Nav_About"]          = new[] { "Σχετικά", "About", "Über" },
            ["Menu_DeleteDb"]      = new[] { "Διαγραφή της βάσης δεδομένων", "Delete database", "Datenbank löschen" },
            ["Menu_About"]         = new[] { "Σχετικά με", "About Invoice Manager", "Über Invoice Manager" },
            ["Status_Activation"]  = new[] { "Κατάσταση ενεργοποίησης", "Activation status", "Aktivierungsstatus" },
            ["Status_Trial"]       = new[] { "Κατάσταση: Trial", "Status: Trial", "Status: Testversion" },
            ["Status_Activated"]   = new[] { "Κατάσταση: Ενεργοποιημένο", "Status: Activated", "Status: Aktiviert" },
            ["License_TrialActive"]= new[] { "Trial ενεργό", "Trial active", "Testversion aktiv" },
            ["License_DaysLeft"]   = new[] { "Απομένουν {0} από {1} ημέρες", "{0} of {1} days left", "Noch {0} von {1} Tagen" },
            ["License_Activated"]  = new[] { "Ενεργοποιημένο ✔", "Activated ✔", "Aktiviert ✔" },
            ["License_Full"]       = new[] { "Πλήρης άδεια χρήσης", "Full license", "Volllizenz" },
            ["License_Activate"]   = new[] { "Ενεργοποίηση →", "Activate →", "Aktivieren →" },
            ["Lang_Tooltip"]       = new[] { "Γλώσσα", "Language", "Sprache" },

            // ---- Header / KPI ----
            ["Header_Subtitle"]    = new[] { "Διαχείριση αποθέματος και τιμών", "Manage stock and prices", "Bestand und Preise verwalten" },
            ["Search_Placeholder"] = new[] { "Αναζήτηση προϊόντος, κωδικού…", "Search product, code…", "Produkt, Code suchen…" },
            ["Kpi_Count"]          = new[] { "Σύνολο προϊόντων", "Total products", "Produkte gesamt" },
            ["Kpi_Today"]          = new[] { "+{0} σήμερα", "+{0} today", "+{0} heute" },
            ["Kpi_Value"]          = new[] { "Αξία αποθέματος", "Stock value", "Lagerwert" },
            ["Kpi_Suppliers"]      = new[] { "{0} προμηθευτές", "{0} suppliers", "{0} Lieferanten" },
            ["Kpi_Low"]            = new[] { "Χαμηλό απόθεμα", "Low stock", "Niedriger Bestand" },
            ["Kpi_LowHint"]        = new[] { "κάτω από 5 τεμ.", "below 5 pcs", "unter 5 Stk." },
            ["Kpi_BySupplier"]     = new[] { "Αξία ανά προμηθευτή", "Value by supplier", "Wert nach Lieferant" },
            ["Donut_Suppliers"]    = new[] { "προμηθευτές", "suppliers", "Lieferanten" },
            ["Donut_Others"]       = new[] { "Λοιποί", "Others", "Andere" },

            // ---- Φόρμα ----
            ["Form_Title"]         = new[] { "Νέα καταχώρηση", "New entry", "Neuer Eintrag" },
            ["Form_Saved"]         = new[] { "✔ Οι αλλαγές αποθηκεύτηκαν", "✔ Changes saved", "✔ Änderungen gespeichert" },
            ["Field_Supplier"]     = new[] { "Προμηθευτής", "Supplier", "Lieferant" },
            ["Field_Code"]         = new[] { "Κωδικός", "Code", "Code" },
            ["Field_Name"]         = new[] { "Όνομα προϊόντος", "Product name", "Produktname" },
            ["Field_Stock"]        = new[] { "Απόθεμα", "Stock", "Bestand" },
            ["Field_Price"]        = new[] { "Τιμή (€)", "Price (€)", "Preis (€)" },
            ["Field_Total"]        = new[] { "Συνολική αξία (€)", "Total value (€)", "Gesamtwert (€)" },
            ["Ph_Price"]           = new[] { "0,00", "0.00", "0.00" },
            ["Btn_Add"]            = new[] { "Προσθήκη", "Add", "Hinzufügen" },
            ["Btn_Save"]           = new[] { "Αποθήκευση", "Save", "Speichern" },
            ["Btn_OK"]             = new[] { "OK", "OK", "OK" },
            ["Btn_Yes"]            = new[] { "Ναι", "Yes", "Ja" },
            ["Btn_No"]             = new[] { "Όχι", "No", "Nein" },
            ["Btn_Cancel"]         = new[] { "Άκυρο", "Cancel", "Abbrechen" },
            ["Btn_Close"]          = new[] { "Κλείσιμο (Esc)", "Close (Esc)", "Schliessen (Esc)" },
            ["Tip_Undo"]          = new[] { "Αναίρεση", "Undo", "Rückgängig" },
            ["Tip_Redo"]           = new[] { "Επαναφορά", "Redo", "Wiederholen" },

            // ---- Πίνακας ----
            ["List_Title"]         = new[] { "Λίστα προϊόντων", "Product list", "Produktliste" },
            ["Chip_All"]           = new[] { "Όλα", "All", "Alle" },
            ["Chip_Today"]         = new[] { "Σήμερα", "Today", "Heute" },
            ["Chip_Yesterday"]     = new[] { "Χθες", "Yesterday", "Gestern" },
            ["Chip_Date"]          = new[] { "Ημερομηνία", "Date", "Datum" },
            ["Chip_Last"]          = new[] { "Τελευταία", "Latest", "Letzter" },
            ["Col_Supplier"]       = new[] { "ΠΡΟΜΗΘΕΥΤΗΣ", "SUPPLIER", "LIEFERANT" },
            ["Col_Code"]           = new[] { "ΚΩΔΙΚΟΣ", "CODE", "CODE" },
            ["Col_Name"]           = new[] { "ΟΝΟΜΑ ΠΡΟΪΟΝΤΟΣ", "PRODUCT NAME", "PRODUKTNAME" },
            ["Col_Stock"]          = new[] { "ΑΠΟΘΕΜΑ", "STOCK", "BESTAND" },
            ["Col_Price"]          = new[] { "ΤΙΜΗ", "PRICE", "PREIS" },
            ["Col_Total"]          = new[] { "ΣΥΝΟΛΙΚΗ ΑΞΙΑ", "TOTAL VALUE", "GESAMTWERT" },
            ["Badge_Low"]          = new[] { "χαμηλό", "low", "niedrig" },
            ["Tip_Delete"]         = new[] { "Διαγραφή", "Delete", "Löschen" },

            // ---- Τίτλοι παραθύρων μηνυμάτων ----
            ["Title_Error"]        = new[] { "Σφάλμα", "Error", "Fehler" },
            ["Title_Warning"]      = new[] { "Προειδοποίηση", "Warning", "Warnung" },
            ["Title_Done"]         = new[] { "Ολοκλήρωση", "Done", "Fertig" },
            ["Title_Success"]      = new[] { "Επιτυχία", "Success", "Erfolg" },
            ["Title_History"]      = new[] { "Ιστορικό", "History", "Verlauf" },
            ["Title_ConfirmDelete"]= new[] { "Επιβεβαίωση διαγραφής", "Confirm deletion", "Löschen bestätigen" },

            // ---- Σχετικά ----
            ["About_Title"]        = new[] { "Πληροφορίες", "About", "Info" },

            // ---- Ενημερώσεις ----
            ["Upd_Title"]          = new[] { "Έλεγχος ενημέρωσης", "Update check", "Update-Prüfung" },
            ["Upd_NoVersion"]      = new[] { "Δεν βρέθηκε έκδοση ενημέρωσης.", "No update version found.", "Keine Update-Version gefunden." },
            ["Upd_Latest"]         = new[] { "Η εφαρμογή είναι ήδη στην τελευταία έκδοση ({0}).", "You already have the latest version ({0}).", "Sie verwenden bereits die neueste Version ({0})." },
            ["Upd_AvailableTitle"] = new[] { "Διαθέσιμη ενημέρωση", "Update available", "Update verfügbar" },
            ["Upd_Available"] = new[]
            {
                "Η έκδοση {0} είναι διαθέσιμη (τρέχουσα: {1}).\n\nΤα δεδομένα σου φυλάσσονται ξεχωριστά και δεν επηρεάζονται από την ενημέρωση. Η εφαρμογή θα κλείσει και θα ανοίξει ξανά.\n\nΝα γίνει η ενημέρωση τώρα;",
                "Version {0} is available (current: {1}).\n\nYour data is stored separately and is not affected by the update. The app will close and reopen.\n\nUpdate now?",
                "Version {0} ist verfügbar (aktuell: {1}).\n\nIhre Daten werden separat gespeichert und vom Update nicht berührt. Die App wird geschlossen und neu gestartet.\n\nJetzt aktualisieren?"
            },
            ["Upd_NoFile"]         = new[] { "Δεν βρέθηκε το αρχείο ενημέρωσης.", "The update file was not found.", "Die Update-Datei wurde nicht gefunden." },
            ["Upd_CheckError"]     = new[] { "Σφάλμα κατά τον έλεγχο ενημερώσεων:\n{0}", "Error while checking for updates:\n{0}", "Fehler bei der Suche nach Updates:\n{0}" },
            ["Upd_NoUpdater"]      = new[] { "Δεν βρέθηκε το αρχείο Updater.exe!", "Updater.exe was not found!", "Updater.exe wurde nicht gefunden!" },
            ["Upd_InstallError"]   = new[] { "Σφάλμα κατά την ενημέρωση:\n{0}", "Error during update:\n{0}", "Fehler beim Update:\n{0}" },

            // ---- Εκτύπωση ----
            ["Print_Title"]        = new[] { "Τιμολόγιο", "Invoice", "Rechnung" },

            // ---- Διαγραφή / αποθήκευση ----
            ["DeleteAll_Confirm"]  = new[] { "Όλα τα προϊόντα θα διαγραφούν οριστικά. Κάνε πρώτα εξαγωγή CSV αν θέλεις αντίγραφο.", "All products will be deleted permanently. Export to CSV first if you want a copy.", "Alle Produkte werden endgültig gelöscht. Exportieren Sie zuerst als CSV, wenn Sie eine Kopie möchten." },
            ["DeleteAll_Done"]     = new[] { "Όλα τα δεδομένα διαγράφηκαν!", "All data has been deleted!", "Alle Daten wurden gelöscht!" },
            ["Delete_Confirm"]     = new[] { "Το «{0}» θα διαγραφεί από τη λίστα. Μπορείς να το επαναφέρεις με Αναίρεση.", "“{0}” will be removed from the list. You can bring it back with Undo.", "„{0}“ wird aus der Liste entfernt. Mit Rückgängig können Sie es wiederherstellen." },
            ["Save_Error"]         = new[] { "Σφάλμα κατά την αποθήκευση: {0}", "Error while saving: {0}", "Fehler beim Speichern: {0}" },
            ["Invalid_Input"]      = new[] { "Εισάγετε έγκυρα στοιχεία!", "Please enter valid data!", "Bitte gültige Daten eingeben!" },
            ["Undo_None"]          = new[] { "Δεν υπάρχει ενέργεια για αναίρεση.", "Nothing to undo.", "Nichts zum Rückgängigmachen." },
            ["Redo_None"]          = new[] { "Δεν υπάρχει ενέργεια για επαναφορά.", "Nothing to redo.", "Nichts zum Wiederholen." },
            ["Edit_Title"]         = new[] { "Επεξεργασία προϊόντος", "Edit product", "Produkt bearbeiten" },
            ["Edit_Prompt"]        = new[] { "Τροποποιήστε τα δεδομένα και πατήστε 'OK' για αποθήκευση.", "Edit the data and press 'OK' to save.", "Daten bearbeiten und mit 'OK' speichern." },
            ["Edit_Error"]         = new[] { "Σφάλμα κατά την ενημέρωση: {0}", "Error while updating: {0}", "Fehler beim Aktualisieren: {0}" },

            // ---- CSV ----
            ["Csv_Filter"]         = new[] { "Αρχεία CSV (*.csv)|*.csv", "CSV files (*.csv)|*.csv", "CSV-Dateien (*.csv)|*.csv" },
            ["Csv_OpenTitle"]      = new[] { "Επιλέξτε αρχείο CSV", "Select a CSV file", "CSV-Datei auswählen" },
            ["Csv_SaveTitle"]      = new[] { "Αποθήκευση ως CSV", "Save as CSV", "Als CSV speichern" },
            ["Csv_SkipIncomplete"] = new[] { "Παράλειψη γραμμής λόγω ελλιπών δεδομένων: {0}", "Skipping line with missing data: {0}", "Zeile mit fehlenden Daten übersprungen: {0}" },
            ["Csv_SkipNegative"]   = new[] { "Παράλειψη γραμμής με αρνητική τιμή ή απόθεμα: {0}", "Skipping line with negative price or stock: {0}", "Zeile mit negativem Preis oder Bestand übersprungen: {0}" },
            ["Csv_LineError"]      = new[] { "Σφάλμα κατά την ανάγνωση της γραμμής:\n{0}\n\n{1}", "Error reading line:\n{0}\n\n{1}", "Fehler beim Lesen der Zeile:\n{0}\n\n{1}" },
            ["Csv_ImportResult"]   = new[] { "🟢 Ενημερώθηκαν: {0}\n🟡 Προστέθηκαν: {1}", "🟢 Updated: {0}\n🟡 Added: {1}", "🟢 Aktualisiert: {0}\n🟡 Hinzugefügt: {1}" },
            ["Csv_ImportTitle"]    = new[] { "Ολοκλήρωση εισαγωγής", "Import complete", "Import abgeschlossen" },
            ["Csv_ExportDone"]     = new[] { "Η εξαγωγή ολοκληρώθηκε!", "Export complete!", "Export abgeschlossen!" },

            // ---- Άδεια ----
            ["Trial_ExpiredTitle"] = new[] { "Λήξη Trial", "Trial expired", "Testversion abgelaufen" },
            ["Trial_Expired"]      = new[] { "Το Trial έχει λήξει! Παρακαλώ εισάγετε κωδικό ενεργοποίησης.", "The trial has expired! Please enter an activation code.", "Die Testversion ist abgelaufen! Bitte geben Sie einen Aktivierungscode ein." },
            ["Trial_Shutdown"]     = new[] { "Το πρόγραμμα θα τερματιστεί.", "The application will now close.", "Die Anwendung wird jetzt beendet." },

            // ---- Ιστορικό ----
            ["Hist_SelectDate"]    = new[] { "Επιλογή ημερομηνίας", "Select date", "Datum wählen" },
            ["Hist_NoToday"]       = new[] { "Δεν υπάρχουν καταχωρίσεις για σήμερα.", "No entries for today.", "Keine Einträge für heute." },
            ["Hist_NoYesterday"]   = new[] { "Δεν υπάρχουν καταχωρίσεις για χθες.", "No entries for yesterday.", "Keine Einträge für gestern." },
            ["Hist_NoDate"]        = new[] { "Δεν υπάρχουν καταχωρίσεις για την επιλεγμένη ημερομηνία.", "No entries for the selected date.", "Keine Einträge für das gewählte Datum." },
            ["Hist_NoProducts"]    = new[] { "Δεν υπάρχουν προϊόντα.", "There are no products.", "Keine Produkte vorhanden." },
            ["Hist_Last"] = new[]
            {
                "🗓 Τελευταία καταχώρηση:\n\n🏷 Προμηθευτής: {0}\n📌 Όνομα: {1}\n🔢 Κωδικός: {2}\n📦 Απόθεμα: {3}\n💰 Τιμή: {4:0.00} €\n📅 Ημερομηνία: {5:g}",
                "🗓 Latest entry:\n\n🏷 Supplier: {0}\n📌 Name: {1}\n🔢 Code: {2}\n📦 Stock: {3}\n💰 Price: {4:0.00} €\n📅 Date: {5:g}",
                "🗓 Letzter Eintrag:\n\n🏷 Lieferant: {0}\n📌 Name: {1}\n🔢 Code: {2}\n📦 Bestand: {3}\n💰 Preis: {4:0.00} €\n📅 Datum: {5:g}"
            },

            // ---- Αναφορές ----
            ["Reports_Title"]      = new[] { "Αναφορές εξόδων", "Expense reports", "Ausgabenberichte" },
            ["Rep_Supplier"]       = new[] { "ΠΡΟΜΗΘΕΥΤΗΣ", "SUPPLIER", "LIEFERANT" },
            ["Rep_Product"]        = new[] { "ΠΡΟΪΟΝ", "PRODUCT", "PRODUKT" },
            ["Rep_Total"]          = new[] { "ΣΥΝΟΛΟ", "TOTAL", "SUMME" },

            // ---- Ενεργοποίηση ----
            ["Act_Title"]          = new[] { "Ενεργοποίηση", "Activation", "Aktivierung" },
            ["Act_Prompt"]         = new[] { "Εισάγετε τον κωδικό ενεργοποίησης:", "Enter your activation code:", "Aktivierungscode eingeben:" },
            ["Act_Button"]         = new[] { "Ενεργοποίηση", "Activate", "Aktivieren" },
            ["Act_Success"]        = new[] { "Η άδεια ενεργοποιήθηκε με επιτυχία!", "The license was activated successfully!", "Die Lizenz wurde erfolgreich aktiviert!" },
            ["Act_Invalid"]        = new[] { "Λάθος κωδικός ενεργοποίησης", "Invalid activation code", "Ungültiger Aktivierungscode" },
            ["Act_Subtitle"]       = new[] { "Πληκτρολόγησε τον κωδικό που έλαβες με email.", "Enter the code you received by email.", "Geben Sie den Code ein, den Sie per E-Mail erhalten haben." },
            ["Act_Label"]          = new[] { "Κωδικός ενεργοποίησης", "Activation code", "Aktivierungscode" },
            ["Act_NeedCode"]       = new[] { "Χρειάζεσαι κωδικό; info@netronium.ch", "Need a code? info@netronium.ch", "Code benötigt? info@netronium.ch" },

            // ---- Έκδοση 1.1 ----
            ["Brand_By"]           = new[] { "by Netronium", "by Netronium", "by Netronium" },
            ["Nav_NewBadge"]       = new[] { "Νέα", "New", "Neu" },
            ["Kbd_Search"]         = new[] { "Ctrl F", "Ctrl F", "Strg F" },
            ["List_Count"]         = new[] { "{0} προϊόντα", "{0} products", "{0} Produkte" },
            ["List_Total"]         = new[] { "Σύνολο", "Total", "Summe" },
            ["License_ActivateLink"] = new[] { "Ενεργοποίηση άδειας →", "Activate license →", "Lizenz aktivieren →" },
            ["Btn_YesDelete"]      = new[] { "Ναι, διαγραφή", "Yes, delete", "Ja, löschen" },
            ["Title_DeleteProduct"]= new[] { "Διαγραφή προϊόντος;", "Delete product?", "Produkt löschen?" },
            ["Title_DeleteAll"]    = new[] { "Διαγραφή όλων των δεδομένων;", "Delete all data?", "Alle Daten löschen?" },
            ["Rep_Subtitle"]       = new[] { "Αξία αποθέματος ανά προμηθευτή και προϊόν", "Stock value by supplier and product", "Lagerwert nach Lieferant und Produkt" },
            ["Rep_Subtotal"]       = new[] { "Υποσύνολο", "Subtotal", "Zwischensumme" },
            ["Rep_GrandTotal"]     = new[] { "Γενικό σύνολο", "Grand total", "Gesamtsumme" },
            ["Rep_Empty"]          = new[] { "Δεν υπάρχουν προϊόντα ακόμα.", "No products yet.", "Noch keine Produkte." },
            ["Cal_Prev"]           = new[] { "Προηγούμενος μήνας", "Previous month", "Vorheriger Monat" },
            ["Cal_Next"]           = new[] { "Επόμενος μήνας", "Next month", "Nächster Monat" },
            ["Cal_Marked"]         = new[] { "Μέρες με καταχωρίσεις", "Days with entries", "Tage mit Einträgen" },
            ["About_Version"]      = new[] { "Έκδοση {0} · Swiss Made", "Version {0} · Swiss Made", "Version {0} · Swiss Made" },
            ["About_Developer"]    = new[] { "Ανάπτυξη", "Developer", "Entwicklung" },
            ["About_Email"]        = new[] { "Email", "Email", "E-Mail" },
            ["About_Files"]        = new[] { "Αρχεία", "Files", "Dateien" },
            ["About_FilesValue"]   = new[] { "CSV (εισαγωγή & εξαγωγή)", "CSV (import & export)", "CSV (Import & Export)" },
            ["About_Storage"]      = new[] { "Αποθήκευση", "Storage", "Speicherung" },
            ["About_StorageValue"] = new[] { "Τοπική βάση SQLite", "Local SQLite database", "Lokale SQLite-Datenbank" },
            ["About_DataFolder"]   = new[] { "Δεδομένα", "Data", "Daten" },
        };
    }

    /// <summary>XAML: {l:Tr Key} ή {l:Tr Key, Prefix='▦   '}</summary>
    [MarkupExtensionReturnType(typeof(object))]
    public class TrExtension : MarkupExtension
    {
        public TrExtension() { }
        public TrExtension(string key) { Key = key; }

        [ConstructorArgument("key")]
        public string Key { get; set; } = "";

        /// <summary>Σταθερό κείμενο πριν τη μετάφραση (π.χ. εικονίδιο).</summary>
        public string? Prefix { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
            if (!string.IsNullOrEmpty(Prefix))
                binding.Converter = new PrefixConverter(Prefix);
            return binding.ProvideValue(serviceProvider);
        }

        private sealed class PrefixConverter : IValueConverter
        {
            private readonly string _prefix;
            public PrefixConverter(string prefix) => _prefix = prefix;
            public object Convert(object value, Type t, object p, CultureInfo c) => _prefix + value;
            public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
        }
    }
}
