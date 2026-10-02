using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OmenMon.AppWpf;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Ec;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

internal static class FanOffTests {
    private static int _passed;

    [STAThread]
    private static int Main() {
        // No App.Main, Platform constructor, driver initialization or real BIOS
        // calls. All hardware interfaces below are in-memory fakes.
        new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try {
            Run("dedicated stop command and repeated selection", Entry);
            Run("CPU/GPU temperature boundaries", TemperatureBoundaries);
            Run("stop command immediately rechecks temperature", StopTemperatureRecheck);
            Run("independent CPU temperature survives RTMP stop interference", IndependentCpuTemperature);
            Run("CPU package MSR decoding rejects unsupported invalid and stale data", PackageMsrReadings);
            Run("plausible temperature bounds protect both sensors", TemperaturePlausibility);
            Run("temperature display rejects invalid stale and unavailable samples", TemperatureDisplayValidity);
            Run("fresh temperature, not cached readings", FreshReadings);
            Run("firmware override and failed confirmation", FirmwareOverride);
            Run("EC batch unavailable and recovery retry", BatchFailure);
            Run("partial recovery and command ordering", RecoveryFailures);
            Run("valid zero telemetry and stop switch readback", ZeroReadings);
            Run("EC timeouts never return zero or partial words", EcTimeouts);
            Run("shared presets and ViewModel mode transitions", ModeTransitions);
            Run("protection, zero display and stale UI publication", PollProtection);
            Run("named fallback is refreshed and invalid data rejected", SensorFallback);
            Run("suspend/resume, disposal and no automatic re-entry", Lifecycle);
            Run("curve and fan-off transitions", CurveTransitions);
            Run("in-flight polling and disposal do not deadlock", ConcurrentDisposal);
            Run("normal exit waits for confirmed recovery", ExitRecovery);
            Run("restoration observes updated manual-control configuration", ManualConfiguration);
            Run("strict restoration does not swallow BIOS level errors", StrictLevelErrors);
            Run("BIOS error with freshly applied EC targets is confirmed", AppliedLevelErrors);
            Run("target confirmation rejects stale partial and unreadable targets", TargetReadbackFailures);
            Run("compatibility confirmation respects setup backend and recovery boundaries", TargetConfirmationBoundaries);
            Run("verified BIOS-error takeover cancels recovery for presets manual and curves", VerifiedBiosTakeover);
            Run("failed stop preparation permits explicit cooling takeover", PreparationTakeover);
            Run("takeover requires fresh release and strict target writes", TakeoverFailures);
            Run("automatic recovery still required after a real stop", AutomaticRecoveryFailures);
            Run("curve takeover rejects failed first writes and stops failed starts", CurveTakeoverFailures);
            Console.WriteLine("PASS: " + _passed + " test groups (fake hardware only)");
            return 0;
        } catch(Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Run(string name, Action test) {
        test();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Assert(bool condition, string message) {
        if(!condition) throw new Exception(message);
    }

    private static void ThrowsTimeout(Action action) {
        try { action(); } catch(TimeoutException) { return; }
        throw new Exception("Expected timeout, not a successful zero/default value");
    }

    private static FanOffTemperatures Cool() => new FanOffTemperatures(45, 43);
    private static FanOffController Controller(FakeFans fans) =>
        new FanOffController(fans, action => { action(); return true; }, () => true);

    private static void Entry() {
        var fans = new FakeFans();
        var control = Controller(fans);
        Assert(control.Enter(Cool) && fans.Off && control.IsOff, "Off must be confirmed");
        Assert(fans.Calls.Count(c => c == "Off:True") == 1, "Use dedicated switch once");
        Assert(!fans.Calls.Contains("Levels:0,0"), "Never stop by writing double-zero levels");
        control.Enter(Cool);
        control.Check(Cool);
        Assert(fans.Calls.Count(c => c == "Off:True") == 1, "Do not renew stop commands");
        Assert(control.RestoreAuto() && !fans.Off, "Explicit leave clears the switch");
        control.Check(Cool);
        Assert(!control.IsOff, "No automatic re-entry after recovery");
    }

    private static void TemperatureBoundaries() {
        foreach(var pair in new[] { new[] {59,59}, new[] {60,59}, new[] {59,60},
            new[] {0,40}, new[] {40,0}, new[] {-1,40}, new[] {125,40} }) {
            var fans = new FakeFans();
            var control = Controller(fans);
            bool allowed = pair[0] == 59 && pair[1] == 59;
            Assert(control.Enter(() => new FanOffTemperatures(pair[0], pair[1])) == allowed,
                "Entry temperature boundary");
            if(!allowed) Assert(!fans.Calls.Contains("Off:True"), "Unsafe entry must not write Off");
        }
        foreach(var temp in new[] { new FanOffTemperatures(60,40), new FanOffTemperatures(40,60), default }) {
            var fans = new FakeFans();
            var control = Controller(fans);
            control.Enter(Cool);
            control.Check(() => temp);
            Assert(!control.IsOff && !fans.Off, "Unsafe monitoring restores fans");
            control.Check(Cool);
            Assert(!control.IsOff, "Cooling alone never restarts Off");
        }
    }

    private static void StopTemperatureRecheck() {
        foreach(string failure in new[] { "CpuLow", "GpuLow", "CpuHigh", "GpuHigh", "CpuRead", "GpuRead" }) {
            using(var fixture = new Fixture()) {
                bool cpu = failure.StartsWith("Cpu");
                Component sensor = cpu ? fixture.Cpu : fixture.Gpu;
                sensor.BeforeRead = () => {
                    if(!fixture.Fans.Off) return;
                    if(failure.EndsWith("Read")) sensor.Fail = true;
                    else sensor.Value = failure.EndsWith("High") ? 60 : 2;
                };
                fixture.Vm.ApplyPreset("Off");
                Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset != "Off",
                    "Bad post-stop temperature must restore immediately, without waiting for a poll: " + failure);
                Assert(fixture.Vm.StatusText.Contains("停扇后保护"), "Post-stop rejection must remain visible");
                if(!failure.EndsWith("High")) {
                    Assert((cpu ? fixture.Vm.CpuTempText : fixture.Vm.GpuTempText) == "--",
                        "Publish the invalid post-stop sample, not a pre-stop cache: " + failure);
                    Assert((cpu ? fixture.Vm.CpuTempBar : fixture.Vm.GpuTempBar) == 0, "Invalid temperature has no normal-looking gauge");
                }
                Assert((cpu ? fixture.Vm.GpuTempText : fixture.Vm.CpuTempText) != "--", "Other sensor remains independent");
                sensor.BeforeRead = null;
                sensor.Fail = false;
                sensor.Value = 45;
                fixture.Poll();
                Assert(!fixture.Fans.Off, "A valid reading does not automatically re-enter Off");
            }
        }
        using(var fixture = new Fixture()) {
            fixture.Fans.IgnoreRestore = true;
            fixture.Cpu.BeforeRead = () => { if(fixture.Fans.Off) fixture.Cpu.Value = 2; };
            fixture.Vm.ApplyPreset("Off");
            Assert(fixture.Vm.ActivePreset == "Recovery" && fixture.Vm.CpuTempText == "--",
                "Failed post-stop restoration retains monitoring and invalid display");
            Assert(fixture.Vm.StatusText.Contains("正在重试") && fixture.Vm.StatusText.Contains("=2°C"),
                "Pending restoration reports the observed bogus value without claiming recovery success");
            fixture.Fans.IgnoreRestore = false;
            fixture.Poll();
            Assert(!fixture.Fans.Off, "Pending post-stop recovery is retried");
        }
    }

    private static void IndependentCpuTemperature() {
        var msr = new FakeMsr();
        var package = new IntelPackageTemperatureComponent("GenuineIntel", "BFEBFBFF000906A3", msr.Read);
        using(var fixture = new Fixture(cpuPackage: package)) {
            fixture.Cpu.BeforeRead = () => fixture.Cpu.Value = fixture.Fans.Off ? 2 : 45;
            Assert(fixture.Vm.CpuTempText == "45" && msr.Calls.Count == 0, "Ordinary monitoring retains configured CPU source");
            fixture.Vm.ApplyPreset("Off");
            Assert(fixture.Fans.Off && fixture.Vm.ActivePreset == "Off" && fixture.Vm.CpuTempText == "47",
                "Independent package reading must keep stop usable when RTMP becomes 2 C");
            Assert(fixture.Vm.StatusText.Contains("CPU Package (MSR)"), "Show the actual independent CPU source");
            msr.Temperature = 51;
            fixture.Gpu.Value = 44;
            fixture.Poll();
            Assert(fixture.Cpu.GetValue() == 2 && fixture.Fans.Off && fixture.Vm.CpuTempText == "51"
                && fixture.Vm.GpuTempText == "44", "Fresh package/GPU data continue while RTMP is corrupted");
            msr.Temperature = 60;
            fixture.Poll();
            Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == "Auto", "Package reaching 60 C triggers automatic restoration");
            int reads = msr.Calls.Count;
            fixture.Poll();
            Assert(fixture.Vm.CpuTempText == "45" && msr.Calls.Count == reads, "Confirmed recovery switches monitoring back to RTMP");
            msr.Temperature = 47;
            fixture.Vm.ApplyPreset("Off");
            msr.FailRegister = 0x1B1;
            fixture.Poll();
            Assert(!fixture.Fans.Off && fixture.Vm.CpuTempText == "--", "Package failure cannot use old sample or fall back to RTMP");
            Assert(fixture.Vm.StatusText.Contains("CPU Package (MSR)"), "Failed source remains identifiable");
            fixture.Poll();
            Assert(fixture.Vm.CpuTempText == "45" && !fixture.Fans.Off, "Fresh RTMP resumes without re-entering stop");
        }
        foreach(string failure in new[] { "High", "Unreadable", "Invalid" }) {
            var probe = new FakeMsr();
            if(failure == "High") probe.Temperature = 60;
            if(failure == "Unreadable") probe.FailRegister = 0x1B1;
            if(failure == "Invalid") probe.Status &= 0x7FFFFFFF;
            var sensor = new IntelPackageTemperatureComponent("GenuineIntel", "BFEBFBFF000906A3", probe.Read);
            using(var fixture = new Fixture(cpuPackage: sensor)) {
                fixture.Vm.ApplyPreset("Off");
                Assert(!fixture.Fans.Calls.Contains("Off:True"), "Package preflight cannot fall back to cool RTMP: " + failure);
            }
        }
        using(var fixture = new Fixture(cpuPackage: package)) {
            msr.FailRegister = 0;
            msr.Temperature = 47;
            fixture.Vm.ApplyPreset("Off");
            fixture.Fans.IgnoreRestore = true;
            msr.FailRegister = 0x1B1;
            fixture.Poll();
            Assert(fixture.Vm.ActivePreset == "Recovery" && fixture.Vm.CpuTempText == "--", "Failed restoration keeps package source armed");
            fixture.Poll();
            Assert(fixture.Vm.CpuTempText == "--", "Recovery cannot fall back to a plausible configured sensor either");
            fixture.Fans.IgnoreRestore = false;
            fixture.Poll();
        }
    }

    private static void PackageMsrReadings() {
        foreach(string id in new[] { "BFEBFBFF000906A3", "BFEBFBFF00090672" }) {
            var probe = new FakeMsr();
            var sensor = new IntelPackageTemperatureComponent("GenuineIntel", id, probe.Read);
            Assert(sensor.IsSupported && sensor.TryRead(out int value) && value == 47, "Read Alder Lake package temperature");
            Assert(probe.Calls.SequenceEqual(new uint[] { 0x1A2, 0x1B1 }), "Only target and package status are read");
            probe.Temperature = 55;
            Assert(sensor.TryRead(out value) && value == 55, "Every reading is fresh");
            probe.FailRegister = 0x1B1;
            Assert(!sensor.TryRead(out value) && value == 0 && sensor.GetValue() == 55,
                "TryRead does not return the old cached reading after failure");
        }
        foreach(string failure in new[] { "TargetRead", "StatusRead", "Validity", "ZeroTarget", "LowTarget", "HighTarget", "Negative", "OverRange", "Throw" }) {
            var probe = new FakeMsr();
            if(failure == "TargetRead") probe.FailRegister = 0x1A2;
            if(failure == "StatusRead") probe.FailRegister = 0x1B1;
            if(failure == "Validity") probe.Status &= 0x7FFFFFFF;
            if(failure == "ZeroTarget") probe.Target = 0;
            if(failure == "LowTarget") probe.Target = 2u << 16;
            if(failure == "HighTarget") probe.Target = 255u << 16;
            if(failure == "Negative") probe.Status = 0x80000000u | (127u << 16);
            if(failure == "OverRange") { probe.Target = 125u << 16; probe.Status = 0x80000000u; }
            if(failure == "Throw") probe.Throw = true;
            var sensor = new IntelPackageTemperatureComponent("GenuineIntel", "BFEBFBFF000906A3", probe.Read);
            Assert(!sensor.TryRead(out _), "Do not invent a package temperature on " + failure);
        }
        foreach(var cpu in new[] {
            new[] { "AuthenticAMD", "BFEBFBFF000906A3" },
            new[] { "GenuineIntel", "BFEBFBFF000806E9" },
            new[] { "GenuineIntel", "BFEBFBFF001906A3" },
            new[] { "GenuineIntel", "invalid" },
            new[] { "GenuineIntel", (string) null }
        }) {
            var probe = new FakeMsr();
            var sensor = new IntelPackageTemperatureComponent(cpu[0], cpu[1], probe.Read);
            Assert(!sensor.IsSupported && !sensor.TryRead(out _) && probe.Calls.Count == 0,
                "Unsupported or unknown CPUs must never receive Intel package MSR reads");
        }
    }

    private static void TemperaturePlausibility() {
        foreach(int value in new[] { 0, 2, 9, 10, 59, 60, Config.MaxBelievableTemperature, Config.MaxBelievableTemperature + 1 }) {
            foreach(bool cpu in new[] { true, false }) {
                var temperature = cpu ? new FanOffTemperatures(value, 43) : new FanOffTemperatures(45, value);
                bool valid = value >= 10 && value <= Config.MaxBelievableTemperature;
                Assert(temperature.IsValid == valid, "Plausibility boundary: " + value);
                var fans = new FakeFans();
                bool expected = valid && value < 60;
                Assert(Controller(fans).Enter(() => temperature) == expected, "Stop boundary: " + value);
                Assert(fans.Calls.Contains("Off:True") == expected, "Invalid initial reading must never send stop");
            }
        }
    }

    private static void TemperatureDisplayValidity() {
        using(var fixture = new Fixture()) {
            Assert(fixture.Vm.CpuTempText == "45" && fixture.Vm.GpuTempText == "43", "Initial fresh display");
            fixture.Vm.ApplyPreset("Off");
            fixture.Cpu.Value = 2;
            fixture.Gpu.Value = 44;
            fixture.Poll();
            Assert(!fixture.Fans.Off && fixture.Vm.CpuTempText == "--" && fixture.Vm.GpuTempText == "44",
                "Polling invalidates bad CPU telemetry, restores fans, and updates GPU");
            Assert(fixture.Vm.StatusText.Contains(Config.GuiTempSensorCpuDefault) && fixture.Vm.StatusText.Contains("=2°C"),
                "Diagnostic identifies the actual CPU source and observed value");
            fixture.Gpu.Value = 42;
            fixture.Poll();
            Assert(fixture.Vm.CpuTempText == "--" && fixture.Vm.GpuTempText == "42", "GPU refreshes while CPU remains invalid");
            fixture.Cpu.Value = 46;
            fixture.Poll();
            Assert(fixture.Vm.CpuTempValid && fixture.Vm.CpuTempText == "46" && fixture.Vm.CpuTempBar > 0,
                "Fresh valid CPU values restore display");
            fixture.Cpu.Fail = true;
            fixture.Gpu.Value = 41;
            fixture.Poll();
            Assert(!fixture.Vm.CpuTempValid && fixture.Vm.CpuTempText == "--" && fixture.Vm.GpuTempText == "41",
                "Read failure cannot redisplay the cached 46 C sample");
            fixture.Cpu.Fail = false;
            fixture.Gpu.Fail = true;
            fixture.Poll();
            Assert(fixture.Vm.CpuTempText == "46" && fixture.Vm.GpuTempText == "--", "Validity is independent in both directions");
            fixture.Gpu.Fail = false;
            fixture.InvokePoll(); // Queue a valid older sample.
            fixture.Cpu.BeforeRead = () => { if(fixture.Fans.Off) fixture.Cpu.Value = 2; };
            fixture.Vm.ApplyPreset("Off");
            DrainDispatcher();
            Assert(fixture.Vm.CpuTempText == "--", "Old valid publication cannot overwrite post-stop invalidity");
        }
        foreach(string mode in new[] { "Locked", "ThrowRequest" }) {
            using(var fixture = new Fixture()) {
                ((ProtocolEc) Hw.Ec).Mode = mode;
                fixture.Poll();
                Assert(fixture.Vm.CpuTempText == "--" && fixture.Vm.GpuTempText == "--",
                    "An unavailable monitoring pass invalidates old temperatures: " + mode);
                ((ProtocolEc) Hw.Ec).Mode = "Ready";
                fixture.Poll();
                Assert(fixture.Vm.CpuTempText == "45" && fixture.Vm.GpuTempText == "43", "Monitoring recovers after batch failure");
            }
        }
    }

    private static void FreshReadings() {
        var cpu = new Component { Value = 45 };
        var gpu = new Component { Value = 43 };
        cpu.SetConstraint(125);
        gpu.SetConstraint(125);
        Assert(FanOffTemperatures.Read(cpu, gpu).CanStop, "Fresh cool temperatures");
        cpu.Fail = true;
        Assert(cpu.GetValue() == 45, "Display cache retained");
        Assert(!FanOffTemperatures.Read(cpu, gpu).CanStop, "Cached low value cannot authorize Off");
        cpu.Fail = false;
        cpu.Value = 0;
        Assert(!FanOffTemperatures.Read(cpu, gpu).CanStop, "Zero temperature invalid immediately");
        cpu.Value = 200;
        Assert(!FanOffTemperatures.Read(cpu, gpu).CanStop, "Above-constraint temperature invalid");
        Assert(!FanOffTemperatures.Read(null, gpu).CanStop, "Missing sensor invalid");
    }

    private static void FirmwareOverride() {
        var fans = new FakeFans();
        var control = Controller(fans);
        control.Enter(Cool);
        fans.Off = false;
        control.Enter(Cool);
        Assert(!control.IsOff && fans.Calls.Count(c => c == "Off:True") == 1,
            "A second click must respect a firmware override");
        fans.IgnoreStop = true;
        Assert(!control.Enter(Cool) && !fans.Off, "Ignored stop request is not success");
        fans.IgnoreStop = false;
        control.Enter(Cool);
        fans.ReadValid = false;
        control.Check(Cool);
        Assert(!control.IsOff && control.RecoveryPending, "Unreadable switch is pending recovery");
        fans.ReadValid = true;
        control.Check(Cool);
        Assert(!control.RecoveryPending && !fans.Off, "Retry confirms only recovery");
    }

    private static void BatchFailure() {
        bool available = true;
        var fans = new FakeFans();
        var control = new FanOffController(fans, action => {
            if(!available) return false;
            action(); return true;
        }, () => true);
        control.Enter(Cool);
        available = false;
        control.Check(Cool);
        Assert(control.RecoveryPending && !control.IsOff, "Batch failure cancels Off intent");
        available = true;
        control.Check(Cool);
        Assert(!control.NeedsMonitoring && !fans.Off, "Recovery retried when lock returns");
        available = false;
        Assert(!control.Enter(Cool), "Cannot enter with unavailable batch");
    }

    private static void RecoveryFailures() {
        var fans = new FakeFans();
        var control = Controller(fans);
        control.Enter(Cool);
        fans.Calls.Clear();
        fans.FailCall = "Max:False";
        Assert(!control.RestoreAuto() && !fans.Off, "Ancillary failure still clears Off");
        Assert(fans.Calls[0] == "Off:False", "Clear Off before all ancillary commands");
        Assert(fans.Calls.Contains("Countdown:0"), "Continue after failed recovery step");
        Assert(fans.Calls.IndexOf("Manual:False") > fans.Calls.IndexOf("Levels:255,255"),
            "Release manual after level command");
        fans.FailCall = null;
        control.Check(Cool);
        Assert(!control.RecoveryPending, "Recovery succeeds after transient error");
        control.Enter(Cool);
        fans.IgnoreRestore = true;
        Assert(!control.RestoreAuto() && control.RecoveryPending, "Ignored release is never success");
        fans.IgnoreRestore = false;
        control.Check(Cool);
        Assert(!control.NeedsMonitoring, "Recovery readback eventually confirms release");
    }

    private static void ZeroReadings() {
        var component = new Component { Value = 3000 };
        var rate = new Component { Value = 36 };
        var fan = new Fan(BiosData.FanType.Cpu, new Component(), rate, new Component(), component);
        Assert(fan.TryGetSpeed(out int speed) && speed == 3000, "Initial RPM");
        component.Value = 0;
        Assert(fan.TryGetSpeed(out speed) && speed == 0, "Valid zero RPM is immediate");
        rate.Value = 0;
        Assert(fan.TryGetRate(out int percent) && percent == 0, "Valid zero percent");
        component.Fail = true;
        Assert(!fan.TryGetSpeed(out speed), "Failed read is not a zero observation");
        var toggle = new Component { Value = 2 };
        var fans = new FanArray(new IFan[] { fan, fan }, new Component(), new Component(), new Component(), toggle);
        Assert(fans.TryGetOff(out bool off) && off, "Valid Off switch");
        toggle.Value = 0;
        Assert(fans.TryGetOff(out off) && !off, "First valid zero confirms switch release");
        toggle.Fail = true;
        Assert(!fans.TryGetOff(out off), "No cached switch confirmation");
        toggle.Fail = false;
        toggle.Value = 3;
        Assert(!fans.TryGetOff(out off), "Unknown switch value rejected");
    }

    private static void EcTimeouts() {
        int retries = Config.EcRetryLimit, waits = Config.EcWaitLimit;
        Config.EcRetryLimit = 2;
        Config.EcWaitLimit = 1;
        try {
            var ec = new ProtocolEc { Mode = "Busy" };
            ThrowsTimeout(() => ec.ReadByte(0x10));
            ThrowsTimeout(() => ec.ReadWord(0x10));
            ThrowsTimeout(() => ec.WriteByte(0x10, 2));
            ThrowsTimeout(() => ec.WriteWord(0x10, 2));
            ec.Mode = "NoData";
            for(int i = 0; i < Config.EcFailLimit + 2; i++)
                ThrowsTimeout(() => ec.ReadByte(0x10));
            ec.Mode = "Partial";
            ThrowsTimeout(() => ec.ReadWord(0x10));
            ec.Mode = "Ready";
            ec.Data = 0;
            Assert(ec.ReadByte(0x10) == 0 && ec.ReadWord(0x10) == 0, "Real zero stays valid");
        } finally {
            Config.EcRetryLimit = retries;
            Config.EcWaitLimit = waits;
        }
    }

    private static void ModeTransitions() {
        Assert(HardwareViewModel.Presets.Count == 6, "Six shared presets");
        Assert(HardwareViewModel.Presets.Count(p => p.IsOff) == 1, "Unique Off preset");
        Assert(HardwareViewModel.GetPreset("Silent").Percent == 36, "Silent unchanged");
        using(var fixture = new Fixture()) {
            var page = new ModePage(fixture.Vm);
            var root = (System.Windows.Controls.StackPanel)
                ((System.Windows.Controls.ScrollViewer) page.Content).Content;
            var grid = (System.Windows.Controls.Primitives.UniformGrid) root.Children[1];
            Assert(grid.Columns == 3 && grid.Children.Count == 6, "Six cards fill two rows");
            var status = (System.Windows.Controls.TextBlock) root.Children[2];
            var binding = System.Windows.Data.BindingOperations.GetBinding(status,
                System.Windows.Controls.TextBlock.TextProperty);
            Assert(binding.Path.Path == nameof(HardwareViewModel.StatusText), "Mode page shows protection feedback");
        }
        foreach(string preset in new[] { "Auto", "Max", "High", "Mid", "Silent" }) {
            using(var fixture = new Fixture()) {
                fixture.Vm.ApplyPreset("Off");
                Assert(fixture.Vm.ActivePreset == "Off", "Off selected");
                fixture.Vm.ApplyPreset(preset);
                Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == preset, "Switch from Off to " + preset);
            }
        }
        using(var fixture = new Fixture()) {
            fixture.Vm.ApplyPreset("Off");
            fixture.Vm.EnterManualMode();
            Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == "Manual", "Manual leaves Off");
            fixture.Vm.ApplyPreset("Off");
            fixture.Fans.IgnoreRestore = true;
            fixture.Vm.ApplyPreset("High");
            Assert(fixture.Vm.ActivePreset == "Recovery", "New mode cannot hide failed release");
            fixture.Fans.IgnoreRestore = false;
        }
    }

    private static void PollProtection() {
        using(var fixture = new Fixture()) {
            fixture.Poll();
            Assert(fixture.Vm.CpuFanRpm == 1800, "Initial RPM published");
            fixture.Vm.ApplyPreset("Off");
            fixture.Fans.Telemetry.Rpm = 0;
            fixture.Fans.Telemetry.Percent = 0;
            fixture.Poll();
            Assert(fixture.Vm.CpuFanRpm == 0 && fixture.Vm.GpuFanPct == 0, "Stopped readings published");
            fixture.Cpu.Value = 60;
            fixture.Poll();
            Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == "Auto", "Poll restores on high CPU");
            Assert(fixture.Vm.StatusText.Contains("60"), "Protection reason remains visible");
            fixture.Cpu.Value = 45;
            fixture.Poll();
            Assert(!fixture.Fans.Off, "Cooling does not re-enter Off");
            fixture.Vm.ApplyPreset("Off");
            fixture.Cpu.Fail = true;
            fixture.Poll();
            Assert(!fixture.Fans.Off, "Failed sensor triggers protection despite cached low value");
            fixture.Cpu.Fail = false;
            fixture.Vm.ApplyPreset("Off");
            fixture.InvokePoll(); // queue an Off UI publication, then supersede it
            fixture.Vm.ApplyPreset("High");
            DrainDispatcher();
            Assert(fixture.Vm.ActivePreset == "High", "Old poll cannot overwrite a newer mode");
            fixture.Fans.Telemetry.Rpm = 1800;
            fixture.Poll();
            fixture.Fans.Telemetry.Valid = false;
            fixture.Poll();
            Assert(fixture.Vm.CpuFanRpm == 1800, "Failed telemetry preserves display, not fake zero");
        }
    }

    private static void SensorFallback() {
        string name = Config.GuiTempSensorCpu;
        Config.GuiTempSensorCpu = "missing-sensor";
        try {
            using(var fixture = new Fixture()) {
                fixture.Vm.ApplyPreset("Off");
                Assert(fixture.Fans.Off, "Named fallback is actually refreshed");
                fixture.Cpu.Fail = true;
                fixture.Poll();
                Assert(!fixture.Fans.Off, "Failed fallback does not reuse cache");
            }
        } finally { Config.GuiTempSensorCpu = name; }
    }

    private static void Lifecycle() {
        using(var fixture = new Fixture()) {
            fixture.Vm.ApplyPreset("Off");
            fixture.Vm.HandlePowerChange(true);
            Assert(!fixture.Fans.Off, "Suspend restores before returning");
            fixture.Vm.ApplyPreset("Off");
            Assert(!fixture.Fans.Off, "Cannot stop while suspended");
            fixture.Vm.HandlePowerChange(false);
            DrainDispatcher();
            Assert(fixture.Vm.ActivePreset == "Auto", "Rapid resume discards stale Off UI");
            fixture.Vm.ApplyPreset("Off");
            fixture.Vm.Dispose();
            Assert(!fixture.Fans.Off, "Dispose restores stop switch");
            fixture.Vm.Dispose();
            fixture.Vm.ApplyPreset("Off");
            Assert(!fixture.Fans.Off, "Disposed VM rejects new commands");
        }
        using(var fixture = new Fixture())
            Assert(fixture.Vm.ActivePreset == "Auto" && !fixture.Fans.Off, "New session defaults to Auto");
    }

    private static void CurveTransitions() {
        Config.LocaleInit();
        const string name = "FanOffTest";
        Config.FanProgram[name] = new FanProgramData(name, BiosData.FanMode.Default,
            BiosData.GpuPowerLevel.Medium, new SortedDictionary<byte, byte[]> {
                [0] = new byte[] { 20, 20 }, [60] = new byte[] { 40, 40 }
            });
        try {
            using(var fixture = new Fixture()) {
                fixture.Vm.ApplyPreset("Off");
                fixture.Vm.RunCurve(name);
                Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == "Curve"
                    && fixture.Fans.Calls.Contains("Levels:20,20"), "Curve really runs after Off");
                fixture.Vm.ApplyPreset("Off");
                Assert(fixture.Fans.Off && !fixture.Vm.IsCurveRunning, "Entering Off terminates curve");
                fixture.Fans.Calls.Clear();
                fixture.Poll();
                Assert(!fixture.Fans.Calls.Any(c => c.StartsWith("Levels:")),
                    "Stopped curve cannot overwrite Off on the next poll");
            }
        } finally {
            Config.FanProgram.Remove(name);
        }
    }

    private static void ConcurrentDisposal() {
        using(var fixture = new Fixture())
        using(var entered = new ManualResetEventSlim())
        using(var release = new ManualResetEventSlim()) {
            fixture.Vm.ApplyPreset("Off");
            int blocked = 0;
            fixture.Cpu.BeforeRead = () => {
                if(Interlocked.Exchange(ref blocked, 1) == 0) {
                    entered.Set();
                    if(!release.Wait(5000)) throw new TimeoutException("Test read was never released");
                }
            };
            Task poll = Task.Run(() => fixture.InvokePoll());
            Assert(entered.Wait(5000), "Background poll entered read");
            Task dispose = Task.Run(() => fixture.Vm.Dispose());
            release.Set();
            Assert(Task.WaitAll(new[] { poll, dispose }, 5000), "Poll and Dispose must not await the UI thread");
            DrainDispatcher();
            Assert(!fixture.Fans.Off, "In-flight tick cannot reapply Off after disposal");
        }
    }

    private static void ExitRecovery() {
        using(var fixture = new Fixture()) {
            fixture.Vm.ApplyPreset("Off");
            fixture.Fans.IgnoreRestore = true;
            fixture.Fans.Calls.Clear();
            Assert(!fixture.Vm.TryPrepareExit(), "Unconfirmed recovery cancels normal exit");
            Assert(fixture.Fans.Calls.Count(c => c == "Off:False") == 3, "Exit uses bounded recovery retries");
            Assert(fixture.Vm.ActivePreset == "Recovery", "Pending recovery stays visible");
            fixture.Fans.IgnoreRestore = false;
            fixture.Poll();
            Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == "Auto", "Cancelled exit preserves monitoring");
            Assert(fixture.Vm.TryPrepareExit(), "Exit allowed after recovery confirmed");
        }
    }

    private static void ManualConfiguration() {
        bool manual = false;
        var fans = new FakeFans();
        var control = new FanOffController(fans, action => { action(); return true; }, () => manual);
        control.Enter(Cool);
        manual = true;
        fans.Calls.Clear();
        Assert(control.RestoreAuto(), "Restore after config change");
        Assert(fans.Calls.IndexOf("Manual:False") > fans.Calls.IndexOf("Levels:255,255"),
            "Newly enabled manual control must be released after levels");
    }

    private static void StrictLevelErrors() {
        var oldBios = Hw.Bios;
        bool useEc = Config.FanLevelUseEc, manual = Config.FanLevelNeedManual;
        Hw.Bios = (IBiosCtl) new FailingBiosProxy().GetTransparentProxy();
        Config.FanLevelUseEc = false;
        Config.FanLevelNeedManual = false;
        try {
            var fans = new FanArray(new IFan[] { new FakeFan(), new FakeFan() },
                new Component(), new Component(), new Component(), new Component());
            fans.SetLevels(new byte[] { 20, 20 }); // legacy compatibility still ignores the error
            Assert(!fans.TrySetLevels(new byte[] { 255, 255 }), "Safety path returns BIOS failure");
        } finally {
            Hw.Bios = oldBios;
            Config.FanLevelUseEc = useEc;
            Config.FanLevelNeedManual = manual;
        }
        var fake = new FakeFans();
        var control = Controller(fake);
        control.Enter(Cool);
        fake.FailCall = "Levels:255,255";
        fake.Calls.Clear();
        Assert(!control.RestoreAuto() && control.RecoveryPending, "Failed level release is not success");
        Assert(fake.Calls.Contains("Countdown:0"), "Other recovery steps still run");
        fake.FailCall = null;
        control.Check(Cool);
        Assert(!control.NeedsMonitoring, "Retry completes strict level recovery");
    }

    private static void AppliedLevelErrors() {
        foreach(var levels in new[] { new byte[] { 20, 22 }, new byte[] { 0, 0 }, new byte[] { 255, 255 } }) {
            using(var hardware = new LevelHardware()) {
                Assert(hardware.Fans.TrySetLevels(levels, out string diagnostic),
                    "BIOS error must not reject two freshly applied EC targets");
                Assert(hardware.CpuTarget.ReadCount == 1 && hardware.GpuTarget.ReadCount == 1,
                    "Both targets must be freshly read, including zero and automatic sentinels");
                Assert(diagnostic.Contains("Simulated BIOS status 6"), "Keep the original BIOS diagnostic");
            }
        }
    }

    private static void TargetReadbackFailures() {
        foreach(string failure in new[] { "CpuMismatch", "GpuMismatch", "CpuRead", "GpuRead" }) {
            using(var hardware = new LevelHardware()) {
                hardware.ApplyTargets = false;
                hardware.CpuTarget.Value = 20;
                hardware.GpuTarget.Value = 22;
                hardware.CpuTarget.Update();
                hardware.GpuTarget.Update();
                if(failure == "CpuMismatch") hardware.CpuTarget.Value = 19;
                if(failure == "GpuMismatch") hardware.GpuTarget.Value = 21;
                if(failure == "CpuRead") hardware.CpuTarget.Fail = true;
                if(failure == "GpuRead") hardware.GpuTarget.Fail = true;
                Assert(!hardware.Fans.TrySetLevels(new byte[] { 20, 22 }, out string diagnostic),
                    "Cached matching targets and positive RPM cannot hide " + failure);
                Assert(hardware.CpuTarget.ReadCount == 2 && hardware.GpuTarget.ReadCount == 2,
                    "Even a failed first target must not skip the second diagnostic read");
                Assert(diagnostic.Contains("BIOS 0x2E") && diagnostic.Contains("Simulated BIOS status 6")
                    && diagnostic.Contains("风扇1目标 14") && diagnostic.Contains("风扇2目标 16"),
                    "Report original backend error and both target readbacks");
            }
        }
        using(var hardware = new LevelHardware()) {
            hardware.ApplyGpu = false;
            Assert(!hardware.Fans.TrySetLevels(new byte[] { 20, 22 }), "Partial BIOS application is not success");
            hardware.ApplyTargets = false;
            hardware.CpuTarget.Value = 255;
            hardware.GpuTarget.Value = 22;
            Assert(!hardware.Fans.TrySetLevels(new byte[] { 255, 255 }), "Mixed sentinel/numeric targets are not automatic");
        }
    }

    private static void TargetConfirmationBoundaries() {
        using(var hardware = new LevelHardware()) {
            foreach(var invalid in new byte[][] { null, new byte[0], new byte[] { 20 }, new byte[] { 20, 22, 24 } })
                Assert(!hardware.Fans.TrySetLevels(invalid), "Reject incomplete or excess targets before writing");
            Assert(hardware.Calls.Count == 0, "Invalid targets must not write hardware");
            Config.FanLevelNeedManual = true;
            hardware.Manual.FailWrite = true;
            hardware.CpuTarget.Value = 20;
            hardware.GpuTarget.Value = 22;
            Assert(!hardware.Fans.TrySetLevels(new byte[] { 20, 22 }, out string diagnostic)
                && diagnostic.Contains("手动控制准备失败"), "Manual setup failure is never waived by matching targets");
            Assert(hardware.Calls.Count == 0 && hardware.CpuTarget.ReadCount == 0,
                "Manual setup failure never attempts BIOS write or target confirmation");
            Config.FanLevelNeedManual = false;
            Config.FanLevelUseEc = true;
            hardware.CpuTarget.FailWrite = true;
            Assert(!hardware.Fans.TrySetLevels(new byte[] { 20, 22 }), "EC write failure cannot use BIOS compatibility fallback");
            Assert(hardware.Calls.Count == 0 && hardware.CpuTarget.ReadCount == 0,
                "EC failure must not change backend or use target readback");
        }
        using(var hardware = new LevelHardware()) {
            hardware.ThrowBiosError = false;
            hardware.CpuTarget.Fail = hardware.GpuTarget.Fail = true;
            Assert(hardware.Fans.TrySetLevels(new byte[] { 20, 22 }), "Acknowledged BIOS command needs no compatibility fallback");
            Assert(hardware.CpuTarget.ReadCount == 0 && hardware.GpuTarget.ReadCount == 0,
                "Successful BIOS commands do not acquire a new EC read dependency");
        }
        using(var hardware = new LevelHardware()) {
            hardware.UnexpectedError = true;
            Assert(!hardware.Fans.TrySetLevels(new byte[] { 20, 22 }), "Unexpected exceptions remain failures even if targets match");
            Assert(hardware.CpuTarget.ReadCount == 0, "Only BIOS status exceptions enable compatibility readback");
        }
        using(var hardware = new LevelHardware()) {
            var controller = new FanOffController(hardware.Fans, action => { action(); return true; }, () => false);
            hardware.Countdown.FailWrite = true;
            Assert(!controller.RestoreAuto() && controller.RecoveryPending,
                "Confirmed FF FF does not waive other automatic recovery failures");
            hardware.Countdown.FailWrite = false;
            Assert(controller.RestoreAuto() && !controller.NeedsMonitoring,
                "Matching sentinels plus the complete recovery sequence can confirm Auto");
        }
    }

    private static void VerifiedBiosTakeover() {
        const string curve = "VerifiedBiosTakeover";
        Config.LocaleInit();
        Config.FanProgram[curve] = new FanProgramData(curve, BiosData.FanMode.Default,
            BiosData.GpuPowerLevel.Medium, new SortedDictionary<byte, byte[]> { [0] = new byte[] { 20, 22 } });
        try {
            foreach(string preset in new[] { "Max", "High", "Mid", "Silent", "Manual", "Curve" }) {
                using(var hardware = new LevelHardware())
                using(var fixture = new Fixture(hardware.Fans)) {
                    hardware.ApplyAutomatic = false;
                    fixture.Vm.ApplyPreset("Off");
                    Assert(fixture.Vm.ActivePreset == "Recovery", "Rejected FF FF creates pending recovery");
                    fixture.InvokePoll(); // Leave an old Recovery publication queued.
                    hardware.Calls.Clear();
                    if(preset == "Manual") fixture.Vm.EnterManualMode();
                    else if(preset == "Curve") fixture.Vm.RunCurve(curve);
                    else fixture.Vm.ApplyPreset(preset);
                    Assert(fixture.Vm.ActivePreset == preset, "Fresh targets unblock BIOS-error takeover: " + preset);
                    Assert(!hardware.Calls.Contains("Levels:255,255"), "Successful takeover does not fall back to Auto");
                    DrainDispatcher();
                    Assert(fixture.Vm.ActivePreset == preset, "Queued recovery cannot overwrite verified takeover");
                    hardware.Calls.Clear();
                    for(int i = 0; i < 3; i++) fixture.Poll();
                    Assert(fixture.Vm.ActivePreset == preset && !hardware.Calls.Contains("Levels:255,255"),
                        "Verified takeover cancels recovery retries: " + preset);
                    hardware.ApplyAutomatic = true;
                    if(preset == "Curve") fixture.Vm.StopCurve();
                }
            }
            using(var hardware = new LevelHardware())
            using(var fixture = new Fixture(hardware.Fans)) {
                fixture.Vm.ApplyPreset("Off");
                Assert(fixture.Vm.ActivePreset == "Off" && hardware.Fans.GetOff(), "Real stop path starts confirmed");
                hardware.ApplyAutomatic = false;
                hardware.CpuTarget.Value = 20;
                hardware.GpuTarget.Value = 22;
                fixture.Cpu.Value = 60;
                fixture.Poll();
                Assert(fixture.Vm.ActivePreset == "Recovery", "Unconfirmed automatic levels retain protection after a real stop");
                fixture.Vm.ApplyPreset("High");
                Assert(fixture.Vm.ActivePreset == "High" && !hardware.Fans.GetOff(),
                    "Verified BIOS-error target can also take over after an actual stop");
                hardware.Calls.Clear();
                fixture.Poll();
                Assert(!hardware.Calls.Contains("Levels:255,255"), "Real-stop takeover also cancels automatic retries");
            }
            using(var hardware = new LevelHardware())
            using(var fixture = new Fixture(hardware.Fans)) {
                hardware.ApplyTargets = false;
                fixture.Vm.ApplyPreset("Off");
                fixture.Vm.ApplyPreset("High");
                Assert(fixture.Vm.ActivePreset == "Recovery" && fixture.Vm.StatusText.Contains("BIOS 0x2E")
                    && fixture.Vm.StatusText.Contains("Simulated BIOS status 6")
                    && fixture.Vm.StatusText.Contains("读回 00"), "Genuine rejection retains recovery with useful diagnostics");
                fixture.Poll();
                Assert(!fixture.Vm.TryPrepareExit(), "Unconfirmed target and Auto cannot allow unattended exit");
                hardware.ApplyTargets = true;
                fixture.Vm.ApplyPreset("Auto");
                Assert(fixture.Vm.ActivePreset == "Auto", "Fresh sentinel confirmation unblocks automatic recovery too");
            }
        } finally { Config.FanProgram.Remove(curve); }
    }

    private static void PreparationTakeover() {
        foreach(string preset in new[] { "Max", "High", "Mid", "Silent", "Manual" }) {
            using(var fixture = new Fixture()) {
                fixture.Fans.FailCall = "Levels:255,255";
                fixture.Vm.ApplyPreset("Off");
                Assert(!fixture.Fans.Calls.Contains("Off:True"), "Failed preparation must not send stop");
                Assert(fixture.Vm.StatusText.Contains("未发送停扇指令")
                    && fixture.Vm.StatusText.Contains("FF FF"), "Show failed preparation step");
                Assert(fixture.Vm.ActivePreset == "Recovery", "Partial preparation is not Auto success");
                fixture.Poll();
                Assert(fixture.Vm.StatusText.Contains("未发送停扇指令"), "Retry retains preparation context");
                fixture.Fans.Calls.Clear();
                if(preset == "Manual") fixture.Vm.EnterManualMode();
                else fixture.Vm.ApplyPreset(preset);
                Assert(fixture.Vm.ActivePreset == preset && fixture.Vm.IsManualMode,
                    "Explicit cooling takeover unblocks " + preset);
                Assert(!fixture.Fans.Calls.Contains("Levels:255,255"), "Takeover does not retry automatic levels");
                fixture.Fans.Calls.Clear();
                fixture.Poll();
                Assert(fixture.Vm.ActivePreset == preset && !fixture.Fans.Calls.Contains("Levels:255,255"),
                    "Successful takeover cancels recovery retries");
            }
        }
    }

    private static void TakeoverFailures() {
        foreach(string failure in new[] { "Read", "StillOff", "StrictFalse", "StrictThrow", "Countdown" }) {
            using(var fixture = new Fixture()) {
                fixture.Fans.FailCall = "Levels:255,255";
                fixture.Vm.ApplyPreset("Off");
                fixture.Fans.Calls.Clear();
                if(failure == "Read") fixture.Fans.ReadValid = false;
                if(failure == "StillOff") {
                    fixture.Fans.Off = true;
                    fixture.Fans.IgnoreRestore = true;
                }
                if(failure == "StrictFalse" || failure == "StrictThrow") {
                    fixture.Fans.StrictFailure = true;
                    fixture.Fans.StrictThrows = failure == "StrictThrow";
                }
                if(failure == "Countdown") fixture.Fans.SecondFailCall = "Countdown:" + Config.FanCountdownExtendInterval;
                fixture.Vm.ApplyPreset("High");
                Assert(fixture.Vm.ActivePreset == "Recovery" && !fixture.Vm.IsManualMode,
                    "Failed takeover must not publish target: " + failure);
                if(failure == "Read" || failure == "StillOff")
                    Assert(!fixture.Fans.Calls.Any(c => c.StartsWith("Levels:") && c != "Levels:255,255"),
                        "Fresh switch failure prevents target write despite positive RPM");
                Assert(fixture.Fans.Calls.Contains("Countdown:0"), "Failed takeover keeps recovery attempts");
                Assert(!fixture.Vm.TryPrepareExit(), "Failed takeover cannot silently discard recovery");
                fixture.Fans.ReadValid = true;
                fixture.Fans.IgnoreRestore = false;
                fixture.Fans.StrictFailure = false;
                fixture.Fans.SecondFailCall = null;
                fixture.Fans.FailCall = null;
            }
        }
    }

    private static void AutomaticRecoveryFailures() {
        using(var fixture = new Fixture()) {
            fixture.Vm.ApplyPreset("Off");
            fixture.Fans.FailCall = "Levels:255,255";
            fixture.Cpu.Value = 60;
            fixture.Poll();
            Assert(!fixture.Fans.Off && fixture.Vm.ActivePreset == "Recovery", "Released SFAN is not full Auto confirmation");
            Assert(!fixture.Vm.StatusText.Contains("未发送停扇指令"), "Real stop must not claim no stop was sent");
            fixture.Fans.Calls.Clear();
            Assert(!fixture.Vm.TryPrepareExit(), "Ancillary error still blocks unattended exit");
            Assert(fixture.Fans.Calls.Count(c => c == "Off:False") == 3, "Exit keeps bounded retries");
            fixture.Vm.HandlePowerChange(true);
            fixture.Vm.HandlePowerChange(false);
            DrainDispatcher();
            Assert(fixture.Vm.ActivePreset == "Recovery", "Power events retain full recovery requirement");
            fixture.Vm.ApplyPreset("High");
            Assert(fixture.Vm.ActivePreset == "High", "Explicit strict cooling can replace failed Auto after a real stop");
        }
    }

    private static void CurveTakeoverFailures() {
        const string name = "TakeoverTest";
        Config.LocaleInit();
        Config.FanProgram[name] = new FanProgramData(name, BiosData.FanMode.Default,
            BiosData.GpuPowerLevel.Medium, new SortedDictionary<byte, byte[]> { [0] = new byte[] { 20, 20 } });
        try {
            foreach(string failure in new[] { "None", "StrictFalse", "PostRead" }) {
                using(var fixture = new Fixture()) {
                    fixture.Fans.FailCall = "Levels:255,255";
                    fixture.Vm.ApplyPreset("Off");
                    fixture.Fans.StrictFailure = failure == "StrictFalse";
                    if(failure == "PostRead") fixture.Fans.AfterStrictWrite = () => fixture.Fans.ReadValid = false;
                    fixture.Vm.RunCurve(name);
                    if(failure == "None") {
                        Assert(fixture.Vm.ActivePreset == "Curve", "Strict curve startup bypasses automatic level error");
                    } else {
                        Assert(fixture.Vm.ActivePreset == "Recovery", "Failed curve takeover retains recovery");
                        fixture.Fans.FailCall = null;
                        fixture.Fans.StrictFailure = false;
                        fixture.Fans.AfterStrictWrite = null;
                        fixture.Fans.ReadValid = true;
                        fixture.Poll();
                        fixture.Fans.Calls.Clear();
                        fixture.Poll();
                        Assert(!fixture.Fans.Calls.Any(c => c == "Levels:20,20"), "Failed curve cannot restart after recovery");
                        Assert(!fixture.Vm.IsCurveRunning, "Failed curve not displayed as active");
                    }
                    fixture.Fans.FailCall = null;
                    fixture.Vm.StopCurve();
                }
            }
        } finally { Config.FanProgram.Remove(name); }
    }

    private sealed class FakeMsr {
        public readonly List<uint> Calls = new List<uint>();
        public uint Target = 100u << 16;
        public uint Status = 0x80000000u | (53u << 16);
        public uint FailRegister;
        public bool Throw;
        public int Temperature { set => Status = 0x80000000u | ((uint) (100 - value) << 16); }
        public bool Read(uint index, out uint eax, out uint edx) {
            Calls.Add(index);
            if(Throw) throw new TimeoutException("Simulated MSR failure");
            if(index != 0x1A2 && index != 0x1B1) throw new InvalidOperationException("Unexpected MSR");
            eax = index == 0x1A2 ? Target : Status;
            edx = 0;
            return index != FailRegister;
        }
    }

    private sealed class LevelHardware : IDisposable {
        public readonly Component CpuTarget = new Component();
        public readonly Component GpuTarget = new Component();
        public readonly Component Countdown = new Component();
        public readonly Component Manual = new Component();
        public readonly Component Mode = new Component();
        public readonly Component Switch = new Component();
        public readonly List<string> Calls = new List<string>();
        public readonly FanArray Fans;
        public bool ApplyTargets = true, ApplyAutomatic = true, ApplyGpu = true;
        public bool ThrowBiosError = true, UnexpectedError;
        private readonly IBiosCtl _oldBios = Hw.Bios;
        private readonly bool _useEc = Config.FanLevelUseEc, _manual = Config.FanLevelNeedManual;

        public LevelHardware() {
            Config.FanLevelUseEc = false;
            Config.FanLevelNeedManual = false;
            Fans = new FanArray(new IFan[] {
                new Fan(BiosData.FanType.Cpu, CpuTarget, new Component(), new Component(), new Component { Value = 1800 }),
                new Fan(BiosData.FanType.Gpu, GpuTarget, new Component(), new Component(), new Component { Value = 1800 })
            }, Countdown, Manual, Mode, Switch);
            Hw.Bios = (IBiosCtl) new LevelBiosProxy(this).GetTransparentProxy();
        }

        public void Dispose() {
            Hw.Bios = _oldBios;
            Config.FanLevelUseEc = _useEc;
            Config.FanLevelNeedManual = _manual;
        }
    }

    private sealed class LevelBiosProxy : RealProxy {
        private readonly LevelHardware _hardware;
        public LevelBiosProxy(LevelHardware hardware) : base(typeof(IBiosCtl)) { _hardware = hardware; }
        public override IMessage Invoke(IMessage message) {
            var call = (IMethodCallMessage) message;
            if(call.MethodName == "SetFanLevel") {
                var levels = (byte[]) call.Args[0];
                _hardware.Calls.Add("Levels:" + string.Join(",", levels));
                if(_hardware.ApplyTargets && (_hardware.ApplyAutomatic || levels[0] != 255 || levels[1] != 255)) {
                    _hardware.CpuTarget.Value = levels[0];
                    if(_hardware.ApplyGpu) _hardware.GpuTarget.Value = levels[1];
                }
                if(_hardware.UnexpectedError)
                    return new ReturnMessage(new TimeoutException("Simulated transport timeout"), call);
                if(_hardware.ThrowBiosError)
                    return new ReturnMessage(new BiosException("Simulated BIOS status 6"), call);
            } else if(call.MethodName == "SetMaxFan") {
                _hardware.Calls.Add("Max:" + call.Args[0]);
            } else if(call.MethodName == "SetFanMode") {
                _hardware.Mode.Value = (int) (BiosData.FanMode) call.Args[0];
            } else {
                return new ReturnMessage(new InvalidOperationException("Unexpected BIOS call: " + call.MethodName), call);
            }
            return new ReturnMessage(null, null, 0, call.LogicalCallContext, call);
        }
    }

    private sealed class FailingBiosProxy : RealProxy {
        public FailingBiosProxy() : base(typeof(IBiosCtl)) { }
        public override IMessage Invoke(IMessage message) =>
            new ReturnMessage(new BiosException("Simulated BIOS error"), (IMethodCallMessage) message);
    }

    // Fake the platform settings used by the real curve engine, never WMI.
    private sealed class SettingsProxy : RealProxy {
        public SettingsProxy() : base(typeof(ISettings)) { }
        public override IMessage Invoke(IMessage message) {
            var call = (IMethodCallMessage) message;
            if(call.MethodName != "GetGpuPower" && call.MethodName != "SetGpuPower"
                && call.MethodName != "GetGpuCustomTgp" && call.MethodName != "GetGpuPpab")
                return new ReturnMessage(new InvalidOperationException("Unexpected fake settings call: " + call.MethodName), call);
            var type = ((MethodInfo) call.MethodBase).ReturnType;
            object result = type == typeof(void) ? null : Activator.CreateInstance(type);
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        }
    }

    private static void DrainDispatcher() {
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
    }

    private sealed class Fixture : IDisposable {
        public readonly FakeFans Fans = new FakeFans();
        public readonly Component Cpu = new Component { Value = 45 };
        public readonly Component Gpu = new Component { Value = 43 };
        public readonly HardwareViewModel Vm;
        private readonly IEmbeddedController _oldEc;

        public Fixture(IFanArray fans = null, IPlatformReadComponent cpuPackage = null) {
            _oldEc = Hw.Ec;
            Hw.Ec = new ProtocolEc { Mode = "Ready" };
            Cpu.SetName(Config.GuiTempSensorCpuDefault);
            Gpu.SetName(Config.GuiTempSensorGpuDefault);
            // Bypass Platform's hardware-discovery constructor, then inject every
            // backend used by the ViewModel before it can poll.
            var platform = (Platform) FormatterServices.GetUninitializedObject(typeof(Platform));
            SetProperty(platform, "Fans", fans ?? Fans);
            SetProperty(platform, "CpuPackageTemperature", cpuPackage);
            SetProperty(platform, "System", (ISettings) new SettingsProxy().GetTransparentProxy());
            SetProperty(platform, "Temperature", new IPlatformReadComponent[] { Cpu, Gpu });
            SetProperty(platform, "TemperatureName", new[] { Config.GuiTempSensorCpuDefault, Config.GuiTempSensorGpuDefault });
            SetProperty(platform, "TemperatureUse", new[] { true, true });
            Vm = new HardwareViewModel(platform);
            ((System.Timers.Timer) typeof(HardwareViewModel).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(Vm)).Stop();
            DrainDispatcher();
        }

        public void InvokePoll() => typeof(HardwareViewModel)
            .GetMethod("PollHardware", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Vm, null);
        public void Poll() { InvokePoll(); DrainDispatcher(); }
        public void Dispose() { Vm.Dispose(); DrainDispatcher(); Hw.Ec = _oldEc; }
        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    }

    private sealed class Component : PlatformComponentAbstract, IPlatformReadWriteComponent {
        public int Value;
        public bool Fail, FailWrite;
        public int ReadCount;
        public Action BeforeRead;
        public Component() : base(PlatformData.AccessType.Read | PlatformData.AccessType.Write) {
            Constraint = 10000;
        }
        protected override int Read() {
            ReadCount++;
            BeforeRead?.Invoke();
            if(Fail) throw new TimeoutException();
            return Value;
        }
        protected override void Write(int value) {
            if(FailWrite) throw new TimeoutException("Simulated component write failure");
            Value = value;
        }
    }

    private sealed class FakeFan : IFan {
        public int Rpm = 1800, Percent = 36;
        public bool Valid = true;
        public BiosData.FanType GetFanType() => BiosData.FanType.Cpu;
        public int GetLevel() => 20;
        public int GetRate() => Percent;
        public int GetSpeed() => Rpm;
        public bool TryGetRate(out int rate) { rate = Percent; return Valid; }
        public bool TryGetSpeed(out int speed) { speed = Rpm; return Valid; }
        public bool TryGetTargetLevel(out int level) { level = 20; return Valid; }
        public void SetLevel(int level) { }
        public void SetRate(int rate) { }
    }

    private sealed class FakeFans : IFanArray {
        public readonly List<string> Calls = new List<string>();
        public readonly FakeFan Telemetry = new FakeFan();
        public bool Off, IgnoreStop, IgnoreRestore;
        public bool ReadValid = true;
        public string FailCall, SecondFailCall;
        public bool StrictFailure, StrictThrows;
        public Action AfterStrictWrite;
        public IFan[] Fan => new IFan[] { Telemetry, Telemetry };
        private void Record(string call) {
            Calls.Add(call);
            if(call == FailCall || call == SecondFailCall) throw new TimeoutException();
        }
        public void SetOff(bool flag) {
            Record("Off:" + flag);
            if(!(flag ? IgnoreStop : IgnoreRestore)) Off = flag;
        }
        public bool GetOff() => Off;
        public bool TryGetOff(out bool flag) { flag = Off; return ReadValid; }
        public int GetCountdown() => 0;
        public void SetCountdown(int value) => Record("Countdown:" + value);
        public byte[] GetLevels() => new byte[] { 0xFF, 0xFF };
        public void SetLevels(byte[] levels) => Record("Levels:" + string.Join(",", levels));
        public bool TrySetLevels(byte[] levels) {
            if(StrictFailure) {
                Calls.Add("StrictLevelsFailed");
                if(StrictThrows) throw new TimeoutException("Strict levels exception");
                return false;
            }
            try {
                SetLevels(levels);
                AfterStrictWrite?.Invoke();
                return true;
            } catch { return false; }
        }
        public bool TrySetLevels(byte[] levels, out string diagnostic) {
            bool success = TrySetLevels(levels);
            diagnostic = success ? "" : "Simulated level failure";
            return success;
        }
        public bool GetMax() => false;
        public void SetMax(bool flag) => Record("Max:" + flag);
        public bool GetManual() => false;
        public void SetManual(bool flag) => Record("Manual:" + flag);
        public BiosData.FanMode GetMode() => BiosData.FanMode.Default;
        public void SetMode(BiosData.FanMode mode) => Record("Mode:" + mode);
    }

    private sealed class ProtocolEc : EmbeddedControllerAbstract {
        public string Mode;
        public byte Data = 0x34;
        private byte _register;
        public override void Initialize() { IsInitialized = true; }
        public override void Close() { }
        public override bool Request(int timeout) {
            if(Mode == "ThrowRequest") throw new TimeoutException("Simulated EC request failure");
            return Mode != "Locked";
        }
        public override void Release() { }
        protected override byte ReadIoPort(Port port) {
            if(port == Port.Data) return Data;
            if(Mode == "Busy") return (byte) Status.InFull;
            if(Mode == "NoData" || (Mode == "Partial" && _register == 0x11)) return 0;
            return (byte) Status.OutFull;
        }
        protected override void WriteIoPort(Port port, byte value) {
            if(port == Port.Data) _register = value;
        }
    }
}
