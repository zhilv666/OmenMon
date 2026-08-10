  //\\   OmenMon WPF Curve Settings Page — temperature→speed curve editor
 //  \\  Curve chips + status/apply bar on top, full-width threshold table below.
     //  https://omenmon.github.io/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

        // The chip standing for "no curve at all — the firmware decides". It is
        // not a fan program, so it lives only in the page, never in the config.
        private const string BiosEntry = "BiosDefault";
        private const string BiosLabel = "BIOS 默认";

        // Chip label text blocks, so the visuals can be repainted by name
        private readonly Dictionary<string, TextBlock> _chipLabels = new Dictionary<string, TextBlock>();

        // The two mutually exclusive lower cards: the threshold editor, and the
        // note shown instead when the BIOS entry is the one selected
        private Border _tableCard;
        private Border _biosCard;

        // Control-bar pieces repainted as the engine starts and stops
        private Ellipse _statusDot;
        private TextBlock _statusText;
        private Button _applyBtn;
        private TextBlock _applyLabel;
        private PropertyChangedEventHandler _onVmChanged;

        // Segoe MDL2 glyphs for the header actions
        private const int GlyphExport = 0xE896;   // tray with a downward arrow
        private const int GlyphImport = 0xE898;   // tray with an upward arrow

        public CurvePage(HardwareViewModel vm) {
            _vm = vm;
            DataContext = vm;
            Background  = T.Br(T.BgMain);
            Title       = "曲线设置";

            // Horizontal scrolling stays off: with it enabled the content is
            // measured against infinite width, and the table's star-sized
            // columns collapse onto their text instead of sharing the row
            var scroll = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(BuildHeader());
            root.Children.Add(BuildControlBar());

            _biosCard  = BuildBiosCard();
            _tableCard = BuildTableCard();
            root.Children.Add(_biosCard);
            root.Children.Add(_tableCard);

            scroll.Content = root;
            Content = scroll;

            // Start on whatever is actually in effect: the running curve, or
            // the BIOS entry when nothing is
            if(!string.IsNullOrEmpty(_vm.ActiveCurve) && _vm.CurveNames.Contains(_vm.ActiveCurve))
                SelectCurve(_vm.ActiveCurve);
            else
                SelectCurve(BiosEntry);

            // Track the engine so the status line and the button stay truthful
            PaintRunState();
            _onVmChanged = (s, e) => {
                if(e.PropertyName == nameof(HardwareViewModel.ActiveCurve)
                    || e.PropertyName == nameof(HardwareViewModel.CurveStatus))
                    PaintRunState();
            };
            _vm.PropertyChanged += _onVmChanged;
            Unloaded += (s, e) => { if(_onVmChanged != null) _vm.PropertyChanged -= _onVmChanged; };
        }

        // ── Header: title + import / export actions ──────────────────────
        private UIElement BuildHeader() {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 22) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel();
            texts.Children.Add(new TextBlock {
                Text = "曲线设置", FontSize = 24, FontWeight = FontWeights.Bold,
                Foreground = T.Br(T.FgPri), Margin = new Thickness(0, 0, 0, 4)
            });
            texts.Children.Add(new TextBlock {
                Text = "曲线只在自动模式下生效；未运行曲线时由 BIOS 默认控制。选中后点应用即生效，状态会被记住，下次启动自动恢复",
                FontSize = 12.5, Foreground = T.Br(T.FgSec), TextWrapping = TextWrapping.Wrap
            });
            grid.Children.Add(texts);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            actions.Children.Add(IconButton(GlyphExport, "导出配置到文件", DoExport));
            actions.Children.Add(IconButton(GlyphImport, "从文件导入配置", DoImport));
            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);

            return grid;
        }

        private Button IconButton(int glyph, string tip, System.Action onClick) {
            var btn = new Button {
                Width = 38, Height = 34, Cursor = Cursors.Hand, ToolTip = tip,
                Background = SWM.Brushes.Transparent, BorderThickness = new Thickness(0),
                Margin = new Thickness(6, 0, 0, 0),
                Content = new TextBlock {
                    Text = ((char) glyph).ToString(),
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16, Foreground = T.Br(T.FgSec),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btn.Template = T.RoundedButtonTemplate(8);
            btn.MouseEnter += (s, e) => btn.Background = T.Br(T.BgHover);
            btn.MouseLeave += (s, e) => btn.Background = SWM.Brushes.Transparent;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        // ── Control bar: curve chips | status | apply ─────────────────────
        private UIElement BuildControlBar() {
            var card = T.Card(new Thickness(0, 0, 0, T.CardGap));
            card.Padding = new Thickness(16, 14, 16, 14);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _chips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            BuildChips();
            grid.Children.Add(_chips);

            // Status: coloured dot + what the engine is doing
            var status = new StackPanel {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 14, 0)
            };
            _statusDot = new Ellipse { Width = 8, Height = 8, Fill = T.Br(T.FgMute), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            _statusText = new TextBlock { FontSize = 12.5, Foreground = T.Br(T.FgSec), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
            status.Children.Add(_statusDot);
            status.Children.Add(_statusText);
            Grid.SetColumn(status, 1);
            grid.Children.Add(status);

            // One button that applies or stops, depending on the current state
            _applyBtn = new Button {
                Padding = new Thickness(20, 10, 20, 10), Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center
            };
            _applyLabel = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = T.Br("#FFFFFF") };
            _applyBtn.Content = _applyLabel;
            _applyBtn.Template = T.RoundedButtonTemplate(T.RadiusSm);
            _applyBtn.MouseEnter += ApplyHoverIn;
            _applyBtn.MouseLeave += ApplyHoverOut;
            _applyBtn.Click += (s, e) => {
                if(_selected == BiosEntry) {
                    _vm.ApplyPreset("Auto");
                    Toast("已交回 BIOS，风扇按主板固件策略调节");
                } else if(_selected == null) {
                    Toast("请先选择一条曲线");
                } else if(_selected == _vm.ActiveCurve) {
                    _vm.StopCurve();
                } else {
                    _vm.RunCurve(_selected);
                }
                PaintRunState();
            };
            Grid.SetColumn(_applyBtn, 2);
            grid.Children.Add(_applyBtn);

            card.Child = grid;
            return card;
        }

        // Reflects the engine state in the dot, the status line and the button.
        // The status line refreshes every poll while a curve runs, so only the
        // parts that actually change state are rebuilt.
        private string _paintedState;

        private void PaintRunState() {
            bool running = _vm.IsCurveRunning;

            if(_statusText != null)
                _statusText.Text = running
                    ? _vm.CurveStatus
                    : "状态：BIOS 默认控制（未运行曲线）";

            // What the button does depends on the selection, not just the engine
            bool selectedIsBios = _selected == BiosEntry;
            bool selectedRunning = !selectedIsBios && _selected == _vm.ActiveCurve;
            string state = selectedIsBios ? "bios" : selectedRunning ? "stop" : "run";

            RefreshChipVisuals();

            if(_paintedState == state) return;
            _paintedState = state;

            if(_statusDot != null)
                _statusDot.Fill = running ? T.Br(T.Green) : T.Br(T.FgMute);

            if(_applyBtn != null) {
                _applyBtn.Background = T.Br(ApplyAccent());
                _applyLabel.Text =
                    selectedIsBios  ? "▷  交回 BIOS" :
                    selectedRunning ? "■  停止曲线"  : "▷  应用曲线";
            }
        }

        // Blue hands control back, red stops, green applies
        private SWM.Color ApplyAccent() {
            if(_selected == BiosEntry) return T.Blue;
            return _selected == _vm.ActiveCurve ? T.Red : T.Green;
        }

        private void ApplyHoverIn(object s, MouseEventArgs e) {
            SWM.Color a = ApplyAccent();
            _applyBtn.Background = new SolidColorBrush(SWM.Color.FromRgb(
                (byte) System.Math.Min(a.R + 22, 255),
                (byte) System.Math.Min(a.G + 22, 255),
                (byte) System.Math.Min(a.B + 22, 255)));
        }

        private void ApplyHoverOut(object s, MouseEventArgs e) {
            _applyBtn.Background = T.Br(ApplyAccent());
        }

        // ── The BIOS default entry has nothing to edit, just an explanation ──
        private Border BuildBiosCard() {
            var card = T.Card();
            var stack = new StackPanel();

            stack.Children.Add(T.SectionTitle("BIOS 默认"));
            stack.Children.Add(T.Caption(
                "不使用任何自定义曲线，风扇转速完全由主板固件按出厂策略决定。",
                new Thickness(0, 4, 0, 14)));

            stack.Children.Add(NoteLine("这是程序没有运行曲线时的默认状态，也是「性能模式」页「自动」对应的状态。"));
            stack.Children.Add(NoteLine("曲线只在自动模式下生效：一旦在「风扇控制」页拖动滑块或选了固定档位，曲线就会停止。"));
            stack.Children.Add(NoteLine("这一项和内置的 Auto / Power / Silent 一样不可删除，方便随时切回来。"));

            card.Child = stack;
            return card;
        }

        private static UIElement NoteLine(string text) {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock {
                Text = "·", FontSize = 13, Foreground = T.Br(T.FgMute),
                Margin = new Thickness(2, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top
            });
            row.Children.Add(new TextBlock {
                Text = text, FontSize = 12.5, Foreground = T.Br(T.FgSec),
                TextWrapping = TextWrapping.Wrap, MaxWidth = 760
            });
            return row;
        }

        // ── Curve chips ──────────────────────────────────────────────────
        private void BuildChips() {
            _chips.Children.Clear();
            _chipLabels.Clear();

            // Handing the fans back to the firmware is a state like any other,
            // so it gets a chip rather than being hidden behind a stop button
            _chips.Children.Add(BuildChip(BiosEntry, BiosLabel,
                "不使用任何曲线，风扇完全交给主板固件调节（默认状态）", false));

            foreach(var name in _vm.CurveNames) {
                bool builtIn = HardwareViewModel.IsBuiltInCurve(name);
                _chips.Children.Add(BuildChip(name, name,
                    builtIn ? "内置曲线，不可删除" : "自定义曲线，鼠标移上去可删除", !builtIn));
            }

            _chips.Children.Add(BuildAddChip());
            RefreshChipVisuals();
        }

        // One chip. Deletable ones grow a × on hover; clicking it removes the
        // curve without also selecting the chip underneath.
        private Border BuildChip(string key, string text, string tip, bool deletable) {
            var chip = new Border {
                CornerRadius = new CornerRadius(T.RadiusSm),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 8, deletable ? 10 : 16, 8),
                Margin = new Thickness(0, 3, 8, 3),
                Cursor = Cursors.Hand,
                Tag = key,
                ToolTip = tip
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var label = new TextBlock {
                Text = text, FontSize = 13, FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(label);
            _chipLabels[key] = label;

            if(deletable) {
                var close = new TextBlock {
                    Text = "✕", FontSize = 11, Margin = new Thickness(7, 0, 0, 0),
                    Padding = new Thickness(4, 2, 4, 2),
                    Foreground = T.Br(T.FgMute), VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Hidden, ToolTip = "删除这条曲线",
                    // A TextBlock with no brush of its own is not hit-testable,
                    // so without this the click would fall through to the chip
                    // and merely select the curve instead of deleting it
                    Background = SWM.Brushes.Transparent
                };
                close.MouseEnter += (s, e) => close.Foreground = T.Br(T.Red);
                close.MouseLeave += (s, e) => close.Foreground = T.Br(T.FgMute);
                close.MouseLeftButtonUp += (s, e) => { e.Handled = true; DeleteCurve(key); };
                row.Children.Add(close);
                chip.MouseEnter += (s, e) => close.Visibility = Visibility.Visible;
                chip.MouseLeave += (s, e) => close.Visibility = Visibility.Hidden;
            }

            chip.Child = row;
            chip.MouseLeftButtonUp += (s, e) => SelectCurve(key);
            return chip;
        }

        private void DeleteCurve(string name) {
            var confirm = MessageBox.Show(
                $"确定删除曲线「{name}」吗？此操作会写入配置文件，无法撤销。",
                "删除曲线", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if(confirm != MessageBoxResult.OK) return;

            bool wasRunning = _vm.ActiveCurve == name;
            if(!_vm.DeleteCurve(name)) { Toast("内置曲线不可删除"); return; }

            BuildChips();
            SelectCurve(BiosEntry);
            Toast(wasRunning
                ? $"已删除曲线「{name}」，风扇已交回 BIOS"
                : $"已删除曲线「{name}」");
        }

        // The dashed "+" chip that creates a new curve
        private Border BuildAddChip() {
            var chip = new Border {
                Width = 34, Height = 34,
                CornerRadius = new CornerRadius(17),
                BorderThickness = new Thickness(1),
                BorderBrush = T.Br(T.BorderCol),
                Background = SWM.Brushes.Transparent,
                Margin = new Thickness(0, 3, 8, 3),
                Cursor = Cursors.Hand,
                ToolTip = "新建曲线（复制当前选中的曲线）",
                Child = new TextBlock {
                    Text = "＋", FontSize = 14, Foreground = T.Br(T.FgMute),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            // Border has no dashed-corner-radius combination that renders well,
            // so the dashes come from the stroke pattern on the border brush
            chip.MouseEnter += (s, e) => chip.Background = T.Br(T.BgHover);
            chip.MouseLeave += (s, e) => chip.Background = SWM.Brushes.Transparent;
            chip.MouseLeftButtonUp += (s, e) => AddCurve();
            return chip;
        }

        private void AddCurve() {
            string name = PromptForName("新建曲线", "自定义");
            if(string.IsNullOrWhiteSpace(name)) return;

            if(!_vm.CreateCurve(name, _selected)) {
                Toast("名称为空或已存在，换一个试试");
                return;
            }

            BuildChips();
            SelectCurve(name.Trim());
            Toast($"已新建曲线「{name.Trim()}」");
        }

        private void SelectCurve(string name) {
            _selected = name;

            bool isBios = name == BiosEntry;
            if(_biosCard  != null) _biosCard.Visibility  = isBios ? Visibility.Visible : Visibility.Collapsed;
            if(_tableCard != null) _tableCard.Visibility = isBios ? Visibility.Collapsed : Visibility.Visible;

            if(_rows != null) {
                _rows.Clear();
                if(!isBios)
                    foreach(var r in _vm.GetCurveRows(name))
                        _rows.Add(r);
            }

            _paintedState = null;   // the button depends on what is selected
            PaintRunState();
        }

        private void RefreshChipVisuals() {
            foreach(var child in _chips.Children) {
                var chip = child as Border;
                if(chip == null || !(chip.Tag is string)) continue;

                string key = (string) chip.Tag;
                bool selected = key == _selected;

                // The BIOS chip is "running" precisely when no curve is
                bool running = key == BiosEntry
                    ? !_vm.IsCurveRunning
                    : key == _vm.ActiveCurve;

                // Selected is a blue outline; whatever is actually driving the
                // fans gets green, so the two states never look alike
                SWM.Color accent = running ? T.Green : T.Blue;
                bool lit = selected || running;

                chip.Background  = lit ? T.Tint(accent, 38) : T.Br(T.BgCard2);
                chip.BorderBrush = lit ? new SolidColorBrush(accent) : T.Br(T.BorderCol);

                TextBlock label;
                if(_chipLabels.TryGetValue(key, out label))
                    label.Foreground = lit ? new SolidColorBrush(accent) : T.Br(T.FgSec);
            }
        }

        // ── Table card: thresholds + row actions ─────────────────────────
        private Border BuildTableCard() {
            var card = T.Card();
            card.Padding = new Thickness(0);   // the table spans the full width

            var stack = new StackPanel();

            var head = new StackPanel { Margin = new Thickness(20, 18, 20, 14) };
            head.Children.Add(T.SectionTitle("温度阈值表"));
            head.Children.Add(T.Caption("温度 °C → 风扇转速 %（0-100）。双击单元格编辑。", new Thickness(0, 2, 0, 0)));
            stack.Children.Add(head);

            _rows = new ObservableCollection<HardwareViewModel.CurveRow>();
            _grid = BuildGrid();
            stack.Children.Add(_grid);

            stack.Children.Add(new Border { Height = 1, Background = T.Br(T.BorderCol) });

            var footer = new Grid { Margin = new Thickness(20, 14, 20, 18) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            var addBtn = T.PillButton("＋ 添加节点", T.Blue);
            addBtn.Click += (s, e) => {
                int nextTemp = _rows.Count > 0 ? System.Math.Min(_rows.Max(r => r.Temperature) + 5, 100) : 40;
                _rows.Add(new HardwareViewModel.CurveRow { Temperature = nextTemp, CpuPercent = 50, GpuPercent = 50 });
            };
            var delBtn = T.PillButton("－ 删除节点", T.Red);
            delBtn.Margin = new Thickness(10, 0, 0, 0);
            delBtn.Click += (s, e) => {
                var doomed = _grid.SelectedItems.Cast<object>().OfType<HardwareViewModel.CurveRow>().ToList();
                if(doomed.Count == 0) { Toast("请先选中要删除的行"); return; }
                foreach(var r in doomed) _rows.Remove(r);
            };
            left.Children.Add(addBtn);
            left.Children.Add(delBtn);
            footer.Children.Add(left);

            var saveBtn = T.PillButton("保存修改", T.Blue, filled: true);
            saveBtn.Click += (s, e) => {
                if(_selected == null) { Toast("请先选择一条曲线"); return; }
                _vm.SaveCurveRows(_selected, new List<HardwareViewModel.CurveRow>(_rows));
                Toast($"已保存曲线「{_selected}」到配置文件");
            };
            Grid.SetColumn(saveBtn, 1);
            footer.Children.Add(saveBtn);

            stack.Children.Add(footer);

            _toast = new TextBlock {
                FontSize = 12, Foreground = T.Br(T.Green),
                Margin = new Thickness(20, 0, 20, 16), TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(_toast);

            card.Child = stack;
            return card;
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
                FontSize            = 13.5,
                SelectionMode       = DataGridSelectionMode.Extended,
                SelectionUnit       = DataGridSelectionUnit.FullRow
            };

            // Column headers: spaced small caps over a hairline rule
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, SWM.Brushes.Transparent));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgSec)));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(20, 10, 20, 10)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, T.Br(T.BorderCol)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 1, 0, 1)));
            grid.ColumnHeaderStyle = headerStyle;

            // Cells
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, SWM.Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgPri)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0, 9, 0, 9)));
            var sel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(Control.BackgroundProperty, T.Tint(T.Blue, 60)));
            sel.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgPri)));
            cellStyle.Triggers.Add(sel);
            grid.CellStyle = cellStyle;

            // Rows
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, SWM.Brushes.Transparent));
            rowStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 40.0));
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
            editStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(18, 6, 18, 6)));

            grid.Columns.Add(NumCol("温度 (°C)", "Temperature", editStyle, HorizontalAlignment.Left));
            grid.Columns.Add(NumCol("CPU 风扇 (%)", "CpuPercent", editStyle, HorizontalAlignment.Right));
            grid.Columns.Add(NumCol("GPU 风扇 (%)", "GpuPercent", editStyle, HorizontalAlignment.Right));
            return grid;
        }

        private DataGridTextColumn NumCol(string header, string prop, Style editStyle, HorizontalAlignment align) {
            // Values sit against the column edge the header is aligned to, so a
            // column of numbers lines up on its digits rather than drifting.
            // The inset lives on the value itself rather than on the cell, so
            // that it matches the header padding exactly.
            var elementStyle = new Style(typeof(TextBlock));
            elementStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, align));
            elementStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            elementStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(20, 0, 20, 0)));

            var editing = new Style(typeof(TextBox), editStyle);
            editing.Setters.Add(new Setter(TextBox.TextAlignmentProperty,
                align == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left));

            return new DataGridTextColumn {
                Header = header,
                HeaderStyle = HeaderAlignment(align),
                Binding = new System.Windows.Data.Binding(prop) { Mode = System.Windows.Data.BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                ElementStyle = elementStyle,
                EditingElementStyle = editing
            };
        }

        // Per-column header alignment, inheriting the grid-wide header style
        private Style HeaderAlignment(HorizontalAlignment align) {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SWM.Brushes.Transparent));
            style.Setters.Add(new Setter(Control.ForegroundProperty, T.Br(T.FgSec)));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(20, 10, 20, 10)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, T.Br(T.BorderCol)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 1, 0, 1)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, align));
            return style;
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

        // ── A minimal modal text prompt (WPF has no input box of its own) ──
        private string PromptForName(string title, string initial) {
            var win = new Window {
                Width = 360, SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = SWM.Brushes.Transparent, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var shell = new Border {
                Background = T.Br(T.BgCard), CornerRadius = new CornerRadius(T.Radius),
                BorderBrush = T.Br(T.BorderCol), BorderThickness = new Thickness(1),
                Padding = new Thickness(22)
            };
            var stack = new StackPanel();
            stack.Children.Add(T.SectionTitle(title, new Thickness(0, 0, 0, 12)));

            var box = new TextBox {
                Text = initial, FontSize = 14,
                Background = T.Br(T.BgCard2), Foreground = T.Br(T.FgPri),
                CaretBrush = T.Br(T.FgPri), BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(box);

            string result = null;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = T.PillButton("取消", T.FgMute);
            var ok = T.PillButton("确定", T.Blue, filled: true);
            ok.Margin = new Thickness(10, 0, 0, 0);
            cancel.Click += (s, e) => win.Close();
            ok.Click += (s, e) => { result = box.Text; win.Close(); };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            stack.Children.Add(buttons);

            box.KeyDown += (s, e) => {
                if(e.Key == Key.Enter) { result = box.Text; win.Close(); }
                else if(e.Key == Key.Escape) win.Close();
            };

            shell.Child = stack;
            win.Content = shell;
            win.Loaded += (s, e) => { box.SelectAll(); box.Focus(); };
            win.ShowDialog();
            return result;
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
                    SelectCurve(BiosEntry);
                    Toast("已导入配置");
                } else Toast("导入失败");
            }
        }

        private void Toast(string msg) { if(_toast != null) _toast.Text = msg; }
    }
}
