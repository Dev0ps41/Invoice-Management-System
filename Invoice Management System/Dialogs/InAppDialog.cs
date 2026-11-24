using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Invoice_Management_System.Localization;

namespace Invoice_Management_System.Dialogs
{
    /// <summary>Χειριστήριο ενός ανοιχτού παραθύρου μέσα στην εφαρμογή.</summary>
    public sealed class DialogHandle
    {
        private readonly DispatcherFrame _frame;
        internal DialogHandle(DispatcherFrame frame) => _frame = frame;

        public object? Result { get; private set; }

        /// <summary>Στοιχείο που παίρνει το focus όταν ανοίγει το παράθυρο.</summary>
        public IInputElement? InitialFocus { get; set; }

        /// <summary>false: δεν κλείνει με ✕ / Esc (π.χ. όσο τρέχει μια εισαγωγή). Κλείνει μόνο με Close().</summary>
        public bool Cancelable { get; set; } = true;

        /// <summary>Τι γίνεται με Enter όταν το focus δεν είναι σε κουμπί (π.χ. μέσα σε TextBox).</summary>
        public Action? DefaultAction { get; set; }

        public void Close(object? result = null)
        {
            Result = result;
            _frame.Continue = false;
        }
    }

    /// <summary>Το εικονίδιο στην επικεφαλίδα του παραθύρου.</summary>
    public enum DialogIcon { None, Info, Warning, Error, Question, Delete, Key, Chart, Calendar, Logo }

    /// <summary>
    /// Modal παράθυρα που ανοίγουν ΜΕΣΑ στο κύριο παράθυρο (κάρτα πάνω από σκούρο φόντο),
    /// αντί για ξεχωριστά παράθυρα των Windows. Το Show(...) έχει την ίδια υπογραφή με το MessageBox.Show
    /// και, όπως εκείνο, περιμένει μέχρι να κλείσει πριν συνεχίσει ο κώδικας.
    /// </summary>
    public static class InAppDialog
    {
        private static Window? _owner;
        private static Panel? _host;
        private static readonly List<DispatcherFrame> OpenFrames = new();

        private enum Kind { Primary, Secondary, Danger }

        public static void Attach(Window owner, Panel host)
        {
            _owner = owner;
            _host = host;
            // Αν κλείσει η εφαρμογή ενώ είναι ανοιχτό παράθυρο, ελευθερώνονται όλα
            owner.Closing += (_, _) => { foreach (var f in OpenFrames.ToList()) f.Continue = false; };
        }

        private static bool IsAvailable => _host != null && _owner is { IsVisible: true };

        // ======================= Αντικατάσταση του MessageBox.Show =======================
        public static MessageBoxResult Show(string text)
            => Show(text, "", MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string text, string caption)
            => Show(text, caption, MessageBoxButton.OK, MessageBoxImage.None);

        /// <param name="danger">Κόκκινο κουμπί «Ναι, διαγραφή» και εικονίδιο κάδου (για διαγραφές).</param>
        public static MessageBoxResult Show(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon, bool danger = false)
        {
            if (!IsAvailable)
                return MessageBox.Show(text, caption, buttons, icon);

            var affirmative = danger ? Kind.Danger : Kind.Primary;
            string yesKey = danger ? "Btn_YesDelete" : "Btn_Yes";
            var specs = buttons switch
            {
                MessageBoxButton.OKCancel => new[] { (MessageBoxResult.Cancel, "Btn_Cancel", Kind.Secondary), (MessageBoxResult.OK, "Btn_OK", affirmative) },
                MessageBoxButton.YesNo => new[] { (MessageBoxResult.No, "Btn_No", Kind.Secondary), (MessageBoxResult.Yes, yesKey, affirmative) },
                MessageBoxButton.YesNoCancel => new[] { (MessageBoxResult.Cancel, "Btn_Cancel", Kind.Secondary), (MessageBoxResult.No, "Btn_No", Kind.Secondary), (MessageBoxResult.Yes, yesKey, affirmative) },
                _ => new[] { (MessageBoxResult.OK, "Btn_OK", Kind.Primary) }
            };
            var cancel = buttons switch
            {
                MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.OK
            };

            var dialogIcon = danger ? DialogIcon.Delete : icon switch
            {
                MessageBoxImage.Error => DialogIcon.Error,
                MessageBoxImage.Warning => DialogIcon.Warning,
                MessageBoxImage.Question => DialogIcon.Question,
                MessageBoxImage.Information => DialogIcon.Info,
                _ => DialogIcon.None
            };

            // Το μήνυμα μπαίνει κάτω από τον τίτλο, δίπλα στο εικονίδιο (όπως στο σχέδιο)
            var result = Run(caption, text, dialogIcon, null, null, 460,
                             specs.Select(s => ((object)s.Item1, s.Item2, s.Item3)).ToArray(), null);
            return result is MessageBoxResult r ? r : cancel;
        }

        // ======================= Παράθυρο με δικό του περιεχόμενο =======================
        /// <summary>
        /// Ανοίγει οποιοδήποτε περιεχόμενο σε κάρτα. Επιστρέφει ό,τι δοθεί στο handle.Close(...), ή null αν κλείσει με ✕ / Esc.
        /// </summary>
        /// <param name="headerExtra">Π.χ. κουμπί «Εκτύπωση» δίπλα στο ✕.</param>
        public static object? ShowCustom(string title, FrameworkElement content, double width = 480,
                                         Action<DialogHandle>? setup = null, string? subtitle = null,
                                         DialogIcon icon = DialogIcon.None, FrameworkElement? headerExtra = null)
            => Run(title, subtitle, icon, content, headerExtra, width, Array.Empty<(object, string, Kind)>(), setup);

        // ======================= Υλοποίηση =======================
        private static object? Run(string title, string? subtitle, DialogIcon icon, FrameworkElement? body, FrameworkElement? headerExtra,
                                   double width, (object Result, string TextKey, Kind Kind)[] buttons, Action<DialogHandle>? setup)
        {
            var frame = new DispatcherFrame();
            var handle = new DialogHandle(frame);
            var previousFocus = Keyboard.FocusedElement;

            var dock = new DockPanel { LastChildFill = true };

            // ---- Επικεφαλίδα: [εικονίδιο] τίτλος/υπότιτλος [extra] [✕] ----
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (IconTile(icon) is { } tile)
            {
                tile.Margin = new Thickness(0, 0, 14, 0);
                tile.VerticalAlignment = string.IsNullOrEmpty(subtitle) ? VerticalAlignment.Center : VerticalAlignment.Top;
                header.Children.Add(tile);
            }

            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(title) ? "Invoice Manager" : title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Res<Brush>("Ink"),
                TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrEmpty(subtitle))
            {
                titles.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    FontSize = 14,
                    LineHeight = 21,
                    Margin = new Thickness(0, 6, 0, 0),
                    Foreground = Res<Brush>("Ink2"),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);

            if (headerExtra != null)
            {
                headerExtra.Margin = new Thickness(12, 0, 0, 0);
                headerExtra.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(headerExtra, 2);
                header.Children.Add(headerExtra);
            }

            var closeBtn = new Button
            {
                Style = Res<Style>("BtnGhost"),
                ToolTip = Loc.T("Btn_Close"),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Content = new Icon { Data = Res<Geometry>("Ico.X"), Width = 16, Height = 16, StrokeWidth = 2.2 }
            };
            AutomationProperties.SetName(closeBtn, Loc.T("Btn_Close"));
            closeBtn.Click += (_, _) => handle.Close(null);
            Grid.SetColumn(closeBtn, 3);
            header.Children.Add(closeBtn);

            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // ---- Κουμπιά ----
            Button? primary = null;
            if (buttons.Length > 0)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
                foreach (var b in buttons)
                {
                    var btn = new Button
                    {
                        Content = Loc.T(b.TextKey),
                        MinWidth = 96,
                        Margin = new Thickness(10, 0, 0, 0),
                        Style = Res<Style>(b.Kind switch { Kind.Danger => "BtnDanger", Kind.Secondary => "BtnSecondary", _ => "BtnPrimary" })
                    };
                    var value = b.Result;
                    btn.Click += (_, _) => handle.Close(value);
                    if (b.Kind != Kind.Secondary) primary = btn;
                    row.Children.Add(btn);
                }
                DockPanel.SetDock(row, Dock.Bottom);
                dock.Children.Add(row);
            }

            if (body != null)
            {
                body.Margin = new Thickness(0, 20, 0, 0);
                dock.Children.Add(body);
            }

            var card = new Border
            {
                Background = Res<Brush>("Card"),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(24),
                Width = Math.Min(width, Math.Max(320, _host!.ActualWidth - 48)),
                MaxHeight = Math.Max(240, _host.ActualHeight - 48),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = Res<System.Windows.Media.Effects.Effect>("DialogShadow"),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.96, 0.96),
                Child = dock
            };
            AutomationProperties.SetName(card, title);

            // ---- Επίπεδο: σκούρο φόντο + κάρτα. Καλύπτει όλο το παράθυρο, οπότε το από κάτω δεν δέχεται κλικ ----
            var layer = new Grid { Opacity = 0 };
            KeyboardNavigation.SetTabNavigation(layer, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetControlTabNavigation(layer, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(layer, KeyboardNavigationMode.Cycle);
            layer.Children.Add(new Border { Background = Res<Brush>("Backdrop") });
            layer.Children.Add(card);

            layer.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (handle.Cancelable) handle.Close(null);
                }
                else if (e.Key == Key.Enter && e.OriginalSource is not ButtonBase)
                {
                    // Να μην πατηθεί το «Προσθήκη» (IsDefault) του κύριου παραθύρου από πίσω
                    e.Handled = true;
                    if (handle.DefaultAction != null) handle.DefaultAction();
                    else primary?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            };

            setup?.Invoke(handle);
            if (!handle.Cancelable) closeBtn.Visibility = Visibility.Collapsed;

            layer.Loaded += (_, _) =>
            {
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                layer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
                var scale = (ScaleTransform)card.RenderTransform;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });

                layer.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    var target = handle.InitialFocus ?? (IInputElement?)primary ?? closeBtn;
                    Keyboard.Focus(target);
                });
            };

            _host.Children.Add(layer);
            OpenFrames.Add(frame);
            try
            {
                Dispatcher.PushFrame(frame); // περιμένει μέχρι το handle.Close(...)
            }
            finally
            {
                OpenFrames.Remove(frame);
                _host.Children.Remove(layer);
                if (previousFocus is UIElement { IsVisible: true } el) el.Focus();
            }
            return handle.Result;
        }

        /// <summary>Τετράγωνο με εικονίδιο (44×44, το λογότυπο 52×52 γεμάτο).</summary>
        private static FrameworkElement? IconTile(DialogIcon icon)
        {
            (string geo, string fg, string bg)? spec = icon switch
            {
                DialogIcon.Error => ("Ico.AlertCircle", "Red", "RedSoft"),
                DialogIcon.Delete => ("Ico.TrashLines", "Red", "RedSoft"),
                DialogIcon.Warning => ("Ico.Alert", "AmberIcon", "AmberSoft"),
                DialogIcon.Question => ("Ico.Question", "AccentInk", "AccentSoft"),
                DialogIcon.Info => ("Ico.Info", "AccentInk", "AccentSoft"),
                DialogIcon.Key => ("Ico.Key", "AccentInk", "AccentSoft"),
                DialogIcon.Chart => ("Ico.Chart", "AccentInk", "AccentSoft"),
                DialogIcon.Calendar => ("Ico.Calendar", "AccentInk", "AccentSoft"),
                DialogIcon.Logo => ("Ico.Receipt", "Card", "Accent"),
                _ => null
            };
            if (spec is not { } s) return null;

            double size = icon == DialogIcon.Logo ? 52 : 44;
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(icon == DialogIcon.Logo ? 14 : 12),
                Background = Res<Brush>(s.bg),
                Child = new Icon { Data = Res<Geometry>(s.geo), Foreground = Res<Brush>(s.fg), Width = size / 2, Height = size / 2 }
            };
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);
    }
}
