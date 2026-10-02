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

        private bool _cpuTempValid, _gpuTempValid;
        public bool CpuTempValid { get => _cpuTempValid; private set => Set(ref _cpuTempValid, value); }
        public bool GpuTempValid { get => _gpuTempValid; private set => Set(ref _gpuTempValid, value); }
        public string CpuTempText => CpuTempValid ? CpuTemp.ToString() : "--";
        public string GpuTempText => GpuTempValid ? GpuTemp.ToString() : "--";
        private string _temperatureWarning = "";

        // Derived: progress bar widths / colours change at 60 / 75 °C
        public double CpuTempBar => CpuTempValid ? Math.Min(Math.Max(CpuTemp, 0), 100) / 100.0 : 0;
        public double GpuTempBar => GpuTempValid ? Math.Min(Math.Max(GpuTemp, 0), 100) / 100.0 : 0;

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

        // Manual slider values as a percentage of max fan speed (0-100), matching
        // the displayed fan-rate %. Converted to the 0-55 hardware level on apply.
        private double _cpuFanLevel = 40;
        public double CpuFanLevel {
            get => _cpuFanLevel;
            set { Set(ref _cpuFanLevel, value); }
        }

        private double _gpuFanLevel = 40;
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

        // Measured refresh cadence and how long the hardware pass itself took,
        // so the real rate is something to read off rather than assume
        private string _pollInfo = "";
        public string PollInfo { get => _pollInfo; set => Set(ref _pollInfo, value); }

        private readonly System.Diagnostics.Stopwatch _pollClock =
            System.Diagnostics.Stopwatch.StartNew();
        private long _lastPollMs;

        // --- Fan mode label -----------------------------------------------
        private string _fanModeLabel = "默认";
        public string FanModeLabel { get => _fanModeLabel; set => Set(ref _fanModeLabel, value); }

        // Single source of truth for the active fan mode, used by every page to
        // highlight the current selection: any Presets key, plus Manual/Curve
        private string _activePreset = "Auto";
        public string ActivePreset { get => _activePreset; set => Set(ref _activePreset, value); }

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
        private readonly FanOffController _fanOff;
        private readonly object _controlGate = new object();
        private bool _disposed;
        private bool _suspended;
        private int _controlVersion;

        // Poll cadence [s], and how many poll ticks make up one fan-program
        // interval. Both come from the configuration; the poll interval used to
        // be hard-coded at 3 s, which ignored UpdateMonitorInterval entirely
        // and left the read-outs visibly behind other monitoring tools.
        private int _pollInterval;
        private int _programEvery;
        private int _programTick;
#endregion

#region Initialization & Disposal
        public HardwareViewModel(Platform platform) {
            Platform = platform;

            // Fan-curve engine — status is surfaced through CurveStatus instead
            _program = new FanProgram(platform, (sev, msg) => { });
            _fanOff = new FanOffController(platform.Fans, Hw.EcBatch, () => Config.FanLevelNeedManual);

            _pollInterval = Math.Max(1, Config.UpdateMonitorInterval);
            _programEvery = Math.Max(1, (int) Math.Round(
                (double) Config.UpdateProgramInterval / _pollInterval));

            _timer = new System.Timers.Timer(_pollInterval * 1000);
            _timer.Elapsed += (s, e) => PollHardware();
            _timer.AutoReset = true;
            _timer.Start();

            // Fire once immediately
            PollHardware();
        }

        public void Dispose() {
            lock(_controlGate) {
                if(_disposed) return;
                _disposed = true;
                ++_controlVersion;
                _timer?.Stop();
                _timer?.Dispose();
                RestoreFanOffForLifecycle("程序退出，取消停扇", 3);
            }
        }

        // Power events may run off the UI thread. Restore synchronously, but
        // never wait for the dispatcher while holding the hardware gate.
        public void HandlePowerChange(bool suspended) {
            int version;
            bool changed;
            lock(_controlGate) {
                if(_disposed) return;
                _suspended = suspended;
                version = ++_controlVersion;
                changed = _fanOff.NeedsMonitoring || ActivePreset == "Off" || ActivePreset == "Recovery";
                if(changed) RestoreFanOffForLifecycle("电源状态变化，取消停扇");
                UpdatePollInterval();
            }
            PostUpdate(() => {
                if(changed) PublishFanOffState();
            }, version);
        }

        // A normal exit can be cancelled while recovery is still pending. Keep
        // polling alive until the stop switch and automatic control are released.
        public bool TryPrepareExit() {
            lock(_controlGate) {
                if(_disposed) return !_fanOff.NeedsMonitoring;
                ++_controlVersion;
                bool monitoredOff = _fanOff.NeedsMonitoring;
                bool restored = RestoreFanOffForLifecycle("程序退出，取消停扇", 3);
                if(monitoredOff) PublishFanOffState();
                UpdatePollInterval();
                return restored;
            }
        }

        private bool RestoreFanOffForLifecycle(string reason, int attempts = 1) {
            if(!_fanOff.NeedsMonitoring) return true;
            bool silent = App.IsErrorSilent;
            App.IsErrorSilent = true;
            try {
                for(int i = 0; i < attempts; i++)
                    if(_fanOff.RestoreAuto(reason)) return true;
                return false;
            } finally { App.IsErrorSilent = silent; }
        }

        private void PostUpdate(Action update, int version) {
            var dispatcher = Application.Current?.Dispatcher;
            if(dispatcher == null || dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(new Action(() => {
                lock(_controlGate) {
                    if(_disposed || version != _controlVersion) return;
                    update();
                }
            }));
        }
#endregion

#region Hardware Polling
        // Guards against a slow tick overlapping the next one now that polling
        // runs every second: two threads reading the Embedded Controller at the
        // same time would only fight over its mutex and drop readings
        private int _polling;

        private void PollHardware() {
            if(System.Threading.Interlocked.CompareExchange(ref _polling, 1, 0) != 0)
                return;

            Action publish = null;
            int version = 0;
            try {
                lock(_controlGate) {
                    if(_disposed || _suspended) return;
                    bool silent = App.IsErrorSilent;
                    App.IsErrorSilent = true;
                    try {
                        version = _controlVersion;
                        long startMs = _pollClock.ElapsedMilliseconds;
                        FanOffTemperatures temperatures = default;
                        int rpm0 = -1, rpm1 = -1, pct0 = -1, pct1 = -1;

                        bool locked = Hw.EcBatch(() => {
                            Platform.UpdateTemperature(true);
                            // Display and stop protection share this fresh sample;
                            // a failed read must never fall back to the cache.
                            temperatures = ReadFanOffTemperatures();

                            // Failed reads keep the previous display; a successful
                            // zero is a real stopped fan and must be published.
                            if(!Platform.Fans.Fan[0].TryGetSpeed(out rpm0)) rpm0 = -1;
                            if(!Platform.Fans.Fan[1].TryGetSpeed(out rpm1)) rpm1 = -1;
                            if(!Platform.Fans.Fan[0].TryGetRate(out pct0)) pct0 = -1;
                            if(!Platform.Fans.Fan[1].TryGetRate(out pct1)) pct1 = -1;
                        });

                        bool monitoredOff = _fanOff.NeedsMonitoring;
                        // Never authorize Off with the cached display values.
                        // An unavailable monitoring pass is itself unsafe.
                        _fanOff.Check(() => locked ? temperatures : default);
                        UpdatePollInterval();

                        string curveStatus = null;
                        if(!_fanOff.NeedsMonitoring && _program != null && _program.IsEnabled) {
                            bool due = ++_programTick >= _programEvery;
                            byte maxTemp = Platform.GetMaxTemperature();
                            bool crossed = _program.GetLevel(maxTemp) != _program.LastLevel;
                            if(due || crossed) {
                                _programTick = 0;
                                try { _program.Update(); } catch { }
                            }
                            curveStatus = GetCurveStatus(maxTemp);
                        }

                        long readMs = _pollClock.ElapsedMilliseconds - startMs;
                        long sinceMs = _lastPollMs == 0 ? 0 : startMs - _lastPollMs;
                        _lastPollMs = startMs;
                        string pollStr = locked
                            ? string.Format("刷新 {0:0.0}s · 读取 {1} ms", sinceMs / 1000.0, readMs)
                            : string.Format("刷新 {0:0.0}s · EC 被占用，本次跳过", sinceMs / 1000.0);
                        TimeSpan up = DateTime.Now - _startTime;
                        string uptStr = string.Format("{0:D2}:{1:D2}:{2:D2}",
                            (int) up.TotalHours, up.Minutes, up.Seconds);

                        publish = () => {
                            if(rpm0 >= 0) CpuFanRpm = rpm0;
                            if(rpm1 >= 0) GpuFanRpm = rpm1;
                            if(pct0 >= 0) CpuFanPct = pct0;
                            if(pct1 >= 0) GpuFanPct = pct1;
                            PublishTemperatures(temperatures);
                            if(monitoredOff) PublishFanOffState();
                            if(!IsManualMode) {
                                if(pct0 >= 0) CpuFanLevel = pct0;
                                if(pct1 >= 0) GpuFanLevel = pct1;
                            }
                            if(curveStatus != null) CurveStatus = curveStatus;
                            UptimeText = uptStr;
                            PollInfo = pollStr;
                            StatusText = WithTemperatureWarning(!string.IsNullOrEmpty(_fanOff.Message)
                                ? _fanOff.Message
                                : CpuTemp >= 75 ? "⚠ 温度偏高" : "● 系统正常");
                        };
                    } catch {
                        bool monitoredOff = _fanOff.NeedsMonitoring;
                        if(monitoredOff) {
                            _fanOff.RestoreAuto("停扇保护：监控异常");
                            UpdatePollInterval();
                        }
                        publish = () => {
                            PublishTemperatures(default);
                            if(monitoredOff) PublishFanOffState();
                            else StatusText = WithTemperatureWarning("监控读取未完成");
                        };
                    } finally {
                        App.IsErrorSilent = silent;
                    }
                }
                if(publish != null) PostUpdate(publish, version);
            } finally {
                System.Threading.Interlocked.Exchange(ref _polling, 0);
            }
        }

        // A missing configured name can use its built-in fallback. A failed
        // reading cannot silently substitute another sensor for CPU or GPU.
        private FanOffTemperatures ReadFanOffTemperatures(bool enteringOff = false) {
            // Once selected for stop protection, an unreadable package sensor
            // must not fall back to RTMP: SFAN can invalidate that EC reading.
            var cpu = (enteringOff || _fanOff.NeedsMonitoring) && Platform.CpuPackageTemperature != null
                ? Platform.CpuPackageTemperature
                : Platform.GetTemperatureSensor(Config.GuiTempSensorCpu)
                    ?? Platform.GetTemperatureSensor(Config.GuiTempSensorCpuDefault);
            var gpu = Platform.GetTemperatureSensor(Config.GuiTempSensorGpu)
                ?? Platform.GetTemperatureSensor(Config.GuiTempSensorGpuDefault);
            return FanOffTemperatures.Read(cpu, gpu);
        }

        private void UpdatePollInterval() {
            // A slow user-configured display cadence must not delay protection.
            double interval = (_fanOff.NeedsMonitoring ? 1 : _pollInterval) * 1000;
            if(_timer.Interval != interval) _timer.Interval = interval;
        }

        private void NotifyBars() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuTempBar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuTempBar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuFanBar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuFanBar)));
        }

        private void PublishTemperatures(FanOffTemperatures temperatures) {
            CpuTempValid = temperatures.Cpu.IsValid;
            GpuTempValid = temperatures.Gpu.IsValid;
            CpuTemp = CpuTempValid ? temperatures.Cpu.Value : 0;
            GpuTemp = GpuTempValid ? temperatures.Gpu.Value : 0;
            _temperatureWarning = temperatures.InvalidMessage;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuTempText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuTempText)));
            NotifyBars();
        }

        private string WithTemperatureWarning(string message) {
            return string.IsNullOrEmpty(_temperatureWarning) || message.Contains(_temperatureWarning) ? message
                : "⚠ " + _temperatureWarning + "；" + message;
        }
#endregion

#region Fan Presets
        // The single definition of every one-tap fan preset. The performance-mode
        // page, the tray menu and ApplyPreset() all read this list, so the options
        // can no longer drift apart — the tray menu used to be hand-written and
        // was missing 高速 as a result.
        public sealed class FanPreset {

            public string Key    { get; private set; }  // Identifier, also ActivePreset
            public string Name   { get; private set; }  // Full name, for cards and menus
            public string Short  { get; private set; }  // Short name, for the status line
            public string Icon   { get; private set; }
            public string Detail { get; private set; }
            public int Percent   { get; private set; }  // Fan speed [%], negative for auto
            public string Accent { get; private set; }  // Theme accent color name

            public FanPreset(string key, string name, string shortName,
                string icon, string detail, int percent, string accent) {

                this.Key = key;
                this.Name = name;
                this.Short = shortName;
                this.Icon = icon;
                this.Detail = detail;
                this.Percent = percent;
                this.Accent = accent;

            }

            // Whether this preset hands the fans back to the BIOS
            public bool IsAuto {
                get { return this.Percent < 0; }
            }

            public bool IsOff => this.Key == "Off";

        }

        public static readonly IList<FanPreset> Presets = new List<FanPreset> {
            new FanPreset("Auto",   "自动",     "自动", "⚙",  "BIOS 自动调节\n跟随温度曲线",  -1, "Blue"),
            new FanPreset("Max",    "最大转速", "最大", "🚀", "全速散热\n适合游戏 / 高负载",  100, "Red"),
            new FanPreset("High",   "高速",     "高速", "⚡", "较高转速\n性能与噪音平衡",      82, "Amber"),
            new FanPreset("Mid",    "中速",     "中速", "🌿", "适合日常写代码\n开浏览器",      67, "Green"),
            new FanPreset("Silent", "安静",     "安静", "🌙", "最低转速\n极致安静体验",        36, "Violet"),
            new FanPreset("Off",    "关闭风扇", "关闭", "⏻",  "仅限低负载 · 低于 60°C\n升温 / 读数异常恢复自动", 0, "Blue")
        }.AsReadOnly();

        // Looks a preset up by its identifier, null if there is no such preset
        public static FanPreset GetPreset(string key) {
            foreach(FanPreset preset in Presets)
                if(preset.Key == key)
                    return preset;
            return null;
        }
#endregion

#region Fan Control Actions
        // Off has a dedicated hardware switch; it is not a zero-speed level.
        // All other fixed presets share the manual slider path.
        public void ApplyPreset(string preset) {
            RunFanAction(() => {
                FanPreset entry = GetPreset(preset);
                if(entry == null) return;
                if(entry.IsOff) {
                    StopCurveInternal();
                    FanOffTemperatures temperatures = default;
                    _fanOff.Enter(() => temperatures = ReadFanOffTemperatures(true));
                    // Publish the final safety observation, not a pre-stop cache.
                    PublishTemperatures(temperatures);
                    PublishFanOffState();
                } else if(entry.IsAuto) {
                    SetAuto();
                } else {
                    ApplyCoolingMode(strict => ApplyPresetLevel(entry.Key, entry.Short, entry.Percent, strict));
                }
            });
        }

        private void RunFanAction(Action action) {
            lock(_controlGate) {
                if(_disposed || _suspended) return;
                ++_controlVersion;
                bool silent = App.IsErrorSilent;
                App.IsErrorSilent = true;
                try {
                    action();
                } catch {
                    StatusText = "⚠ 风扇设置未完成，请检查实际转速";
                    if(_fanOff.NeedsMonitoring) {
                        _fanOff.RestoreAuto("风扇设置异常，取消停扇");
                        PublishFanOffState();
                    }
                } finally {
                    UpdatePollInterval();
                    App.IsErrorSilent = silent;
                }
            }
        }

        private void ApplyCoolingMode(Action<bool> apply) {
            if(_fanOff.NeedsMonitoring) {
                if(!_fanOff.TryTakeControl(() => apply(true))) {
                    // A curve may have started before the final switch read failed.
                    if(_program.IsEnabled) {
                        try { StopCurveInternal(); }
                        finally { _fanOff.RestoreAuto("曲线接管未确认"); }
                    }
                    PublishFanOffState();
                    return;
                }
            } else {
                _fanOff.ClearMessage();
                apply(false);
            }
            StatusText = WithTemperatureWarning("已请求目标风扇模式，请检查实际转速");
        }

        // Configuration import has no replacement cooling mode: it still needs
        // the entire automatic recovery, unlike an explicit mode takeover.
        private bool LeaveFanOff() {
            if(_fanOff.NeedsMonitoring) {
                bool restored = _fanOff.RestoreAuto();
                PublishFanOffState();
                if(!restored) return false;
            }
            _fanOff.ClearMessage();
            return true;
        }

        private void PublishFanOffState() {
            ActivePreset = _fanOff.IsOff ? "Off" : _fanOff.RecoveryPending ? "Recovery" : "Auto";
            FanModeLabel = _fanOff.IsOff ? "关闭（温度保护）"
                : _fanOff.RecoveryPending ? "恢复待确认" : "自动";
            IsManualMode = false;
            StatusText = WithTemperatureWarning(string.IsNullOrEmpty(_fanOff.Message) ? "已请求 BIOS 自动" : _fanOff.Message);
        }

        // Auto: hand the fans back to the BIOS default curve.
        private void SetAuto() {
            StopCurveInternal();
            _fanOff.RestoreAuto();
            PublishFanOffState();
        }

        // A fixed preset expressed as a fan-speed percentage — same mechanism as
        // manual control, so it engages on the first click and the sliders (also
        // in %) mirror the chosen value.
        private void ApplyPresetLevel(string key, string label, int pct, bool strict) {
            StopCurveInternal();
            ApplyLevel(PctToLevel(pct), strict);
            ActivePreset = key;
            FanModeLabel = label;
            IsManualMode = true;
            CpuFanLevel = pct;
            GpuFanLevel = pct;
        }

        // Applies one level to both fans through the manual/BIOS path.
        private void ApplyLevel(byte level, bool strict) {
            ApplyLevels(new byte[] { level, level }, strict);
        }

        private void ApplyLevels(byte[] levels, bool strict) {
            Platform.Fans.SetMax(false);
            Platform.Fans.SetOff(false);
            if(strict) {
                if(!Platform.Fans.TrySetLevels(levels, out string diagnostic))
                    throw new InvalidOperationException("目标风扇等级写入未确认：" + diagnostic);
            } else {
                Platform.Fans.SetLevels(levels);
            }
            Platform.Fans.SetMode(Platform.Fans.GetMode());
            Platform.Fans.SetCountdown(Config.FanCountdownExtendInterval);
        }

        // Stops a running curve program (if any) and clears the selection.
        private void StopCurveInternal() {
            try {
                if(_program != null && _program.IsEnabled)
                    _program.Terminate();
            } finally {
                ActiveCurve = "";
                CurveStatus = "曲线未运行";
            }
        }

        // Switches to manual mode. Seeds both sliders from the current
        // real fan rate so the starting point matches what the fans are
        // actually doing, then applies that level immediately.
        public void EnterManualMode() {
            RunFanAction(() => ApplyCoolingMode(strict => {
                CpuFanLevel = _cpuFanPct > 0 ? _cpuFanPct : 40;
                GpuFanLevel = _gpuFanPct > 0 ? _gpuFanPct : 40;
                ApplyManualLevelsInternal(strict);
            }));
        }

        public void ApplyManualLevels() {
            RunFanAction(() => {
                if(!IsManualMode) return;
                ApplyCoolingMode(ApplyManualLevelsInternal);
            });
        }

        private void ApplyManualLevelsInternal(bool strict) {
            StopCurveInternal();
            byte cpuLvl = PctToLevel((int) Math.Round(CpuFanLevel));
            byte gpuLvl = PctToLevel((int) Math.Round(GpuFanLevel));
            ApplyLevels(new byte[] { cpuLvl, gpuLvl }, strict);
            IsManualMode = true;
            ActivePreset = "Manual";
            FanModeLabel = "手动";
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
            set {
                Set(ref _activeCurve, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurveRunning)));
            }
        }

        // Whether a curve is driving the fans right now
        public bool IsCurveRunning => !string.IsNullOrEmpty(_activeCurve);

        // Curves that ship with the application. They are the ones the fan
        // programs in the stock configuration refer to, so removing one would
        // leave the settings inconsistent — only user-made curves can be deleted.
        public static readonly IList<string> BuiltInCurves =
            new List<string> { "Auto", "Power", "Silent" }.AsReadOnly();

        public static bool IsBuiltInCurve(string name) {
            foreach(string builtIn in BuiltInCurves)
                if(string.Equals(builtIn, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // Removes a user-made curve and persists the change. Refuses to touch
        // the built-in ones. Stops the curve first if it happens to be running,
        // so the fans are handed back rather than left on its last levels.
        public bool DeleteCurve(string name) {
            try {
                if(string.IsNullOrEmpty(name) || IsBuiltInCurve(name))
                    return false;
                if(!Config.FanProgram.ContainsKey(name))
                    return false;

                if(_activeCurve == name)
                    ApplyPreset("Auto");

                Config.FanProgram.Remove(name);
                Config.Save();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurveNames)));
                return true;

            } catch { return false; }
        }

        // Adds a new curve, copied from an existing one when one is named, and
        // writes it to the configuration file. Returns false if the name is
        // blank or already taken.
        public bool CreateCurve(string name, string cloneFrom = null) {
            try {
                if(string.IsNullOrWhiteSpace(name))
                    return false;

                name = name.Trim();
                if(Config.FanProgram.ContainsKey(name))
                    return false;

                var levels = new System.Collections.Generic.SortedDictionary<byte, byte[]>();
                BiosData.FanMode mode = BiosData.FanMode.Default;
                BiosData.GpuPowerLevel power = BiosData.GpuPowerLevel.Medium;

                if(!string.IsNullOrEmpty(cloneFrom) && Config.FanProgram.ContainsKey(cloneFrom)) {
                    FanProgramData source = Config.FanProgram[cloneFrom];
                    mode = source.FanMode;
                    power = source.GpuPower;
                    foreach(var kv in source.Level)
                        levels[kv.Key] = new byte[] { kv.Value[0], kv.Value[1] };
                } else {
                    // A gentle starting shape the user can then edit
                    levels[0]  = new byte[] { PctToLevel(36),  PctToLevel(36)  };
                    levels[60] = new byte[] { PctToLevel(51),  PctToLevel(51)  };
                    levels[70] = new byte[] { PctToLevel(73),  PctToLevel(73)  };
                    levels[80] = new byte[] { PctToLevel(100), PctToLevel(100) };
                }

                Config.FanProgram[name] = new FanProgramData(name, mode, power, levels);
                Config.Save();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurveNames)));
                return true;

            } catch { return false; }
        }

        // Live account of what the running curve last decided, so the rule
        // firing is something the user can actually see rather than infer
        private string _curveStatus = "曲线未运行";
        public string CurveStatus { get => _curveStatus; set => Set(ref _curveStatus, value); }

        // Composes the status line from the threshold the engine settled on
        private string GetCurveStatus(byte temperature) {
            string text;
            try {
                byte level = _program.LastLevel;
                byte[] fans = Config.FanProgram[_activeCurve].Level[level];
                text = string.Format(
                    "运行中 · 最高温 {0}°C → 命中阈值 {1}°C → 风扇 {2}% / {3}%",
                    temperature, level, LevelToPct(fans[0]), LevelToPct(fans[1]));
            } catch {
                text = string.Format("运行中 · 最高温 {0}°C", temperature);
            }
            return text;
        }

        // Runs a named temperature→speed curve in auto mode. The FanProgram
        // engine sets fan levels by temperature; PollHardware drives Update().
        public void RunCurve(string name) {
            RunFanAction(() => {
                if(string.IsNullOrEmpty(name) || !Config.FanProgram.ContainsKey(name))
                    return;
                ApplyCoolingMode(strict => {
                    Platform.Fans.SetMax(false);
                    Platform.Fans.SetOff(false);
                    _programTick = 0;
                    if(!_program.Run(name, strict: strict))
                        throw new InvalidOperationException("曲线启动未确认");

                    IsManualMode = false;
                    ActivePreset = "Curve";
                    ActiveCurve = name;
                    FanModeLabel = "曲线：" + name;
                });
            });
        }

        // Stops any running curve and hands the fans back to the BIOS default.
        public void StopCurve() {
            RunFanAction(SetAuto);
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
            lock(_controlGate) {
                if(_disposed || _suspended) return false;
                ++_controlVersion;
                // Imported sensor mappings must never take over an active stop.
                if(!LeaveFanOff()) return false;
                try {
                    System.IO.File.Copy(path, Config.FilePath, true);
                    Config.Load();
                    Platform.ReloadTemperatureSensors();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurveNames)));
                    return true;
                } catch { return false; }
                finally { UpdatePollInterval(); }
            }
        }

        // 0-55 hardware level  ↔  0-100 percentage helpers
        private static int  LevelToPct(byte level) => (int) Math.Round(level / 55.0 * 100.0);
        private static byte PctToLevel(int pct)    => (byte) Math.Round(Math.Min(Math.Max(pct, 0), 100) / 100.0 * 55.0);
#endregion

    }

}
