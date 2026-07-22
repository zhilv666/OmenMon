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

                // Create tray icon
                BuildTray();

                // Create windows (hidden by default)
                _main    = new MainWindow(ViewModel);
                _overlay = new OverlayWindow(ViewModel);

                // Show main window on startup
                _main.Show();

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
            ViewModel?.Dispose();
            _tray?.Dispose();
            base.OnExit(e);
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

            _menu.Items.Add(new ToolStripSeparator());

            // Fan presets in tray
            var fanMenu = new ToolStripMenuItem("风扇控制");
            foreach(var p in new[] { ("自动","Auto"),("最大","Max"),("中速","Mid"),("安静","Silent"),("关闭","Off") }) {
                var (label, key) = p;
                var item = new ToolStripMenuItem(label);
                var capturedKey = key;
                item.Click += (s, ev) => ViewModel.ApplyPreset(capturedKey);
                fanMenu.DropDownItems.Add(item);
            }
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
        }
#endregion

    }

}
