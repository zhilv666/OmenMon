  //\\   OmenMon WPF Main Window — dark control panel with sidebar navigation
 //  \\  https://omenmon.github.io/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    public class MainWindow : Window {

        private HardwareViewModel _vm;
        private Frame _content;
        private Button _activeNav;
        private string _currentPage = "Dashboard";

        // Nav registry: page-key → button, so switching is centralized
        private readonly System.Collections.Generic.Dictionary<string, Button> _navButtons
            = new System.Collections.Generic.Dictionary<string, Button>();

        // Always-on-top ("pin") toggle state
        private Button _pinBtn;
        private TextBlock _pinIcon;

        public MainWindow(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;

            Title              = "OmenMon";
            Width              = 980;
            Height             = 680;
            MinWidth           = 880;
            MinHeight          = 580;
            WindowStyle        = WindowStyle.None;
            AllowsTransparency = true;
            Background         = SWM.Brushes.Transparent;
            ResizeMode         = ResizeMode.CanResize;

            Content = BuildRoot();
            Navigate("Dashboard");
        }

        // ── Root: rounded shell with subtle border ─────────────────────────
        private UIElement BuildRoot() {
            var shell = new Border {
                Background       = T.Br(T.BgWindow),
                CornerRadius     = new CornerRadius(12),
                BorderBrush      = T.Br(T.BorderCol),
                BorderThickness  = new Thickness(1),
                Margin           = new Thickness(0)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildTitleBar());
            var body = BuildBody();
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            shell.Child = root;
            return shell;
        }

        // ── Title bar ──────────────────────────────────────────────────────
        private UIElement BuildTitleBar() {
            var bar = new Border {
                Background = T.Br(T.BgSide),
                CornerRadius = new CornerRadius(12, 12, 0, 0)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.MouseLeftButtonDown += (s, e) => { if(e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
            namePanel.Children.Add(new TextBlock { Text = "⬡", FontSize = 18, Foreground = T.Br(T.Blue), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            namePanel.Children.Add(new TextBlock { Text = "OmenMon", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = T.Br(T.FgPri), VerticalAlignment = VerticalAlignment.Center });
            namePanel.Children.Add(new TextBlock { Text = "  硬件监控与控制", FontSize = 11, Foreground = T.Br(T.FgMute), VerticalAlignment = VerticalAlignment.Center });
            grid.Children.Add(namePanel);

            var chrome = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(chrome, 1);
            var btnTheme = ChromeBtn(T.IsDark ? "🌙" : "☀", "主题：" + T.ModeName + "（点击切换）", false);
            btnTheme.Click += (s, e) => { T.CycleMode(); RebuildUI(); };
            var btnOverlay = ChromeBtn("⧉", "悬浮监控", false);
            btnOverlay.Click += (s, e) => WpfApp.Instance.ToggleOverlay();
            var btnMin = ChromeBtn("—", "最小化", false);
            btnMin.Click += (s, e) => WindowState = WindowState.Minimized;
            var btnClose = ChromeBtn("✕", "关闭", true);
            btnClose.Click += (s, e) => Hide();
            chrome.Children.Add(btnTheme);
            chrome.Children.Add(btnOverlay);
            chrome.Children.Add(BuildPinButton());
            chrome.Children.Add(btnMin);
            chrome.Children.Add(btnClose);
            grid.Children.Add(chrome);

            bar.Child = grid;
            return bar;
        }

        // ── Body: sidebar + content ────────────────────────────────────────
        private UIElement BuildBody() {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            grid.Children.Add(BuildSidebar());
            _content = new Frame {
                NavigationUIVisibility = NavigationUIVisibility.Hidden,
                Background = T.Br(T.BgMain)
            };
            // Round the bottom-right corner of the content area to match shell
            var contentHost = new Border {
                CornerRadius = new CornerRadius(0, 0, 12, 0),
                ClipToBounds = true,
                Child = _content
            };
            Grid.SetColumn(contentHost, 1);
            grid.Children.Add(contentHost);
            return grid;
        }

        // ── Sidebar ────────────────────────────────────────────────────────
        private UIElement BuildSidebar() {
            var shell = new Border {
                Background = T.Br(T.BgSide),
                CornerRadius = new CornerRadius(0, 0, 0, 12)
            };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nav = new StackPanel { Margin = new Thickness(12, 18, 12, 0) };
            nav.Children.Add(NavBtn("📊", "监控面板", "Dashboard"));
            nav.Children.Add(NavBtn("🌀", "风扇控制", "Fan"));
            nav.Children.Add(NavBtn("⚡", "性能模式", "Mode"));
            nav.Children.Add(NavBtn("📈", "曲线设置", "Curve"));
            nav.Children.Add(NavBtn("🪟", "悬浮监控", "Overlay"));
            nav.Children.Add(NavBtn("⚙", "设置", "Settings"));
            root.Children.Add(nav);

            // Hardware status mini-panel (live)
            var statusCard = new Border {
                Background = T.Br(T.BgCard),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(12, 0, 12, 16),
                BorderBrush = T.Br(T.BorderCol),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(statusCard, 1);
            var sc = new StackPanel();
            sc.Children.Add(new TextBlock { Text = "实时状态", Foreground = T.Br(T.FgMute), FontSize = 10, Margin = new Thickness(0, 0, 0, 10) });
            sc.Children.Add(SideStatRow("CPU 温度", "CpuTemp", "{0}°C", T.Blue));
            sc.Children.Add(SideBar("CpuTempBar", T.Blue));
            sc.Children.Add(SideStatRow("GPU 温度", "GpuTemp", "{0}°C", T.Green));
            sc.Children.Add(SideBar("GpuTempBar", T.Green));

            var modeRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            modeRow.ColumnDefinitions.Add(new ColumnDefinition());
            modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            modeRow.Children.Add(new TextBlock { Text = "风扇模式", FontSize = 11, Foreground = T.Br(T.FgMute) });
            var modeVal = new TextBlock { FontSize = 11, FontWeight = FontWeights.Medium, Foreground = T.Br(T.FgPri), HorizontalAlignment = HorizontalAlignment.Right };
            modeVal.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("FanModeLabel") { Mode = System.Windows.Data.BindingMode.OneWay });
            Grid.SetColumn(modeVal, 1);
            modeRow.Children.Add(modeVal);
            sc.Children.Add(modeRow);

            statusCard.Child = sc;
            root.Children.Add(statusCard);

            shell.Child = root;
            return shell;
        }

        // ── Navigation ─────────────────────────────────────────────────────
        private void Navigate(string page) {
            _currentPage = page;
            switch(page) {
                case "Dashboard": _content.Navigate(new DashboardPage(_vm)); break;
                case "Fan":       _content.Navigate(new FanPage(_vm));       break;
                case "Mode":      _content.Navigate(new ModePage(_vm));      break;
                case "Curve":     _content.Navigate(new CurvePage(_vm));     break;
                case "Overlay":   _content.Navigate(new OverlayPage(_vm));   break;
                case "Settings":  _content.Navigate(new SettingsPage(_vm));  break;
            }
            // Reflect active state
            if(_navButtons.TryGetValue(page, out var btn) && btn != _activeNav) {
                if(_activeNav != null) SetNavInactive(_activeNav);
                _activeNav = btn;
                SetNavActive(btn);
            }
        }

        // Rebuilds the whole window chrome + current page. Called after a
        // theme change: every control re-reads its colors from Theme.
        private void RebuildUI() {
            _navButtons.Clear();
            _activeNav = null;
            Content = BuildRoot();
            Navigate(_currentPage);
        }

        private Button NavBtn(string icon, string label, string page) {
            var btn = new Button {
                Background = SWM.Brushes.Transparent,
                Foreground = T.Br(T.FgSec),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 11, 14, 11),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 0),
                Tag = page
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = icon, Margin = new Thickness(0, 0, 12, 0), FontSize = 15 });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            btn.Content = sp;
            btn.Template = T.RoundedButtonTemplate(8);

            btn.MouseEnter += (s, e) => { if(btn != _activeNav) btn.Background = T.Br(T.BgHover); };
            btn.MouseLeave += (s, e) => { if(btn != _activeNav) btn.Background = SWM.Brushes.Transparent; };
            btn.Click += (s, e) => Navigate(page);

            _navButtons[page] = btn;
            if(_activeNav == null) { _activeNav = btn; SetNavActive(btn); }
            return btn;
        }

        private void SetNavActive(Button btn)   { btn.Background = T.Br(T.BgActive); btn.Foreground = T.Br(T.Blue); }
        private void SetNavInactive(Button btn) { btn.Background = SWM.Brushes.Transparent; btn.Foreground = T.Br(T.FgSec); }

        // ── Helpers ────────────────────────────────────────────────────────
        private Button ChromeBtn(string glyph, string tip, bool isClose) {
            var btn = new Button {
                Width = 34, Height = 30,
                Background = SWM.Brushes.Transparent,
                Foreground = T.Br(T.FgMute),
                BorderThickness = new Thickness(0),
                Content = new TextBlock { Text = glyph, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                Cursor = Cursors.Hand, ToolTip = tip
            };
            btn.Template = T.RoundedButtonTemplate(6);
            var hover = isClose ? T.Hex("#C0303C") : T.BgHover;
            btn.MouseEnter += (s, e) => { btn.Background = new SolidColorBrush(hover); btn.Foreground = T.Br(T.FgPri); };
            btn.MouseLeave += (s, e) => { btn.Background = SWM.Brushes.Transparent; btn.Foreground = T.Br(T.FgMute); };
            return btn;
        }

        // Always-on-top toggle. Uses the native Segoe MDL2 pin glyphs so the
        // pin stands upright (Pinned) when on and tilts (Pin) when off.
        private Button BuildPinButton() {
            _pinBtn = new Button {
                Width = 34, Height = 30,
                Background = SWM.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _pinIcon = new TextBlock {
                FontFamily = new SWM.FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pinBtn.Content = _pinIcon;
            _pinBtn.Template = T.RoundedButtonTemplate(6);
            _pinBtn.MouseEnter += (s, e) => { if(!Topmost) _pinBtn.Background = T.Br(T.BgHover); };
            _pinBtn.MouseLeave += (s, e) => RefreshPin();
            _pinBtn.Click += (s, e) => { Topmost = !Topmost; RefreshPin(); };
            RefreshPin();
            return _pinBtn;
        }

        private void RefreshPin() {
            bool on = Topmost;
            _pinIcon.Text       = ((char)(on ? 0xE840 : 0xE718)).ToString();   // pinned=upright pin, unpinned=tilted pin
            _pinIcon.Foreground = on ? T.Br(T.Blue) : T.Br(T.FgMute);
            _pinBtn.Background  = on ? T.Br(T.BgActive) : SWM.Brushes.Transparent;
            _pinBtn.ToolTip     = on ? "已置顶 · 点击取消置顶" : "置顶显示";
        }

        private Grid SideStatRow(string label, string valueProp, string fmt, SWM.Color accent) {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = T.Br(T.FgMute) });
            var val = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(accent), HorizontalAlignment = HorizontalAlignment.Right };
            val.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(valueProp) { StringFormat = fmt, Mode = System.Windows.Data.BindingMode.OneWay });
            Grid.SetColumn(val, 1);
            row.Children.Add(val);
            return row;
        }

        private ProgressBar SideBar(string prop, SWM.Color accent) {
            var pb = T.Bar(accent, 3);
            pb.Margin = new Thickness(0, 0, 0, 8);
            pb.SetBinding(ProgressBar.ValueProperty, new System.Windows.Data.Binding(prop) { Mode = System.Windows.Data.BindingMode.OneWay });
            return pb;
        }
    }
}
