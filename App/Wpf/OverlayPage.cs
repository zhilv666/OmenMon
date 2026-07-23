  //\\   OmenMon WPF Overlay Page — pick the floating-monitor template
 //  \\  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    // Lets the user pick which floating-monitor template the overlay uses,
    // and show / hide it. Selecting a template applies it live via WpfApp.
    public class OverlayPage : Page {

        private readonly HardwareViewModel _vm;
        private readonly Dictionary<OverlayWindow.OverlayStyle, Border> _cards
            = new Dictionary<OverlayWindow.OverlayStyle, Border>();

        public OverlayPage(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;
            Background   = T.Br(T.BgMain);
            Title        = "悬浮监控";

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(T.PageHeader("悬浮监控", "选择悬浮监控的样式模板，并控制其显示"));

            // ── Card: show / hide ────────────────────────────────────────
            var showCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var showGrid = new Grid();
            showGrid.ColumnDefinitions.Add(new ColumnDefinition());
            showGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var showText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            showText.Children.Add(T.SectionTitle("悬浮窗口", new Thickness(0, 0, 0, 4)));
            showText.Children.Add(T.Caption("在桌面显示一个始终置顶的小型监控面板"));
            showGrid.Children.Add(showText);

            var toggleBtn = T.PillButton("显示 / 隐藏", T.Blue, true);
            toggleBtn.VerticalAlignment = VerticalAlignment.Center;
            toggleBtn.Click += (s, e) => WpfApp.Instance.ToggleOverlay();
            Grid.SetColumn(toggleBtn, 1);
            showGrid.Children.Add(toggleBtn);

            showCard.Child = showGrid;
            root.Children.Add(showCard);

            // ── Card: appearance (opacity + size) ────────────────────────
            var apCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var apStack = new StackPanel();
            apStack.Children.Add(T.SectionTitle("外观调节", new Thickness(0, 0, 0, 4)));
            apStack.Children.Add(T.Caption("拖动滑块实时调整悬浮监控的透明度与大小比例",
                new Thickness(0, 0, 0, 10)));

            apStack.Children.Add(AppearanceSlider("透明度", 25, 100,
                OverlayWindow.CurrentOpacity * 100,
                v => WpfApp.Instance.SetOverlayOpacity(v / 100.0)));
            apStack.Children.Add(AppearanceSlider("大小比例", 60, 160,
                OverlayWindow.CurrentScale * 100,
                v => WpfApp.Instance.SetOverlayScale(v / 100.0)));

            apCard.Child = apStack;
            root.Children.Add(apCard);

            // ── Card: template picker ────────────────────────────────────
            var tplCard = T.Card();
            var tplStack = new StackPanel();
            tplStack.Children.Add(T.SectionTitle("样式模板", new Thickness(0, 0, 0, 4)));
            tplStack.Children.Add(T.Caption("点击选择悬浮监控的外观；切换后立即生效",
                new Thickness(0, 0, 0, 16)));

            var options = new Grid();
            options.ColumnDefinitions.Add(new ColumnDefinition());
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            options.ColumnDefinitions.Add(new ColumnDefinition());

            var cardOpt = BuildTemplateCard(
                OverlayWindow.OverlayStyle.Card, "经典卡片",
                "环形温度仪表 + 卡片布局，信息丰富、层次分明", PreviewCard());
            options.Children.Add(cardOpt);

            var compactOpt = BuildTemplateCard(
                OverlayWindow.OverlayStyle.Compact, "简约信息条",
                "横向信息条 + 进度条，紧凑清爽、一目了然", PreviewCompact());
            Grid.SetColumn(compactOpt, 2);
            options.Children.Add(compactOpt);

            tplStack.Children.Add(options);
            tplCard.Child = tplStack;
            root.Children.Add(tplCard);

            scroll.Content = root;
            Content = scroll;

            Paint();
        }

        // A selectable template option: preview thumbnail + name + description.
        private Border BuildTemplateCard(OverlayWindow.OverlayStyle style, string name,
                                         string desc, UIElement preview) {
            var card = new Border {
                Background      = T.Br(T.BgCard2),
                CornerRadius    = new CornerRadius(T.RadiusSm),
                BorderThickness = new Thickness(2),
                BorderBrush     = T.Br(T.BorderCol),
                Padding         = new Thickness(14),
                Cursor          = Cursors.Hand
            };

            var stack = new StackPanel();

            var thumb = new Border {
                Height          = 118,
                Background      = T.Br("#0C0C13"),
                CornerRadius    = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush     = T.Br(T.BorderCol),
                Padding         = new Thickness(10),
                Margin          = new Thickness(0, 0, 0, 12),
                Child           = preview
            };
            stack.Children.Add(thumb);

            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var check = new TextBlock {
                Text = "✓", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = T.Br(T.Blue), Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed
            };
            head.Children.Add(check);
            head.Children.Add(new TextBlock {
                Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = T.Br(T.FgPri)
            });
            stack.Children.Add(head);

            stack.Children.Add(new TextBlock {
                Text = desc, FontSize = 11.5, Foreground = T.Br(T.FgMute), TextWrapping = TextWrapping.Wrap
            });

            card.Child = stack;
            card.Tag   = check;   // stash the check mark so Paint() can toggle it
            card.MouseLeftButtonUp += (s, e) => Select(style);
            _cards[style] = card;
            return card;
        }

        private void Select(OverlayWindow.OverlayStyle style) {
            WpfApp.Instance.SetOverlayStyle(style);
            Paint();
        }

        // A labelled slider row: title + live percentage on the right, slider below.
        private FrameworkElement AppearanceSlider(string title, double min, double max,
                                                  double current, Action<double> apply) {
            var wrap = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock {
                Text = title, FontSize = 13.5, FontWeight = FontWeights.Medium, Foreground = T.Br(T.FgPri)
            });
            var valLbl = new TextBlock {
                FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = T.Br(T.Blue),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            valLbl.Text = string.Format("{0:0}%", current);
            Grid.SetColumn(valLbl, 1);
            head.Children.Add(valLbl);
            wrap.Children.Add(head);

            var slider = T.FanSlider(T.Blue);
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Value   = Math.Min(Math.Max(current, min), max);
            slider.Margin  = new Thickness(0, 10, 0, 0);
            slider.ValueChanged += (s, e) => {
                valLbl.Text = string.Format("{0:0}%", slider.Value);
                apply(slider.Value);
            };
            wrap.Children.Add(slider);

            return wrap;
        }

        // Highlights the active template and shows its check mark.
        private void Paint() {
            foreach(var kv in _cards) {
                bool on = kv.Key == OverlayWindow.CurrentStyle;
                kv.Value.BorderBrush = on ? T.Br(T.Blue) : T.Br(T.BorderCol);
                kv.Value.Background  = on ? T.Tint(T.Blue, 26) : T.Br(T.BgCard2);
                if(kv.Value.Tag is TextBlock check)
                    check.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ── Miniature previews (schematic, not live) ───────────────────────
        private static SolidColorBrush B(string hex) => T.Br(hex);
        private static readonly SWM.Color Blue  = SWM.Color.FromRgb(0x4E, 0x9E, 0xFF);
        private static readonly SWM.Color Green = SWM.Color.FromRgb(0x3F, 0xE0, 0x8A);

        // "经典卡片" preview: two accent cards each with a ring + bar.
        private UIElement PreviewCard() {
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            col.Children.Add(MiniRingCard(Blue));
            col.Children.Add(MiniRingCard(Green));
            return col;
        }

        private UIElement MiniRingCard(SWM.Color accent) {
            var card = new Border {
                Background = new SolidColorBrush(SWM.Color.FromArgb(0x33, accent.R, accent.G, accent.B)),
                BorderBrush = new SolidColorBrush(SWM.Color.FromArgb(0x88, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 6, 7, 6),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var g = new StackPanel { Orientation = Orientation.Horizontal };
            card.Child = g;
            // ring
            var ring = new System.Windows.Shapes.Ellipse {
                Width = 20, Height = 20, StrokeThickness = 3,
                Stroke = new SolidColorBrush(accent), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            g.Children.Add(ring);
            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 150 };
            lines.Children.Add(MiniLine(accent, 44, 5));
            lines.Children.Add(new Border { Height = 5 });
            lines.Children.Add(MiniBar(accent));
            g.Children.Add(lines);
            return card;
        }

        // "简约信息条" preview: two flat rows each with a dot, text, and bar.
        private UIElement PreviewCompact() {
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            col.Children.Add(MiniRow(Blue));
            col.Children.Add(new Border { Height = 10 });
            col.Children.Add(MiniRow(Green));
            return col;
        }

        private UIElement MiniRow(SWM.Color accent) {
            var wrap = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new System.Windows.Shapes.Ellipse {
                Width = 8, Height = 8, Fill = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            top.Children.Add(MiniLine(accent, 40, 6));
            top.Children.Add(new Border { Width = 10 });
            top.Children.Add(MiniLine(SWM.Color.FromRgb(0x7A, 0x7A, 0x96), 60, 6));
            wrap.Children.Add(top);
            wrap.Children.Add(new Border { Height = 6 });
            wrap.Children.Add(MiniBar(accent));
            return wrap;
        }

        private static Border MiniLine(SWM.Color c, double w, double h) => new Border {
            Width = w, Height = h, CornerRadius = new CornerRadius(h / 2),
            Background = new SolidColorBrush(SWM.Color.FromArgb(0xCC, c.R, c.G, c.B)),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
        };

        private static Border MiniBar(SWM.Color accent) {
            var track = new Border {
                Height = 5, CornerRadius = new CornerRadius(2.5),
                Background = new SolidColorBrush(SWM.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF))
            };
            var fill = new Border {
                Height = 5, Width = 90, CornerRadius = new CornerRadius(2.5),
                Background = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var grid = new Grid();
            grid.Children.Add(track);
            grid.Children.Add(fill);
            return new Border { Child = grid };
        }
    }
}
