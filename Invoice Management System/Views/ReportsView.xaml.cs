using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Invoice_Management_System.Localization;

namespace Invoice_Management_System
{
    /// <summary>
    /// Αναφορές εξόδων: αξία αποθέματος ανά προμηθευτή και προϊόν, με υποσύνολα και γενικό σύνολο.
    /// Ανοίγει μέσα στο κύριο παράθυρο (InAppDialog).
    /// </summary>
    public partial class ReportsView : UserControl
    {
        public sealed record Row(string Supplier, string Product, string Total, Brush Background,
                                 FontWeight ProductWeight, Brush ProductBrush, FontWeight TotalWeight, bool IsSubtotal);

        private List<(string Supplier, List<(string Product, decimal Total)> Items, decimal Sum)> _groups = new();
        private decimal _grandTotal;

        public ReportsView()
        {
            InitializeComponent();
            LoadReports();
        }

        private static string Money(decimal v) => "€" + v.ToString("N2", Loc.Instance.Culture);

        private void LoadReports()
        {
            using var context = new AppDbContext();

            _groups = context.Products
                .AsEnumerable()
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Supplier) ? "—" : p.Supplier.Trim())
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(g =>
                {
                    var items = g.GroupBy(p => p.Name)
                                 .Select(x => (Product: x.Key, Total: Math.Round(x.Sum(p => p.Stock * p.Price), 2)))
                                 .OrderBy(x => x.Product, StringComparer.CurrentCultureIgnoreCase)
                                 .ToList();
                    return (Supplier: g.Key, Items: items, Sum: items.Sum(i => i.Total));
                })
                .ToList();

            _grandTotal = _groups.Sum(g => g.Sum);

            var white = (Brush)FindResource("Card");
            var subtle = (Brush)FindResource("Subtle");
            var ink = (Brush)FindResource("Ink");
            var ink2 = (Brush)FindResource("Ink2");

            var list = new List<Row>();
            foreach (var g in _groups)
            {
                for (int i = 0; i < g.Items.Count; i++)
                    list.Add(new Row(i == 0 ? g.Supplier : "", g.Items[i].Product, Money(g.Items[i].Total), white,
                                     FontWeights.Medium, ink, FontWeights.Medium, false));
                list.Add(new Row("", Loc.T("Rep_Subtotal"), Money(g.Sum), subtle, FontWeights.Bold, ink2, FontWeights.ExtraBold, true));
            }

            rows.ItemsSource = list;
            txtGrandTotal.Text = Money(_grandTotal);
            txtEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Εκτύπωση της αναφοράς (διάλογος εκτύπωσης των Windows).</summary>
        public void Print()
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;

            var doc = new FlowDocument
            {
                FontFamily = (FontFamily)FindResource("AppFont"),
                FontSize = 12,
                PagePadding = new Thickness(48),
                ColumnWidth = double.PositiveInfinity
            };
            doc.Blocks.Add(new Paragraph(new Bold(new Run(Loc.T("Reports_Title")))) { FontSize = 18, Margin = new Thickness(0, 0, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run(Loc.T("Rep_Subtitle") + " · " + DateTime.Now.ToString("g", Loc.Instance.Culture)))
                { Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 16) });

            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            var group = new TableRowGroup();

            TableCell Cell(string text, bool bold = false, TextAlignment align = TextAlignment.Left) =>
                new(new Paragraph(bold ? new Bold(new Run(text)) : new Run(text)) { TextAlignment = align, Margin = new Thickness(0) })
                { Padding = new Thickness(6, 5, 6, 5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) };

            var head = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFC)) };
            head.Cells.Add(Cell(Loc.T("Rep_Supplier"), true));
            head.Cells.Add(Cell(Loc.T("Rep_Product"), true));
            head.Cells.Add(Cell(Loc.T("Rep_Total"), true, TextAlignment.Right));
            group.Rows.Add(head);

            foreach (var g in _groups)
            {
                for (int i = 0; i < g.Items.Count; i++)
                {
                    var r = new TableRow();
                    r.Cells.Add(Cell(i == 0 ? g.Supplier : "", true));
                    r.Cells.Add(Cell(g.Items[i].Product));
                    r.Cells.Add(Cell(Money(g.Items[i].Total), false, TextAlignment.Right));
                    group.Rows.Add(r);
                }
                var sub = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFC)) };
                sub.Cells.Add(Cell(""));
                sub.Cells.Add(Cell(Loc.T("Rep_Subtotal"), true));
                sub.Cells.Add(Cell(Money(g.Sum), true, TextAlignment.Right));
                group.Rows.Add(sub);
            }

            var total = new TableRow();
            total.Cells.Add(Cell(Loc.T("Rep_GrandTotal"), true));
            total.Cells.Add(Cell(""));
            total.Cells.Add(Cell(Money(_grandTotal), true, TextAlignment.Right));
            group.Rows.Add(total);

            table.RowGroups.Add(group);
            doc.Blocks.Add(table);

            doc.PageWidth = dialog.PrintableAreaWidth;
            doc.PageHeight = dialog.PrintableAreaHeight;
            dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, Loc.T("Reports_Title"));
        }
    }
}
