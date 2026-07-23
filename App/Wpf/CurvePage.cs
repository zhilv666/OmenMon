  //\\   OmenMon WPF Curve Settings Page — temperature→speed curve editor
 //  \\  Named curves as chips, themed table, XML import/export.
     //  https://omenmon.github.io/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    public class CurvePage : Page {

        private HardwareViewModel _vm;
        private ObservableCollection<HardwareViewModel.CurveRow> _rows;
        private DataGrid _grid;
        private WrapPanel _chips;
        private TextBlock _toast;
        private string _selected;

        public CurvePage(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;
            Background  = T.Br(T.BgMain);
            Title       = "曲线设置";

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(T.PageHeader("曲线设置", "编辑温度-转速曲线：到达某温度时风扇转到对应百分比，自动模式按所选曲线运行"));

            // ── Card 1: curve picker + run controls ─────────────────────
            var pickCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var pickStack = new StackPanel();
            pickStack.Children.Add(T.SectionTitle("选择曲线", new Thickness(0, 0, 0, 10)));

            _chips = new WrapPanel();
            BuildChips();
            pickStack.Children.Add(_chips);

            var runRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            var runBtn = T.PillButton("▶  运行此曲线", T.Green);
            runBtn.Click += (s, e) => {
                if(_selected == null) { Toast("请先选择一条曲线"); return; }
                _vm.RunCurve(_selected);
                Toast($"已启动曲线「{_selected}」— 风扇将按温度自动调速");
            };
            var stopBtn = T.PillButton("■  停止", T.Red);
            stopBtn.Margin = new Thickness(10, 0, 0, 0);
            stopBtn.Click += (s, e) => { _vm.StopCurve(); Toast("已停止曲线，交回 BIOS 控制"); };
            runRow.Children.Add(runBtn);
            runRow.Children.Add(stopBtn);

            var badge = new TextBlock { FontSize = 12, Foreground = T.Br(T.FgSec), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            badge.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("FanModeLabel") { StringFormat = "当前状态：{0}", Mode = System.Windows.Data.BindingMode.OneWay });
            runRow.Children.Add(badge);
            pickStack.Children.Add(runRow);
            pickCard.Child = pickStack;
            root.Children.Add(pickCard);

            // ── Card 2: threshold table ──────────────────────────────────
            var gridCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var gridStack = new StackPanel();
            gridStack.Children.Add(T.SectionTitle("温度阈值表"));
            gridStack.Children.Add(T.Caption("温度 °C → 风扇转速 %（0-100）。双击单元格编辑。", new Thickness(0, 0, 0, 12)));

            _rows = new ObservableCollection<HardwareViewModel.CurveRow>();
            _grid = BuildGrid();
            gridStack.Children.Add(_grid);

            var tblBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var addBtn = T.PillButton("＋ 添加行", T.Blue);
            addBtn.Click += (s, e) => {
                int nextTemp = _rows.Count > 0 ? System.Math.Min(_rows.Max(r => r.Temperature) + 5, 100) : 40;
                _rows.Add(new HardwareViewModel.CurveRow { Temperature = nextTemp, CpuPercent = 50, GpuPercent = 50 });
            };
            var delBtn = T.PillButton("－ 删除选中行", T.Amber);
            delBtn.Margin = new Thickness(10, 0, 0, 0);
            delBtn.Click += (s, e) => {
                foreach(var r in _grid.SelectedItems.Cast<object>().OfType<HardwareViewModel.CurveRow>().ToList())
                    _rows.Remove(r);
            };
            var saveBtn = T.PillButton("保存曲线", T.Blue, filled: true);
            saveBtn.Margin = new Thickness(10, 0, 0, 0);
            saveBtn.Click += (s, e) => {
                if(_selected == null) { Toast("请先选择一条曲线"); return; }
                _vm.SaveCurveRows(_selected, new List<HardwareViewModel.CurveRow>(_rows));
                Toast($"已保存曲线「{_selected}」到配置文件");
            };
            tblBtns.Children.Add(addBtn);
            tblBtns.Children.Add(delBtn);
            tblBtns.Children.Add(saveBtn);
            gridStack.Children.Add(tblBtns);
            gridCard.Child = gridStack;
            root.Children.Add(gridCard);

            // ── Card 3: import / export ──────────────────────────────────
            var ioCard = T.Card();
            var ioStack = new StackPanel();
            ioStack.Children.Add(T.SectionTitle("配置导入 / 导出"));
            ioStack.Children.Add(T.Caption("导出 / 导入完整配置（所有曲线与设置）为 XML 文件。", new Thickness(0, 0, 0, 12)));
            var ioBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var exportBtn = T.PillButton("导出配置…", T.Green);
            exportBtn.Click += (s, e) => DoExport();
            var importBtn = T.PillButton("导入配置…", T.Blue);
            importBtn.Margin = new Thickness(10, 0, 0, 0);
            importBtn.Click += (s, e) => DoImport();
            ioBtns.Children.Add(exportBtn);
            ioBtns.Children.Add(importBtn);
            ioStack.Children.Add(ioBtns);
            _toast = new TextBlock { FontSize = 12, Foreground = T.Br(T.Green), Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
            ioStack.Children.Add(_toast);
            ioCard.Child = ioStack;
            root.Children.Add(ioCard);

            scroll.Content = root;
            Content = scroll;

            // Select the first curve initially
            var names = _vm.CurveNames;
            if(names.Count > 0) SelectCurve(names[0]);
        }

        // ── Curve chips ──────────────────────────────────────────────────
        private void BuildChips() {
            _chips.Children.Clear();
            foreach(var name in _vm.CurveNames) {
                var captured = name;
                var chip = new Border {
                    CornerRadius = new CornerRadius(T.RadiusSm),
                    Padding = new Thickness(16, 8, 16, 8),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand
                };
                chip.Child = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.Medium };
                chip.MouseLeftButtonUp += (s, e) => SelectCurve(captured);
                chip.Tag = captured;
                _chips.Children.Add(chip);
            }
            RefreshChipVisuals();
        }

        private void SelectCurve(string name) {
            _selected = name;
            RefreshChipVisuals();
            _rows.Clear();
            foreach(var r in _vm.GetCurveRows(name))
                _rows.Add(r);
        }

        private void RefreshChipVisuals() {
            foreach(Border chip in _chips.Children) {
                bool active = (string) chip.Tag == _selected;
                chip.Background = active ? T.Br(T.BgActive) : T.Br(T.BgCard2);
                chip.BorderBrush = active ? T.Br(T.Blue) : T.Br(T.BorderCol);
                chip.BorderThickness = new Thickness(1);
                ((TextBlock) chip.Child).Foreground = active ? T.Br(T.Blue) : T.Br(T.FgSec);
            }
        }

        // ── Themed DataGrid ──────────────────────────────────────────────
        private DataGrid BuildGrid() {
            var grid = new DataGrid {
                AutoGenerateColumns = false,
                CanUserAddRows      = false,   // explicit ＋ button instead of the ghost row
                CanUserDeleteRows   = true,
                CanUserResizeRows   = false,
                CanUserSortColumns  = false,
                HeadersVisibility   = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = T.Br(T.BorderCol),
                Background          = SWM.Brushes.Transparent,
                BorderThickness     = new Thickness(0),
                RowBackground       = SWM.Brushes.Transparent,
                AlternatingRowBackground = T.Tint(T.BgCard2, 90),
                ItemsSource         = _rows,
                MinHeight           = 220,
                FontSize            = 13,
                SelectionMode       = DataGridSelectionMode.Extended,
                SelectionUnit       = DataGridSelectionUnit.FullRow
            };

            // Column headers
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, T.Br(T.BgCard)));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgMute)));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, T.Br(T.BorderCol)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            grid.ColumnHeaderStyle = headerStyle;

            // Cells
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, SWM.Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgPri)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
            var sel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(Control.BackgroundProperty, T.Tint(T.Blue, 60)));
            sel.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgPri)));
            cellStyle.Triggers.Add(sel);
            grid.CellStyle = cellStyle;

            // Rows
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, SWM.Brushes.Transparent));
            rowStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 34.0));
            grid.RowStyle = rowStyle;

            // Forward the mouse wheel to the page's ScrollViewer — a DataGrid's
            // internal ScrollViewer otherwise swallows wheel events even when it
            // has nothing to scroll, which traps scrolling over the table.
            grid.PreviewMouseWheel += ForwardWheelToParent;

            // Editing textbox (default is white-on-white in dark mode)
            var editStyle = new Style(typeof(TextBox));
            editStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, T.Br(T.BgCard2)));
            editStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, T.Br(T.FgPri)));
            editStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, T.Br(T.FgPri)));
            editStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
            editStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(8, 5, 8, 5)));

            grid.Columns.Add(NumCol("温度 (°C)", "Temperature", editStyle));
            grid.Columns.Add(NumCol("CPU 风扇 (%)", "CpuPercent", editStyle));
            grid.Columns.Add(NumCol("GPU 风扇 (%)", "GpuPercent", editStyle));
            return grid;
        }

        private DataGridTextColumn NumCol(string header, string prop, Style editStyle) {
            return new DataGridTextColumn {
                Header = header,
                Binding = new System.Windows.Data.Binding(prop) { Mode = System.Windows.Data.BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                EditingElementStyle = editStyle
            };
        }

        // Re-raise a swallowed wheel event on the parent so the outer page
        // ScrollViewer scrolls when the pointer is over the table.
        private static void ForwardWheelToParent(object sender, MouseWheelEventArgs e) {
            if(e.Handled) return;
            e.Handled = true;
            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            if(sender is FrameworkElement fe && fe.Parent is UIElement parent)
                parent.RaiseEvent(args);
        }

        // ── Import / Export ──────────────────────────────────────────────
        private void DoExport() {
            var dlg = new Microsoft.Win32.SaveFileDialog {
                Filter = "XML 配置文件 (*.xml)|*.xml",
                FileName = "OmenMon-配置.xml"
            };
            if(dlg.ShowDialog() == true)
                Toast(_vm.ExportConfig(dlg.FileName) ? "已导出到 " + dlg.FileName : "导出失败");
        }

        private void DoImport() {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "XML 配置文件 (*.xml)|*.xml" };
            if(dlg.ShowDialog() == true) {
                if(_vm.ImportConfig(dlg.FileName)) {
                    BuildChips();
                    var names = _vm.CurveNames;
                    if(names.Count > 0) SelectCurve(names[0]);
                    Toast("已导入配置");
                } else Toast("导入失败");
            }
        }

        private void Toast(string msg) { if(_toast != null) _toast.Text = msg; }
    }
}
