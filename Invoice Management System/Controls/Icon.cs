using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Invoice_Management_System
{
    /// <summary>
    /// Εικονίδιο γραμμής (stroke) σε κουτί 24×24, όπως τα SVG του mockup.
    /// Χρώμα = Foreground (κληρονομείται από το κουμπί/κείμενο γύρω του).
    /// Χρήση: &lt;local:Icon Data="{StaticResource Ico.Search}" Width="18" Height="18"/&gt;
    /// Τα σχήματα είναι στο Themes/Icons.xaml, το template στο Themes/ModernTheme.xaml.
    /// </summary>
    public class Icon : Control
    {
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(Geometry), typeof(Icon));

        public static readonly DependencyProperty StrokeWidthProperty =
            DependencyProperty.Register(nameof(StrokeWidth), typeof(double), typeof(Icon), new PropertyMetadata(2.0));

        public Geometry? Data
        {
            get => (Geometry?)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public double StrokeWidth
        {
            get => (double)GetValue(StrokeWidthProperty);
            set => SetValue(StrokeWidthProperty, value);
        }

        static Icon()
        {
            FocusableProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
            IsTabStopProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        }
    }
}

namespace Invoice_Management_System
{
    /// <summary>
    /// Σημαίνει ένα πεδίο (Input style) ως λανθασμένο: κόκκινο περίγραμμα ακόμα κι όταν έχει focus.
    /// Χρήση: InputError.SetHasError(textBox, true)
    /// </summary>
    public static class InputError
    {
        public static readonly System.Windows.DependencyProperty HasErrorProperty =
            System.Windows.DependencyProperty.RegisterAttached("HasError", typeof(bool), typeof(InputError),
                new System.Windows.PropertyMetadata(false));

        public static bool GetHasError(System.Windows.DependencyObject d) => (bool)d.GetValue(HasErrorProperty);
        public static void SetHasError(System.Windows.DependencyObject d, bool value) => d.SetValue(HasErrorProperty, value);
    }
}
