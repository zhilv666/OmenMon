  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Independent Intel package temperature for fan-stop protection
     //  https://omenmon.github.io/

using System;
using System.Globalization;
using OmenMon.Driver;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {

    public sealed class IntelPackageTemperatureComponent : PlatformComponentAbstract, IPlatformReadComponent {
        public delegate bool MsrReader(uint index, out uint eax, out uint edx);

        private const uint TemperatureTarget = 0x1A2;
        private const uint PackageThermalStatus = 0x1B1;
        private readonly MsrReader _readMsr;
        public bool IsSupported { get; }

        public IntelPackageTemperatureComponent(string manufacturer, string processorId, MsrReader readMsr) {
            _readMsr = readMsr;
            IsSupported = SupportsProcessor(manufacturer, processorId);
            Constraint = Config.MaxBelievableTemperature;
            Size = PlatformData.DataSize.Byte;
            LinkType = PlatformData.LinkType.Msr;
            SetName("CPU Package (MSR)");
        }

        public static IntelPackageTemperatureComponent Create() {
            try {
                using(var info = new WmiInfo()) {
                    var cpu = info.GetSingleProcessor();
                    if(cpu == null || !cpu.TryGetValue("Manufacturer", out string manufacturer)
                        || !cpu.TryGetValue("ProcessorId", out string processorId))
                        return null;
                    var sensor = new IntelPackageTemperatureComponent(manufacturer, processorId, Ring0.ReadMsr);
                    return sensor.IsSupported ? sensor : null;
                }
            } catch {
                return null;
            }
        }

        private static bool SupportsProcessor(string manufacturer, string processorId) {
            if(!string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal)
                || processorId == null || processorId.Length != 16
                || !ulong.TryParse(processorId, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong id))
                return false;

            // Win32_Processor.ProcessorId contains the CPUID signature in its
            // low DWORD. Limit reads to known Alder Lake Core package sensors;
            // this is not a generic Intel/AMD MSR probe.
            uint signature = (uint) id;
            uint family = (signature >> 8) & 0xF;
            uint model = ((signature >> 4) & 0xF) | ((signature >> 12) & 0xF0);
            return family == 6 && ((signature >> 20) & 0xFF) == 0
                && (model == 0x97 || model == 0x9A);
        }

        protected override int Read() {
            if(!IsSupported || _readMsr == null)
                throw new InvalidOperationException("CPU package temperature is unsupported.");
            if(!_readMsr(TemperatureTarget, out uint target, out _))
                throw new InvalidOperationException("CPU temperature target read failed.");
            int tjMax = (int) ((target >> 16) & 0xFF);
            if(tjMax < 70 || tjMax > 125)
                throw new InvalidOperationException("CPU temperature target is invalid.");

            if(!_readMsr(PackageThermalStatus, out uint status, out _) || (status & 0x80000000) == 0)
                throw new InvalidOperationException("CPU package temperature read is invalid.");

            // Intel package DTS: valid bit 31, distance to TjMax in bits 22:16.
            // Read TjMax from the CPU; never assume a fixed fallback value.
            int temperature = tjMax - (int) ((status >> 16) & 0x7F);
            if(temperature < 0 || temperature > Config.MaxBelievableTemperature)
                throw new InvalidOperationException("CPU package temperature is out of range.");
            return temperature;
        }
    }
}
