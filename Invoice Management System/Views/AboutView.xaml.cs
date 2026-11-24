using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Invoice_Management_System
{
    /// <summary>«Σχετικά με»: στοιχεία Netronium, έκδοση και φάκελος δεδομένων.</summary>
    public partial class AboutView : UserControl
    {
        public Button OkButton => okButton;

        public AboutView()
        {
            InitializeComponent();
            txtDataFolder.Text = DataLocation.DataFolder;
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { /* χωρίς πρόγραμμα email: τίποτα */ }
            e.Handled = true;
        }
    }
}
