  //\\   OmenMon WPF Fan Control Page — manual speed control
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    public class FanPage : Page {

        private HardwareViewModel _vm;

        public FanPage(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;
            Background  = T.Br(T.BgMain);
            Title       = "风扇控制";

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(T.PageHeader("风扇控制", "手动调节 CPU 和 GPU 风扇转速"));

            // ── Manual-control toggle ────────────────────────────────────
            var toggleCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var toggleGrid = new Grid();
            toggleGrid.ColumnDefinitions.Add(new ColumnDefinition());
            toggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var togglePanel = new StackPanel();
            togglePanel.Children.Add(T.SectionTitle("手动控制"));
            var modeHint = T.Caption("", new Thickness(0, 2, 0, 0));
            modeHint.SetBinding(TextBlock.TextProperty, new Binding("FanModeLabel") { StringFormat = "当前：{0}　自动模式由 BIOS 控制风扇", Mode = BindingMode.OneWay });
            togglePanel.Children.Add(modeHint);
            toggleGrid.Children.Add(togglePanel);

            var modeChk = new CheckBox {
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand,
                Foreground = T.Br(T.FgPri), FontSize = 13
            };
            // OneWay + Click (not TwoWay + Checked/Unchecked): the binding only
            // reflects the live mode for display, while just a genuine user click
            // toggles it. Checked/Unchecked also fire when the binding updates on
            // page load, which used to re-enter manual mode and clobber the active
            // preset — e.g. picking 最大 then opening this page dropped it to 手动
            // and reseeded the sliders from the current fan %, freezing the read-out.
            modeChk.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsManualMode") { Mode = BindingMode.OneWay });
            modeChk.Click += (s, e) => {
                if(modeChk.IsChecked == true) _vm.EnterManualMode();
                else _vm.ApplyPreset("Auto");
            };
            Grid.SetColumn(modeChk, 1);
            toggleGrid.Children.Add(modeChk);
            toggleCard.Child = toggleGrid;
            root.Children.Add(toggleCard);

            // ── Fan cards ────────────────────────────────────────────────
            root.Children.Add(FanCard("🌀", "CPU 风扇", "CpuFanRpm", "CpuFanPct", "CpuFanLevel", T.Blue,  new Thickness(0, 0, 0, T.CardGap)));
            root.Children.Add(FanCard("🌀", "GPU 风扇", "GpuFanRpm", "GpuFanPct", "GpuFanLevel", T.Green, new Thickness(0, 0, 0, T.CardGap)));

            // ── Apply button ─────────────────────────────────────────────
            var applyBtn = T.PillButton("应用手动转速", T.Blue, filled: true);
            applyBtn.Click += (s, e) => _vm.ApplyManualLevels();
            applyBtn.SetBinding(Button.IsEnabledProperty, new Binding("ManualControlsEnabled") { Mode = BindingMode.OneWay });
            root.Children.Add(applyBtn);

            scroll.Content = root;
            Content = scroll;
        }

        private Border FanCard(string icon, string label, string rpmProp, string pctProp,
                               string levelProp, SWM.Color accent, Thickness margin) {
            var card = T.Card(margin);
            var sp = new StackPanel();

            // Header: icon + label | RPM
            var hdr = new Grid();
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lblPanel = new StackPanel { Orientation = Orientation.Horizontal };
            lblPanel.Children.Add(new TextBlock { Text = icon, FontSize = 18, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            lblPanel.Children.Add(new TextBlock { Text = label, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = T.Br(T.FgPri), VerticalAlignment = VerticalAlignment.Center });
            hdr.Children.Add(lblPanel);
            var rpmPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var rpmVal = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(accent), VerticalAlignment = VerticalAlignment.Center };
            rpmVal.SetBinding(TextBlock.TextProperty, new Binding(rpmProp) { Mode = BindingMode.OneWay });
            rpmPanel.Children.Add(rpmVal);
            rpmPanel.Children.Add(new TextBlock { Text = " RPM", FontSize = 12, Foreground = T.Br(T.FgMute), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 0, 3) });
            Grid.SetColumn(rpmPanel, 1);
            hdr.Children.Add(rpmPanel);
            sp.Children.Add(hdr);

            // Percentage
            var pctLbl = new TextBlock { FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(accent), Margin = new Thickness(0, 12, 0, 0) };
            pctLbl.SetBinding(TextBlock.TextProperty, new Binding(pctProp) { StringFormat = "{0}%", Mode = BindingMode.OneWay });
            sp.Children.Add(pctLbl);
            sp.Children.Add(T.Caption("转速百分比", new Thickness(0, 0, 0, 6)));

            // Slider
            var slider = T.FanSlider(accent);
            slider.Margin = new Thickness(0, 10, 0, 4);
            slider.SetBinding(Slider.ValueProperty, new Binding(levelProp) { Mode = BindingMode.TwoWay });
            slider.SetBinding(Slider.IsEnabledProperty, new Binding("ManualControlsEnabled") { Mode = BindingMode.OneWay });
            slider.PreviewMouseUp += (s, e) => { if(_vm.ManualControlsEnabled) _vm.ApplyManualLevels(); };
            sp.Children.Add(slider);

            // Scale
            var scale = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            for(int i = 0; i < 5; i++) scale.ColumnDefinitions.Add(new ColumnDefinition());
            string[] pcts = { "0%", "25%", "50%", "75%", "100%" };
            for(int i = 0; i < 5; i++) {
                var lbl = new TextBlock {
                    Text = pcts[i], FontSize = 10, Foreground = T.Br(T.FgMute),
                    HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : i == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Center
                };
                Grid.SetColumn(lbl, i);
                scale.Children.Add(lbl);
            }
            sp.Children.Add(scale);

            card.Child = sp;
            return card;
        }
    }
}
