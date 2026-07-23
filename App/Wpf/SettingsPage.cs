  //\\   OmenMon WPF Settings Page — startup & system integration
 //  \\  Toggles map to Windows Task Scheduler tasks (Hw.TaskGet/TaskSet).
     //  https://omenmon.github.io/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SWM = System.Windows.Media;
using OmenMon.Library;
using T = OmenMon.AppWpf.Theme;

namespace OmenMon.AppWpf {

    // Settings: startup autorun, Omen-key capture, Advanced Optimus fix.
    // These are all Task Scheduler entries, so each toggle reads its live
    // state from Hw.TaskGet and writes with Hw.TaskSet (needs elevation).
    public class SettingsPage : Page {

        private TextBlock _status;
        private const string DefaultNote = "以上选项通过 Windows 任务计划实现，需以管理员身份运行方可更改。";

        public SettingsPage(HardwareViewModel vm) {
            DataContext = vm;
            Background  = T.Br(T.BgMain);
            Title       = "设置";

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root   = new StackPanel { Margin = new Thickness(T.PagePad, T.PagePad - 6, T.PagePad, T.PagePad) };

            root.Children.Add(T.PageHeader("设置", "开机启动、按键与系统集成"));

            // ── Card: 启动 ───────────────────────────────────────────────
            var startCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var startStack = new StackPanel();
            startStack.Children.Add(T.SectionTitle("启动", new Thickness(0, 0, 0, 4)));
            startStack.Children.Add(SettingRow("🚀", "开机自启",
                "登录 Windows 后自动启动 OmenMon 并最小化到托盘",
                () => Hw.TaskGet(Config.TaskId.Gui),
                v  => Hw.TaskSet(Config.TaskId.Gui, v)));
            startCard.Child = startStack;
            root.Children.Add(startCard);

            // ── Card: 系统集成 ───────────────────────────────────────────
            var sysCard = T.Card(new Thickness(0, 0, 0, T.CardGap));
            var sysStack = new StackPanel();
            sysStack.Children.Add(T.SectionTitle("系统集成", new Thickness(0, 0, 0, 4)));
            sysStack.Children.Add(SettingRow("⌨", "Omen 键响应",
                "按下键盘上的 Omen 键时唤起 OmenMon 窗口",
                () => Hw.TaskGet(Config.TaskId.Key),
                v  => Hw.TaskSet(Config.TaskId.Key, v)));
            sysStack.Children.Add(Divider());
            sysStack.Children.Add(SettingRow("🖥", "独显直连修复",
                "开机自动修复 nVidia Advanced Optimus 的显示 / 卡顿问题",
                () => Hw.TaskGet(Config.TaskId.Mux),
                v  => Hw.TaskSet(Config.TaskId.Mux, v)));
            sysCard.Child = sysStack;
            root.Children.Add(sysCard);

            // Status / note line (also shows the result of a toggle)
            _status = new TextBlock {
                FontSize = 12, Foreground = T.Br(T.FgMute),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 2, 0, 0),
                Text = DefaultNote
            };
            root.Children.Add(_status);

            scroll.Content = root;
            Content = scroll;
        }

        // One settings row: icon + title/description on the left, toggle on the right.
        private FrameworkElement SettingRow(string icon, string title, string desc,
                                            Func<bool> get, Action<bool> set) {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock {
                Text = icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(new TextBlock {
                Text = title, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = T.Br(T.FgPri)
            });
            texts.Children.Add(new TextBlock {
                Text = desc, FontSize = 11.5, Foreground = T.Br(T.FgMute),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);

            var toggle = BuildToggle(title, get, set);
            Grid.SetColumn(toggle, 2);
            grid.Children.Add(toggle);

            return grid;
        }

        // Pill toggle: reads its initial state from get(), calls set() on click,
        // then re-reads get() to reflect the real outcome (task ops can fail if
        // not elevated) and reports it in the status line.
        private Border BuildToggle(string title, Func<bool> get, Action<bool> set) {
            bool state = false;
            try { state = get(); } catch { }

            var track = new Border {
                Width = 46, Height = 26, CornerRadius = new CornerRadius(13),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center
            };
            var knob = new Border {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                Background = T.Br("#FFFFFF"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 3, 0)
            };
            track.Child = knob;

            void Paint() {
                track.Background = state ? T.Br(T.Blue) : T.Br(T.BgCard2);
                knob.HorizontalAlignment = state ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }
            Paint();

            track.MouseLeftButtonUp += (s, e) => {
                bool target = !state;
                try { set(target); } catch { }
                try { state = get(); } catch { state = target; }
                Paint();
                Flash(state == target
                    ? (target ? $"已开启「{title}」" : $"已关闭「{title}」")
                    : $"「{title}」未生效 — 请以管理员身份运行");
            };

            return track;
        }

        private Border Divider() =>
            new Border { Height = 1, Background = T.Br(T.BorderCol), Opacity = 0.6, Margin = new Thickness(0, 4, 0, 4) };

        private void Flash(string msg) { if(_status != null) _status.Text = msg; }
    }
}
