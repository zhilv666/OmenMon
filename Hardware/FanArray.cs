  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Ec;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {

#region Interface
    // Defines an interface for interacting with the fan system
    public interface IFanArray {

        public IFan[] Fan { get; }

        // Retrieves or sets the countdown value
        // until automatic settings are restored [s]
        public int GetCountdown();
        public void SetCountdown(int countdown);

        // Retrieves or sets the levels
        // of all fans at the same time
        public byte[] GetLevels();
        public void SetLevels(byte[] levels);
        public bool TrySetLevels(byte[] levels);
        public bool TrySetLevels(byte[] levels, out string diagnostic);

        // Retrieves or sets maximum fan speed
        public bool GetMax();  
        public void SetMax(bool flag);

        // Retrieves or sets manual fan control state
        public bool GetManual();
        public void SetManual(bool flag);

        // Retrieves or sets the current fan mode
        public BiosData.FanMode GetMode();
        public void SetMode(BiosData.FanMode mode);

        // Retrieves the fan off switch status
        // or switches the fan off
        public bool GetOff();
        public bool TryGetOff(out bool flag);
        public void SetOff(bool flag);


    }
#endregion

#region Implementation
    // Implements a mechanism for interacting with the fan system
    public class FanArray : IFanArray {

        // Fan array
        public IFan[] Fan { get; private set; }

        // Stores the countdown platform component
        protected IPlatformReadWriteComponent Countdown;

        // Stores the manual toggle component
        protected IPlatformReadWriteComponent Manual;

        // Stores the fan mode component
        protected IPlatformReadWriteComponent Mode;

        // Stores the fan on and off switch component
        protected IPlatformReadWriteComponent Switch;

        // Constructs a fan array instance
        public FanArray(
            IFan[] fan,
            IPlatformReadWriteComponent fanCountdown,
            IPlatformReadWriteComponent fanManual,
            IPlatformReadWriteComponent fanMode,
            IPlatformReadWriteComponent fanSwitch) {

            // Initialize the fan array
            this.Fan = new IFan[PlatformData.FanCount];

            // Define the CPU fan
            this.Fan[0] = fan[0];

            // Define the GPU fan
            this.Fan[1] = fan[1];

            // Define the countdown component
            this.Countdown = fanCountdown;

            // Define the mode component
            this.Manual = fanManual;

            // Define the mode component
            this.Mode = fanMode;

            // Define the switch component
            this.Switch = fanSwitch;

        }

        // Retrieves the countdown value [s]
        // until automatic settings are restored
        public int GetCountdown() {
            this.Countdown.Update();
            return this.Countdown.GetValue();
        }

        // Sets the countdown value [s]
        public void SetCountdown(int countdown) {
            this.Countdown.SetValue(countdown);
        }

        // Retrieves the levels of all fans at the same time
        public byte[] GetLevels() {
            return Hw.BiosGet(Hw.Bios.GetFanLevel);
        }

        // Sets the levels of all fans at the same time
        public void SetLevels(byte[] levels) {
            if(Config.FanLevelNeedManual)
                this.SetManual(true);

            if(Config.FanLevelUseEc) {
                WriteLevels(levels);
            } else {
                try {
                    WriteLevels(levels);
                } catch {
                    // Some models apply levels despite a BIOS error. Preserve
                    // that compatibility for ordinary manual/curve commands.
                }
            }
        }

        // Safety transitions require an acknowledged command or fresh matching
        // targets, not merely an ignored BIOS error or nonzero fan speeds.
        public bool TrySetLevels(byte[] levels) {
            return TrySetLevels(levels, out _);
        }

        public bool TrySetLevels(byte[] levels, out string diagnostic) {
            diagnostic = "";
            if(levels == null || levels.Length != this.Fan.Length) {
                diagnostic = "风扇目标等级数量无效";
                return false;
            }

            try {
                if(Config.FanLevelNeedManual)
                    this.SetManual(true);
            } catch(Exception ex) {
                diagnostic = "手动控制准备失败：" + ex.Message;
                return false;
            }

            try {
                WriteLevels(levels);
                return true;
            } catch(BiosException ex) when(!Config.FanLevelUseEc) {
                // Some firmware applies 0x2E but returns an error. Verify both
                // EC targets; FF FF must also match exactly, not actual RPM.
                bool confirmed = true;
                var readings = new string[levels.Length];
                for(int i = 0; i < levels.Length; i++) {
                    string prefix = "风扇" + (i + 1) + "目标 " + levels[i].ToString("X2");
                    try {
                        if(this.Fan[i].TryGetTargetLevel(out int target)) {
                            confirmed &= target == levels[i];
                            readings[i] = prefix + " / 读回 " + target.ToString("X2");
                        } else {
                            confirmed = false;
                            readings[i] = prefix + " / 读回失败";
                        }
                    } catch(Exception readError) {
                        confirmed = false;
                        readings[i] = prefix + " / 读回失败：" + readError.Message;
                    }
                }
                diagnostic = "BIOS 0x2E：" + ex.Message + "；" + string.Join("；", readings);
                return confirmed;
            } catch(Exception ex) {
                diagnostic = (Config.FanLevelUseEc ? "EC 等级写入：" : "BIOS 等级写入：") + ex.Message;
                return false;
            }
        }

        private void WriteLevels(byte[] levels) {
            if(Config.FanLevelUseEc) {
                for(int i = 0; i < levels.Length; i++)
                    this.Fan[i].SetLevel(levels[i]);
            } else {
                Hw.BiosSet(Hw.Bios.SetFanLevel, levels);
            }
        }

        // Retrieves the manual fan speed toggle status
        public bool GetManual() {
            return this.Manual.GetValue() == (byte) PlatformData.FanManual.On;
        }

        // Sets the manual fan speed toggle status
        public void SetManual(bool flag) {
            this.Manual.SetValue(flag ?
                (byte) PlatformData.FanManual.On : (byte) PlatformData.FanManual.Off);
        }

        // Retrieves the maximum fan speed status
        public bool GetMax() {
            return Hw.BiosGet<bool>(Hw.Bios.GetMaxFan);
        }

        // Sets the maximum fan speed status
        public void SetMax(bool flag) {
            Hw.BiosSet(Hw.Bios.SetMaxFan, flag);
        }

        // Retrieves the current fan mode
        public BiosData.FanMode GetMode() {
            this.Mode.Update();
            return (BiosData.FanMode) this.Mode.GetValue();
        }

        // Sets the current fan mode
        public void SetMode(BiosData.FanMode mode) {
            Hw.BiosSet<BiosData.FanMode>(Hw.Bios.SetFanMode, mode);
            // Note: WMI BIOS call preferred over this.Mode.SetValue((byte) mode);
        }

        // Retrieves the fan off switch status
        public bool GetOff() {
            this.Switch.Update();
            return ((PlatformData.FanSwitch) this.Switch.GetValue()) == PlatformData.FanSwitch.Off;
        }

        // The stop switch has two valid values. Never mistake a failed read or
        // the component's old cached state for confirmation of a command.
        public bool TryGetOff(out bool flag) {
            flag = false;
            if(!this.Switch.TryRead(out int value))
                return false;
            if(value != (int) PlatformData.FanSwitch.On
                && value != (int) PlatformData.FanSwitch.Off)
                return false;
            flag = value == (int) PlatformData.FanSwitch.Off;
            return true;
        }

        // Switches the fan off or back on
        public void SetOff(bool flag) {
            this.Switch.SetValue(flag ?
                (int) PlatformData.FanSwitch.Off : (int) PlatformData.FanSwitch.On);
        }
#endregion

    }

}
