using System;
using System.Linq;
using System.Windows;


namespace Invoice_Management_System
{
    public partial class ReportsWindow : Window
    {
        private readonly AppDbContext _context = new AppDbContext();

        public ReportsWindow()
        {
            InitializeComponent();
            LoadReports();
        }

        private void OpenReports_Click(object sender, RoutedEventArgs e)
        {
            ReportsWindow reportsWindow = new ReportsWindow();
            reportsWindow.ShowDialog();
        }




        private void LoadReports()
        {
            var reports = _context.Products
                .AsEnumerable() // ✅ Μεταφέρουμε τα δεδομένα στη μνήμη για να λειτουργήσει το GroupBy
                .GroupBy(p => new { p.Supplier, p.Name }) // Χρησιμοποιούμε το σωστό όνομα ιδιότητας για το προϊόν
                .Select(g => new
                {
                    Προμηθευτής = g.Key.Supplier,
                    Προϊόν = g.Key.Name, // Αντικαθιστούμε το ProductName με το Name
                    Σύνολο = Math.Round(g.Sum(p => p.Stock * p.Price), 2).ToString("0.00 €")
                })
                .OrderBy(r => r.Προμηθευτής) // Ομαδοποιούμε ανά προμηθευτή
                .ThenBy(r => r.Προϊόν) // και προϊόν
                .ToList();

            reportGrid.ItemsSource = reports;
        }



    }
}
