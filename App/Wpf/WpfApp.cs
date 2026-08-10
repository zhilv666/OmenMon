  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  WPF Application — manages tray icon and window lifecycle
     //  https://omenmon.github.io/

using System;
using System.Windows;
using System.Windows.Forms;
using OmenMon.Hardware.Platform;
using OmenMon.Library;
using Application = System.Windows.Application;

namespace OmenMon.AppWpf {

    // WPF Application class: owns the tray icon, hardware platform,
    // and both windows (main control panel + overlay).
    public class WpfApp : Application {

        internal static WpfApp Instance => (WpfApp) Current;

        // Hardware backend shared between all windows
        internal Platform           Platform { get; private set; }
        internal HardwareViewModel  ViewModel { get; private set; }

        // Windows
        private MainWindow    _main;
        private OverlayWindow _overlay;

        // WinForms tray icon (WPF has no built-in NotifyIcon)
        private NotifyIcon    _tray;
        private ContextMenuStrip _menu;

#region Entry Point
        // Static entry — called from OmenMon App.cs via new WpfApp().Run()
        public static void RunWpfApp() {
            var app = new WpfApp();
            app.Run();
        }
#endregion

#region Startup & Shutdown
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            // Log any unhandled exception to a file so crashes can be diagnosed
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
                LogCrash(ev.ExceptionObject as Exception, "AppDomain");
            this.DispatcherUnhandledException += (s, ev) => {
                LogCrash(ev.Exception, "Dispatcher");
                ev.Handled = true;  // keep the app alive instead of dying silently
            };

            try {

                // Config & hardware are already initialised in App.Main;
                // do NOT call Config.Initialize()/Hw.Initialize() again here.
                // BUT the BIOS and Embedded Controller must be initialised
                // before creating the Platform — otherwise all EC/BIOS reads
                // (temperature, fan speed) silently return nothing. This mirrors
                // what GuiOp does in the original WinForms path.
                Hw.BiosInit();
                Hw.EcInit();
                Platform = new Platform();
                ViewModel = new HardwareViewModel(Platform);

                // Remember which curve is running, so it does not have to be
                // applied by hand after every restart
                ViewModel.PropertyChanged += (s, ev) => {
                    if(ev.PropertyName != nameof(HardwareViewModel.ActiveCurve)) return;
                    if(Config.GuiFanCurveActive == ViewModel.ActiveCurve) return;
                    Config.GuiFanCurveActive = ViewModel.ActiveCurve;
                    SaveConfigSoon();
                };

                // Create tray icon
                BuildTray();

                // Restore the overlay's saved appearance before the window gets
                // built, so it comes up looking exactly as it was left
                OverlayWindow.RestoreFromConfig();

                // Create windows (hidden by default)
                _main    = new MainWindow(ViewModel);
                _overlay = new OverlayWindow(ViewModel);

                // Show main window on startup
                _main.Show();

                // Bring the overlay back if it was on screen when last dismissed
                if(Config.GuiOverlayShow)
                    _overlay.Show();

                // Resume whichever curve was running when the app last exited
                if(!string.IsNullOrEmpty(Config.GuiFanCurveActive)
                    && Config.FanProgram.ContainsKey(Config.GuiFanCurveActive))
                    ViewModel.RunCurve(Config.GuiFanCurveActive);

            } catch(Exception ex) {
                LogCrash(ex, "OnStartup");
                throw;
            }
        }

        // Writes crash details next to the executable for diagnosis
        private static void LogCrash(Exception ex, string source) {
            try {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "crash.log");
                System.IO.File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source})\r\n{ex}\r\n\r\n");
            } catch { }
        }

        protected override void OnExit(ExitEventArgs e) {
            SaveConfigNow();
            ViewModel?.Dispose();
            _tray?.Dispose();
            base.OnExit(e);
        }
#endregion

#region Settings Persistence
        // Overlay changes are written straight back to OmenMon.xml. The
        // appearance sliders fire continuously while being dragged, so the
        // writes are batched rather than hitting the file on every pixel.
        private System.Windows.Threading.DispatcherTimer _saveTimer;

        private void SaveConfigSoon() {
            if(_saveTimer == null) {
                _saveTimer = new System.Windows.Threading.DispatcherTimer {
                    Interval = TimeSpan.FromMilliseconds(800)
                };
                _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); TrySaveConfig(); };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        // Flushes any pending write immediately (used on the way out)
        private void SaveConfigNow() {
            if(_saveTimer != null && _saveTimer.IsEnabled) {
                _saveTimer.Stop();
                TrySaveConfig();
            }
        }

        private static void TrySaveConfig() {
            try { Config.Save(); } catch { }
        }
#endregion

#region Tray Icon
        private void BuildTray() {
            _menu = new ContextMenuStrip();

            var itemShow = new ToolStripMenuItem("显示控制面板");
            itemShow.Click += (s, ev) => ShowMain();
            _menu.Items.Add(itemShow);

            var itemOverlay = new ToolStripMenuItem("悬浮监控");
            itemOverlay.Click += (s, ev) => ToggleOverlay();
            _menu.Items.Add(itemOverlay);

            // Tick the entry while the overlay is actually on screen
            _menu.Opening += (s, ev) =>
                itemOverlay.Checked = _overlay != null && _overlay.IsVisible;

            _menu.Items.Add(new ToolStripSeparator());

            // Fan presets in tray, taken from the same table the performance-mode
            // page builds its cards from, so the two can never fall out of step
            var fanMenu = new ToolStripMenuItem("风扇控制");
            foreach(var preset in HardwareViewModel.Presets) {
                var item = new ToolStripMenuItem(preset.Name) { Tag = preset.Key };

                if(preset.IsAuto) {
                    // Auto is not one state but two: hand the fans to the
                    // firmware, or run one of the configured curves. A
                    // placeholder keeps the submenu arrow visible until the
                    // real entries are built when it is opened.
                    item.DropDownItems.Add(new ToolStripMenuItem());
                    item.DropDownOpening += (s, ev) => BuildAutoSubmenu((ToolStripMenuItem) s);
                } else {
                    item.Click += (s, ev) => ViewModel.ApplyPreset((string) ((ToolStripMenuItem) s).Tag);
                }

                fanMenu.DropDownItems.Add(item);
            }

            // Mark whichever preset is in effect when the submenu is opened
            fanMenu.DropDownOpening += (s, ev) => {
                foreach(ToolStripMenuItem item in fanMenu.DropDownItems) {
                    string key = (string) item.Tag;
                    var preset = HardwareViewModel.GetPreset(key);
                    // A running curve counts as Auto, since that is where it lives
                    item.Checked = preset != null && preset.IsAuto
                        ? ViewModel.ActivePreset == key || ViewModel.IsCurveRunning
                        : key == ViewModel.ActivePreset;
                }
            };

            _menu.Items.Add(fanMenu);

            _menu.Items.Add(new ToolStripSeparator());

            var itemExit = new ToolStripMenuItem("退出");
            itemExit.Click += (s, ev) => Shutdown();
            _menu.Items.Add(itemExit);

            _tray = new NotifyIcon {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location),
                Text = "OmenMon — 硬件监控",
                ContextMenuStrip = _menu,
                Visible = true
            };

            _tray.DoubleClick += (s, ev) => ShowMain();
        }

        // Fills the Auto submenu: the firmware default first, then every
        // configured curve. Rebuilt on each open because curves can be added
        // and deleted from the curve page while the application is running.
        private void BuildAutoSubmenu(ToolStripMenuItem parent) {
            parent.DropDownItems.Clear();

            var bios = new ToolStripMenuItem("BIOS 默认") {
                Checked = !ViewModel.IsCurveRunning && ViewModel.ActivePreset == "Auto"
            };
            bios.Click += (s, ev) => ViewModel.ApplyPreset("Auto");
            parent.DropDownItems.Add(bios);

            if(Config.FanProgram.Count > 0)
                parent.DropDownItems.Add(new ToolStripSeparator());

            foreach(string name in Config.FanProgram.Keys) {
                string captured = name;
                var item = new ToolStripMenuItem("曲线：" + name) {
                    Checked = ViewModel.ActiveCurve == captured
                };
                item.Click += (s, ev) => ViewModel.RunCurve(captured);
                parent.DropDownItems.Add(item);
            }
        }
#endregion

#region Window Management
        public void ShowMain() {
            if(_main == null || !_main.IsLoaded) {
                _main = new MainWindow(ViewModel);
            }
            _main.Show();
            _main.Activate();
        }

        public void ToggleOverlay() {
            if(_overlay == null || !_overlay.IsLoaded) {
                _overlay = new OverlayWindow(ViewModel);
            }
            if(_overlay.IsVisible)
                _overlay.Hide();
            else
                _overlay.Show();
            NoteOverlayVisible(_overlay.IsVisible);
        }

        // Records whether the overlay should come back on the next start. Called
        // on an explicit user action only — never while the app is shutting down,
        // which would otherwise always store it as hidden.
        public void NoteOverlayVisible(bool visible) {
            if(Config.GuiOverlayShow == visible) return;
            Config.GuiOverlayShow = visible;
            SaveConfigSoon();
        }

        // Switches the overlay template. The window is rebuilt from scratch so
        // the new layout takes effect; if it was on screen it stays on screen.
        public void SetOverlayStyle(OverlayWindow.OverlayStyle style) {
            OverlayWindow.CurrentStyle = style;
            Config.GuiOverlayStyle = style.ToString();
            bool wasVisible = _overlay != null && _overlay.IsVisible;
            if(_overlay != null) { _overlay.Close(); _overlay = null; }
            _overlay = new OverlayWindow(ViewModel);
            if(wasVisible) _overlay.Show();
            SaveConfigSoon();
        }

        // Live overlay appearance — applied to the current window (if any) and
        // stored so the next-created overlay picks up the same values.
        public void SetOverlayOpacity(double opacity) {
            OverlayWindow.CurrentOpacity = OverlayWindow.ClampOpacity(opacity);
            _overlay?.ApplyOpacity(opacity);
            Config.GuiOverlayOpacity = (int) Math.Round(OverlayWindow.CurrentOpacity * 100);
            SaveConfigSoon();
        }

        public void SetOverlayScale(double scale) {
            OverlayWindow.CurrentScale = OverlayWindow.ClampScale(scale);
            _overlay?.ApplyScale(scale);
            Config.GuiOverlayScale = (int) Math.Round(OverlayWindow.CurrentScale * 100);
            SaveConfigSoon();
        }

        // Remembers where the overlay was dragged to
        public void SaveOverlayPosition(double left, double top) {
            int x = (int) Math.Round(left);
            int y = (int) Math.Round(top);
            if(Config.GuiOverlayLeft == x && Config.GuiOverlayTop == y) return;
            Config.GuiOverlayLeft = x;
            Config.GuiOverlayTop = y;
            SaveConfigSoon();
        }
#endregion

    }

}
