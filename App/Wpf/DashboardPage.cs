  //\\   OmenMon WPF Dashboard Page — real-time monitoring
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SWM = System.Windows.Media;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    public class DashboardPage : Page {

        public DashboardPage(HardwareViewModel vm) {
            DataContext = vm;
            Background  = T.Br(T.BgMain);
            Title       = "监控面板";

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(T.PageHeader("监控面板", "实时监控系统温度与风扇状态"));

            // 2×2 metric cards
            var grid = new UniformGrid { Columns = 2 };
            double g = T.CardGap / 2;
            grid.Children.Add(MonCard("🌡", "CPU 温度", "CpuTemp", "{0}°C", "CpuTempBar", T.Blue,  new Thickness(0, 0, g, T.CardGap)));
            grid.Children.Add(MonCard("🖥", "GPU 温度", "GpuTemp", "{0}°C", "GpuTempBar", T.Green, new Thickness(g, 0, 0, T.CardGap)));
            grid.Children.Add(MonCard("🌀", "CPU 风扇", "CpuFanRpm", "{0} RPM", "CpuFanBar", T.Blue,  new Thickness(0, 0, g, 0), sub: "CpuFanPct"));
            grid.Children.Add(MonCard("🌀", "GPU 风扇", "GpuFanRpm", "{0} RPM", "GpuFanBar", T.Green, new Thickness(g, 0, 0, 0), sub: "GpuFanPct"));
            root.Children.Add(grid);

            // Status strip
            var statusCard = T.Card(new Thickness(0, T.CardGap, 0, 0));
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = T.Br(T.Green), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            statusRow.Children.Add(dot);
            var statusTxt = new TextBlock { FontSize = 12.5, Foreground = T.Br(T.FgSec), VerticalAlignment = VerticalAlignment.Center };
            statusTxt.SetBinding(TextBlock.TextProperty, OneWay("StatusText"));
            statusRow.Children.Add(statusTxt);
            var uptime = new TextBlock { FontSize = 12, Foreground = T.Br(T.FgMute), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            uptime.SetBinding(TextBlock.TextProperty, new Binding("UptimeText") { StringFormat = "运行时长  {0}", Mode = BindingMode.OneWay });
            var statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusGrid.Children.Add(statusRow);
            Grid.SetColumn(uptime, 1);
            statusGrid.Children.Add(uptime);
            statusCard.Child = statusGrid;
            root.Children.Add(statusCard);

            scroll.Content = root;
            Content = scroll;
        }

        private static Binding OneWay(string path) => new Binding(path) { Mode = BindingMode.OneWay };

        private Border MonCard(string icon, string label, string valueProp, string valueFmt,
                               string barProp, SWM.Color accent, Thickness margin, string sub = null) {
            var card = T.Card(margin);

            var sp = new StackPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            header.Children.Add(new TextBlock { Text = icon, FontSize = 15, Margin = new Thickness(0, 0, 8, 0) });
            header.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = T.Br(T.FgSec), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(header);

            var val = new TextBlock { FontSize = 42, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(accent) };
            val.SetBinding(TextBlock.TextProperty, new Binding(valueProp) { StringFormat = valueFmt, Mode = BindingMode.OneWay });
            sp.Children.Add(val);

            if(sub != null) {
                var subLbl = new TextBlock { FontSize = 12, Foreground = T.Br(T.FgMute), Margin = new Thickness(0, 2, 0, 4) };
                subLbl.SetBinding(TextBlock.TextProperty, new Binding(sub) { StringFormat = "{0}%", Mode = BindingMode.OneWay });
                sp.Children.Add(subLbl);
            }

            var bar = T.Bar(accent);
            bar.Margin = new Thickness(0, 12, 0, 0);
            bar.SetBinding(ProgressBar.ValueProperty, OneWay(barProp));
            sp.Children.Add(bar);

            card.Child = sp;
            return card;
        }
    }
}
