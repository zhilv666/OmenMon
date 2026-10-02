  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Ec;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {

#region Interface
    // Defines an interface for interacting with a fan
    public interface IFan {

        // Retrieves the fan type
        public BiosData.FanType GetFanType();

        public int GetLevel();  // Retrieves the fan level [krpm]
        public int GetRate();   // Retrieves the fan rate [%]
        public int GetSpeed();  // Retrieves the fan speed [rpm]
        public bool TryGetRate(out int rate);
        public bool TryGetSpeed(out int speed);
        public bool TryGetTargetLevel(out int level);

        public void SetLevel(int level);  // Sets the fan level [krpm]
        public void SetRate(int rate);    // Sets the fan rate [%]

    }
#endregion

    // Implements a mechanism for interacting with a fan
    public class Fan : IFan {

#region Implementation
        // Stores the fan type
        protected BiosData.FanType FanType;

        // Stores the level data component
        protected IPlatformReadWriteComponent Level;

        // Stores the rate data components (separate read and write)
        protected IPlatformReadComponent RateRead;
        protected IPlatformWriteComponent RateWrite;

        // Stores the speed data component
        protected IPlatformReadComponent Speed;

        // Constructs a fan instance
        public Fan(
            BiosData.FanType type,
            IPlatformReadWriteComponent level,
            IPlatformReadComponent rateRead,
            IPlatformWriteComponent rateWrite,
            IPlatformReadComponent speed) {

            this.FanType = type;
            this.Level = level;
            this.RateRead = rateRead;
            this.RateRead.SetConstraint(Config.MaxBelievablePercent);
            this.RateWrite = rateWrite;
            this.Speed = speed;
            this.Speed.SetConstraint(Config.FanLevelMax * (100 + Config.MaxBelievableFanSpeedPercentOverMax));

        }

        // Retrieves the fan type
        public virtual BiosData.FanType GetFanType() {
            return this.FanType;
        }

        // Retrieves the fan level [krpm]
        public virtual int GetLevel() {
            return Hw.BiosGet(Hw.Bios.GetFanLevel)[(int) this.FanType - 1];
        }

        // Retrieves the fan rate [%]
        public virtual int GetRate() {
            this.RateRead.Update();
            return this.RateRead.GetValue();
        }

        // Retrieves the fan speed [rpm]
        public virtual int GetSpeed() {
            this.Speed.Update();
            return this.Speed.GetValue();
        }

        public bool TryGetRate(out int rate) {
            return this.RateRead.TryRead(out rate);
        }

        public bool TryGetSpeed(out int speed) {
            return this.Speed.TryRead(out speed);
        }

        // The EC target is distinct from the BIOS current speed level.
        public bool TryGetTargetLevel(out int level) {
            return this.Level.TryRead(out level);
        }

        // Sets the fan level [krpm]
        public virtual void SetLevel(int level) {
            this.Level.SetValue(level);
        }

        // Sets the fan rate [%]
        public virtual void SetRate(int rate) {
            this.RateWrite.SetValue(rate);
        }
#endregion

    }

}
