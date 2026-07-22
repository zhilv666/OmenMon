  //\\   OmenMon WPF Overlay Window — pure C#, transparent floating monitor
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;

namespace OmenMon.AppWpf {

    public class OverlayWindow : Window {

        static SolidColorBrush B(string hex) => new SolidColorBrush((SWM.Color) SWM.ColorConverter.ConvertFromString(hex));
        static SolidColorBrush B(SWM.Color c) => new SolidColorBrush(c);
        static readonly SWM.Color Blue  = SWM.Color.FromRgb(78, 158, 255);
        static readonly SWM.Color Green = SWM.Color.FromRgb(78, 255, 158);
        static readonly SWM.Color FgMute = SWM.Color.FromRgb(85, 85, 120);

        private HardwareViewModel _vm;

        public OverlayWindow(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;

            Title              = "OmenMon Overlay";
            Width              = 300;
            Height             = 250;
            WindowStyle        = WindowStyle.None;
            AllowsTransparency = true;
            Background         = Brushes.Transparent;
            Topmost            = true;
            ResizeMode         = ResizeMode.NoResize;
            ShowInTaskbar      = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Content = BuildContent();

            Loaded += (s, e) => {
                Left = SystemParameters.WorkArea.Right - ActualWidth - 20;
                Top  = SystemParameters.WorkArea.Bottom - ActualHeight - 20;
            };
        }

        private UIElement BuildContent() {
            var shell = new Border {
                Background      = B("#DD12121A"),
                CornerRadius    = new CornerRadius(12),
                BorderBrush     = B("#44FFFFFF"),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            grid.Children.Add(BuildHeader());
            var body = BuildBody();
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
            var status = BuildStatus();
            Grid.SetRow(status, 2);
            grid.Children.Add(status);

            shell.Child = grid;
            return shell;
        }

        private UIElement BuildHeader() {
            var hdr = new Grid { Background = B("#22FFFFFF") };
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.MouseLeftButtonDown += (s, e) => { if(e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var titlePanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            titlePanel.Children.Add(new TextBlock { Text = "⬡", FontSize = 13, Foreground = B(Blue), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            titlePanel.Children.Add(new TextBlock { Text = "THERMAL CONTROL", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = B("#A0A0C0"), FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center });
            hdr.Children.Add(titlePanel);

            // Pin — state is shown three ways (colored emoji ignores Foreground):
            // background tint + icon opacity + tooltip text
            var pin = new Button {
                Width = 28, Height = 28,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            var pinIcon = new TextBlock { Text = "📌", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            pin.Content = pinIcon;
            void RefreshPin() {
                bool on = Topmost;
                pin.Background = on ? B("#404E9EFF") : Brushes.Transparent;
                pinIcon.Opacity = on ? 1.0 : 0.4;
                pin.ToolTip = on ? "已置顶 — 点击取消" : "未置顶 — 点击置顶";
            }
            pin.MouseEnter += (s, e) => pin.Background = B("#22FFFFFF");
            pin.MouseLeave += (s, e) => RefreshPin();
            pin.Click += (s, e) => { Topmost = !Topmost; RefreshPin(); };
            RefreshPin();
            Grid.SetColumn(pin, 1);
            hdr.Children.Add(pin);

            // Close
            var close = OverlayBtn("✕");
            close.Margin = new Thickness(0, 0, 4, 0);
            close.Click += (s, e) => Hide();
            Grid.SetColumn(close, 2);
            hdr.Children.Add(close);

            return hdr;
        }

        private UIElement BuildBody() {
            var sp = new StackPanel { Margin = new Thickness(14, 8, 14, 4) };

            // PROCESSOR
            sp.Children.Add(SectionLabel("PROCESSOR", Blue));
            sp.Children.Add(new TextBlock { Text = "HP VICTUS 15-fa0xxx", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = B("#C0C0D8"), Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(TempRow("CpuTempBar", "CpuTemp", Blue));
            sp.Children.Add(FanRow("CpuFanRpm", Blue, new Thickness(0, 4, 0, 10)));

            // Separator
            sp.Children.Add(new Border { Height = 1, Background = B("#22FFFFFF"), Margin = new Thickness(0, 0, 0, 8) });

            // GRAPHICS
            sp.Children.Add(SectionLabel("GRAPHICS", Green));
            sp.Children.Add(new TextBlock { Text = "NVIDIA GeForce GTX 1650", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = B("#C0C0D8"), Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(TempRow("GpuTempBar", "GpuTemp", Green));
            sp.Children.Add(FanRow("GpuFanRpm", Green, new Thickness(0, 4, 0, 0)));

            return sp;
        }

        private UIElement BuildStatus() {
            var border = new Border { Background = B("#22FFFFFF"), CornerRadius = new CornerRadius(0, 0, 12, 12) };
            var grid = new Grid { Margin = new Thickness(12, 0, 12, 0) };

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = B(Green.ToString()), Margin = new Thickness(0, 0, 5, 0) });
            var statusText = new TextBlock { FontSize = 9, FontFamily = new FontFamily("Consolas"), Foreground = B("#888899"), VerticalAlignment = VerticalAlignment.Center };
            statusText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StatusText") { Mode = System.Windows.Data.BindingMode.OneWay });
            left.Children.Add(statusText);
            grid.Children.Add(left);

            var uptime = new TextBlock { FontSize = 9, FontFamily = new FontFamily("Consolas"), Foreground = B(FgMute.ToString()), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            uptime.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UptimeText") { StringFormat = "UPTIME: {0}", Mode = System.Windows.Data.BindingMode.OneWay });
            grid.Children.Add(uptime);

            border.Child = grid;
            return border;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static TextBlock SectionLabel(string text, SWM.Color c) =>
            new TextBlock { Text = text, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(c), FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 0, 0, 2) };

        private static UIElement TempRow(string barProp, string tempProp, SWM.Color c) {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var bar = new ProgressBar { Maximum = 1, Height = 4, VerticalAlignment = VerticalAlignment.Center, Background = B("#22333355"), Foreground = new SolidColorBrush(c), BorderThickness = new Thickness(0) };
            bar.SetBinding(ProgressBar.ValueProperty, new System.Windows.Data.Binding(barProp) { Mode = System.Windows.Data.BindingMode.OneWay });
            grid.Children.Add(bar);

            var tb = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(c), FontFamily = new FontFamily("Consolas"), Margin = new Thickness(8, 0, 0, 0) };
            var run = new System.Windows.Documents.Run();
            run.SetBinding(System.Windows.Documents.Run.TextProperty, new System.Windows.Data.Binding(tempProp) { Mode = System.Windows.Data.BindingMode.OneWay });
            tb.Inlines.Add(run);
            tb.Inlines.Add(new System.Windows.Documents.Run(" °C") { FontSize = 10, Foreground = B(FgMute.ToString()) });
            Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);
            return grid;
        }

        private static UIElement FanRow(string rpmProp, SWM.Color c, Thickness margin) {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = margin };
            sp.Children.Add(new TextBlock { Text = "🌀", FontSize = 11, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = "FAN SPEED", FontSize = 9, Foreground = B("#555578"), FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center });
            var rpm = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(c), Margin = new Thickness(8, 0, 2, 0), FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
            rpm.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(rpmProp) { Mode = System.Windows.Data.BindingMode.OneWay });
            sp.Children.Add(rpm);
            sp.Children.Add(new TextBlock { Text = " RPM", FontSize = 9, Foreground = B("#555578"), FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        private static Button OverlayBtn(string glyph) {
            var btn = new Button {
                Width = 28, Height = 28,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new TextBlock { Text = glyph, FontSize = 11, Foreground = B("#888899"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => btn.Background = B("#22FFFFFF");
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
            return btn;
        }
    }
}
