  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Temporary fan stop with fail-closed temperature protection
     //  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

namespace OmenMon.AppWpf {

    internal readonly struct TemperatureSample {
        // Conservative plausibility bound for laptop CPU/GPU stop protection.
        public const int Minimum = 10;
        public readonly int Value;
        public readonly bool WasRead;
        public readonly string Source;
        public bool IsValid => WasRead && Value >= Minimum && Value <= Config.MaxBelievableTemperature;

        public TemperatureSample(int value, bool wasRead = true, string source = null) {
            Value = value;
            WasRead = wasRead;
            Source = source;
        }

        public static TemperatureSample Read(IPlatformReadComponent sensor) {
            string source = null;
            try {
                source = sensor?.GetName();
                int value = 0;
                bool read = sensor != null && sensor.TryRead(out value);
                return new TemperatureSample(value, read, source);
            } catch {
                return new TemperatureSample(0, false, source);
            }
        }

        public string Describe(string label) {
            string name = label + (string.IsNullOrEmpty(Source) ? "" : "（" + Source + "）");
            return WasRead ? name + "=" + Value + "°C" : name + "温度读取失败或无效";
        }
    }

    internal readonly struct FanOffTemperatures {
        public readonly TemperatureSample Cpu;
        public readonly TemperatureSample Gpu;

        public FanOffTemperatures(int cpu, int gpu)
            : this(new TemperatureSample(cpu), new TemperatureSample(gpu)) { }

        private FanOffTemperatures(TemperatureSample cpu, TemperatureSample gpu) {
            Cpu = cpu;
            Gpu = gpu;
        }

        public bool IsValid => Cpu.IsValid && Gpu.IsValid;
        public bool CanStop => IsValid
            && Cpu.Value < FanOffController.TemperatureLimit
            && Gpu.Value < FanOffController.TemperatureLimit;

        public string InvalidMessage {
            get {
                var failures = new List<string>();
                if(!Cpu.IsValid) failures.Add(Cpu.Describe("CPU"));
                if(!Gpu.IsValid) failures.Add(Gpu.Describe("GPU"));
                return failures.Count == 0 ? "" : "温度读取异常：" + string.Join("；", failures);
            }
        }

        public string StopFailure => IsValid
            ? "温度须低于 60°C：" + Cpu.Describe("CPU") + "；" + Gpu.Describe("GPU")
            : InvalidMessage;

        public static FanOffTemperatures Read(
            IPlatformReadComponent cpu, IPlatformReadComponent gpu) {
            return new FanOffTemperatures(TemperatureSample.Read(cpu), TemperatureSample.Read(gpu));
        }
    }

    // The caller serializes this controller with other fan writes. Batching is
    // injected so all state transitions can be tested without opening a driver.
    internal sealed class FanOffController {
        public const int TemperatureLimit = 60;

        private readonly IFanArray _fans;
        private readonly Func<Action, bool> _batch;
        private readonly Func<bool> _manual;
        private string _reason;
        private bool _stopRequested;

        public bool IsOff { get; private set; }
        public bool RecoveryPending { get; private set; }
        public bool NeedsMonitoring => IsOff || RecoveryPending;
        public string Message { get; private set; } = "";

        public FanOffController(IFanArray fans, Func<Action, bool> batch, Func<bool> manual) {
            _fans = fans;
            _batch = batch;
            _manual = manual;
        }

        public bool Enter(Func<FanOffTemperatures> readTemperatures) {
            // A second click must not reassert Off against a firmware override.
            if(IsOff) {
                Check(readTemperatures);
                return IsOff;
            }
            if(!RestoreAuto(_stopRequested ? null : "停扇前准备失败，未发送停扇指令")) return false;

            string reason = "停扇请求未能确认";
            string cpuSource = null;
            bool eligible = false;
            bool success = TryBatch(() => {
                FanOffTemperatures temperature = readTemperatures();
                if(!temperature.CanStop) {
                    reason = temperature.StopFailure + "，未关闭风扇";
                    return;
                }
                eligible = true;
                // Mark recovery necessary BEFORE the write: a failed command
                // could still have reached the controller on some hardware.
                RecoveryPending = true;
                _stopRequested = true;
                _fans.SetOff(true);
                IsOff = _fans.TryGetOff(out bool off) && off;
                // Some firmware changes temperature telemetry when SFAN is set.
                // Never publish success using only the pre-stop reading.
                temperature = readTemperatures();
                cpuSource = temperature.Cpu.Source;
                if(!temperature.CanStop) {
                    reason = "停扇后保护：" + temperature.StopFailure;
                    IsOff = false;
                }
            });

            if(success && eligible && IsOff) {
                RecoveryPending = false;
                _reason = null;
                Message = "关闭指令已确认 · 以实际 RPM 为准 · 60°C 自动恢复"
                    + (string.IsNullOrEmpty(cpuSource) ? "" : " · CPU 温度源：" + cpuSource);
                return true;
            }

            RestoreAuto(reason);
            return false;
        }

        public void Check(Func<FanOffTemperatures> readTemperatures) {
            if(RecoveryPending) {
                RestoreAuto(_reason);
                return;
            }
            if(!IsOff) return;

            string reason = "停扇保护：硬件读取失败";
            bool safe = false;
            bool success = TryBatch(() => {
                FanOffTemperatures temperature = readTemperatures();
                if(!temperature.CanStop) {
                    reason = "停扇保护：" + temperature.StopFailure;
                    return;
                }
                if(!_fans.TryGetOff(out bool off)) return;
                if(!off) {
                    reason = "固件已取消停扇";
                    return;
                }
                safe = true;
            });
            if(!success || !safe) RestoreAuto(reason);
        }

        public bool RestoreAuto(string reason = null) {
            IsOff = false;
            RecoveryPending = true;
            _reason = reason;
            var failures = new List<string>();
            bool released = false;
            bool success = TryBatch(() => {
                // Continue every recovery step even after an ancillary failure.
                Attempt(failures, "停扇开关释放", () => _fans.SetOff(false));
                Attempt(failures, "关闭最大转速", () => _fans.SetMax(false));
                Attempt(failures, "自动等级 FF FF", () => {
                    if(!_fans.TrySetLevels(new byte[] { 0xFF, 0xFF }, out string diagnostic))
                        throw new InvalidOperationException("写入未确认：" + diagnostic);
                });
                Attempt(failures, "BIOS 默认模式", () => _fans.SetMode(BiosData.FanMode.Default));
                // SetLevels may enable manual control, so release it last.
                Attempt(failures, "释放手动控制", () => {
                    if(_manual()) _fans.SetManual(false);
                });
                Attempt(failures, "倒计时归零", () => _fans.SetCountdown(0));
                Attempt(failures, "停扇开关读回", () => {
                    released = IsSwitchReleased();
                });
            });
            if(!success) failures.Add("EC 批处理不可用或执行失败");
            RecoveryPending = !success || !released || failures.Count != 0;
            if(!RecoveryPending) _stopRequested = false;
            Message = RecoveryPending
                ? "⚠ " + (string.IsNullOrEmpty(reason) ? "" : reason + "；")
                    + (released ? "停扇开关已释放，自动恢复未完整确认" : "风扇恢复未确认")
                    + "：" + string.Join("；", failures) + "；正在重试，请检查实际转速"
                : string.IsNullOrEmpty(reason) ? "" : reason + " · 已请求 BIOS 自动";
            return !RecoveryPending;
        }

        // A different cooling mode need not first succeed at BIOS automatic
        // levels. Keep recovery armed until its strict initial write completes.
        public bool TryTakeControl(Action apply) {
            IsOff = false;
            RecoveryPending = true;
            var failures = new List<string>();
            bool applied = false;
            bool success = TryBatch(() => {
                if(!Attempt(failures, "停扇开关释放", () => _fans.SetOff(false))) return;
                if(!Attempt(failures, "停扇开关读回", () => IsSwitchReleased())) return;
                if(!Attempt(failures, "目标模式设置", apply)) return;
                applied = Attempt(failures, "切换后停扇开关读回", () => IsSwitchReleased());
            });
            if(success && applied) {
                RecoveryPending = false;
                _stopRequested = false;
                _reason = null;
                Message = "";
                return true;
            }
            if(!success) failures.Add("EC 批处理不可用或执行失败");
            RestoreAuto("模式切换未确认：" + string.Join("；", failures));
            return false;
        }

        private bool IsSwitchReleased() {
            if(!_fans.TryGetOff(out bool off))
                throw new InvalidOperationException("读取失败或值无效");
            if(off) throw new InvalidOperationException("停扇标志仍开启");
            return true;
        }

        public void ClearMessage() {
            if(!NeedsMonitoring) Message = "";
        }

        private bool TryBatch(Action action) {
            try { return _batch(action); } catch { return false; }
        }

        private static bool Attempt(List<string> failures, string step, Action action) {
            try { action(); return true; } catch(Exception ex) {
                failures.Add(step + "（" + ex.Message + "）");
                return false;
            }
        }
    }
}
