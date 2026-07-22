  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  WPF ViewModel — polls hardware and exposes bindable properties
     //  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

namespace OmenMon.AppWpf {

    // Shared ViewModel polled on a timer; all WPF windows bind to this.
    public class HardwareViewModel : INotifyPropertyChanged, IDisposable {

#region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = "") {
            if(!Equals(field, value)) {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
#endregion

#region Hardware Data
        // --- Temperature --------------------------------------------------
        private int _cpuTemp;
        public int CpuTemp { get => _cpuTemp; set => Set(ref _cpuTemp, value); }

        private int _gpuTemp;
        public int GpuTemp { get => _gpuTemp; set => Set(ref _gpuTemp, value); }

        // Derived: progress bar widths / colours change at 60 / 75 °C
        public double CpuTempBar => Math.Min(Math.Max(CpuTemp, 0), 100) / 100.0;
        public double GpuTempBar => Math.Min(Math.Max(GpuTemp, 0), 100) / 100.0;

        // --- Fan ----------------------------------------------------------
        private int _cpuFanRpm;
        public int CpuFanRpm { get => _cpuFanRpm; set => Set(ref _cpuFanRpm, value); }

        private int _gpuFanRpm;
        public int GpuFanRpm { get => _gpuFanRpm; set => Set(ref _gpuFanRpm, value); }

        private int _cpuFanPct;
        public int CpuFanPct { get => _cpuFanPct; set => Set(ref _cpuFanPct, value); }

        private int _gpuFanPct;
        public int GpuFanPct { get => _gpuFanPct; set => Set(ref _gpuFanPct, value); }

        public double CpuFanBar => _cpuFanPct / 100.0;
        public double GpuFanBar => _gpuFanPct / 100.0;

        // Manual slider values (0-55 krpm range)
        private double _cpuFanLevel = 20;
        public double CpuFanLevel {
            get => _cpuFanLevel;
            set { Set(ref _cpuFanLevel, value); }
        }

        private double _gpuFanLevel = 20;
        public double GpuFanLevel {
            get => _gpuFanLevel;
            set { Set(ref _gpuFanLevel, value); }
        }

        // --- System -------------------------------------------------------
        private string _statusText = "系统监控中";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private string _uptimeText = "00:00:00";
        public string UptimeText { get => _uptimeText; set => Set(ref _uptimeText, value); }

        private DateTime _startTime = DateTime.Now;

        // --- Fan mode label -----------------------------------------------
        private string _fanModeLabel = "默认";
        public string FanModeLabel { get => _fanModeLabel; set => Set(ref _fanModeLabel, value); }

        // --- Manual / Auto mode -------------------------------------------
        // Default is auto: the BIOS controls the fans. Sliders and preset
        // buttons are only enabled once the user switches to manual mode.
        private bool _isManualMode;
        public bool IsManualMode {
            get => _isManualMode;
            set {
                Set(ref _isManualMode, value);
                // IsEnabled bindings watch this derived flag
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ManualControlsEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModeLabel)));
            }
        }

        // Bound to IsEnabled of sliders / preset buttons
        public bool ManualControlsEnabled => _isManualMode;

        // Friendly label for the mode switch
        public string ModeLabel => _isManualMode ? "手动模式" : "自动模式";
#endregion

#region Platform Reference
        internal Platform Platform { get; private set; }
        private System.Timers.Timer _timer;

        // Fan-curve engine (runs the temperature→speed table from the config)
        private FanProgram _program;

        // How many poll ticks between fan-program updates (poll is 3 s;
        // Config.UpdateProgramInterval is in seconds — run the curve roughly
        // on that cadence)
        private int _programTick;
#endregion

#region Initialization & Disposal
        public HardwareViewModel(Platform platform) {
            Platform = platform;

            // Fan-curve engine — status callback is a no-op in the GUI
            _program = new FanProgram(platform, (sev, msg) => { });

            _timer = new System.Timers.Timer(3000);
            _timer.Elapsed += (s, e) => PollHardware();
            _timer.AutoReset = true;
            _timer.Start();

            // Fire once immediately
            PollHardware();
        }

        public void Dispose() {
            _timer?.Stop();
            _timer?.Dispose();
        }
#endregion

#region Hardware Polling
        private void PollHardware() {
            try {
                Platform.UpdateTemperature();

                int cputRaw = Platform.Temperature.Length > 0 ? Platform.Temperature[0].GetValue() : 0;
                int gptmRaw = Platform.Temperature.Length > 1 ? Platform.Temperature[1].GetValue() : 0;

                int rpm0 = 0, rpm1 = 0, pct0 = 0, pct1 = 0;
                try { rpm0 = Platform.Fans.Fan[0].GetSpeed(); } catch { }
                try { rpm1 = Platform.Fans.Fan[1].GetSpeed(); } catch { }
                try { pct0 = Platform.Fans.Fan[0].GetRate();  } catch { }
                try { pct1 = Platform.Fans.Fan[1].GetRate();  } catch { }

                TimeSpan up = DateTime.Now - _startTime;
                string uptStr = string.Format("{0:D2}:{1:D2}:{2:D2}",
                    (int) up.TotalHours, up.Minutes, up.Seconds);

                Application.Current?.Dispatcher.Invoke(() => {
                    if(cputRaw > 0) { CpuTemp = cputRaw; NotifyBars(); }
                    if(gptmRaw > 0) { GpuTemp = gptmRaw; NotifyBars(); }
                    if(rpm0 > 0)    { CpuFanRpm = rpm0; NotifyBars(); }
                    if(rpm1 > 0)    { GpuFanRpm = rpm1; NotifyBars(); }
                    if(pct0 > 0)    { CpuFanPct = pct0; NotifyBars(); }
                    if(pct1 > 0)    { GpuFanPct = pct1; NotifyBars(); }

                    // In auto mode the sliders mirror the live fan level so the
                    // user sees what the fans are actually doing. In manual mode
                    // the sliders hold the user's chosen value and are not touched.
                    if(!IsManualMode) {
                        int lvl0 = 0, lvl1 = 0;
                        try { var lv = Platform.Fans.GetLevels(); lvl0 = lv[0]; lvl1 = lv[1]; } catch { }
                        if(lvl0 > 0) CpuFanLevel = Math.Min(Math.Max(lvl0, 20), 55);
                        if(lvl1 > 0) GpuFanLevel = Math.Min(Math.Max(lvl1, 20), 55);
                    }

                    UptimeText = uptStr;
                    StatusText = CpuTemp >= 75 ? "⚠ 温度偏高" : "● 系统正常";
                });

                // Drive the fan-curve engine on roughly the configured cadence.
                // Only runs when a curve program is active (auto mode + a curve
                // selected); otherwise this is a no-op.
                if(_program != null && _program.IsEnabled) {
                    int every = Math.Max(1, Config.UpdateProgramInterval / 3);
                    if(++_programTick >= every) {
                        _programTick = 0;
                        try { _program.Update(); } catch { }
                    }
                }

            } catch { /* hardware read errors are non-fatal */ }
        }

        private void NotifyBars() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuTempBar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuTempBar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuFanBar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuFanBar)));
        }
#endregion

#region Fan Control Actions
        public void ApplyPreset(string preset) {
            try {
                bool isMax = Platform.Fans.GetMax();
                bool isOff = Platform.Fans.GetOff();

                switch(preset) {
                    case "Auto":
                        FanModeLabel = "自动";
                        IsManualMode = false;
                        Platform.Fans.SetMax(false);
                        Platform.Fans.SetOff(false);
                        Platform.Fans.SetLevels(new byte[] {0xFF, 0xFF});
                        Platform.Fans.SetMode(BiosData.FanMode.Default);
                        break;
                    case "Max":
                        FanModeLabel = "最大";
                        IsManualMode = true;
                        if(isOff) Platform.Fans.SetOff(false);
                        Platform.Fans.SetMax(true);
                        break;
                    case "High":
                        FanModeLabel = "高速";
                        ApplyLevel(45);
                        break;
                    case "Mid":
                        FanModeLabel = "中速";
                        ApplyLevel(37);
                        break;
                    case "Low":
                        FanModeLabel = "低速";
                        ApplyLevel(27);
                        break;
                    case "Silent":
                        FanModeLabel = "安静";
                        ApplyLevel(20);
                        break;
                    case "Off":
                        FanModeLabel = "关闭";
                        IsManualMode = true;
                        if(isMax) Platform.Fans.SetMax(false);
                        Platform.Fans.SetOff(true);
                        break;
                }
            } catch { }
        }

        private void ApplyLevel(byte level) {
            IsManualMode = true;
            Platform.Fans.SetMax(false);
            Platform.Fans.SetOff(false);
            Platform.Fans.SetLevels(new byte[] {level, level});
            Platform.Fans.SetMode(Platform.Fans.GetMode());
            Platform.Fans.SetCountdown(Config.FanCountdownExtendInterval);
        }

        // Switches to manual mode. Seeds both sliders from the current
        // real fan rate so the starting point matches what the fans are
        // actually doing, then applies that level immediately.
        public void EnterManualMode() {
            IsManualMode = true;
            // Seed sliders from current rate (mapped into the 20-55 range)
            int seed0 = _cpuFanPct > 0 ? 20 + (_cpuFanPct * 35 / 100) : 20;
            int seed1 = _gpuFanPct > 0 ? 20 + (_gpuFanPct * 35 / 100) : 20;
            CpuFanLevel = Math.Min(Math.Max(seed0, 20), 55);
            GpuFanLevel = Math.Min(Math.Max(seed1, 20), 55);
            ApplyManualLevels();
        }

        public void ApplyManualLevels() {
            if(!IsManualMode) return;
            try {
                // A manual slider change overrides any running curve program
                if(_program != null && _program.IsEnabled)
                    _program.Terminate();

                ApplyLevel((byte) Math.Round(CpuFanLevel));
                // For GPU fan specifically, we override with its own slider
                Platform.Fans.SetLevels(new byte[] {
                    (byte) Math.Round(CpuFanLevel),
                    (byte) Math.Round(GpuFanLevel)});
                Platform.Fans.SetMode(Platform.Fans.GetMode());
                Platform.Fans.SetCountdown(Config.FanCountdownExtendInterval);
                FanModeLabel = "手动";
            } catch { }
        }
#endregion

#region Fan Curve Programs
        // Names of all configured fan curves (from OmenMon.xml <FanPrograms>)
        public System.Collections.Generic.List<string> CurveNames =>
            new System.Collections.Generic.List<string>(Config.FanProgram.Keys);

        // The curve currently selected/running in auto mode ("" = none)
        private string _activeCurve = "";
        public string ActiveCurve {
            get => _activeCurve;
            set { Set(ref _activeCurve, value); }
        }

        // Runs a named temperature→speed curve in auto mode. The FanProgram
        // engine sets fan levels by temperature; PollHardware drives Update().
        public void RunCurve(string name) {
            try {
                if(string.IsNullOrEmpty(name) || !Config.FanProgram.ContainsKey(name))
                    return;

                // Auto mode: the curve (not the user) drives the fans, so the
                // manual sliders stay locked.
                IsManualMode = false;

                Platform.Fans.SetMax(false);
                Platform.Fans.SetOff(false);

                _programTick = 0;
                _program.Run(name);

                ActiveCurve  = name;
                FanModeLabel = "曲线：" + name;
            } catch { }
        }

        // Stops any running curve and hands the fans back to the BIOS default.
        public void StopCurve() {
            try {
                if(_program != null && _program.IsEnabled)
                    _program.Terminate();
                ActiveCurve = "";
            } catch { }
        }
#endregion

#region Curve Editing & Config Import / Export
        // One editable row of a fan curve: temperature and per-fan percentages
        public class CurveRow {
            public int Temperature { get; set; }
            public int CpuPercent  { get; set; }
            public int GpuPercent  { get; set; }
        }

        // Returns the temperature→[cpu,gpu] level table of a named curve,
        // as an editable list of rows (temperature, cpu%, gpu%).
        public System.Collections.Generic.List<CurveRow> GetCurveRows(string name) {
            var rows = new System.Collections.Generic.List<CurveRow>();
            if(!Config.FanProgram.ContainsKey(name)) return rows;
            foreach(var kv in Config.FanProgram[name].Level)
                rows.Add(new CurveRow {
                    Temperature = kv.Key,
                    // Stored levels are 0-55 "krpm" units — show as 0-100%
                    CpuPercent = LevelToPct(kv.Value[0]),
                    GpuPercent = LevelToPct(kv.Value[1])
                });
            return rows;
        }

        // Overwrites a curve's table from edited rows and persists to XML.
        public void SaveCurveRows(string name, System.Collections.Generic.List<CurveRow> rows) {
            try {
                if(!Config.FanProgram.ContainsKey(name)) return;
                var tbl = new System.Collections.Generic.SortedDictionary<byte, byte[]>();
                foreach(var r in rows)
                    tbl[(byte) r.Temperature] = new byte[] {
                        PctToLevel(r.CpuPercent), PctToLevel(r.GpuPercent) };
                Config.FanProgram[name].Level = tbl;
                Config.Save();

                // If this curve is currently running, restart it so edits apply
                if(_activeCurve == name && _program.IsEnabled)
                    RunCurve(name);
            } catch { }
        }

        // Writes the whole configuration (all curves + settings) to a file.
        public bool ExportConfig(string path) {
            try {
                Config.Save();                       // flush in-memory state to Config.FilePath
                System.IO.File.Copy(Config.FilePath, path, true);
                return true;
            } catch { return false; }
        }

        // Loads a configuration file, replacing the active one, then reloads.
        public bool ImportConfig(string path) {
            try {
                System.IO.File.Copy(path, Config.FilePath, true);
                Config.Load();                       // re-read curves + settings
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurveNames)));
                return true;
            } catch { return false; }
        }

        // 0-55 hardware level  ↔  0-100 percentage helpers
        private static int  LevelToPct(byte level) => (int) Math.Round(level / 55.0 * 100.0);
        private static byte PctToLevel(int pct)    => (byte) Math.Round(Math.Min(Math.Max(pct, 0), 100) / 100.0 * 55.0);
#endregion

    }

}
