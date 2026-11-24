using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Invoice_Management_System.Activation
{
    /// <summary>Φόρμα ενεργοποίησης. Ανοίγει μέσα στο κύριο παράθυρο (InAppDialog).</summary>
    public partial class ActivationView : UserControl
    {
        private const string PermanentKey = "Y12Z-34AB-56CD-7894"; //  Παράδειγμα μόνιμου κωδικού

        private readonly AppDbContext _context;

        /// <summary>Καλείται όταν η άδεια ενεργοποιηθεί επιτυχώς.</summary>
        public event Action? Activated;

        public TextBox KeyBox => txtLicenseKey;

        public ActivationView(AppDbContext context)
        {
            InitializeComponent();
            _context = context;
        }

        private void ActivateLicense_Click(object sender, RoutedEventArgs e) => TryActivate();

        public void TryActivate()
        {
            string enteredKey = txtLicenseKey.Text.Trim();

            if (enteredKey == PermanentKey)
            {
                var license = _context.Licenses.FirstOrDefault();
                if (license != null)
                {
                    license.LicenseKey = enteredKey;
                    license.IsActivated = true;
                    _context.SaveChanges();
                    Activated?.Invoke();
                }
            }
            else
            {
                // Το σφάλμα εμφανίζεται κάτω από το πεδίο, με κόκκινο περίγραμμα, χωρίς επιπλέον παράθυρο
                errorRow.Visibility = Visibility.Visible;
                InputError.SetHasError(txtLicenseKey, true);
                txtLicenseKey.Focus();
                txtLicenseKey.SelectAll();
            }
        }

        private void LicenseKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errorRow.Visibility != Visibility.Visible) return;
            errorRow.Visibility = Visibility.Collapsed;
            InputError.SetHasError(txtLicenseKey, false);
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { /* χωρίς πρόγραμμα email: τίποτα */ }
            e.Handled = true;
        }
    }
}
