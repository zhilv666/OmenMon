  //\\   OmenMon WPF Performance Mode Page — one-tap fan presets
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    public class ModePage : Page {

        private HardwareViewModel _vm;
        private readonly System.Collections.Generic.List<System.Action> _cardPainters
            = new System.Collections.Generic.List<System.Action>();
        private System.ComponentModel.PropertyChangedEventHandler _onVmChanged;

        public ModePage(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;
            Background  = T.Br(T.BgMain);
            Title       = "性能模式";

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(T.PageHeader("性能模式", "一键切换风扇预设"));

            // (icon, title, desc, preset, accent)
            var modes = new (string, string, string, string, SWM.Color)[] {
                ("⚙",  "自动",    "BIOS 自动调节\n跟随温度曲线",   "Auto",   T.Blue),
                ("🚀", "最大转速", "全速散热\n适合游戏 / 高负载",   "Max",    T.Red),
                ("⚡", "高速",    "较高转速\n性能与噪音平衡",       "High",   T.Amber),
                ("🌿", "中速",    "适合日常写代码\n开浏览器",       "Mid",    T.Green),
                ("🌙", "安静",    "最低转速\n极致安静体验",         "Silent", T.Violet),
            };

            var grid = new UniformGrid { Columns = 3 };
            double g = T.CardGap / 2;
            for(int i = 0; i < modes.Length; i++) {
                var (icon, title, desc, preset, accent) = modes[i];
                int col = i % 3, rowIdx = i / 3;
                var card = ModeCard(icon, title, desc, preset, accent);
                card.Margin = new Thickness(col == 0 ? 0 : g, 0, col == 2 ? 0 : g, rowIdx == 0 ? T.CardGap : 0);
                grid.Children.Add(card);
            }
            root.Children.Add(grid);

            // Reflect the active preset now, and stay in sync if it changes elsewhere
            RefreshActive();
            _onVmChanged = (s, e) => {
                if(e.PropertyName == nameof(HardwareViewModel.ActivePreset))
                    RefreshActive();
            };
            _vm.PropertyChanged += _onVmChanged;
            Unloaded += (s, e) => { if(_onVmChanged != null) _vm.PropertyChanged -= _onVmChanged; };

            scroll.Content = root;
            Content = scroll;
        }

        private void RefreshActive() {
            foreach(var paint in _cardPainters) paint();
        }

        private Border ModeCard(string icon, string title, string desc, string preset, SWM.Color accent) {
            var card = new Border {
                Background      = T.Br(T.BgCard),
                CornerRadius    = new CornerRadius(T.Radius),
                BorderBrush     = T.Br(T.BorderCol),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(16, 22, 16, 22),
                Cursor          = Cursors.Hand
            };

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock {
                Text = icon, FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(accent)
            });
            sp.Children.Add(new TextBlock {
                Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = T.Br(T.FgPri), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            });
            sp.Children.Add(new TextBlock {
                Text = desc, FontSize = 11, Foreground = T.Br(T.FgMute),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
            });

            var applyPill = new Border {
                CornerRadius        = new CornerRadius(6),
                Padding             = new Thickness(16, 6, 16, 6),
                Margin              = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var pillText = new TextBlock {
                FontSize = 11.5, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(accent), HorizontalAlignment = HorizontalAlignment.Center
            };
            applyPill.Child = pillText;
            sp.Children.Add(applyPill);

            card.Child = sp;

            // Highlight this card while its preset is the active fan mode.
            void Paint() {
                bool active = _vm.ActivePreset == preset;
                card.BorderBrush     = active ? new SolidColorBrush(accent) : T.Br(T.BorderCol);
                card.BorderThickness = new Thickness(active ? 2 : 1);
                card.Background      = active ? T.Tint(accent, 30) : T.Br(T.BgCard);
                applyPill.Background = active ? T.Tint(accent, 95) : T.Tint(accent, 55);
                pillText.Text        = active ? "● 使用中" : "应用";
            }
            card.MouseEnter += (s, e) => {
                if(_vm.ActivePreset != preset) { card.BorderBrush = new SolidColorBrush(accent); card.Background = T.Br(T.BgHover); }
            };
            card.MouseLeave += (s, e) => Paint();
            card.MouseLeftButtonUp += (s, e) => { _vm.ApplyPreset(preset); RefreshActive(); };

            _cardPainters.Add(Paint);
            Paint();
            return card;
        }
    }
}
