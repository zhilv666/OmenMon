  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Ec;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {

    // Manages the hardware sensors
    public class Platform {

#region Data
        // Last maximum temperature reading
        public byte LastMaxTemperature { get; private set; }

        // System information
        public ISettings System { get; private set; }

        // Fan sensors and controls
        public IFanArray Fans { get; private set; }

        // Independent of the EC temperature registers used by fan firmware.
        public IPlatformReadComponent CpuPackageTemperature { get; private set; }

        // Temperature sensor array and which of these values are used
        public IPlatformReadComponent[] Temperature { get; private set; }
        public bool[] TemperatureUse { get; private set; }

        // Configured name of each sensor, in the same order as the array above,
        // so that a specific sensor can be looked up without relying on position
        public string[] TemperatureName { get; private set; }
#endregion

#region Initialization
        // Initializes the class
        public Platform() {

            // Initialize the system settings
            InitSystem();

            // Initialize the fan controls
            InitFans();

            // Initialize the temperature controls
            InitTemperature();
            CpuPackageTemperature = IntelPackageTemperatureComponent.Create();

        }

        // Initializes the fan controls
        private void InitFans() {

            // Fan array can be product-specific
            switch(this.System.GetProduct()) {

                case "?": // Default
                case "8A13":
                case "8A14":
                default:

                    this.Fans = new FanArray(
                        new IFan[] {

                            // Define the CPU fan
                            new Fan(
                                BiosData.FanType.Cpu,
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.SRP1,
                                    PlatformData.AccessType.Read | PlatformData.AccessType.Write),
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.XGS1,
                                    PlatformData.AccessType.Read),
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.XSS1,
                                    PlatformData.AccessType.Write),
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.RPM1,
                                    PlatformData.AccessType.Read,
                                    PlatformData.DataSize.Word)),

                            // Define the GPU fan
                            new Fan(
                                BiosData.FanType.Gpu,
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.SRP2,
                                    PlatformData.AccessType.Read | PlatformData.AccessType.Write),
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.XGS2,
                                    PlatformData.AccessType.Read),
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.XSS2,
                                    PlatformData.AccessType.Write),
                                new EcComponent(
                                    (byte) EmbeddedControllerData.Register.RPM3, // Not a mistake, RPM2 is fan #0
                                    PlatformData.AccessType.Read,
                                    PlatformData.DataSize.Word)) },

                        // Define the countdown component
                        new EcComponent(
                            (byte) EmbeddedControllerData.Register.XFCD,
                            PlatformData.AccessType.Read | PlatformData.AccessType.Write),

                        // Define the manual toggle component
                        new EcComponent(
                            (byte) EmbeddedControllerData.Register.OMCC,
                            PlatformData.AccessType.Read | PlatformData.AccessType.Write), 

                        // Define the mode component
                        new EcComponent(
                            (byte) EmbeddedControllerData.Register.HPCM,
                            PlatformData.AccessType.Read | PlatformData.AccessType.Write), 

                        // Define the switch component
                        new EcComponent(
                            (byte) EmbeddedControllerData.Register.SFAN,
                            PlatformData.AccessType.Read | PlatformData.AccessType.Write));

                    break;

            }

        }

        // Initializes the system settings
        private void InitSystem() {
            this.System = new Settings();
        }

        // Initializes the temperature controls
        private void InitTemperature() {

            // Collect the sensors first, then publish them as arrays: an entry
            // with an unrecognized source is skipped entirely, so that the name,
            // use-flag and component arrays always stay aligned with each other
            var components = new System.Collections.Generic.List<IPlatformReadComponent>();
            var names = new System.Collections.Generic.List<string>();
            var use = new System.Collections.Generic.List<bool>();

            // Process each sensor loaded from the configuration
            foreach(string name in Config.TemperatureSensor.Keys) {

                IPlatformReadComponent component = null;

                switch(Config.TemperatureSensor[name].Source) {

                    // Add an Embedded Controller sensor
                    case PlatformData.LinkType.EmbeddedController:
                        component = new EcComponent(
                            Config.TemperatureSensor[name].Register,
                            Config.MaxBelievableTemperature);
                        break;

                    // Add a WMI BIOS sensor
                    case PlatformData.LinkType.WmiBios:
                        component = new WmiBiosTemperatureComponent(Config.MaxBelievableTemperature);
                        break;

                }

                // Skip anything that could not be resolved to a sensor
                if(component == null)
                    continue;

                // Name the component after its configuration entry, so that
                // the two always agree even for a non-standard register
                component.SetName(name);

                components.Add(component);
                names.Add(name);

                // Set whether the sensor can be used for maximum temperature
                use.Add(Config.TemperatureSensor[name].Use);

            }

            this.Temperature = components.ToArray();
            this.TemperatureName = names.ToArray();
            this.TemperatureUse = use.ToArray();

        }
#endregion

#region Information Retrieval
        // Rebuilds the sensor array from the current configuration. Needed after
        // the settings are re-read at run time (a configuration import), since
        // otherwise the sensors set up at start-up stay in effect until restart
        // — including their use flags, which decide the maximum temperature.
        public void ReloadTemperatureSensors() {
            InitTemperature();
        }

        // Looks a temperature sensor up by its configured name, returning null
        // if no such sensor is defined. Preferred over indexing into the array,
        // which depends on the order the sensors happen to appear in the settings
        public IPlatformReadComponent GetTemperatureSensor(string name) {

            // Snapshot both arrays: a reload can swap them under a caller that
            // polls on another thread, and the two are assigned separately
            IPlatformReadComponent[] sensors = this.Temperature;
            string[] names = this.TemperatureName;

            if(string.IsNullOrEmpty(name) || names == null || sensors == null)
                return null;

            for(int i = 0; i < names.Length && i < sensors.Length; i++)
                if(string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    return sensors[i];

            return null;

        }

        // Reports whether a named sensor counts towards the maximum temperature
        public bool IsTemperatureUsed(string name) {

            string[] names = this.TemperatureName;
            bool[] used = this.TemperatureUse;

            if(string.IsNullOrEmpty(name) || names == null || used == null)
                return false;

            for(int i = 0; i < names.Length && i < used.Length; i++)
                if(string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    return used[i];

            return false;

        }

        // Obtains the maximum value from the platform temperature array
        public byte GetMaxTemperature(bool forceUpdate = false) {

            // Update the platform temperature readings first
            // if forced to do so
            if(forceUpdate)
                UpdateTemperature(true);

            IPlatformReadComponent[] sensors = this.Temperature;
            bool[] used = this.TemperatureUse;

            // Reset the state
            this.LastMaxTemperature = 0;
            byte value;

            if(sensors == null || used == null)
                return this.LastMaxTemperature;

            // Iterate through the platform temperature array
            for(int i = 0; i < sensors.Length && i < used.Length; i++)

                // Obtain the reading from each temperature sensor
                // If the value is higher than the current candidate
                if(used[i] // Ignore certain sensors
                    && (value = (byte) sensors[i].GetValue())
                        > this.LastMaxTemperature)

                    // Update the candidate
                    this.LastMaxTemperature = value;

            // Return the result
            return this.LastMaxTemperature;

        }
#endregion

#region Updates
        // Updates everything
        public void UpdateAll() {
            UpdateFans();
            UpdateSystem();
            UpdateTemperature();
        }

        // Updates the fan readings
        public void UpdateFans() {
            // Fan readings updated at retrieval time
        }

        // Updates the system settings
        public void UpdateSystem() {
            // System settings updated either only once
            // during initialization, or at retrieval time
        }

        // Updates the temperature readings
        public void UpdateTemperature(bool onlyUsed = false) {
            for(int i = 0; i < Temperature.Length; i++)
                if(!onlyUsed || this.TemperatureUse[i])
                    this.Temperature[i].Update();
        }

        // Updates a single named sensor, ignored if there is no such sensor.
        // Lets a caller refresh a display-only sensor without also paying for
        // the ones it does not need (the BIOS sensor is a WMI call, not an
        // Embedded Controller read, and is markedly slower than the rest)
        public bool UpdateTemperature(string name) {
            IPlatformReadComponent sensor = GetTemperatureSensor(name);
            return sensor != null && sensor.Update();
        }
#endregion

    }

}
