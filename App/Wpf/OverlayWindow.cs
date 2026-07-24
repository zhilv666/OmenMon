  //\\   OmenMon WPF Overlay Window — compact translucent hardware monitor
 //  \\  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using SWM = System.Windows.Media;

namespace OmenMon.AppWpf {

    // A small always-on-top glass panel showing live CPU/GPU temp + fan.
    // Each component is a card with a circular temperature ring, a big accent
    // read-out, a fan-speed bar and RPM / rate stat badges.
    public class OverlayWindow : Window {

        // ── Overlay templates ──────────────────────────────────────────────
        // Card:    the original ring-gauge cards (rich, taller).
        // Compact: a flat horizontal info-bar per component (image #1 style).
        public enum OverlayStyle { Card, Compact }

        // Which template new overlay windows use. Changed from the sidebar
        // "悬浮监控" page; WpfApp recreates the window to apply it.
        public static OverlayStyle CurrentStyle = OverlayStyle.Card;

        // Live-adjustable appearance, shared across overlay windows and set
        // from the sidebar. Opacity 0.25–1.0, scale 0.6–1.6 (1.0 = 100%).
        public static double CurrentOpacity = 1.0;
        public static double CurrentScale   = 1.0;

        private readonly OverlayStyle _style;
        private ScaleTransform _scale;
        private Border _shellBorder;   // The outer glass background; opacity slider targets this only.

        // ── palette ────────────────────────────────────────────────────────
        static SolidColorBrush B(string hex) => new SolidColorBrush((SWM.Color) SWM.ColorConverter.ConvertFromString(hex));
        static SolidColorBrush B(SWM.Color c) => new SolidColorBrush(c);
        static readonly SWM.Color Blue  = SWM.Color.FromRgb(0x4E, 0x9E, 0xFF);   // CPU accent
        static readonly SWM.Color Green = SWM.Color.FromRgb(0x3F, 0xE0, 0x8A);   // GPU accent
        const string FgPri   = "#F2F2F8";
        const string FgSec   = "#AEAEC6";
        const string FgMute  = "#7A7A96";
        const string Glass   = "#E60E0E16";   // translucent dark backdrop (transparency preserved)
        const string Stroke  = "#22FFFFFF";
        const string Track   = "#22FFFFFF";

        // Segoe MDL2 glyphs
        const int GlyphFan   = 0xE72C;   // rotation / fan
        const int GlyphPin   = 0xE840;   // pinned
        const int GlyphUnpin = 0xE718;   // unpinned

        // Ring geometry
        static readonly Point RingCenter = new Point(60, 60);
        const double RingRadius = 50;
        const double RingStart  = 135;   // open at the bottom, 270° sweep
        const double RingSweep  = 270;

        private readonly HardwareViewModel _vm;
        private readonly List<Action> _updaters = new List<Action>();

        public OverlayWindow(HardwareViewModel vm) {
            _vm = vm;
            _style = CurrentStyle;
            DataContext = vm;

            Title                 = "OmenMon Overlay";
            double baseWidth      = _style == OverlayStyle.Compact ? 440 : 384;
            SizeToContent         = SizeToContent.WidthAndHeight;
            WindowStyle           = WindowStyle.None;
            AllowsTransparency    = true;
            Background            = Brushes.Transparent;
            Topmost               = true;
            ResizeMode            = ResizeMode.NoResize;
            ShowInTaskbar         = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            // Fixed-width content, scaled as a whole via a LayoutTransform so the
            // window (SizeToContent) grows/shrinks with the chosen size ratio.
            var content = (FrameworkElement)(_style == OverlayStyle.Compact ? BuildCompactContent() : BuildContent());
            content.Width = baseWidth;
            _scale = new ScaleTransform(ClampScale(CurrentScale), ClampScale(CurrentScale));
            content.LayoutTransform = _scale;
            Content = content;

            _vm.PropertyChanged += OnVmChanged;
            Closed += (s, e) => _vm.PropertyChanged -= OnVmChanged;

            Loaded += (s, e) => {
                Left = SystemParameters.WorkArea.Right  - ActualWidth  - 20;
                Top  = SystemParameters.WorkArea.Bottom - ActualHeight - 20;
                RunUpdaters();
            };
        }

        // Redraw the ring arcs whenever the polled data changes.
        private void OnVmChanged(object s, PropertyChangedEventArgs e) => Dispatcher.Invoke(RunUpdaters);
        private void RunUpdaters() { foreach(var u in _updaters) u(); }

        // ── Live appearance controls (driven from the sidebar) ─────────────
        public static double ClampOpacity(double v) => v < 0.25 ? 0.25 : v > 1.0 ? 1.0 : v;
        public static double ClampScale(double v)   => v < 0.6  ? 0.6  : v > 1.6 ? 1.6 : v;

        // Fades only the background shell; content (text/rings/bars) stays opaque.
        public void ApplyOpacity(double o) {
            CurrentOpacity = ClampOpacity(o);
            if(_shellBorder != null) _shellBorder.Opacity = CurrentOpacity;
        }

        // Scales the panel uniformly; SizeToContent resizes the window to match.
        public void ApplyScale(double s) {
            CurrentScale = ClampScale(s);
            if(_scale != null) { _scale.ScaleX = CurrentScale; _scale.ScaleY = CurrentScale; }
        }

        private UIElement BuildContent() {
            _shellBorder = new Border {
                Background      = B(Glass),
                CornerRadius    = new CornerRadius(16),
                BorderBrush     = B(Stroke),
                BorderThickness = new Thickness(1),
                Opacity         = ClampOpacity(CurrentOpacity)
            };

            var root = new StackPanel();
            root.Children.Add(BuildHeader());
            root.Children.Add(BuildComponent("CPU", "CpuTemp", () => _vm.CpuTempBar, "CpuFanBar", "CpuFanRpm", "CpuFanPct", Blue));
            root.Children.Add(BuildComponent("GPU", "GpuTemp", () => _vm.GpuTempBar, "GpuFanBar", "GpuFanRpm", "GpuFanPct", Green));
            root.Children.Add(BuildFooter());

            _shellBorder.Child = root;
            return _shellBorder;
        }

        // ── Compact template (image #1): flat horizontal info bars ─────────
        private UIElement BuildCompactContent() {
            _shellBorder = new Border {
                Background      = B(Glass),
                CornerRadius    = new CornerRadius(18),
                BorderBrush     = B(Stroke),
                BorderThickness = new Thickness(1),
                Opacity         = ClampOpacity(CurrentOpacity)
            };
            _shellBorder.MouseLeftButtonDown += (s, e) => DragMove();

            var root = new StackPanel();
            root.Children.Add(BuildCompactHeader());
            root.Children.Add(BuildCompactRow("CPU", "CpuTemp", "CpuTempBar", "CpuFanRpm", "CpuFanPct", Blue));
            root.Children.Add(BuildCompactRow("GPU", "GpuTemp", "GpuTempBar", "GpuFanRpm", "GpuFanPct", Green));
            root.Children.Add(BuildCompactFooter());

            _shellBorder.Child = root;
            return _shellBorder;
        }

        // Compact header: logo + title on the left, pin + close on the right.
        private UIElement BuildCompactHeader() {
            var hdr = new Grid { Background = Brushes.Transparent, Margin = new Thickness(20, 16, 12, 6) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.MouseLeftButtonDown += (s, e) => { if(e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = "⬢", FontSize = 20, Foreground = B(Blue), Margin = new Thickness(0, 0, 11, 0), VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(new TextBlock { Text = "OmenMon", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = B(FgPri), VerticalAlignment = VerticalAlignment.Center });
            hdr.Children.Add(title);

            var pin = BuildPin();
            Grid.SetColumn(pin, 1);
            hdr.Children.Add(pin);

            var close = CloseBtn();
            Grid.SetColumn(close, 2);
            hdr.Children.Add(close);

            return hdr;
        }

        // One compact row: ● LABEL   [fan] RPM | pct%   big°C   + full-width bar
        private UIElement BuildCompactRow(string label, string tempProp, string barProp,
                                          string rpmProp, string pctProp, SWM.Color accent) {
            var wrap = new StackPanel { Margin = new Thickness(20, 12, 20, 4) };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });   // label
            row.ColumnDefinitions.Add(new ColumnDefinition());                                 // fan + rate
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // big temp

            // ● LABEL
            var lbl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lbl.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = B(accent), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            lbl.Children.Add(new TextBlock { Text = label, FontSize = 21, FontWeight = FontWeights.Bold, Foreground = B(FgPri), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(lbl);

            // [fan] 0000 RPM | 00%
            var mid = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var chip = new Border {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(12),
                Background = B(SWM.Color.FromArgb(0x2E, accent.R, accent.G, accent.B)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
                Child = Mdl2(GlyphFan, 17, accent)
            };
            mid.Children.Add(chip);

            var rpm = new TextBlock { FontSize = 21, FontWeight = FontWeights.Bold, Foreground = B(FgPri), VerticalAlignment = VerticalAlignment.Center };
            rpm.SetBinding(TextBlock.TextProperty, OneWay(rpmProp));
            mid.Children.Add(rpm);
            mid.Children.Add(new TextBlock { Text = "RPM", FontSize = 13, Foreground = B(FgMute), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 0, 3) });

            mid.Children.Add(new Border { Width = 1, Height = 22, Background = B(Track), Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center });

            var pct = new TextBlock { FontSize = 21, FontWeight = FontWeights.Bold, Foreground = B(accent), VerticalAlignment = VerticalAlignment.Center };
            pct.SetBinding(TextBlock.TextProperty, new Binding(pctProp) { StringFormat = "{0}%", Mode = BindingMode.OneWay });
            mid.Children.Add(pct);

            Grid.SetColumn(mid, 1);
            row.Children.Add(mid);

            // big°C
            var temp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right };
            var bt = new TextBlock { FontSize = 40, FontWeight = FontWeights.Bold, Foreground = B(accent), VerticalAlignment = VerticalAlignment.Bottom };
            bt.SetBinding(TextBlock.TextProperty, OneWay(tempProp));
            temp.Children.Add(bt);
            temp.Children.Add(new TextBlock { Text = "℃", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = B(accent), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3, 0, 0, 6) });
            Grid.SetColumn(temp, 2);
            row.Children.Add(temp);

            wrap.Children.Add(row);

            var bar = RoundedBar(barProp, accent, 8);
            bar.Margin = new Thickness(0, 12, 0, 0);
            wrap.Children.Add(bar);

            return wrap;
        }

        // Compact footer: thin separator, status dot + text, uptime on the right.
        private UIElement BuildCompactFooter() {
            var wrap = new StackPanel();
            wrap.Children.Add(new Border { Height = 1, Background = B(Stroke), Margin = new Thickness(20, 12, 20, 0) });

            var grid = new Grid { Margin = new Thickness(20, 13, 20, 16) };

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = B(Green), Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center });
            var status = new TextBlock { FontSize = 14, Foreground = B(FgSec), VerticalAlignment = VerticalAlignment.Center };
            status.SetBinding(TextBlock.TextProperty, OneWay("StatusText"));
            left.Children.Add(status);
            grid.Children.Add(left);

            var uptime = new TextBlock { FontSize = 14, Foreground = B(FgMute), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            uptime.SetBinding(TextBlock.TextProperty, new Binding("UptimeText") { StringFormat = "运行 {0}", Mode = BindingMode.OneWay });
            grid.Children.Add(uptime);

            wrap.Children.Add(grid);
            return wrap;
        }


        private UIElement BuildHeader() {
            var hdr = new Grid { Height = 46, Background = Brushes.Transparent, Margin = new Thickness(4, 2, 4, 0) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.MouseLeftButtonDown += (s, e) => { if(e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            title.Children.Add(new TextBlock { Text = "⬢", FontSize = 17, Foreground = B(Blue), Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(new TextBlock { Text = "OmenMon", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = B(FgPri), VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(new Border { Margin = new Thickness(10, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = PulseLine(Blue, 28, 14) });
            hdr.Children.Add(title);

            var pin = BuildPin();
            Grid.SetColumn(pin, 1);
            hdr.Children.Add(pin);

            var close = CloseBtn();
            Grid.SetColumn(close, 2);
            hdr.Children.Add(close);

            return hdr;
        }

        // Pin toggle — filled accent chip when pinned, muted when not.
        private Button BuildPin() {
            var pin = new Button { Width = 38, Height = 34, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 4, 0) };
            pin.Template = Theme.RoundedButtonTemplate(9);
            var icon = Mdl2(GlyphPin, 14, Blue);
            pin.Content = icon;
            void Refresh() {
                bool on = Topmost;
                icon.Text = ((char)(on ? GlyphPin : GlyphUnpin)).ToString();
                icon.Foreground = on ? B(Blue) : B(FgMute);
                pin.Background = on ? B("#334E9EFF") : Brushes.Transparent;
                pin.ToolTip = on ? "已置顶 · 点击取消" : "置顶";
            }
            pin.MouseEnter += (s, e) => { if(!Topmost) pin.Background = B("#18FFFFFF"); };
            pin.MouseLeave += (s, e) => Refresh();
            pin.Click += (s, e) => { Topmost = !Topmost; Refresh(); };
            Refresh();
            return pin;
        }

        private Button CloseBtn() {
            var btn = new Button {
                Width = 38, Height = 34, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0),
                Content = new TextBlock { Text = "✕", FontSize = 13, Foreground = B(FgSec), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                ToolTip = "隐藏"
            };
            btn.Template = Theme.RoundedButtonTemplate(9);
            btn.MouseEnter += (s, e) => btn.Background = B("#33C0303C");
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
            btn.Click += (s, e) => Hide();
            return btn;
        }

        // ── One component card: ring gauge + read-out + fan bar + stats ────
        private UIElement BuildComponent(string label, string tempProp, Func<double> frac,
                                         string fanBarProp, string rpmProp, string pctProp, SWM.Color accent) {
            var card = new Border {
                Background      = B(CardBg(accent)),
                CornerRadius    = new CornerRadius(14),
                BorderBrush     = B(SWM.Color.FromArgb(0x66, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(16, 14, 18, 14),
                Margin          = new Thickness(12, 10, 12, 2),
                Effect          = new DropShadowEffect { Color = accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.28 }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var ring = BuildRing(tempProp, frac, accent);
            ring.Margin = new Thickness(0, 0, 16, 0);
            ring.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(ring);

            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(col, 1);

            // Title row: ● LABEL .................... big accent temp °C
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lbl.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = B(accent), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) });
            lbl.Children.Add(new TextBlock { Text = label, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = B(FgPri), VerticalAlignment = VerticalAlignment.Center });
            titleRow.Children.Add(lbl);

            var bigTemp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            var bt = new TextBlock { FontSize = 30, FontWeight = FontWeights.Bold, Foreground = B(accent), VerticalAlignment = VerticalAlignment.Bottom };
            bt.SetBinding(TextBlock.TextProperty, OneWay(tempProp));
            bigTemp.Children.Add(bt);
            bigTemp.Children.Add(new TextBlock { Text = "℃", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = B(accent), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 0, 4) });
            Grid.SetColumn(bigTemp, 1);
            titleRow.Children.Add(bigTemp);
            col.Children.Add(titleRow);

            // Fan label
            var fanLbl = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(1, 9, 0, 0) };
            fanLbl.Children.Add(Mdl2(GlyphFan, 12, accent, HorizontalAlignment.Left));
            fanLbl.Children.Add(new TextBlock { Text = "风扇", FontSize = 12, Foreground = B(FgMute), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) });
            col.Children.Add(fanLbl);

            // Fan-speed bar
            var bar = RoundedBar(fanBarProp, accent, 7);
            bar.Margin = new Thickness(0, 7, 0, 0);
            col.Children.Add(bar);

            // Stats: [fan] RPM   |   [pulse] rate
            var stats = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            stats.ColumnDefinitions.Add(new ColumnDefinition());

            var rpm = StatBadge(Mdl2(GlyphFan, 15, accent), rpmProp, null, "RPM", accent);
            stats.Children.Add(rpm);

            var divider = new Border { Width = 1, Height = 32, Background = B(Track), Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(divider, 1);
            stats.Children.Add(divider);

            var rate = StatBadge(PulseLine(accent, 18, 14), pctProp, "{0}%", "转速", accent);
            Grid.SetColumn(rate, 2);
            stats.Children.Add(rate);

            col.Children.Add(stats);
            grid.Children.Add(col);

            card.Child = grid;
            return card;
        }

        // Circular temperature gauge with the value in the centre.
        private FrameworkElement BuildRing(string tempProp, Func<double> frac, SWM.Color accent) {
            var g = new Grid { Width = 120, Height = 120 };

            var track = new Path { Stroke = B(Track), StrokeThickness = 9, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Data = Arc(RingStart, RingSweep) };
            g.Children.Add(track);

            var fill = new Path { Stroke = B(accent), StrokeThickness = 9, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            _updaters.Add(() => fill.Data = Arc(RingStart, RingSweep * Clamp01(frac())));
            g.Children.Add(fill);

            var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var num = new TextBlock { FontSize = 34, FontWeight = FontWeights.Bold, Foreground = B(accent), HorizontalAlignment = HorizontalAlignment.Center };
            num.SetBinding(TextBlock.TextProperty, OneWay(tempProp));
            center.Children.Add(num);
            center.Children.Add(new TextBlock { Text = "℃", FontSize = 12, Foreground = B(FgMute), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) });
            g.Children.Add(center);

            return g;
        }

        // A stat badge: round accent chip + big value over a muted caption.
        private UIElement StatBadge(UIElement glyph, string valueProp, string fmt, string caption, SWM.Color accent) {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var chip = new Border {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
                Background = B(SWM.Color.FromArgb(0x2E, accent.R, accent.G, accent.B)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0),
                Child = glyph
            };
            row.Children.Add(chip);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var value = new TextBlock { FontSize = 17, FontWeight = FontWeights.Bold, Foreground = B(FgPri) };
            value.SetBinding(TextBlock.TextProperty, fmt == null ? OneWay(valueProp) : new Binding(valueProp) { StringFormat = fmt, Mode = BindingMode.OneWay });
            text.Children.Add(value);
            text.Children.Add(new TextBlock { Text = caption, FontSize = 10, Foreground = B(FgMute), Margin = new Thickness(0, -1, 0, 0) });
            row.Children.Add(text);

            return row;
        }

        // ── Footer: status + uptime ────────────────────────────────────────
        private UIElement BuildFooter() {
            var border = new Border { Background = B("#14FFFFFF"), CornerRadius = new CornerRadius(0, 0, 15, 15), Padding = new Thickness(18, 9, 18, 10), Margin = new Thickness(0, 8, 0, 0) };
            var grid = new Grid();

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = B(Green), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            var status = new TextBlock { FontSize = 12, Foreground = B(FgSec), VerticalAlignment = VerticalAlignment.Center };
            status.SetBinding(TextBlock.TextProperty, OneWay("StatusText"));
            left.Children.Add(status);
            grid.Children.Add(left);

            var uptime = new TextBlock { FontSize = 12, Foreground = B(FgMute), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            uptime.SetBinding(TextBlock.TextProperty, new Binding("UptimeText") { StringFormat = "运行 {0}", Mode = BindingMode.OneWay });
            grid.Children.Add(uptime);

            border.Child = grid;
            return border;
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private static Binding OneWay(string path) => new Binding(path) { Mode = BindingMode.OneWay };
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // Dark card base tinted slightly toward the accent, kept translucent.
        private static SWM.Color CardBg(SWM.Color a) {
            byte Mix(byte b, byte t) => (byte)(b * 0.88 + t * 0.12);
            return SWM.Color.FromArgb(0xCC, Mix(0x16, a.R), Mix(0x16, a.G), Mix(0x1E, a.B));
        }

        private static TextBlock Mdl2(int code, double size, SWM.Color color, HorizontalAlignment ha = HorizontalAlignment.Center) => new TextBlock {
            Text = ((char)code).ToString(),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size, Foreground = B(color),
            HorizontalAlignment = ha, VerticalAlignment = VerticalAlignment.Center
        };

        // A small ECG-style pulse line (tintable, unlike an emoji).
        private static Polyline PulseLine(SWM.Color c, double w, double h) {
            double m = h / 2;
            return new Polyline {
                Stroke = B(c), StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Points = new PointCollection(new[] {
                    new Point(0, m), new Point(w * 0.18, m), new Point(w * 0.34, m - h * 0.42),
                    new Point(w * 0.52, m + h * 0.42), new Point(w * 0.66, m - h * 0.26),
                    new Point(w * 0.78, m), new Point(w, m)
                })
            };
        }

        // A rounded track/indicator progress bar (default WPF bars are square).
        private static ProgressBar RoundedBar(string valueProp, SWM.Color accent, double height) {
            double rad = height / 2;
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
                " xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ProgressBar'>" +
                "  <Border CornerRadius='" + rad + "' Background='" + Track + "'>" +
                "    <Border x:Name='PART_Indicator' CornerRadius='" + rad + "' HorizontalAlignment='Left'>" +
                "      <Border.Background><SolidColorBrush Color='" + accent + "'/></Border.Background>" +
                "    </Border>" +
                "  </Border>" +
                "</ControlTemplate>";
            var bar = new ProgressBar { Maximum = 1, Height = height, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            bar.Template = (ControlTemplate) XamlReader.Parse(xaml);
            bar.SetBinding(ProgressBar.ValueProperty, OneWay(valueProp));
            return bar;
        }

        // Arc path over the ring circle: 0° = 3 o'clock, clockwise, y-down.
        private static Geometry Arc(double startDeg, double sweepDeg) {
            if(sweepDeg <= 0.05) return Geometry.Empty;
            if(sweepDeg > 359.99) sweepDeg = 359.99;
            Point p0 = OnCircle(startDeg);
            Point p1 = OnCircle(startDeg + sweepDeg);
            var fig = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment(p1, new Size(RingRadius, RingRadius), 0, sweepDeg > 180, SweepDirection.Clockwise, true));
            var g = new PathGeometry();
            g.Figures.Add(fig);
            g.Freeze();
            return g;
        }

        private static Point OnCircle(double deg) {
            double a = deg * Math.PI / 180.0;
            return new Point(RingCenter.X + RingRadius * Math.Cos(a), RingCenter.Y + RingRadius * Math.Sin(a));
        }
    }
}
