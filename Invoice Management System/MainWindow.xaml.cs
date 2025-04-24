using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using Invoice_Management_System.Activation;
using Invoice_Management_System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Invoice_Management_System.Models;
using System.IO.Compression;
using System.Reflection;
using Microsoft.EntityFrameworkCore;


using UndoAction = Invoice_Management_System.Models.UndoAction;







namespace Invoice_Management_System
{



    public partial class MainWindow : Window
    {
        private readonly AppDbContext _context = new AppDbContext();

        private Stack<UndoAction> undoStack = new();
        private Stack<UndoAction> redoStack = new();


        private const string RepoOwner = "Dev0ps41";
        private const string RepoName = "InvoiceManagementSystemUpdates";
        private const string LatestReleaseUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        private static readonly string CurrentVersion =
            System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ProductVersion;




        public MainWindow()
        {
            InitializeComponent();
            CheckLicense(); // Έλεγχος trial/permanent
            LoadProducts();
            LoadAutoCompleteData();
            DataContext = this; //  Επιτρέπει την αυτόματη σύνδεση των δεδομένων

        }



        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "request");

                    string json = await client.GetStringAsync(LatestReleaseUrl);
                    JObject release = JObject.Parse(json);
                    string latestVersion = release["tag_name"]?.ToString()?.Trim().ToLower().Replace("v", "");

                    if (string.IsNullOrWhiteSpace(latestVersion))
                    {
                        MessageBox.Show("Δεν βρέθηκε έκδοση ενημέρωσης.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (latestVersion == CurrentVersion.ToLower())
                    {
                        MessageBox.Show($"Η εφαρμογή είναι ήδη στην τελευταία έκδοση ({CurrentVersion}).", "Έλεγχος Ενημέρωσης", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 🔔 Νέα ειδοποίηση για backup
                    var result = MessageBox.Show(
                        $"Νέα έκδοση {latestVersion} είναι διαθέσιμη (τρέχουσα: {CurrentVersion}).\n\n" +
                        "⚠️ Πριν προχωρήσεις, **συνιστάται να κάνεις εξαγωγή της βάσης δεδομένων** (CSV) από το μενού.\n\n" +
                        "Θέλεις να συνεχίσεις με την ενημέρωση;",
                        "Διαθέσιμη Ενημέρωση",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        string downloadUrl = release["assets"]?[0]?["browser_download_url"]?.ToString();
                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            await DownloadAndInstallUpdate(downloadUrl);
                        }
                        else
                        {
                            MessageBox.Show("Δεν βρέθηκε το αρχείο ενημέρωσης.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Σφάλμα κατά τον έλεγχο ενημερώσεων:\n" + ex.Message, "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }








        private async Task DownloadAndInstallUpdate(string downloadUrl)
        {
            try
            {
                string tempZipPath = Path.Combine(Path.GetTempPath(), "update.zip");
                string installPath = AppDomain.CurrentDomain.BaseDirectory;

                // Κατεβάζουμε το ZIP αρχείο
                using (HttpClient client = new HttpClient())
                {
                    byte[] data = await client.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(tempZipPath, data);
                }

                // Εκκίνηση του Updater.exe με δικαιώματα διαχειριστή
                string updaterExe = Path.Combine(installPath, "Updater", "Updater.exe");

                if (!File.Exists(updaterExe))
                {
                    MessageBox.Show("Δεν βρέθηκε το αρχείο Updater.exe!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = updaterExe,
                    Arguments = $"\"{tempZipPath}\" \"{installPath}\"",
                    UseShellExecute = true,
                    Verb = "runas" // ζητά δικαιώματα διαχειριστή
                };

                Process.Start(psi);

                Application.Current.Shutdown(); // Κλείσιμο κύριας εφαρμογής
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα κατά την ενημέρωση:\n{ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }








        private void LoadProducts()
        {
            var products = _context.Products.ToList();
            productGrid.ItemsSource = products;

            if (products.Any()) // Αν υπάρχουν προϊόντα, ενημέρωσε τα πεδία εισαγωγής
            {
                var lastProduct = products.Last(); // Παίρνουμε το τελευταίο προϊόν που προστέθηκε
                txtSupplier.Text = lastProduct.Supplier;
                txtProductCode.Text = lastProduct.ProductCode;
                txtName.Text = lastProduct.Name;
                txtStock.Text = lastProduct.Stock.ToString();
                txtPrice.Text = lastProduct.Price.ToString("0.00");
            }
        }


        public ObservableCollection<string> SupplierSuggestions { get; set; } = new();
        public ObservableCollection<string> ProductCodeSuggestions { get; set; } = new();
        public ObservableCollection<string> ProductNameSuggestions { get; set; } = new();

        private void LoadAutoCompleteData()
        {
            SupplierSuggestions.Clear();
            ProductCodeSuggestions.Clear();
            ProductNameSuggestions.Clear();

            var products = _context.Products.ToList();

            foreach (var product in products)
            {
                if (!SupplierSuggestions.Contains(product.Supplier))
                    SupplierSuggestions.Add(product.Supplier);

                if (!ProductCodeSuggestions.Contains(product.ProductCode))
                    ProductCodeSuggestions.Add(product.ProductCode);

                if (!ProductNameSuggestions.Contains(product.Name))
                    ProductNameSuggestions.Add(product.Name);
            }
        }





        // Εμφάνιση πληροφοριών εφαρμογής
        private void ShowInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                $"📌 Διαχείριση Τιμολογίων\n\n" +
                $"🛠 Ανάπτυξη: Christos Zioulos\n" +
                $"🛠 Email: christos.zioulos@gmail.com\n" +
                $"📅 Έκδοση: {CurrentVersion} Swiss Made\n" +
                $"📂 Υποστηριζόμενα αρχεία: CSV\n" +
                $"💾 Αποθήκευση: Βάση δεδομένων SQL Server\n\n" +
                $"🔹 Μπορείς να εισάγεις, να επεξεργαστείς και να διαγράψεις προϊόντα.\n" +
                $"🔹 Υποστηρίζεται εισαγωγή & εξαγωγή αρχείων CSV.\n\n" +
                $"✨ Ευχαριστούμε που χρησιμοποιείτε την εφαρμογή! ✨",
                "Πληροφορίες", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //  Eκτύπωση
        private void PrintInvoice_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateFlowDocument();
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, "Invoice Print");
            }
        }

        // Δημιουργία FlowDocument για την εκτύπωση
        private FlowDocument CreateFlowDocument()
        {
            FlowDocument doc = new FlowDocument();
            doc.ColumnWidth = 370; // Ρύθμιση πλάτους εκτύπωσης
            doc.PagePadding = new Thickness(50);

            // Τίτλος του Invoice
            Paragraph title = new Paragraph(new Bold(new Run("Τιμολόγιο")))
            {
                FontSize = 12,
                TextAlignment = TextAlignment.Center
            };
            doc.Blocks.Add(title);

            // Προσθήκη των 6 πεδίων
            Table table = new Table();
            table.CellSpacing = 10;
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });

            // Προσθήκη Header στη λίστα
            TableRowGroup headerGroup = new TableRowGroup();
            TableRow headerRow = new TableRow();
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Προμηθευτής")))));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Κωδικός")))));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Όνομα Προϊόντος")))));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Απόθεμα")))));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Τιμή (€)")))));
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Συνολική Αξία (€)")))));
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);

            // Προσθήκη Δεδομένων από το DataGrid
            TableRowGroup dataGroup = new TableRowGroup();
            foreach (var item in productGrid.Items)
            {
                if (item is Product product)
                {
                    TableRow row = new TableRow();
                    row.Cells.Add(new TableCell(new Paragraph(new Run(product.Supplier))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(product.ProductCode))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(product.Name))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(product.Stock.ToString("0.##")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(product.Price.ToString("€0.00")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(product.TotalValue.ToString("€0.00")))));
                    dataGroup.Rows.Add(row);
                }
            }
            table.RowGroups.Add(dataGroup);

            doc.Blocks.Add(table);
            return doc;
        }

        // Διαγραφή όλων των προϊόντων
        private void DeleteAllData_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Είσαι σίγουρος ότι θέλεις να διαγράψεις όλα τα δεδομένα;", "Επιβεβαίωση Διαγραφής",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _context.Products.RemoveRange(_context.Products); // Διαγραφή όλων των προϊόντων
                _context.SaveChanges();
                LoadProducts(); // Επαναφόρτωση του DataGrid
                MessageBox.Show("Όλα τα δεδομένα διαγράφηκαν!", "Ολοκλήρωση", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private void ImportCSV_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Επιλέξτε αρχείο CSV"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string[] lines = File.ReadAllLines(openFileDialog.FileName, System.Text.Encoding.UTF8);
                List<Product> products = new List<Product>();
                var existingProducts = _context.Products.ToList();
                var greekCulture = new System.Globalization.CultureInfo("el-GR");

                foreach (string line in lines.Skip(1)) // Παράκαμψη επικεφαλίδας
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split(';');

                    if (values.Length < 5)
                    {
                        MessageBox.Show($"Παράλειψη γραμμής λόγω ελλιπών δεδομένων: {line}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    try
                    {
                        string supplier = values[0].Trim();
                        string code = values[1].Trim();
                        string name = values[2].Trim();
                        decimal stock = decimal.Parse(values[3].Trim().Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);


                        // Χωρίς αντικατάσταση , με parsing ελληνικού format
                        string priceString = values[4].Trim().Replace("€", "");
                        decimal price = decimal.Parse(priceString, greekCulture);

                        if (stock < 0 || price < 0)
                        {
                            MessageBox.Show($"Παράλειψη γραμμής με αρνητική τιμή ή απόθεμα: {line}", "Προειδοποίηση", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }

                        bool exists = existingProducts.Any(p => p.ProductCode == code && p.Name == name);
                        if (exists)
                        {
                            MessageBox.Show($"Το προϊόν \"{name}\" με κωδικό \"{code}\" υπάρχει ήδη. Παράλειψη.", "Προειδοποίηση", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }

                        var product = new Product
                        {
                            Supplier = supplier,
                            ProductCode = code,
                            Name = name,
                            Stock = stock,
                            Price = price
                        };

                        products.Add(product);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Σφάλμα κατά την ανάγνωση της γραμμής:\n{line}\n\n{ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                if (products.Count > 0)
                {
                    _context.Products.AddRange(products);
                    _context.SaveChanges();
                    LoadProducts();

                    MessageBox.Show($"Επιτυχής εισαγωγή {products.Count} προϊόντων!", "Ολοκλήρωση", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Δεν βρέθηκαν έγκυρα ή νέα δεδομένα στο αρχείο.", "Προσοχή", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                ClearFields(); // 🔸 Καθαρισμός πεδίων μετά την εισαγωγή CSV

            }
        }









        //  Εξαγωγή σε CSV
        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Αποθήκευση ως CSV"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var products = _context.Products.ToList();

                // UTF-8 with BOM για σωστή συμβατότητα με Excel
                var utf8WithBom = new System.Text.UTF8Encoding(true);

                using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, utf8WithBom))
                {
                    // Επικεφαλίδες στα ελληνικά
                    writer.WriteLine("Προμηθευτής;Κωδικός;Όνομα Προϊόντος;Απόθεμα;Τιμή (€);Συνολική Αξία (€)");

                    var greekCulture = new System.Globalization.CultureInfo("el-GR");

                    foreach (var product in products)
                    {
                        string supplier = product.Supplier;
                        string code = product.ProductCode;
                        string name = product.Name;
                        string stock = product.Stock.ToString("0.##");
                        string price = product.Price.ToString("0.00", greekCulture); // Με κόμμα
                        string total = (product.Stock * product.Price).ToString("0.00", greekCulture); // Με κόμμα

                        writer.WriteLine($"{supplier};{code};{name};{stock};{price};{total}");
                    }
                }

                MessageBox.Show("Η εξαγωγή ολοκληρώθηκε!", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }





        private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                if (comboBox.Foreground == Brushes.Gray)
                {
                    comboBox.Text = "";
                    comboBox.Foreground = Brushes.Black;
                }
            }
        }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                if (string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    if (comboBox == txtSupplier) comboBox.Text = "Προμηθευτής";
                    else if (comboBox == txtProductCode) comboBox.Text = "Κωδικός";
                    else if (comboBox == txtName) comboBox.Text = "Όνομα Προϊόντος";

                    comboBox.Foreground = Brushes.Gray;
                }
            }
        }


        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Foreground == Brushes.Gray)
            {
                textBox.Text = "";
                textBox.Foreground = Brushes.Black;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (textBox == txtStock) textBox.Text = "Απόθεμα";
                else if (textBox == txtPrice) textBox.Text = "Τιμή";

                textBox.Foreground = Brushes.Gray;
            }
        }



        //  Επεξεργασία προϊόντος
        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (productGrid.SelectedItem is Product selectedProduct)
            {
                txtSupplier.Text = selectedProduct.Supplier;
                txtProductCode.Text = selectedProduct.ProductCode;
                txtName.Text = selectedProduct.Name;
                txtStock.Text = selectedProduct.Stock.ToString();
                txtPrice.Text = selectedProduct.Price.ToString();

                if (MessageBox.Show("Τροποποιήστε τα δεδομένα και πατήστε 'OK' για αποθήκευση.",
                                    "Επεξεργασία Προϊόντος", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                {
                    try
                    {
                        selectedProduct.Supplier = txtSupplier.Text;
                        selectedProduct.ProductCode = txtProductCode.Text;
                        selectedProduct.Name = txtName.Text;
                        selectedProduct.Stock = decimal.Parse(txtStock.Text);
                        selectedProduct.Price = decimal.Parse(txtPrice.Text);

                        _context.Products.Update(selectedProduct);
                        _context.SaveChanges();
                        LoadProducts();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Σφάλμα κατά την ενημέρωση: " + ex.Message, "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void productGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (e.Row.Item is Product updatedProduct)
                    {
                        try
                        {
                            // Αντιγραφή αρχικών τιμών πριν την αλλαγή
                            var existingProduct = _context.Products
                                .AsNoTracking() // Πολύ σημαντικό για να πάρεις snapshot πριν αλλάξει
                                .FirstOrDefault(p => p.Id == updatedProduct.Id);

                            if (existingProduct != null)
                            {
                                // Πιέζουμε στην undoStack πριν γίνει SaveChanges
                                undoStack.Push(new UndoAction
                                {
                                    ActionType = UndoType.Update,
                                    ProductBefore = new Product
                                    {
                                        Id = existingProduct.Id,
                                        Supplier = existingProduct.Supplier,
                                        ProductCode = existingProduct.ProductCode,
                                        Name = existingProduct.Name,
                                        Stock = existingProduct.Stock,
                                        Price = existingProduct.Price
                                    },
                                    ProductAfter = new Product
                                    {
                                        Id = updatedProduct.Id,
                                        Supplier = updatedProduct.Supplier,
                                        ProductCode = updatedProduct.ProductCode,
                                        Name = updatedProduct.Name,
                                        Stock = updatedProduct.Stock,
                                        Price = updatedProduct.Price
                                    }
                                });

                                redoStack.Clear(); // Κάθε νέα αλλαγή καθαρίζει το redo
                            }

                            _context.Products.Update(updatedProduct);
                            _context.SaveChanges();

                            Dispatcher.InvokeAsync(async () =>
                            {
                                await Task.Delay(3000);
                                saveStatusText.Visibility = Visibility.Collapsed;
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Σφάλμα κατά την αποθήκευση: " + ex.Message, "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                });
            }
        }



        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _context.SaveChanges();
                saveStatusText.Visibility = Visibility.Visible;

                // Απόκρυψη μετά από 2 δευτερόλεπτα
                Task.Delay(2000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => saveStatusText.Visibility = Visibility.Collapsed);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Σφάλμα κατά την αποθήκευση: " + ex.Message);
            }
        }



        //  Διαγραφή προϊόντος
        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (productGrid.SelectedItem is Product selectedProduct)
            {
                if (MessageBox.Show($"Θέλεις να διαγράψεις το προϊόν {selectedProduct.Name};",
                                    "Επιβεβαίωση Διαγραφής", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    undoStack.Push(new UndoAction
                    {
                        ActionType = UndoType.Delete,
                        ProductBefore = new Product
                        {
                            Id = selectedProduct.Id,
                            Supplier = selectedProduct.Supplier,
                            ProductCode = selectedProduct.ProductCode,
                            Name = selectedProduct.Name,
                            Stock = selectedProduct.Stock,
                            Price = selectedProduct.Price
                        }
                    });

                    redoStack.Clear(); // καθαρίζουμε το redo stack

                    _context.Products.Remove(selectedProduct);
                    _context.SaveChanges();
                    LoadProducts();
                }
            }
        }






        // Αναζήτηση προϊόντος (διορθωμένο για EF Core)
        private void SearchProduct(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearch.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                productGrid.ItemsSource = _context.Products.ToList();
                return;
            }

            var filteredProducts = _context.Products
                .AsEnumerable() // <<< Διορθώνει το θέμα με το ToString()
                .Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains(searchText)) ||
                    (!string.IsNullOrEmpty(p.ProductCode) && p.ProductCode.ToLower().Contains(searchText)) ||
                    (!string.IsNullOrEmpty(p.Supplier) && p.Supplier.ToLower().Contains(searchText)) ||
                    p.Stock.ToString().Contains(searchText) ||
                    p.Price.ToString("0.00").Contains(searchText))
                .ToList();

            productGrid.ItemsSource = filteredProducts;
        }






        private void ClearFields()
        {
            txtSupplier.Text = "";
            txtProductCode.Text = "";
            txtName.Text = "";
            txtStock.Text = "";
            txtPrice.Text = "";

            txtSupplier.SelectedIndex = -1;
            txtProductCode.SelectedIndex = -1;
            txtName.SelectedIndex = -1;

            txtSupplier.Focus(); //  Τοποθετούμε τον κέρσορα στο πρώτο πεδίο
        }

        private void OpenReports_Click(object sender, RoutedEventArgs e)
        {
            ReportsWindow reportsWindow = new ReportsWindow();
            reportsWindow.Show();
        }



        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSupplier.Text) ||
                string.IsNullOrWhiteSpace(txtProductCode.Text) ||
                string.IsNullOrWhiteSpace(txtName.Text) ||
                !decimal.TryParse(txtStock.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal stock) ||
                !decimal.TryParse(txtPrice.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
            {
                MessageBox.Show("Εισάγετε έγκυρα στοιχεία!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var existingProduct = _context.Products.FirstOrDefault(p =>
                p.ProductCode == txtProductCode.Text && p.Name == txtName.Text);

            if (existingProduct != null)
            {
                // ⏪ Βήμα 1: Push σε Undo πριν γίνει αλλαγή
                undoStack.Push(new UndoAction
                {
                    ActionType = UndoType.Update,
                    ProductBefore = new Product
                    {
                        Id = existingProduct.Id,
                        Supplier = existingProduct.Supplier,
                        ProductCode = existingProduct.ProductCode,
                        Name = existingProduct.Name,
                        Stock = existingProduct.Stock,
                        Price = existingProduct.Price
                    },
                    ProductAfter = new Product
                    {
                        Id = existingProduct.Id,
                        Supplier = existingProduct.Supplier,
                        ProductCode = existingProduct.ProductCode,
                        Name = existingProduct.Name,
                        Stock = existingProduct.Stock + stock,
                        Price = ((existingProduct.Price * existingProduct.Stock) + (price * stock)) / (existingProduct.Stock + stock)
                    }
                });

                // Τροποποίηση προϊόντος
                existingProduct.Stock += stock;
                decimal newTotalValue = stock * price;
                existingProduct.Price = ((existingProduct.Price * (existingProduct.Stock - stock)) + newTotalValue) / existingProduct.Stock;

                _context.SaveChanges();
                LoadProducts();
            }
            else
            {
                var newProduct = new Product
                {
                    Supplier = txtSupplier.Text,
                    ProductCode = txtProductCode.Text,
                    Name = txtName.Text,
                    Stock = stock,
                    Price = price
                };

                _context.Products.Add(newProduct);
                _context.SaveChanges();

                undoStack.Push(new UndoAction
                {
                    ActionType = UndoType.Add,
                    ProductBefore = newProduct
                });

                redoStack.Clear();
                LoadProducts();
            }
            ClearFields(); // 🔹 Καθαρίζει τα πεδία μετά την εισαγωγή

        }

        private void UndoLastAction_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("Δεν υπάρχει ενέργεια για αναίρεση.");
                return;
            }

            var lastAction = undoStack.Pop();

            switch (lastAction.ActionType)
            {
                case UndoType.Add:
                    var addedEntry = _context.Products.FirstOrDefault(p =>
                        p.ProductCode == lastAction.ProductBefore.ProductCode &&
                        p.Name == lastAction.ProductBefore.Name);
                    if (addedEntry != null)
                    {
                        _context.Products.Remove(addedEntry);
                        _context.SaveChanges();

                        redoStack.Push(new UndoAction
                        {
                            ActionType = UndoType.Delete,
                            ProductBefore = lastAction.ProductBefore
                        });
                    }
                    break;

                case UndoType.Delete:
                    _context.Products.Add(lastAction.ProductBefore);
                    _context.SaveChanges();

                    redoStack.Push(new UndoAction
                    {
                        ActionType = UndoType.Add,
                        ProductBefore = lastAction.ProductBefore
                    });
                    break;

                case UndoType.Update:
                    var productToUpdate = _context.Products.FirstOrDefault(p => p.Id == lastAction.ProductBefore.Id);
                    if (productToUpdate != null && lastAction.ProductAfter != null)
                    {
                        // Αποθηκεύουμε την "τρέχουσα" κατάσταση για την επαναφορά (Redo)
                        var currentState = new Product
                        {
                            Id = productToUpdate.Id,
                            Supplier = productToUpdate.Supplier,
                            ProductCode = productToUpdate.ProductCode,
                            Name = productToUpdate.Name,
                            Stock = productToUpdate.Stock,
                            Price = productToUpdate.Price
                        };

                        // Εφαρμόζουμε την "παλιά" κατάσταση (Undo)
                        productToUpdate.Supplier = lastAction.ProductBefore.Supplier;
                        productToUpdate.ProductCode = lastAction.ProductBefore.ProductCode;
                        productToUpdate.Name = lastAction.ProductBefore.Name;
                        productToUpdate.Stock = lastAction.ProductBefore.Stock;
                        productToUpdate.Price = lastAction.ProductBefore.Price;

                        _context.Products.Update(productToUpdate);
                        _context.SaveChanges();

                        redoStack.Push(new UndoAction
                        {
                            ActionType = UndoType.Update,
                            ProductBefore = lastAction.ProductBefore, // Δηλαδή ξαναγυρίζουμε σε αυτό
                            ProductAfter = currentState                // Από αυτό
                        });
                    }
                    break;
            }

            LoadProducts();
        }






        // Redo
        private void RedoLastAction_Click(object sender, RoutedEventArgs e)
        {
            if (redoStack.Count == 0)
            {
                MessageBox.Show("Δεν υπάρχει ενέργεια για επαναφορά.");
                return;
            }

            var lastRedo = redoStack.Pop();

            switch (lastRedo.ActionType)
            {
                case UndoType.Add:
                    _context.Products.Add(lastRedo.ProductBefore);
                    _context.SaveChanges();

                    undoStack.Push(lastRedo); // Επαναφέρει στο Undo
                    break;

                case UndoType.Delete:
                    var toDelete = _context.Products.FirstOrDefault(p =>
                        p.ProductCode == lastRedo.ProductBefore.ProductCode &&
                        p.Name == lastRedo.ProductBefore.Name);
                    if (toDelete != null)
                    {
                        _context.Products.Remove(toDelete);
                        _context.SaveChanges();
                        undoStack.Push(lastRedo);
                    }
                    break;

                case UndoType.Update:
                    var toUpdate = _context.Products.FirstOrDefault(p => p.Id == lastRedo.ProductBefore.Id);
                    if (toUpdate != null && lastRedo.ProductAfter != null)
                    {
                        // Επαναφορά σε ProductAfter (το νέο που είχε γίνει πριν την αναίρεση)
                        toUpdate.Supplier = lastRedo.ProductAfter.Supplier;
                        toUpdate.ProductCode = lastRedo.ProductAfter.ProductCode;
                        toUpdate.Name = lastRedo.ProductAfter.Name;
                        toUpdate.Stock = lastRedo.ProductAfter.Stock;
                        toUpdate.Price = lastRedo.ProductAfter.Price;

                        _context.Products.Update(toUpdate);
                        _context.SaveChanges();

                        // Πιέζει την αντίστροφη ενέργεια στο Undo για επόμενη αναίρεση
                        undoStack.Push(new UndoAction
                        {
                            ActionType = UndoType.Update,
                            ProductBefore = lastRedo.ProductAfter,
                            ProductAfter = lastRedo.ProductBefore
                        });
                    }
                    break;
            }

            LoadProducts();
        }









        // Άνοιγμα παραθύρου εισαγωγής κωδικού
        private void OpenActivationWindow()
        {
            OpenActivationWindow(this, new RoutedEventArgs()); //  Καλούμε τη μέθοδο με σωστά ορίσματα
        }

        private void OpenActivationWindow(object sender, RoutedEventArgs e)
        {
            ActivationWindow activationWindow = new ActivationWindow(_context);
            activationWindow.ShowDialog();
        }




        private void CheckLicense()
        {
            var license = _context.Licenses.FirstOrDefault();

            if (license == null)
            {
                license = new License
                {
                    LicenseKey = "TRIAL",
                    ActivationDate = DateTime.Now,
                    IsActivated = false
                };

                _context.Licenses.Add(license);
                _context.SaveChanges();
            }

            if (!license.IsActivated)
            {
                if ((DateTime.Now - license.ActivationDate).TotalDays > 14)
                {
                    MessageBox.Show("Το Trial έχει λήξει! Παρακαλώ εισάγετε κωδικό ενεργοποίησης.", "Λήξη Trial", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OpenActivationWindow();

                    // Ελέγχει ξανά αν το πρόγραμμα ενεργοποιήθηκε
                    license = _context.Licenses.FirstOrDefault();

                    if (license == null || !license.IsActivated)
                    {
                        MessageBox.Show("Το πρόγραμμα θα τερματιστεί.", "Λήξη Trial", MessageBoxButton.OK, MessageBoxImage.Error);
                        Application.Current.Shutdown();
                    }
                }

                activationStatusMenuItem.Header = "Κατάσταση: Trial";
            }

        }

        // Καταχωρίσεις ΣΗΜΕΡΑ
        private void ShowTodayProducts_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var results = _context.Products
                .Where(p => p.CreatedAt.Date == today)
                .ToList();

            productGrid.ItemsSource = results;

            if (!results.Any())
                MessageBox.Show("Δεν υπάρχουν καταχωρίσεις για σήμερα.", "Ιστορικό", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Καταχωρίσεις ΧΘΕΣ
        private void ShowYesterdayProducts_Click(object sender, RoutedEventArgs e)
        {
            var yesterday = DateTime.Today.AddDays(-1);
            var results = _context.Products
                .Where(p => p.CreatedAt.Date == yesterday)
                .ToList();

            productGrid.ItemsSource = results;

            if (!results.Any())
                MessageBox.Show("Δεν υπάρχουν καταχωρίσεις για χθες.", "Ιστορικό", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Καταχωρίσεις βάσει ημερομηνίας
        private void ShowProductsByDate_Click(object sender, RoutedEventArgs e)
        {
            var datePicker = new System.Windows.Controls.DatePicker { SelectedDate = DateTime.Today };
            var dialog = new Window
            {
                Title = "Επιλογή Ημερομηνίας",
                Content = datePicker,
                Width = 250,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Owner = this
            };

            dialog.ShowDialog();

            if (datePicker.SelectedDate.HasValue)
            {
                DateTime selectedDate = datePicker.SelectedDate.Value.Date;
                var results = _context.Products
                    .Where(p => p.CreatedAt.Date == selectedDate)
                    .ToList();

                productGrid.ItemsSource = results;

                if (!results.Any())
                    MessageBox.Show("Δεν υπάρχουν καταχωρίσεις για την επιλεγμένη ημερομηνία.", "Ιστορικό", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Τελευταία Καταχώρηση
        private void ShowLastProductDate_Click(object sender, RoutedEventArgs e)
        {
            var last = _context.Products.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (last != null)
            {
                string message =
                    $"🗓 Τελευταία καταχώρηση:\n\n" +
                    $"🏷 Προμηθευτής: {last.Supplier}\n" +
                    $"📌 Όνομα: {last.Name}\n" +
                    $"🔢 Κωδικός: {last.ProductCode}\n" +
                    $"📦 Απόθεμα: {last.Stock}\n" +
                    $"💰 Τιμή: {last.Price:0.00} €\n" +
                    $"📅 Ημερομηνία: {last.CreatedAt:g}";

                MessageBox.Show(message, "Ιστορικό");
            }
            else
            {
                MessageBox.Show("Δεν υπάρχουν προϊόντα.", "Ιστορικό");
            }
        }


        // Επαναφορά Όλων των Καταχωρίσεων
        private void ShowAllProducts_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            MessageBox.Show("Επαναφέρθηκαν όλες οι καταχωρίσεις.", "Ιστορικό");
        }




        private void productGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
