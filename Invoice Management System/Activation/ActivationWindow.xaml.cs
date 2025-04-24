using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Invoice_Management_System.Activation;

namespace Invoice_Management_System.Activation
{
    public partial class ActivationWindow : Window
    {
        private readonly AppDbContext _context;

        public ActivationWindow(AppDbContext context)
        {
            InitializeComponent();
            _context = context;
        }

        private void ActivateLicense_Click(object sender, RoutedEventArgs e)
        {
            string enteredKey = txtLicenseKey.Text.Trim();

            if (enteredKey == "Y12Z-34AB-56CD-7894") //  Παράδειγμα μόνιμου κωδικού
            {
                var license = _context.Licenses.FirstOrDefault();
                if (license != null)
                {
                    license.LicenseKey = enteredKey; //  Να επιτρέπεται η αλλαγή
                    license.IsActivated = true; //  Να υπάρχει η ιδιότητα
                    _context.SaveChanges();

                    MessageBox.Show("Η άδεια ενεργοποιήθηκε με επιτυχία!", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (Application.Current.Windows.OfType<MainWindow>().FirstOrDefault() is MainWindow mainWindow)
                    {
                        mainWindow.activationStatusMenuItem.Header = "Κατάσταση: Ενεργοποιημένο";
                    }

                    this.Close();
                }
            }
            else
            {
                MessageBox.Show("Λάθος κωδικός ενεργοποίησης!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    } 
}
