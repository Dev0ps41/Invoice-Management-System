using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Invoice_Management_System.Dialogs;
using Invoice_Management_System.Localization;

namespace Invoice_Management_System
{
    // Dashboard (KPI, donut, κάρτα άδειας) και επιλογή γλώσσας.
    public partial class MainWindow
    {
        private const decimal LowStockLimit = 5m;
        private const int TrialDays = 14; // ίδιο όριο με το CheckLicense()

        private static readonly Brush[] DonutPalette =
        {
            Freeze("#4F46E5"), Freeze("#0EA5E9"), Freeze("#10B981"), Freeze("#F59E0B"), Freeze("#94A3B8")
        };

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Κάθε φορά που αλλάζει το ItemsSource (LoadProducts, αναζήτηση, φίλτρα) ανανεώνονται οι κάρτες.
            DependencyPropertyDescriptor
                .FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid))
                .AddValueChanged(productGrid, (_, _) => { UpdateDashboard(); UpdateListSummary(); });

            Loc.Instance.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => Loc.Instance.LanguageChanged -= OnLanguageChanged;

            // Τα μηνύματα/παράθυρα ανοίγουν μέσα σε αυτό το παράθυρο
            InAppDialog.Attach(this, dialogHost);

            SyncLanguageButtons();
            UpdateDashboard();
            UpdateListSummary();
            RefreshLicenseUi();

            // Σιωπηλός έλεγχος για νέα έκδοση: εμφανίζει το σήμα «Νέα» στις Ενημερώσεις
            _ = CheckForUpdateBadgeAsync();

            // Έλεγχος άδειας αφού εμφανιστεί το παράθυρο, ώστε το «Λήξη Trial» και η Ενεργοποίηση να ανοίξουν μέσα του
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
            {
                CheckLicense();
                RefreshLicenseUi();
            });
        }

        // ===================== Γλώσσα =====================
        private void Language_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string code })
                Loc.Instance.SetLanguage(code);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            Language = XmlLanguage.GetLanguage(Loc.Instance.Culture.IetfLanguageTag);
            SyncLanguageButtons();
            UpdateDashboard();
            UpdateListSummary();
            RefreshLicenseUi();

            // Ξαναδημιουργεί τις γραμμές ώστε οι αριθμοί να πάρουν τη μορφή της νέας γλώσσας
            try
            {
                productGrid.CommitEdit(DataGridEditingUnit.Row, true);
                productGrid.Items.Refresh();
            }
            catch (InvalidOperationException) { /* γραμμή σε επεξεργασία: ανανεώνεται στο επόμενο φόρτωμα */ }
        }

        private void SyncLanguageButtons()
        {
            langEl.IsChecked = Loc.Instance.Code == "el";
            langEn.IsChecked = Loc.Instance.Code == "en";
            langDe.IsChecked = Loc.Instance.Code == "de";
        }

        // Ctrl+F: αναζήτηση
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F &&
                System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control &&
                dialogHost.Children.Count == 0)
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
                e.Handled = true;
            }
        }

        // Πλήθος και σύνολο των γραμμών που φαίνονται τώρα στον πίνακα (μετά από αναζήτηση/φίλτρο)
        private void UpdateListSummary()
        {
            var shown = (productGrid.ItemsSource as IEnumerable<Product>)?.ToList() ?? new List<Product>();
            txtCount.Text = Loc.T("List_Count", shown.Count);
            txtGridTotal.Text = "€" + shown.Sum(p => p.TotalValue).ToString("N2", Loc.Instance.Culture);
        }

        private async Task CheckForUpdateBadgeAsync()
        {
            try
            {
                var latest = await GetLatestVersionAsync();
                updateBadge.Visibility = latest != null && IsNewer(latest, CurrentVersion) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { /* χωρίς internet: απλώς δεν δείχνουμε σήμα */ }
        }

        // Ανοίγει το ContextMenu ενός κουμπιού του sidebar με αριστερό κλικ
        private void OpenContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } btn)
            {
                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                menu.IsOpen = true;
            }
        }

        // ===================== KPI / donut =====================
        private void UpdateDashboard()
        {
            var culture = Loc.Instance.Culture;

            // Οι KPI αφορούν όλη τη βάση, όχι μόνο το φιλτραρισμένο grid
            var all = _context.Products.AsNoTrackingList();

            kpiCount.Text = all.Count.ToString("N0", culture);
            kpiToday.Text = Loc.T("Kpi_Today", all.Count(p => p.CreatedAt.Date == DateTime.Today));
            kpiValue.Text = "€" + all.Sum(p => p.TotalValue).ToString("N2", culture);
            kpiLow.Text = all.Count(p => p.Stock < LowStockLimit).ToString(culture);

            var bySupplier = all
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Supplier) ? "—" : p.Supplier)
                .Select(g => (Label: g.Key, Value: (double)g.Sum(p => p.TotalValue)))
                .OrderByDescending(x => x.Value)
                .ToList();

            kpiSuppliers.Text = Loc.T("Kpi_Suppliers", bySupplier.Count);

            // Μέχρι 4 προμηθευτές: όλοι με το όνομά τους. Περισσότεροι: top 3 + "Λοιποί"
            var top = bySupplier.Count <= 4 ? bySupplier.ToList() : bySupplier.Take(3).ToList();
            double rest = bySupplier.Count <= 4 ? 0 : bySupplier.Skip(3).Sum(x => x.Value);
            if (rest > 0) top.Add((Loc.T("Donut_Others"), rest));

            double total = top.Sum(x => x.Value);
            var slices = top.Select((x, i) => new DonutSlice(
                x.Label, x.Value, total > 0 ? x.Value / total * 100 : 0, DonutPalette[i % DonutPalette.Length],
                "€" + x.Value.ToString("N2", culture))).ToList();

            supplierDonut.CenterLabel = Loc.T("Donut_Suppliers");
            supplierDonut.Slices = slices;
            supplierDonut.CenterText = bySupplier.Count.ToString();
            donutLegend.ItemsSource = slices;
        }

        // ===================== Άδεια =====================
        /// <summary>Ενημερώνει την κάρτα άδειας και το μενού κατάστασης. Καλείται και από το ActivationWindow.</summary>
        internal void RefreshLicenseUi()
        {
            var license = _context.Licenses.AsNoTrackingFirstOrDefault();
            if (license is { IsActivated: true })
            {
                activationStatusMenuItem.Header = Loc.T("Status_Activated");
                txtLicenseTitle.Text = Loc.T("License_Activated");
                txtLicenseDays.Text = Loc.T("License_Full");
                licenseBar.Width = double.NaN;
                licenseBar.HorizontalAlignment = HorizontalAlignment.Stretch;
                licenseBar.Background = Freeze("#34D399");
                return;
            }

            activationStatusMenuItem.Header = Loc.T("Status_Trial");
            txtLicenseTitle.Text = Loc.T("License_TrialActive");

            int used = license == null ? 0 : (int)(DateTime.Now - license.ActivationDate).TotalDays;
            int left = Math.Clamp(TrialDays - used, 0, TrialDays);
            txtLicenseDays.Text = Loc.T("License_DaysLeft", left, TrialDays);
            // 182 ≈ πλάτος sidebar (248) μείον padding
            licenseBar.HorizontalAlignment = HorizontalAlignment.Left;
            licenseBar.Width = 182.0 * left / TrialDays;
            if (left <= 3) licenseBar.Background = Freeze("#F59E0B");
        }

        private static SolidColorBrush Freeze(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            b.Freeze();
            return b;
        }
    }

    internal static class DbSetExtensions
    {
        public static List<Product> AsNoTrackingList(this Microsoft.EntityFrameworkCore.DbSet<Product> set)
            => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(set).ToList();

        public static License? AsNoTrackingFirstOrDefault(this Microsoft.EntityFrameworkCore.DbSet<License> set)
            => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(set).FirstOrDefault();
    }

    // ===================== Donut chart (χωρίς εξωτερική βιβλιοθήκη) =====================
    public record DonutSlice(string Label, double Value, double Percent, Brush Brush, string ValueText = "");

    public class DonutChart : FrameworkElement
    {
        private IReadOnlyList<DonutSlice> _slices = Array.Empty<DonutSlice>();
        public IReadOnlyList<DonutSlice> Slices
        {
            get => _slices;
            set { _slices = value; InvalidateVisual(); }
        }

        private string _centerText = "";
        public string CenterText
        {
            get => _centerText;
            set { _centerText = value; InvalidateVisual(); }
        }

        private string _centerLabel = "";
        public string CenterLabel
        {
            get => _centerLabel;
            set { _centerLabel = value; InvalidateVisual(); }
        }

        public double Thickness { get; set; } = 16;
        public double GapDegrees { get; set; } = 4;

        protected override void OnRender(DrawingContext dc)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) return;

            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            double r = (size - Thickness) / 2;

            // Γκρι "τροχιά" από πίσω
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF5)), Thickness), center, r, r);

            double total = _slices.Sum(s => s.Value);
            if (total > 0)
            {
                double start = -90; // ξεκινά από πάνω
                bool single = _slices.Count(s => s.Value > 0) == 1;
                foreach (var s in _slices.Where(s => s.Value > 0))
                {
                    double sweep = s.Value / total * 360;
                    if (single)
                    {
                        dc.DrawEllipse(null, new Pen(s.Brush, Thickness), center, r, r);
                        break;
                    }
                    double drawn = Math.Max(0.5, sweep - GapDegrees);
                    var pen = new Pen(s.Brush, Thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                    dc.DrawGeometry(null, pen, Arc(center, r, start + GapDegrees / 2, drawn));
                    start += sweep;
                }
            }

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            // Ίδια γραμματοσειρά με την εφαρμογή (Inter), σκούρο κείμενο για καθαρή ανάγνωση
            var family = TextElement.GetFontFamily(this);
            var big = new FormattedText(_centerText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(family, FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal),
                size * 0.19, new SolidColorBrush(Color.FromRgb(0x0B, 0x12, 0x20)), dpi);
            var small = new FormattedText(_centerLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                size * 0.095, new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), dpi);

            dc.DrawText(big, new Point(center.X - big.Width / 2, center.Y - big.Height / 2 - small.Height / 2));
            dc.DrawText(small, new Point(center.X - small.Width / 2, center.Y + big.Height / 2 - small.Height / 2));
        }

        private static Geometry Arc(Point c, double r, double startDeg, double sweepDeg)
        {
            Point P(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
            var fig = new PathFigure { StartPoint = P(startDeg), IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment(P(startDeg + sweepDeg), new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true));
            var g = new PathGeometry(new[] { fig });
            g.Freeze();
            return g;
        }
    }

    // ===================== Converters =====================
    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var words = (value as string ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "?",
                1 => words[0][..Math.Min(2, words[0].Length)].ToUpper(),
                _ => $"{words[0][0]}{words[1][0]}".ToUpper()
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>Σταθερό ζεύγος χρωμάτων (φόντο/κείμενο) ανά προμηθευτή για τα αρχικά στον πίνακα.</summary>
    public class SupplierBrushConverter : IValueConverter
    {
        private static readonly (Brush Bg, Brush Fg)[] Pairs =
        {
            (Make("#E0E7FF"), Make("#3730A3")), (Make("#E0F2FE"), Make("#075985")), (Make("#D1FAE5"), Make("#065F46")),
            (Make("#FEF3C7"), Make("#92400E")), (Make("#FCE7F3"), Make("#9D174D")), (Make("#EDE9FE"), Make("#5B21B6"))
        };

        /// <summary>"Background" ή "Foreground"</summary>
        public string Part { get; set; } = "Background";

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            // Σταθερό hash (το string.GetHashCode αλλάζει σε κάθε εκκίνηση)
            int h = 0;
            foreach (char ch in (value as string ?? "")) h = unchecked(h * 31 + ch);
            var pair = Pairs[(h & int.MaxValue) % Pairs.Length];
            return Part == "Foreground" ? pair.Fg : pair.Bg;
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;

        private static Brush Make(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            b.Freeze();
            return b;
        }
    }

    public class LowStockConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is decimal d && d < 5m;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
