using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using Invoice_Management_System.Activation;
using Invoice_Management_System.Dialogs;
using Invoice_Management_System.Localization;
using System.Diagnostics;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Invoice_Management_System.Models;
using Microsoft.EntityFrameworkCore;

using UndoAction = Invoice_Management_System.Models.UndoAction;

namespace Invoice_Management_System
{
    public partial class MainWindow : Window
    {
        private readonly AppDbContext _context = new AppDbContext();

        private Stack<UndoAction> undoStack = new();
        private Stack<UndoAction> redoStack = new();

        // Όλα τα προϊόντα στη μνήμη, για γρήγορη αναζήτηση χωρίς query σε κάθε πλήκτρο
        private List<Product> _all = new();

        private const string RepoOwner = "Dev0ps41";
        private const string RepoName = "InvoiceManagementSystemUpdates";
        private const string LatestReleaseUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        // Το .NET 8 προσθέτει "+<git commit>" στο ProductVersion (π.χ. 1.0.20.0+122eafc…). Το κόβουμε,
        // αλλιώς φαίνεται στο «Σχετικά» και ο έλεγχος ενημερώσεων βρίσκει πάντα «νέα» έκδοση.
        private static readonly string CurrentVersion =
            (System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ProductVersion ?? "")
            .Split('+')[0];

        public MainWindow()
        {
            // Μορφή αριθμών/ημερομηνιών σύμφωνα με την επιλεγμένη γλώσσα
            Language = XmlLanguage.GetLanguage(Loc.Instance.Culture.IetfLanguageTag);

            InitializeComponent();
            _context.Database.Migrate(); // Δημιουργεί/ενημερώνει τη βάση σε καθαρή εγκατάσταση
            // Ο έλεγχος άδειας (CheckLicense) γίνεται στο Window_Loaded, ώστε τα μηνύματα να ανοίγουν μέσα στο παράθυρο
            LoadProducts();
            LoadAutoCompleteData();
            DataContext = this; //  Επιτρέπει την αυτόματη σύνδεση των δεδομένων
        }

        private static void ShowError(string message) =>
            InAppDialog.Show(message, Loc.T("Title_Error"), MessageBoxButton.OK, MessageBoxImage.Error);

        /// <summary>Η τελευταία δημοσιευμένη έκδοση στο GitHub και το zip της.</summary>
        private static async Task<(string Version, string? DownloadUrl)?> FetchLatestReleaseAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("User-Agent", "InvoiceManager");

            JObject release = JObject.Parse(await client.GetStringAsync(LatestReleaseUrl));
            string? version = release["tag_name"]?.ToString()?.Trim().TrimStart('v', 'V');
            if (string.IsNullOrWhiteSpace(version)) return null;

            string? url = release["assets"]?
                .FirstOrDefault(a => a["name"]?.ToString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)?
                ["browser_download_url"]?.ToString();
            return (version, url);
        }

        private static async Task<string?> GetLatestVersionAsync() => (await FetchLatestReleaseAsync())?.Version;

        /// <summary>Αληθές μόνο αν η latest είναι ΜΕΤΑΓΕΝΕΣΤΕΡΗ (όχι απλώς διαφορετική) από την τρέχουσα.</summary>
        private static bool IsNewer(string latest, string current) =>
            Version.TryParse(latest, out var l) && Version.TryParse(current, out var c)
                ? l > c
                : !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var latest = await FetchLatestReleaseAsync();
                if (latest is not { } release)
                {
                    InAppDialog.Show(Loc.T("Upd_NoVersion"), Loc.T("Title_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!IsNewer(release.Version, CurrentVersion))
                {
                    updateBadge.Visibility = Visibility.Collapsed;
                    InAppDialog.Show(Loc.T("Upd_Latest", CurrentVersion), Loc.T("Upd_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = InAppDialog.Show(
                    Loc.T("Upd_Available", release.Version, CurrentVersion),
                    Loc.T("Upd_AvailableTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    if (!string.IsNullOrEmpty(release.DownloadUrl))
                        await DownloadAndInstallUpdate(release.DownloadUrl);
                    else
                        ShowError(Loc.T("Upd_NoFile"));
                }
            }
            catch (Exception ex)
            {
                ShowError(Loc.T("Upd_CheckError", ex.Message));
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
                    ShowError(Loc.T("Upd_NoUpdater"));
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
                ShowError(Loc.T("Upd_InstallError", ex.Message));
            }
        }

        private void LoadProducts()
        {
            _all = _context.Products.ToList();
            productGrid.ItemsSource = _all;

            if (_all.Any()) // Αν υπάρχουν προϊόντα, ενημέρωσε τα πεδία εισαγωγής
            {
                var lastProduct = _all.Last(); // Παίρνουμε το τελευταίο προϊόν που προστέθηκε
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
            var view = new AboutView();
            InAppDialog.ShowCustom("Invoice Manager", view, width: 440, icon: DialogIcon.Logo,
                subtitle: Loc.T("About_Version", CurrentVersion),
                setup: h => { view.OkButton.Click += (_, _) => h.Close(); h.InitialFocus = view.OkButton; });
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
            FlowDocument doc = new FlowDocument { FontFamily = (FontFamily)FindResource("AppFont"), FontSize = 11 };
            doc.ColumnWidth = 370; // Ρύθμιση πλάτους εκτύπωσης
            doc.PagePadding = new Thickness(50);

            // Τίτλος του Invoice
            Paragraph title = new Paragraph(new Bold(new Run(Loc.T("Print_Title"))))
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
            foreach (var key in new[] { "Field_Supplier", "Field_Code", "Field_Name", "Field_Stock", "Field_Price", "Field_Total" })
                headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run(Loc.T(key))))));
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
            if (InAppDialog.Show(Loc.T("DeleteAll_Confirm"), Loc.T("Title_DeleteAll"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning, danger: true) == MessageBoxResult.Yes)
            {
                _context.Products.RemoveRange(_context.Products); // Διαγραφή όλων των προϊόντων
                _context.SaveChanges();
                LoadProducts(); // Επαναφόρτωση του DataGrid
                InAppDialog.Show(Loc.T("DeleteAll_Done"), Loc.T("Title_Done"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportCSV_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = Loc.T("Csv_Filter"),
                Title = Loc.T("Csv_OpenTitle")
            };
            if (openFileDialog.ShowDialog() != true) return;

            // Η εισαγωγή τρέχει στο παρασκήνιο (CsvImporter), με παράθυρο προόδου μέσα στην εφαρμογή.
            // Τα προβλήματα συγκεντρώνονται και εμφανίζονται ΜΙΑ φορά στο τέλος.
            var progressText = new TextBlock { Text = Loc.T("Csv_Progress", 0), FontSize = 14, Foreground = (Brush)FindResource("Ink2") };
            var bar = new ProgressBar { IsIndeterminate = true, Height = 6, Margin = new Thickness(0, 14, 0, 0),
                                        Foreground = (Brush)FindResource("Accent"), Background = (Brush)FindResource("LineSoft"), BorderThickness = new Thickness(0) };
            var body = new StackPanel { Children = { progressText, bar } };

            string path = openFileDialog.FileName;
            object? outcome = null;
            InAppDialog.ShowCustom(Loc.T("Csv_ImportingTitle"), body, width: 440, subtitle: Path.GetFileName(path),
                icon: DialogIcon.Info, setup: h =>
                {
                    h.Cancelable = false;
                    var progress = new Progress<int>(n => progressText.Text = Loc.T("Csv_Progress", n));
                    Task.Run(() => (object)CsvImporter.Import(path, progress))
                        .ContinueWith(t =>
                        {
                            outcome = t.IsFaulted ? t.Exception!.GetBaseException() : t.Result;
                            h.Close();
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                });

            if (outcome is Exception ex)
            {
                ShowError(Loc.T("Csv_Failed", ex.Message));
                return;
            }
            if (outcome is not CsvImportResult r) return;

            // Το _context έχει παλιά αντίγραφα των προϊόντων: τα αφήνουμε ώστε να διαβαστούν ξανά από τη βάση
            _context.ChangeTracker.Clear();
            undoStack.Clear();
            redoStack.Clear();
            LoadProducts();
            LoadAutoCompleteData();
            ClearFields();

            var text = new System.Text.StringBuilder(Loc.T("Csv_Summary", r.LinesRead, r.Added, r.Updated));
            if (r.Skipped > 0)
            {
                text.Append("\n\n").Append(Loc.T("Csv_SkippedHeader", r.Skipped));
                foreach (var p in r.Problems.Take(10))
                    text.Append('\n').Append(Loc.T("Csv_Err_" + p.Kind, p.LineNumber, p.Value));
                if (r.Skipped > 10) text.Append('\n').Append(Loc.T("Csv_More", r.Skipped - 10));
            }
            InAppDialog.Show(text.ToString(), Loc.T("Csv_ImportTitle"), MessageBoxButton.OK,
                             r.Skipped > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        //  Εξαγωγή σε CSV
        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = Loc.T("Csv_Filter"),
                Title = Loc.T("Csv_SaveTitle")
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var products = _context.Products.ToList();

                // UTF-8 with BOM για σωστή συμβατότητα με Excel
                var utf8WithBom = new System.Text.UTF8Encoding(true);

                using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, utf8WithBom))
                {
                    // Επικεφαλίδες στη γλώσσα της εφαρμογής (η εισαγωγή αγνοεί την πρώτη γραμμή)
                    writer.WriteLine(string.Join(";", new[] { "Field_Supplier", "Field_Code", "Field_Name", "Field_Stock", "Field_Price", "Field_Total" }.Select(Loc.T)));

                    // Οι αριθμοί μένουν σε ελληνική μορφή ώστε το αρχείο να ξαναδιαβάζεται σωστά
                    var greekCulture = new System.Globalization.CultureInfo("el-GR");

                    foreach (var product in products)
                    {
                        string supplier = product.Supplier;
                        string code = product.ProductCode;
                        string name = product.Name;
                        string stock = product.Stock.ToString("0.##", greekCulture);
                        string price = product.Price.ToString("0.00", greekCulture); // Με κόμμα
                        string total = (product.Stock * product.Price).ToString("0.00", greekCulture); // Με κόμμα

                        writer.WriteLine($"{supplier};{code};{name};{stock};{price};{total}");
                    }
                }

                InAppDialog.Show(Loc.T("Csv_ExportDone"), Loc.T("Title_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
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

                if (InAppDialog.Show(Loc.T("Edit_Prompt"), Loc.T("Edit_Title"),
                                    MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
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
                        ShowError(Loc.T("Edit_Error", ex.Message));
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
                            ShowError(Loc.T("Save_Error", ex.Message));
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
                ShowError(Loc.T("Save_Error", ex.Message));
            }
        }

        //  Διαγραφή προϊόντος
        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            // Η γραμμή του κουμπιού που πατήθηκε (το κλικ δεν αλλάζει το SelectedItem)
            if ((sender as FrameworkElement)?.DataContext is Product selectedProduct)
            {
                if (InAppDialog.Show(Loc.T("Delete_Confirm", selectedProduct.Name),
                                    Loc.T("Title_DeleteProduct"), MessageBoxButton.YesNo, MessageBoxImage.Warning, danger: true) == MessageBoxResult.Yes)
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

        // Αναζήτηση προϊόντος (στη μνήμη, χωρίς query σε κάθε πλήκτρο)
        private void SearchProduct(object sender, TextChangedEventArgs e)
        {
            string q = txtSearch.Text?.Trim() ?? "";

            if (q.Length == 0)
            {
                productGrid.ItemsSource = _all;
                return;
            }

            productGrid.ItemsSource = _all.Where(p =>
                    (p.Name?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                    (p.ProductCode?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                    (p.Supplier?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                    p.Stock.ToString().Contains(q) ||
                    p.Price.ToString("0.00").Contains(q))
                .ToList();
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
            var view = new ReportsView();
            var print = new Button
            {
                Style = (Style)FindResource("BtnSecondary"),
                Height = 40,
                Padding = new Thickness(14, 0, 14, 0),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Icon { Data = (Geometry)FindResource("Ico.Printer"), Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0) },
                        new TextBlock { Text = Loc.T("Nav_Print"), FontSize = 13.5 }
                    }
                }
            };
            print.Click += (_, _) => view.Print();
            InAppDialog.ShowCustom(Loc.T("Reports_Title"), view, width: 760, subtitle: Loc.T("Rep_Subtitle"),
                                   icon: DialogIcon.Chart, headerExtra: print);
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSupplier.Text) ||
                string.IsNullOrWhiteSpace(txtProductCode.Text) ||
                string.IsNullOrWhiteSpace(txtName.Text) ||
                !CsvImporter.TryParseNumber(txtStock.Text, out decimal stock) ||
                !CsvImporter.TryParseNumber(txtPrice.Text, out decimal price) || stock < 0 || price < 0)
            {
                ShowError(Loc.T("Invalid_Input"));
                return;
            }

            var existingProduct = _context.Products.FirstOrDefault(p =>
                p.ProductCode == txtProductCode.Text && p.Name == txtName.Text);

            if (existingProduct != null)
            {
                // Νέα ποσότητα και σταθμισμένη μέση τιμή (χωρίς διαίρεση με το 0)
                decimal newStock = existingProduct.Stock + stock;
                decimal newPrice = newStock != 0
                    ? ((existingProduct.Price * existingProduct.Stock) + (price * stock)) / newStock
                    : price;

                // ⏪ Push σε Undo πριν γίνει αλλαγή
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
                        Stock = newStock,
                        Price = newPrice
                    }
                });
                redoStack.Clear();

                existingProduct.Stock = newStock;
                existingProduct.Price = newPrice;

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
                InAppDialog.Show(Loc.T("Undo_None"));
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
                InAppDialog.Show(Loc.T("Redo_None"));
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
            var view = new ActivationView(_context);
            var activated = InAppDialog.ShowCustom(Loc.T("Act_Title"), view, width: 460, subtitle: Loc.T("Act_Subtitle"),
                                                   icon: DialogIcon.Key, setup: h =>
            {
                view.Activated += () => h.Close(true);
                h.DefaultAction = view.TryActivate; // Enter στο πεδίο = Ενεργοποίηση
                h.InitialFocus = view.KeyBox;
            });

            if (activated is true)
            {
                RefreshLicenseUi();
                InAppDialog.Show(Loc.T("Act_Success"), Loc.T("Title_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
                    InAppDialog.Show(Loc.T("Trial_Expired"), Loc.T("Trial_ExpiredTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    OpenActivationWindow();

                    // Ελέγχει ξανά αν το πρόγραμμα ενεργοποιήθηκε
                    license = _context.Licenses.FirstOrDefault();

                    if (license == null || !license.IsActivated)
                    {
                        InAppDialog.Show(Loc.T("Trial_Shutdown"), Loc.T("Trial_ExpiredTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                        Application.Current.Shutdown();
                    }
                }
            }
            // Η κάρτα άδειας και το μενού κατάστασης ενημερώνονται στο RefreshLicenseUi() (MainWindow.Dashboard.cs)
        }

        // Καταχωρίσεις ΣΗΜΕΡΑ
        private void ShowTodayProducts_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var results = _all.Where(p => p.CreatedAt.Date == today).ToList();

            productGrid.ItemsSource = results;

            if (!results.Any())
                InAppDialog.Show(Loc.T("Hist_NoToday"), Loc.T("Title_History"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Καταχωρίσεις ΧΘΕΣ
        private void ShowYesterdayProducts_Click(object sender, RoutedEventArgs e)
        {
            var yesterday = DateTime.Today.AddDays(-1);
            var results = _all.Where(p => p.CreatedAt.Date == yesterday).ToList();

            productGrid.ItemsSource = results;

            if (!results.Any())
                InAppDialog.Show(Loc.T("Hist_NoYesterday"), Loc.T("Title_History"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Καταχωρίσεις βάσει ημερομηνίας
        private void ShowProductsByDate_Click(object sender, RoutedEventArgs e)
        {
            // Ημερολόγιο μέσα στην εφαρμογή: ένα κλικ σε μέρα κλείνει και φιλτράρει. Πράσινες = μέρες με καταχωρίσεις.
            var calendar = new MonthCalendar(_all.Select(p => p.CreatedAt));
            var picked = InAppDialog.ShowCustom(Loc.T("Hist_SelectDate"), calendar, width: 380, icon: DialogIcon.Calendar, setup: h =>
            {
                calendar.DatePicked += d => h.Close(d);
                h.InitialFocus = calendar.TodayButton;
            });

            if (picked is DateTime pickedDate)
            {
                DateTime selectedDate = pickedDate.Date;
                var results = _all.Where(p => p.CreatedAt.Date == selectedDate).ToList();

                productGrid.ItemsSource = results;

                if (!results.Any())
                    InAppDialog.Show(Loc.T("Hist_NoDate"), Loc.T("Title_History"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Τελευταία Καταχώρηση
        private void ShowLastProductDate_Click(object sender, RoutedEventArgs e)
        {
            var last = _context.Products.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (last != null)
            {
                InAppDialog.Show(
                    Loc.T("Hist_Last", last.Supplier, last.Name, last.ProductCode, last.Stock, last.Price, last.CreatedAt),
                    Loc.T("Title_History"));
            }
            else
            {
                InAppDialog.Show(Loc.T("Hist_NoProducts"), Loc.T("Title_History"));
            }
        }

        // Επαναφορά Όλων των Καταχωρίσεων (κουμπί «Προϊόντα» και chip «Όλα»)
        private void ShowAllProducts_Click(object sender, RoutedEventArgs e)
        {
            chipAll.IsChecked = true;
            txtSearch.Text = "";
            LoadProducts();
        }

        private void productGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}
