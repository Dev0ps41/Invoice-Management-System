using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Invoice_Management_System.Localization;

namespace Invoice_Management_System
{
    /// <summary>
    /// Ημερολόγιο μήνα για το φίλτρο «Ημερομηνία». Ένα κλικ σε μέρα καλεί το DatePicked.
    /// Οι μέρες που έχουν καταχωρίσεις σημειώνονται με πράσινο.
    /// </summary>
    public partial class MonthCalendar : UserControl
    {
        private DateTime _month;
        private readonly HashSet<DateTime> _marked;

        public event Action<DateTime>? DatePicked;

        public MonthCalendar(IEnumerable<DateTime> markedDays)
        {
            InitializeComponent();
            _marked = markedDays.Select(d => d.Date).ToHashSet();
            _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            AutomationProperties.SetName(btnPrev, Loc.T("Cal_Prev"));
            AutomationProperties.SetName(btnNext, Loc.T("Cal_Next"));
            btnPrev.ToolTip = Loc.T("Cal_Prev");
            btnNext.ToolTip = Loc.T("Cal_Next");

            BuildWeekdays();
            Render();
        }

        /// <summary>Η σημερινή μέρα, για αρχικό focus.</summary>
        public IInputElement? TodayButton { get; private set; }

        private CultureInfo Culture => Loc.Instance.Culture;
        private DayOfWeek FirstDay => Culture.DateTimeFormat.FirstDayOfWeek;

        private void BuildWeekdays()
        {
            weekdays.Children.Clear();
            var names = Culture.DateTimeFormat.ShortestDayNames;
            for (int i = 0; i < 7; i++)
            {
                var dow = (DayOfWeek)(((int)FirstDay + i) % 7);
                weekdays.Children.Add(new TextBlock
                {
                    Text = names[(int)dow],
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Ink3"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4)
                });
            }
        }

        private void Render()
        {
            var title = _month.ToString("MMMM yyyy", Culture);
            txtMonth.Text = Culture.TextInfo.ToTitleCase(title);

            days.Children.Clear();
            TodayButton = null;

            int offset = ((int)_month.DayOfWeek - (int)FirstDay + 7) % 7;
            var start = _month.AddDays(-offset);

            var accent = (Brush)FindResource("Accent");
            var greenSoft = (Brush)FindResource("GreenSoft");
            var greenInk = (Brush)FindResource("GreenInk");
            var other = (Brush)FindResource("Placeholder");

            for (int i = 0; i < 42; i++)
            {
                var day = start.AddDays(i);
                var btn = new Button
                {
                    Style = (Style)FindResource("DayButton"),
                    Content = day.Day.ToString(Culture),
                    Tag = day
                };
                AutomationProperties.SetName(btn, day.ToString("D", Culture));

                if (day == DateTime.Today)
                {
                    btn.Background = accent;
                    btn.Foreground = Brushes.White;
                    TodayButton = btn;
                }
                else if (_marked.Contains(day))
                {
                    btn.Background = greenSoft;
                    btn.Foreground = greenInk;
                }
                else if (day.Month != _month.Month)
                {
                    btn.Foreground = other;
                    btn.FontWeight = FontWeights.Medium;
                }

                btn.Click += (_, _) => DatePicked?.Invoke(day);
                days.Children.Add(btn);
            }
        }

        private void Prev_Click(object sender, RoutedEventArgs e) { _month = _month.AddMonths(-1); Render(); }
        private void Next_Click(object sender, RoutedEventArgs e) { _month = _month.AddMonths(1); Render(); }
    }
}
