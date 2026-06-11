using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HostPowerMonitor
{
    public enum PowerSourceKind
    {
        Measured,
        Estimated,
        Defaulted,
        Unavailable
    }

    public sealed class HardwareInventory
    {
        public string OperatingSystem;
        public string ComputerModel;
        public string BaseBoard;
        public string CpuName;
        public bool IsLaptop;
        public int CpuCores;
        public int CpuLogicalProcessors;
        public double MemoryGb;
        public int MemoryModules;
        public int MemoryType;
        public int MemorySpeedMhz;
        public bool MemoryIsLowPower;
        public readonly List<GpuInfo> Gpus = new List<GpuInfo>();
        public readonly List<DiskInfo> InternalDisks = new List<DiskInfo>();
    }

    public sealed class GpuInfo
    {
        public string Name;
        public string Vendor;
        public bool IsIntegrated;
        public double EstimatedMaxWatts;
    }

    public sealed class DiskInfo
    {
        public string Model;
        public string InterfaceType;
        public string MediaType;
        public string PnpDeviceId;
        public long SizeBytes;
        public int BusType = -1;
        public bool IsSsdLike;
    }

    public sealed class ComponentPower
    {
        public string Name;
        public double Watts;
        public PowerSourceKind Source;
    }

    public sealed class PowerSample
    {
        public DateTime Timestamp;
        public double CpuWatts;
        public double GpuWatts;
        public double MemoryWatts;
        public double StorageWatts;
        public double PlatformWatts;
        public double TotalBeforeMarginWatts;
        public double TotalWatts;
        public double TodayKWh;
        public double MonthKWh;
        public double TodayCost;
        public double MonthCost;
        public string Summary;
        public readonly List<ComponentPower> Components = new List<ComponentPower>();
    }

    public sealed class AppSettings
    {
        public bool AutoStart = true;
        public bool BubbleVisible = true;
        public double MarginPercent = 15.0;
        public double ElectricityRate = 0.60;
        public bool HighPowerAlert = false;
        public double HighPowerThresholdWatts = 450.0;
        public int SampleSeconds = 2;
        public int HistoryRetentionDays = 30;
        public int BubbleX = -1;
        public int BubbleY = -1;

        public static string AppFolder
        {
            get
            {
                string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(baseFolder, "HostPowerMonitor");
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(AppFolder, "settings.ini"); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                Directory.CreateDirectory(AppFolder);
                if (!File.Exists(SettingsPath))
                    return settings;

                foreach (string rawLine in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    settings.ApplyValue(key, value);
                }
            }
            catch
            {
            }
            settings.Normalize();
            return settings;
        }

        public void Save()
        {
            try
            {
                Normalize();
                Directory.CreateDirectory(AppFolder);
                List<string> lines = new List<string>();
                lines.Add("AutoStart=" + AutoStart.ToString(CultureInfo.InvariantCulture));
                lines.Add("BubbleVisible=" + BubbleVisible.ToString(CultureInfo.InvariantCulture));
                lines.Add("MarginPercent=" + MarginPercent.ToString(CultureInfo.InvariantCulture));
                lines.Add("ElectricityRate=" + ElectricityRate.ToString(CultureInfo.InvariantCulture));
                lines.Add("HighPowerAlert=" + HighPowerAlert.ToString(CultureInfo.InvariantCulture));
                lines.Add("HighPowerThresholdWatts=" + HighPowerThresholdWatts.ToString(CultureInfo.InvariantCulture));
                lines.Add("SampleSeconds=" + SampleSeconds.ToString(CultureInfo.InvariantCulture));
                lines.Add("HistoryRetentionDays=" + HistoryRetentionDays.ToString(CultureInfo.InvariantCulture));
                lines.Add("BubbleX=" + BubbleX.ToString(CultureInfo.InvariantCulture));
                lines.Add("BubbleY=" + BubbleY.ToString(CultureInfo.InvariantCulture));
                File.WriteAllLines(SettingsPath, lines.ToArray(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        public void ApplyAutoStart(string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (key == null)
                        return;
                    if (AutoStart)
                        key.SetValue("HostPowerMonitor", BuildAutoStartCommand(exePath));
                    else
                        key.DeleteValue("HostPowerMonitor", false);
                }
            }
            catch
            {
            }
        }

        internal static string BuildAutoStartCommand(string exePath)
        {
            string path = (exePath ?? "").Trim().Trim('"');
            return "\"" + path + "\"";
        }

        private void ApplyValue(string key, string value)
        {
            bool boolValue;
            double doubleValue;
            int intValue;
            if (key.Equals("AutoStart", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out boolValue))
                AutoStart = boolValue;
            else if (key.Equals("BubbleVisible", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out boolValue))
                BubbleVisible = boolValue;
            else if (key.Equals("MarginPercent", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                MarginPercent = doubleValue;
            else if (key.Equals("ElectricityRate", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                ElectricityRate = doubleValue;
            else if (key.Equals("HighPowerAlert", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out boolValue))
                HighPowerAlert = boolValue;
            else if (key.Equals("HighPowerThresholdWatts", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                HighPowerThresholdWatts = doubleValue;
            else if (key.Equals("SampleSeconds", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out intValue))
                SampleSeconds = intValue;
            else if (key.Equals("HistoryRetentionDays", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out intValue))
                HistoryRetentionDays = intValue;
            else if (key.Equals("BubbleX", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out intValue))
                BubbleX = intValue;
            else if (key.Equals("BubbleY", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out intValue))
                BubbleY = intValue;
        }

        internal void Normalize()
        {
            if (double.IsNaN(MarginPercent) || double.IsInfinity(MarginPercent))
                MarginPercent = 15.0;
            if (MarginPercent < 0)
                MarginPercent = 0;
            if (MarginPercent > 60)
                MarginPercent = 60;
            if (double.IsNaN(ElectricityRate) || double.IsInfinity(ElectricityRate))
                ElectricityRate = 0.60;
            if (ElectricityRate < 0)
                ElectricityRate = 0;
            if (ElectricityRate > 5)
                ElectricityRate = 5;
            if (double.IsNaN(HighPowerThresholdWatts) || double.IsInfinity(HighPowerThresholdWatts))
                HighPowerThresholdWatts = 450.0;
            if (HighPowerThresholdWatts < 50)
                HighPowerThresholdWatts = 50;
            if (HighPowerThresholdWatts > 2000)
                HighPowerThresholdWatts = 2000;
            if (SampleSeconds < 1)
                SampleSeconds = 1;
            if (SampleSeconds > 10)
                SampleSeconds = 10;
            if (HistoryRetentionDays < 7)
                HistoryRetentionDays = 7;
            if (HistoryRetentionDays > 365)
                HistoryRetentionDays = 365;
        }
    }

    internal static class WmiQuery
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

        public static ManagementObjectSearcher Create(string query)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            Configure(searcher);
            return searcher;
        }

        public static ManagementObjectSearcher Create(ManagementScope scope, ObjectQuery query)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
            Configure(searcher);
            return searcher;
        }

        private static void Configure(ManagementObjectSearcher searcher)
        {
            try
            {
                searcher.Options.Timeout = Timeout;
                searcher.Options.ReturnImmediately = true;
                searcher.Options.Rewindable = false;
            }
            catch
            {
            }
        }
    }

    public static class HardwareDetector
    {
        public static HardwareInventory Detect()
        {
            HardwareInventory inventory = new HardwareInventory();
            inventory.OperatingSystem = QueryFirstString("Win32_OperatingSystem", "Caption");
            inventory.ComputerModel = QueryFirstString("Win32_ComputerSystem", "Model");
            inventory.BaseBoard = QueryFirstString("Win32_BaseBoard", "Product");
            DetectChassis(inventory);
            DetectCpu(inventory);
            DetectMemory(inventory);
            DetectGpus(inventory);
            DetectDisks(inventory);
            return inventory;
        }

        private static void DetectCpu(HardwareInventory inventory)
        {
            try
            {
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        inventory.CpuName = SafeString(obj["Name"]);
                        inventory.CpuCores = SafeInt(obj["NumberOfCores"]);
                        inventory.CpuLogicalProcessors = SafeInt(obj["NumberOfLogicalProcessors"]);
                        return;
                    }
                }
            }
            catch
            {
            }
        }

        private static void DetectMemory(HardwareInventory inventory)
        {
            try
            {
                long total = 0;
                int modules = 0;
                int memoryType = 0;
                int speedSum = 0;
                int speedCount = 0;
                bool lowPower = false;
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT Capacity,SMBIOSMemoryType,MemoryType,Speed,ConfiguredClockSpeed,FormFactor FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        total += SafeLong(obj["Capacity"]);
                        modules++;
                        int smbiosType = SafeInt(obj["SMBIOSMemoryType"]);
                        int legacyType = SafeInt(obj["MemoryType"]);
                        int type = smbiosType > 0 ? smbiosType : legacyType;
                        if (type > memoryType)
                            memoryType = type;
                        int configured = SafeInt(obj["ConfiguredClockSpeed"]);
                        int speed = configured > 0 ? configured : SafeInt(obj["Speed"]);
                        if (speed > 0)
                        {
                            speedSum += speed;
                            speedCount++;
                        }
                        int formFactor = SafeInt(obj["FormFactor"]);
                        if (type == 14 || type == 30 || formFactor == 12)
                            lowPower = true;
                    }
                }
                inventory.MemoryGb = Math.Round(total / 1024.0 / 1024.0 / 1024.0, 1);
                inventory.MemoryModules = modules;
                inventory.MemoryType = memoryType;
                inventory.MemorySpeedMhz = speedCount > 0 ? (int)Math.Round(speedSum / (double)speedCount) : 0;
                inventory.MemoryIsLowPower = lowPower;
            }
            catch
            {
            }
        }

        private static void DetectGpus(HardwareInventory inventory)
        {
            try
            {
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT Name,AdapterCompatibility,AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = SafeString(obj["Name"]);
                        if (IsVirtualDisplay(name))
                            continue;

                        GpuInfo gpu = new GpuInfo();
                        gpu.Name = name;
                        gpu.Vendor = SafeString(obj["AdapterCompatibility"]);
                        long ram = SafeLong(obj["AdapterRAM"]);
                        gpu.IsIntegrated = IsIntegratedGpu(name, gpu.Vendor, ram);
                        gpu.EstimatedMaxWatts = EstimateGpuMaxWatts(name, gpu.Vendor, gpu.IsIntegrated, inventory.IsLaptop);
                        inventory.Gpus.Add(gpu);
                    }
                }
            }
            catch
            {
            }
        }

        private static void DetectDisks(HardwareInventory inventory)
        {
            try
            {
                Dictionary<string, int> busTypes = QueryStorageBusTypes();
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT Model,InterfaceType,MediaType,PNPDeviceID,Size FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        DiskInfo disk = new DiskInfo();
                        disk.Model = SafeString(obj["Model"]);
                        disk.InterfaceType = SafeString(obj["InterfaceType"]);
                        disk.MediaType = SafeString(obj["MediaType"]);
                        disk.PnpDeviceId = SafeString(obj["PNPDeviceID"]);
                        disk.SizeBytes = SafeLong(obj["Size"]);
                        int busType;
                        if (busTypes.TryGetValue(NormalizeHardwareName(disk.Model), out busType))
                            disk.BusType = busType;
                        if (!ShouldCountDisk(disk))
                            continue;

                        disk.IsSsdLike = LooksLikeSsd(disk);
                        inventory.InternalDisks.Add(disk);
                    }
                }
            }
            catch
            {
            }
        }

        internal static bool ShouldCountDisk(DiskInfo disk)
        {
            return disk != null && disk.SizeBytes > 0 && !IsExternalDisk(disk);
        }

        private static void DetectChassis(HardwareInventory inventory)
        {
            try
            {
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        ushort[] values = obj["ChassisTypes"] as ushort[];
                        if (values == null)
                            continue;
                        foreach (ushort v in values)
                        {
                            if (v == 8 || v == 9 || v == 10 || v == 14 || v == 30 || v == 31 || v == 32)
                            {
                                inventory.IsLaptop = true;
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static double EstimateCpuMaxWatts(HardwareInventory inventory)
        {
            string name = NormalizeHardwareName(inventory.CpuName);
            if (inventory.IsLaptop)
            {
                if (name.Contains("hx"))
                    return 90;
                if (name.Contains("hs") || name.EndsWith("hs", StringComparison.Ordinal))
                    return 55;
                if (name.Contains("h ") || name.EndsWith("h", StringComparison.Ordinal) || name.Contains("-h"))
                    return 55;
                if (name.Contains("p ") || name.EndsWith("p", StringComparison.Ordinal))
                    return 35;
                if (name.Contains("u ") || name.EndsWith("u", StringComparison.Ordinal) || name.Contains("-u"))
                    return 25;
                return 25;
            }

            if (name.Contains("threadripper"))
                return 280;
            if (name.Contains("9950x") || name.Contains("7950x"))
                return 170;
            if (name.Contains("9900x") || name.Contains("7900x") || name.Contains("7900x3d"))
                return 120;
            if (name.Contains("9800x3d") || name.Contains("7800x3d"))
                return 120;
            if (name.Contains("9700x") || name.Contains("7700x"))
                return 105;
            if (name.Contains("9600x") || name.Contains("7600x"))
                return 88;
            if (name.Contains("ryzen 9"))
                return name.Contains("x") ? 170 : 105;
            if (name.Contains("ryzen 7"))
                return name.Contains("x") ? 120 : 88;
            if (name.Contains("ryzen 5"))
                return name.Contains("9600x") ? 88 : (name.Contains("x") ? 105 : 75);
            if (name.Contains("core ultra 9 285k"))
                return 250;
            if (name.Contains("core ultra 7 265k") || name.Contains("core ultra 5 245k"))
                return 250;
            if (name.Contains("core ultra 9") || name.Contains(" i9-"))
                return name.Contains("k") ? 253 : 125;
            if (name.Contains("core ultra 7") || name.Contains(" i7-"))
                return name.Contains("k") ? 190 : 95;
            if (name.Contains("core ultra 5") || name.Contains(" i5-"))
                return name.Contains("k") ? 150 : 80;
            return 75;
        }

        public static double EstimateGpuMaxWatts(string name, string vendor, bool integrated)
        {
            return EstimateGpuMaxWatts(name, vendor, integrated, false);
        }

        public static double EstimateGpuMaxWatts(string name, string vendor, bool integrated, bool laptop)
        {
            string n = NormalizeHardwareName(name);
            if (integrated)
                return 18;
            bool mobile = laptop || n.Contains("laptop") || n.Contains("mobile") || n.Contains("max-q");

            if (mobile && n.Contains("4090")) return 175;
            if (mobile && n.Contains("4080")) return 175;
            if (mobile && n.Contains("4070")) return 115;
            if (mobile && n.Contains("4060")) return 115;
            if (mobile && n.Contains("4050")) return 80;
            if (mobile && n.Contains("3080")) return 150;
            if (mobile && n.Contains("3070")) return 125;
            if (mobile && n.Contains("3060")) return 115;
            if (mobile && n.Contains("a770m")) return 120;
            if (mobile && n.Contains("a730m")) return 120;
            if (mobile && n.Contains("a550m")) return 80;
            if (mobile)
                return 90;

            if (n.Contains("5090")) return 575;
            if (n.Contains("5080")) return 360;
            if (n.Contains("5070 ti")) return 300;
            if (n.Contains("5070")) return 250;
            if (n.Contains("5060 ti")) return 180;
            if (n.Contains("5060")) return 145;
            if (n.Contains("4090")) return 450;
            if (n.Contains("4080")) return 320;
            if (n.Contains("4070 ti")) return 285;
            if (n.Contains("4070 super")) return 220;
            if (n.Contains("4070")) return 200;
            if (n.Contains("4060 ti")) return 160;
            if (n.Contains("4060")) return 115;
            if (n.Contains("4050")) return 115;
            if (n.Contains("3090 ti")) return 450;
            if (n.Contains("3090")) return 350;
            if (n.Contains("3080 ti")) return 350;
            if (n.Contains("3080")) return 320;
            if (n.Contains("3070 ti")) return 290;
            if (n.Contains("3070")) return 220;
            if (n.Contains("3060 ti")) return 200;
            if (n.Contains("3060")) return 170;
            if (n.Contains("3050")) return 130;
            if (n.Contains("2080 ti")) return 250;
            if (n.Contains("2080")) return 215;
            if (n.Contains("2070")) return 175;
            if (n.Contains("2060")) return 160;
            if (n.Contains("1080 ti")) return 250;
            if (n.Contains("1080")) return 180;
            if (n.Contains("1070")) return 150;
            if (n.Contains("1060")) return 120;
            if (n.Contains("1660")) return 120;
            if (n.Contains("1650")) return 75;
            if (n.Contains("9070 xt")) return 304;
            if (n.Contains("9070")) return 220;
            if (n.Contains("7900 xtx")) return 355;
            if (n.Contains("7900 xt")) return 315;
            if (n.Contains("7900 gre")) return 260;
            if (n.Contains("6950 xt")) return 335;
            if (n.Contains("6900 xt")) return 300;
            if (n.Contains("6800 xt")) return 300;
            if (n.Contains("6800")) return 250;
            if (n.Contains("7800 xt")) return 263;
            if (n.Contains("7700 xt")) return 245;
            if (n.Contains("7600 xt")) return 190;
            if (n.Contains("7600")) return 165;
            if (n.Contains("6750 xt")) return 250;
            if (n.Contains("6700 xt")) return 230;
            if (n.Contains("6650 xt")) return 180;
            if (n.Contains("6600 xt")) return 160;
            if (n.Contains("6600")) return 132;
            if (n.Contains("5700 xt")) return 225;
            if (n.Contains("5600 xt")) return 150;
            if (n.Contains("b580")) return 190;
            if (n.Contains("b570")) return 150;
            if (n.Contains("a770")) return 225;
            if (n.Contains("a750")) return 225;
            if (n.Contains("a580")) return 185;
            if (n.Contains("a380")) return 75;
            if (n.Contains("arc")) return 150;
            if ((vendor ?? "").ToLowerInvariant().Contains("nvidia")) return 180;
            if ((vendor ?? "").ToLowerInvariant().Contains("amd")) return 160;
            if ((vendor ?? "").ToLowerInvariant().Contains("intel")) return 60;
            return 80;
        }

        internal static string NormalizeHardwareName(string value)
        {
            string text = (value ?? "").ToLowerInvariant();
            text = text.Replace("(r)", "").Replace("(tm)", "").Replace("(c)", "");
            while (text.Contains("  "))
                text = text.Replace("  ", " ");
            return text.Trim();
        }

        private static string QueryFirstString(string wmiClass, string property)
        {
            try
            {
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT " + property + " FROM " + wmiClass))
                {
                    foreach (ManagementObject obj in searcher.Get())
                        return SafeString(obj[property]);
                }
            }
            catch
            {
            }
            return "";
        }

        private static bool IsVirtualDisplay(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            return n.Length == 0 || n.Contains("oray") || n.Contains("idd") || n.Contains("virtual") ||
                   n.Contains("remote") || n.Contains("basic render") || n.Contains("mirage");
        }

        private static bool IsIntegratedGpu(string name, string vendor, long adapterRam)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("radeon(tm) graphics") || n.Contains("uhd graphics") || n.Contains("iris") || n.Contains("vega") || n.Contains("integrated"))
                return true;
            if ((vendor ?? "").ToLowerInvariant().Contains("intel") && !n.Contains("arc"))
                return true;
            return adapterRam > 0 && adapterRam <= 1024L * 1024L * 1024L;
        }

        private static bool IsExternalDisk(DiskInfo disk)
        {
            string iface = (disk.InterfaceType ?? "").ToLowerInvariant();
            string pnp = (disk.PnpDeviceId ?? "").ToLowerInvariant();
            string media = (disk.MediaType ?? "").ToLowerInvariant();
            if (disk.BusType == 7 || disk.BusType == 9 || disk.BusType == 12 || disk.BusType == 14 || disk.BusType == 15)
                return true;
            if (iface.Contains("usb") || pnp.StartsWith("usbstor", StringComparison.Ordinal) || pnp.StartsWith("usb\\", StringComparison.Ordinal))
                return true;
            if (media.Contains("removable"))
                return true;
            if ((iface.Contains("scsi") || iface.Contains("usb")) && LooksLikePortableDiskModel(disk.Model))
                return true;
            return false;
        }

        private static bool LooksLikePortableDiskModel(string model)
        {
            string value = NormalizeHardwareName(model);
            return value.Contains("portable") || value.Contains("external") || value.Contains("expansion") ||
                   value.Contains("passport") || value.Contains("elements") || value.Contains("easystore") ||
                   value.Contains("backup plus") || value.Contains("my book") || value.Contains("rugged") ||
                   value.Contains("flash drive") || value.Contains("sandisk extreme") ||
                   value.Contains("card reader") || value.Contains("sd card") || value.Contains("mmc card") ||
                   value.Contains("memory card") || value.Contains("multicard") || value.Contains("multi-card") ||
                   value.Equals("t5", StringComparison.Ordinal) || value.Equals("t7", StringComparison.Ordinal) ||
                   value.Contains("portable ssd t5") || value.Contains("portable ssd t7") ||
                   value.Contains("xs1000") || value.Contains("xs2000");
        }

        private static bool LooksLikeSsd(DiskInfo disk)
        {
            string model = (disk.Model ?? "").ToLowerInvariant();
            string media = (disk.MediaType ?? "").ToLowerInvariant();
            string iface = (disk.InterfaceType ?? "").ToLowerInvariant();
            return model.Contains("ssd") || model.Contains("nvme") || model.Contains("solid") || media.Contains("ssd") ||
                   iface.Contains("nvme") || model.Contains("emmc") || model.Contains("ufs") ||
                   media.Contains("emmc") || media.Contains("ufs") || iface.Contains("ufs") ||
                   disk.BusType == 13 || disk.BusType == 19;
        }

        private static string SafeString(object value)
        {
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
        }

        private static Dictionary<string, int> QueryStorageBusTypes()
        {
            Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                using (ManagementObjectSearcher searcher = WmiQuery.Create(scope, new ObjectQuery("SELECT FriendlyName,BusType FROM MSFT_PhysicalDisk")))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = NormalizeHardwareName(SafeString(obj["FriendlyName"]));
                        if (name.Length == 0)
                            continue;
                        values[name] = SafeInt(obj["BusType"]);
                    }
                }
            }
            catch
            {
            }
            return values;
        }

        private static int SafeInt(object value)
        {
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static long SafeLong(object value)
        {
            try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }
    }

    public sealed class PowerEstimator : IDisposable
    {
        private readonly HardwareInventory _inventory;
        private readonly AppSettings _settings;
        private PerformanceCounter _cpuLoadCounter;
        private PerformanceCounter _cpuPowerCounter;
        private DateTime _lastCpuWmiRead = DateTime.MinValue;
        private double _lastCpuWmiLoad = 20;
        private DateTime _lastNvidiaRead = DateTime.MinValue;
        private DateTime _lastNvidiaSmiRead = DateTime.MinValue;
        private DateTime _lastNvidiaSuccess = DateTime.MinValue;
        private Dictionary<string, double> _lastNvidiaWatts = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private GpuEngineUtilization _gpuEngineUtil;

        public PowerEstimator(HardwareInventory inventory, AppSettings settings)
        {
            _inventory = inventory;
            _settings = settings;
            _cpuLoadCounter = CreateCounter("Processor", "% Processor Time", "_Total");
            _cpuPowerCounter = CreateCpuPackagePowerCounter();
            _gpuEngineUtil = new GpuEngineUtilization();
            WarmCounters();
        }

        public PowerSample ReadSample(DateTime timestamp)
        {
            PowerSample sample = new PowerSample();
            sample.Timestamp = timestamp;

            ComponentPower cpu = ReadCpuPower();
            sample.CpuWatts = cpu.Watts;
            sample.Components.Add(cpu);

            double gpuTotal = 0;
            foreach (ComponentPower gpu in ReadGpuPowers())
            {
                gpuTotal += gpu.Watts;
                sample.Components.Add(gpu);
            }
            sample.GpuWatts = gpuTotal;

            ComponentPower memory = EstimateMemoryPower();
            ComponentPower storage = EstimateStoragePower();
            ComponentPower platform = EstimatePlatformPower();
            sample.MemoryWatts = memory.Watts;
            sample.StorageWatts = storage.Watts;
            sample.PlatformWatts = platform.Watts;
            sample.Components.Add(memory);
            sample.Components.Add(storage);
            sample.Components.Add(platform);

            double beforeMargin = sample.CpuWatts + sample.GpuWatts + sample.MemoryWatts + sample.StorageWatts + sample.PlatformWatts;
            if (beforeMargin < 1)
                beforeMargin = 1;
            sample.TotalBeforeMarginWatts = beforeMargin;
            sample.TotalWatts = beforeMargin * (1.0 + _settings.MarginPercent / 100.0);
            sample.Summary = BuildSummary(sample);
            return sample;
        }

        public void Dispose()
        {
            DisposeCounter(_cpuLoadCounter);
            DisposeCounter(_cpuPowerCounter);
            if (_gpuEngineUtil != null)
                _gpuEngineUtil.Dispose();
        }

        private void WarmCounters()
        {
            try { if (_cpuLoadCounter != null) _cpuLoadCounter.NextValue(); }
            catch { }
            try { if (_cpuPowerCounter != null) _cpuPowerCounter.NextValue(); }
            catch { }
        }

        private ComponentPower ReadCpuPower()
        {
            double measured = ReadCpuPackageWatts();
            if (measured > 0)
                return NewComponent("CPU", measured, PowerSourceKind.Measured);

            double load = ReadCpuLoad();
            double max = HardwareDetector.EstimateCpuMaxWatts(_inventory);
            double idle = _inventory.IsLaptop ? 3.5 : 12.0;
            double factor = Math.Pow(Math.Max(0, Math.Min(100, load)) / 100.0, 1.25);
            double watts = idle + factor * Math.Max(8, max - idle);
            return NewComponent("CPU", watts, PowerSourceKind.Estimated);
        }

        private double ReadCpuPackageWatts()
        {
            try
            {
                if (_cpuPowerCounter == null)
                    return 0;
                double value = _cpuPowerCounter.NextValue();
                if (value > 1000)
                    value = value / 1000.0;
                if (value > 1 && value < 400)
                    return value;
            }
            catch
            {
            }
            return 0;
        }

        private double ReadCpuLoad()
        {
            try
            {
                if (_cpuLoadCounter != null)
                {
                    double value = _cpuLoadCounter.NextValue();
                    if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 100)
                        return value;
                }
            }
            catch
            {
                DisposeCounter(_cpuLoadCounter);
                _cpuLoadCounter = null;
            }

            DateTime now = DateTime.UtcNow;
            if (IsCacheFresh(_lastCpuWmiRead, now, 5))
                return _lastCpuWmiLoad;

            try
            {
                using (ManagementObjectSearcher searcher = WmiQuery.Create("SELECT LoadPercentage FROM Win32_Processor"))
                {
                    List<double> values = new List<double>();
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double value = Convert.ToDouble(obj["LoadPercentage"], CultureInfo.InvariantCulture);
                        if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 100)
                            values.Add(value);
                    }
                    double average;
                    if (TryAverageValidPercent(values, out average))
                    {
                        _lastCpuWmiLoad = average;
                        _lastCpuWmiRead = now;
                        return _lastCpuWmiLoad;
                    }
                }
            }
            catch
            {
            }
            _lastCpuWmiRead = now;
            return _lastCpuWmiLoad;
        }

        internal static bool IsCacheFresh(DateTime lastRead, DateTime now, double cacheSeconds)
        {
            return lastRead != DateTime.MinValue && now >= lastRead && (now - lastRead).TotalSeconds < cacheSeconds;
        }

        internal static bool TryAverageValidPercent(IEnumerable<double> values, out double average)
        {
            average = 0;
            if (values == null)
                return false;
            double sum = 0;
            int count = 0;
            foreach (double value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 100)
                    continue;
                sum += value;
                count++;
            }
            if (count == 0)
                return false;
            average = sum / count;
            return true;
        }

        internal static bool ShouldKeepMeasuredGpuCache(DateTime lastSuccess, DateTime now, double cacheSeconds)
        {
            return IsCacheFresh(lastSuccess, now, cacheSeconds);
        }

        private IEnumerable<ComponentPower> ReadGpuPowers()
        {
            RefreshNvidiaPowersIfNeeded();
            double fallbackUtil = _gpuEngineUtil.ReadTotalUtilizationPercent();

            bool hasMeasuredDiscrete = false;
            int unmeasuredDiscreteCount = 0;
            int unmeasuredIntegratedCount = 0;
            foreach (GpuInfo gpu in _inventory.Gpus)
            {
                double measured;
                if (TryGetMeasuredGpuWatts(gpu, out measured) && measured > 0)
                {
                    if (!gpu.IsIntegrated)
                        hasMeasuredDiscrete = true;
                }
                else if (gpu.IsIntegrated)
                    unmeasuredIntegratedCount++;
                else
                    unmeasuredDiscreteCount++;
            }

            foreach (GpuInfo gpu in _inventory.Gpus)
            {
                double measured;
                if (TryGetMeasuredGpuWatts(gpu, out measured) && measured > 0)
                {
                    yield return NewComponent("GPU", measured, PowerSourceKind.Measured);
                    continue;
                }

                double util = SelectGpuFallbackUtilization(gpu, fallbackUtil, hasMeasuredDiscrete, unmeasuredDiscreteCount, unmeasuredIntegratedCount);
                double watts = EstimateGpuWatts(gpu, util);
                yield return NewComponent("GPU", watts, PowerSourceKind.Estimated);
            }
        }

        internal static double SelectGpuFallbackUtilization(GpuInfo gpu, double fallbackUtil, bool hasMeasuredDiscrete, int unmeasuredDiscreteCount, int unmeasuredIntegratedCount)
        {
            if (gpu == null)
                return 0;
            fallbackUtil = Math.Max(0, Math.Min(100, fallbackUtil));
            if (gpu.IsIntegrated)
            {
                if (hasMeasuredDiscrete || unmeasuredDiscreteCount > 0)
                    return 0;
                return Math.Min(50, fallbackUtil / Math.Max(1, unmeasuredIntegratedCount));
            }
            return fallbackUtil / Math.Max(1, unmeasuredDiscreteCount);
        }

        internal static double EstimateGpuWatts(GpuInfo gpu, double utilizationPercent)
        {
            double idle = gpu.IsIntegrated ? 2.5 : 10.0;
            double util = gpu.IsIntegrated ? Math.Min(50, utilizationPercent) : utilizationPercent;
            double factor = Math.Pow(Math.Max(0, Math.Min(100, util)) / 100.0, 1.1);
            return idle + factor * Math.Max(5, gpu.EstimatedMaxWatts - idle);
        }

        private bool TryGetMeasuredGpuWatts(GpuInfo gpu, out double watts)
        {
            watts = 0;
            if (gpu == null || string.IsNullOrWhiteSpace(gpu.Name))
                return false;
            if (_lastNvidiaWatts.TryGetValue(gpu.Name, out watts))
                return true;

            string target = NormalizeGpuIdentity(gpu.Name);
            foreach (KeyValuePair<string, double> item in _lastNvidiaWatts)
            {
                if (NormalizeGpuIdentity(item.Key).Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    watts = item.Value;
                    return true;
                }
            }
            return false;
        }

        internal static string NormalizeGpuIdentity(string name)
        {
            string text = HardwareDetector.NormalizeHardwareName(name);
            string[] remove = { "nvidia", "geforce", "amd", "radeon", "intel", "graphics", "graphic", "laptop", "mobile", "gpu" };
            foreach (string word in remove)
                text = (" " + text + " ").Replace(" " + word + " ", " ");
            while (text.Contains("  "))
                text = text.Replace("  ", " ");
            return text.Trim();
        }

        private void RefreshNvidiaPowersIfNeeded()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastNvidiaRead).TotalSeconds < 2)
                return;
            _lastNvidiaRead = now;
            Dictionary<string, double> next = NvmlReader.ReadPowerWatts();
            now = DateTime.UtcNow;
            if (next.Count == 0 && (now - _lastNvidiaSmiRead).TotalSeconds >= 10)
            {
                _lastNvidiaSmiRead = now;
                next = NvidiaSmiReader.ReadPowerWatts();
            }
            if (next.Count > 0)
            {
                _lastNvidiaWatts = next;
                _lastNvidiaSuccess = DateTime.UtcNow;
            }
            else if (!ShouldKeepMeasuredGpuCache(_lastNvidiaSuccess, DateTime.UtcNow, 15))
                _lastNvidiaWatts.Clear();
        }

        private ComponentPower EstimateMemoryPower()
        {
            double watts = EstimateMemoryWatts(_inventory);
            return NewComponent("Memory", watts, PowerSourceKind.Estimated);
        }

        internal static double EstimateMemoryWatts(HardwareInventory inventory)
        {
            double memoryGb = Math.Max(0, inventory.MemoryGb);
            double modules = inventory.MemoryModules <= 0 ? Math.Max(1, Math.Ceiling(memoryGb / 16.0)) : inventory.MemoryModules;
            double perGb = 0.11;
            double perModule = 0.9;

            if (inventory.MemoryIsLowPower)
            {
                perGb = 0.055;
                perModule = 0.35;
            }
            else if (inventory.MemoryType == 34)
            {
                perGb = 0.105;
                perModule = 1.15;
            }
            else if (inventory.MemoryType == 26)
            {
                perGb = 0.10;
                perModule = 0.9;
            }
            else if (inventory.MemoryType == 24)
            {
                perGb = 0.12;
                perModule = 1.0;
            }

            if (!inventory.MemoryIsLowPower && inventory.MemorySpeedMhz >= 6000)
                perModule += 0.35;
            else if (!inventory.MemoryIsLowPower && inventory.MemorySpeedMhz >= 4800)
                perModule += 0.15;

            double minimum = inventory.MemoryIsLowPower ? 1.0 : 2.0;
            return Math.Max(minimum, memoryGb * perGb + modules * perModule);
        }

        private ComponentPower EstimateStoragePower()
        {
            return NewComponent("Storage", EstimateStorageWatts(_inventory), PowerSourceKind.Estimated);
        }

        internal static double EstimateStorageWatts(HardwareInventory inventory)
        {
            if (inventory == null || inventory.InternalDisks.Count == 0)
                return inventory != null && inventory.IsLaptop ? 1.2 : 2.0;

            double watts = 0;
            foreach (DiskInfo disk in inventory.InternalDisks)
                watts += EstimateDiskWatts(disk, inventory.IsLaptop);

            if (watts <= 0)
                watts = inventory.IsLaptop ? 1.2 : 2.0;
            return watts;
        }

        internal static double EstimateDiskWatts(DiskInfo disk, bool laptop)
        {
            if (disk == null)
                return 0;

            bool nvme = disk.BusType == 17 || ContainsIgnoreCase(disk.InterfaceType, "nvme") || ContainsIgnoreCase(disk.Model, "nvme");
            bool sata = disk.BusType == 11 || ContainsIgnoreCase(disk.InterfaceType, "sata") || ContainsIgnoreCase(disk.PnpDeviceId, "ven_ata");
            bool sas = disk.BusType == 10 || ContainsIgnoreCase(disk.InterfaceType, "sas");
            bool embeddedFlash = disk.BusType == 13 || disk.BusType == 19 || ContainsIgnoreCase(disk.InterfaceType, "ufs") || ContainsIgnoreCase(disk.Model, "emmc");

            if (disk.IsSsdLike)
            {
                if (embeddedFlash)
                    return laptop ? 0.7 : 0.9;
                if (nvme)
                    return disk.SizeBytes >= Terabytes(2) ? (laptop ? 2.6 : 3.5) : (laptop ? 2.1 : 3.0);
                if (sata)
                    return laptop ? 1.1 : 1.5;
                return laptop ? 1.4 : 2.0;
            }

            if (sas)
                return disk.SizeBytes >= Terabytes(4) ? 8.0 : 6.5;
            if (laptop)
                return disk.SizeBytes >= Terabytes(2) ? 3.0 : 2.5;
            return disk.SizeBytes >= Terabytes(4) ? 7.0 : 5.5;
        }

        private static long Terabytes(int value)
        {
            return (long)value * 1024L * 1024L * 1024L * 1024L;
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return (value ?? "").IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private ComponentPower EstimatePlatformPower()
        {
            double watts = _inventory.IsLaptop ? 8.0 : 24.0;
            if (!_inventory.IsLaptop)
                watts += 6.0; // case fans, pumps, internal lighting, board controllers
            return NewComponent("Board", watts, PowerSourceKind.Defaulted);
        }

        private static string BuildSummary(PowerSample sample)
        {
            int measured = 0;
            int estimated = 0;
            foreach (ComponentPower component in sample.Components)
            {
                if (component.Source == PowerSourceKind.Measured)
                    measured++;
                else
                    estimated++;
            }
            return measured.ToString(CultureInfo.InvariantCulture) + " measured, " +
                   estimated.ToString(CultureInfo.InvariantCulture) + " estimated";
        }

        private static ComponentPower NewComponent(string name, double watts, PowerSourceKind source)
        {
            ComponentPower component = new ComponentPower();
            component.Name = name;
            component.Watts = SafeWatts(watts);
            component.Source = source;
            return component;
        }

        internal static double SafeWatts(double watts)
        {
            if (double.IsNaN(watts) || double.IsInfinity(watts) || watts < 0)
                return 0;
            return watts;
        }

        private static PerformanceCounter CreateCounter(string category, string counter, string instance)
        {
            try
            {
                if (!PerformanceCounterCategory.Exists(category))
                    return null;
                return new PerformanceCounter(category, counter, instance, true);
            }
            catch
            {
                return null;
            }
        }

        private static PerformanceCounter CreateCpuPackagePowerCounter()
        {
            try
            {
                if (!PerformanceCounterCategory.Exists("Energy Meter"))
                    return null;
                PerformanceCounterCategory category = new PerformanceCounterCategory("Energy Meter");
                string[] names = category.GetInstanceNames();
                foreach (string name in names)
                {
                    string lower = name.ToLowerInvariant();
                    if ((lower.Contains("_pkg") || lower.EndsWith("pkg", StringComparison.Ordinal)) && !lower.Contains("_core"))
                        return new PerformanceCounter("Energy Meter", "Power", name, true);
                }
                foreach (string name in names)
                {
                    string lower = name.ToLowerInvariant();
                    if ((lower.Contains("package") || lower.Contains("pkg")) && !lower.Contains("_core"))
                        return new PerformanceCounter("Energy Meter", "Power", name, true);
                }
            }
            catch
            {
            }
            return null;
        }

        private static void DisposeCounter(PerformanceCounter counter)
        {
            try
            {
                if (counter != null)
                    counter.Dispose();
            }
            catch
            {
            }
        }
    }

    public sealed class GpuEngineUtilization : IDisposable
    {
        private readonly List<PerformanceCounter> _counters = new List<PerformanceCounter>();
        private DateTime _lastRefresh = DateTime.MinValue;
        private DateTime _lastRead = DateTime.MinValue;
        private double _lastValue;

        public double ReadTotalUtilizationPercent()
        {
            if ((DateTime.UtcNow - _lastRead).TotalSeconds < 3)
                return _lastValue;

            RefreshIfNeeded();
            double total = 0;
            foreach (PerformanceCounter counter in _counters)
            {
                try
                {
                    total += counter.NextValue();
                }
                catch
                {
                }
            }
            if (total < 0)
                total = 0;
            if (total > 100)
                total = 100;
            _lastValue = total;
            _lastRead = DateTime.UtcNow;
            return _lastValue;
        }

        public void Dispose()
        {
            foreach (PerformanceCounter counter in _counters)
            {
                try { counter.Dispose(); }
                catch { }
            }
            _counters.Clear();
        }

        private void RefreshIfNeeded()
        {
            if ((DateTime.UtcNow - _lastRefresh).TotalSeconds < 15 && _counters.Count > 0)
                return;
            _lastRefresh = DateTime.UtcNow;
            Dispose();
            try
            {
                if (!PerformanceCounterCategory.Exists("GPU Engine"))
                    return;
                PerformanceCounterCategory category = new PerformanceCounterCategory("GPU Engine");
                foreach (string instance in category.GetInstanceNames())
                {
                    string lower = instance.ToLowerInvariant();
                    if (lower.Contains("engtype_3d") || lower.Contains("engtype_compute"))
                    {
                        PerformanceCounter counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                        counter.NextValue();
                        _counters.Add(counter);
                    }
                }
            }
            catch
            {
            }
        }
    }

    public static class NvidiaSmiReader
    {
        public static Dictionary<string, double> ReadPowerWatts()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "nvidia-smi.exe";
                psi.Arguments = "--query-gpu=name,power.draw --format=csv,noheader,nounits";
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    if (!process.WaitForExit(1500))
                    {
                        try { process.Kill(); }
                        catch { }
                        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    return ParsePowerCsv(output);
                }
            }
            catch
            {
            }
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        internal static Dictionary<string, double> ParsePowerCsv(string output)
        {
            Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            using (StringReader reader = new StringReader(output ?? ""))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts.Length < 2)
                        continue;
                    string name = parts[0].Trim();
                    if (name.Length == 0)
                        continue;
                    double watts;
                    if (double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out watts) && watts > 0 && watts < 1200)
                        values[name] = watts;
                }
            }
            return values;
        }
    }

    public static class NvmlReader
    {
        private const int NvmlSuccess = 0;
        private static readonly object Gate = new object();
        private static bool _initAttempted;
        private static bool _available;
        private static readonly List<IntPtr> Devices = new List<IntPtr>();
        private static readonly Dictionary<IntPtr, string> Names = new Dictionary<IntPtr, string>();

        public static Dictionary<string, double> ReadPowerWatts()
        {
            Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (!EnsureInitialized())
                return values;

            lock (Gate)
            {
                foreach (IntPtr device in Devices)
                {
                    try
                    {
                        uint milliwatts = 0;
                        int result = nvmlDeviceGetPowerUsage(device, ref milliwatts);
                        if (result == NvmlSuccess && milliwatts > 0)
                            values[Names[device]] = milliwatts / 1000.0;
                    }
                    catch
                    {
                    }
                }
            }
            return values;
        }

        private static bool EnsureInitialized()
        {
            lock (Gate)
            {
                if (_initAttempted)
                    return _available;
                _initAttempted = true;
                try
                {
                    TryPreloadNvmlLibrary();
                    if (nvmlInit_v2() != NvmlSuccess)
                        return false;
                    uint count = 0;
                    if (nvmlDeviceGetCount_v2(ref count) != NvmlSuccess || count == 0)
                        return false;
                    for (uint i = 0; i < count; i++)
                    {
                        IntPtr handle;
                        if (nvmlDeviceGetHandleByIndex_v2(i, out handle) != NvmlSuccess || handle == IntPtr.Zero)
                            continue;
                        StringBuilder name = new StringBuilder(96);
                        if (nvmlDeviceGetName(handle, name, (uint)name.Capacity) != NvmlSuccess)
                            name.Append("NVIDIA GPU ").Append(i.ToString(CultureInfo.InvariantCulture));
                        Devices.Add(handle);
                        Names[handle] = name.ToString();
                    }
                    _available = Devices.Count > 0;
                }
                catch
                {
                    _available = false;
                }
                return _available;
            }
        }

        internal static string[] CandidateNvmlPaths()
        {
            List<string> paths = new List<string>();
            AddCandidate(paths, Path.Combine(Environment.SystemDirectory, "nvml.dll"));
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string programW6432 = Environment.GetEnvironmentVariable("ProgramW6432") ?? "";
            AddCandidate(paths, Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvml.dll"));
            AddCandidate(paths, Path.Combine(programFiles, "NVIDIA Corporation", "NVML", "nvml.dll"));
            AddCandidate(paths, Path.Combine(programFilesX86, "NVIDIA Corporation", "NVSMI", "nvml.dll"));
            AddCandidate(paths, Path.Combine(programW6432, "NVIDIA Corporation", "NVSMI", "nvml.dll"));
            return paths.ToArray();
        }

        private static void TryPreloadNvmlLibrary()
        {
            foreach (string path in CandidateNvmlPaths())
            {
                try
                {
                    if (!File.Exists(path))
                        continue;
                    if (LoadLibrary(path) != IntPtr.Zero)
                        return;
                }
                catch
                {
                }
            }
        }

        private static void AddCandidate(List<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            foreach (string existing in paths)
            {
                if (existing.Equals(path, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            paths.Add(path);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetCount_v2(ref uint deviceCount);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerUsage(IntPtr device, ref uint power);
    }

    public sealed class HistoryStore
    {
        private readonly string _path;
        private readonly int _retentionDays;
        private readonly object _gate = new object();
        private DateTime _currentMinute = DateTime.MinValue;
        private int _minuteCount;
        private double _minuteWattsSum;
        private double _minuteMinWatts;
        private double _minuteMaxWatts;
        private double _minuteEnergyKWh;

        public HistoryStore(int retentionDays)
        {
            _retentionDays = retentionDays;
            _path = Path.Combine(AppSettings.AppFolder, "history.csv");
            try
            {
                Directory.CreateDirectory(AppSettings.AppFolder);
            }
            catch
            {
            }
            PurgeOldRows();
        }

        public Totals LoadTotals(DateTime now)
        {
            lock (_gate)
            {
                Totals totals = new Totals();
                try
                {
                    if (!File.Exists(_path))
                        return totals;

                    DateTime today = now.Date;
                    DateTime month = new DateTime(now.Year, now.Month, 1);
                    foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
                    {
                        HistoryRow row;
                        if (!TryParseRow(line, out row))
                            continue;
                        if (row.Minute > now.AddMinutes(1))
                            continue;
                        if (row.Minute >= today)
                            totals.TodayKWh += row.EnergyKWh;
                        if (row.Minute >= month)
                            totals.MonthKWh += row.EnergyKWh;
                    }
                }
                catch
                {
                }
                return totals;
            }
        }

        public void AddSample(DateTime timestamp, double watts, double energyKWh)
        {
            lock (_gate)
                AddSampleLocked(timestamp, PowerEstimator.SafeWatts(watts), SafeEnergy(energyKWh));
        }

        private void AddSampleLocked(DateTime timestamp, double watts, double energyKWh)
        {
            DateTime minute = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0);
            if (_currentMinute == DateTime.MinValue)
                StartMinute(minute, watts, energyKWh);
            else if (minute != _currentMinute)
            {
                FlushLocked();
                StartMinute(minute, watts, energyKWh);
            }
            else
            {
                _minuteCount++;
                _minuteWattsSum += watts;
                _minuteEnergyKWh += energyKWh;
                if (watts < _minuteMinWatts)
                    _minuteMinWatts = watts;
                if (watts > _minuteMaxWatts)
                    _minuteMaxWatts = watts;
            }
        }

        public void AddInterval(DateTime start, DateTime end, double watts, double maxIntervalSeconds)
        {
            lock (_gate)
            {
                if (start == DateTime.MinValue || end <= start)
                    return;
                watts = PowerEstimator.SafeWatts(watts);
                if (watts <= 0)
                    return;
                double totalSeconds = (end - start).TotalSeconds;
                if (totalSeconds <= 0 || totalSeconds > maxIntervalSeconds)
                    return;

                DateTime cursor = start;
                while (cursor < end)
                {
                    DateTime nextMinute = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, cursor.Minute, 0).AddMinutes(1);
                    DateTime segmentEnd = nextMinute < end ? nextMinute : end;
                    double seconds = (segmentEnd - cursor).TotalSeconds;
                    if (seconds <= 0)
                        break;
                    double energy = watts * (seconds / 3600.0) / 1000.0;
                    AddSampleLocked(cursor, watts, energy);
                    cursor = segmentEnd;
                }
            }
        }

        public void Flush()
        {
            lock (_gate)
                FlushLocked();
        }

        private void FlushLocked()
        {
            if (_currentMinute == DateTime.MinValue || _minuteCount <= 0)
                return;
            try
            {
                string line = _currentMinute.ToString("o", CultureInfo.InvariantCulture) + "," +
                              (_minuteWattsSum / _minuteCount).ToString("F3", CultureInfo.InvariantCulture) + "," +
                              _minuteMinWatts.ToString("F3", CultureInfo.InvariantCulture) + "," +
                              _minuteMaxWatts.ToString("F3", CultureInfo.InvariantCulture) + "," +
                              _minuteEnergyKWh.ToString("F8", CultureInfo.InvariantCulture);
                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
            _currentMinute = DateTime.MinValue;
        }

        private static double SafeEnergy(double energyKWh)
        {
            if (double.IsNaN(energyKWh) || double.IsInfinity(energyKWh) || energyKWh < 0)
                return 0;
            return energyKWh;
        }

        private void StartMinute(DateTime minute, double watts, double energyKWh)
        {
            _currentMinute = minute;
            _minuteCount = 1;
            _minuteWattsSum = watts;
            _minuteMinWatts = watts;
            _minuteMaxWatts = watts;
            _minuteEnergyKWh = energyKWh;
        }

        private void PurgeOldRows()
        {
            try
            {
                if (!File.Exists(_path))
                    return;
                DateTime cutoff = DateTime.Now.Date.AddDays(-_retentionDays);
                List<string> keep = new List<string>();
                foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
                {
                    HistoryRow row;
                    if (TryParseRow(line, out row) && row.Minute >= cutoff && row.Minute <= DateTime.Now.AddMinutes(1))
                        keep.Add(line);
                }
                File.WriteAllLines(_path, keep.ToArray(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static bool TryParseRow(string line, out HistoryRow row)
        {
            row = new HistoryRow();
            if (string.IsNullOrWhiteSpace(line))
                return false;
            string[] parts = line.Split(',');
            if (parts.Length < 5)
                return false;
            DateTime minute;
            double energy;
            if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out minute))
                return false;
            if (!double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out energy))
                return false;
            if (double.IsNaN(energy) || double.IsInfinity(energy) || energy < 0 || energy > 0.05)
                return false;
            row.Minute = minute;
            row.EnergyKWh = energy;
            return true;
        }

        private struct HistoryRow
        {
            public DateTime Minute;
            public double EnergyKWh;
        }
    }

    public sealed class Totals
    {
        public double TodayKWh;
        public double MonthKWh;
    }

    internal struct EnergyTotalsUpdate
    {
        public double TodayKWh;
        public double MonthKWh;
        public double EnergyKWh;
    }

    internal static class EnergyAccumulator
    {
        public static EnergyTotalsUpdate Apply(DateTime previous, DateTime now, double watts, double currentTodayKWh, double currentMonthKWh, double maxIntervalSeconds)
        {
            EnergyTotalsUpdate result = new EnergyTotalsUpdate();
            result.TodayKWh = currentTodayKWh;
            result.MonthKWh = currentMonthKWh;

            if (previous == DateTime.MinValue)
                return result;
            if (now <= previous)
                return result;

            if (previous.Date != now.Date)
                result.TodayKWh = 0;
            if (previous.Year != now.Year || previous.Month != now.Month)
                result.MonthKWh = 0;

            double dtSeconds = (now - previous).TotalSeconds;
            if (dtSeconds <= 0 || dtSeconds > maxIntervalSeconds)
                return result;

            result.EnergyKWh = watts * (dtSeconds / 3600.0) / 1000.0;
            result.TodayKWh += watts * (SecondsInPeriod(previous, now, now.Date) / 3600.0) / 1000.0;
            DateTime monthStart = new DateTime(now.Year, now.Month, 1);
            result.MonthKWh += watts * (SecondsInPeriod(previous, now, monthStart) / 3600.0) / 1000.0;
            return result;
        }

        private static double SecondsInPeriod(DateTime start, DateTime end, DateTime periodStart)
        {
            DateTime effectiveStart = start > periodStart ? start : periodStart;
            if (end <= effectiveStart)
                return 0;
            return (end - effectiveStart).TotalSeconds;
        }
    }

    internal static class HighPowerAlertPolicy
    {
        public static bool ShouldNotify(bool enabled, double watts, double thresholdWatts, DateTime now, DateTime lastNotification, bool alreadyInHighPowerState, double cooldownMinutes)
        {
            if (!enabled)
                return false;
            watts = PowerEstimator.SafeWatts(watts);
            if (double.IsNaN(thresholdWatts) || double.IsInfinity(thresholdWatts) || thresholdWatts < 1)
                return false;
            if (watts < thresholdWatts)
                return false;
            if (alreadyInHighPowerState)
                return false;
            if (lastNotification == DateTime.MinValue)
                return true;
            if (now < lastNotification)
                return true;
            return (now - lastNotification).TotalMinutes >= Math.Max(1, cooldownMinutes);
        }

        public static bool IsHighPowerState(double watts, double thresholdWatts)
        {
            watts = PowerEstimator.SafeWatts(watts);
            if (double.IsNaN(thresholdWatts) || double.IsInfinity(thresholdWatts) || thresholdWatts < 1)
                return false;
            return watts >= thresholdWatts * 0.90;
        }
    }

    public sealed class MonitorService : IDisposable
    {
        private readonly PowerEstimator _estimator;
        private readonly HistoryStore _history;
        private readonly AppSettings _settings;
        private Timer _timer;
        private readonly object _gate = new object();
        private int _tickInProgress;
        private int _runVersion;
        private DateTime _lastTimestamp = DateTime.MinValue;
        private double _todayKWh;
        private double _monthKWh;
        private bool _running;

        public event Action<PowerSample> SampleReady;

        public MonitorService(PowerEstimator estimator, HistoryStore history, AppSettings settings)
        {
            _estimator = estimator;
            _history = history;
            _settings = settings;
            Totals totals = history.LoadTotals(DateTime.Now);
            _todayKWh = totals.TodayKWh;
            _monthKWh = totals.MonthKWh;
        }

        public bool IsRunning
        {
            get { lock (_gate) return _running; }
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_running)
                    return;
                _running = true;
                _runVersion++;
                _lastTimestamp = DateTime.MinValue;
                _timer = new Timer(Tick, null, 100, Math.Max(1, _settings.SampleSeconds) * 1000);
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                _running = false;
                _runVersion++;
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
            }
            _history.Flush();
        }

        public void Dispose()
        {
            Stop();
        }

        private double MaxEnergyIntervalSeconds
        {
            get
            {
                return Math.Max(10.0, Math.Min(30.0, Math.Max(1, _settings.SampleSeconds) * 3.0));
            }
        }

        private void Tick(object state)
        {
            if (Interlocked.Exchange(ref _tickInProgress, 1) == 1)
                return;

            try
            {
                int capturedVersion;
                lock (_gate)
                {
                    if (!_running)
                        return;
                    capturedVersion = _runVersion;
                }

                DateTime now = DateTime.Now;
                PowerSample sample = _estimator.ReadSample(now);
                DateTime previousTimestamp;
                lock (_gate)
                {
                    if (!IsCurrentRunSnapshot(_running, capturedVersion, _runVersion))
                        return;

                    previousTimestamp = _lastTimestamp;
                    double maxEnergyIntervalSeconds = MaxEnergyIntervalSeconds;
                    EnergyTotalsUpdate totals = EnergyAccumulator.Apply(previousTimestamp, now, sample.TotalWatts, _todayKWh, _monthKWh, maxEnergyIntervalSeconds);
                    _lastTimestamp = now;
                    _todayKWh = totals.TodayKWh;
                    _monthKWh = totals.MonthKWh;
                    sample.TodayKWh = _todayKWh;
                    sample.MonthKWh = _monthKWh;
                    sample.TodayCost = _todayKWh * _settings.ElectricityRate;
                    sample.MonthCost = _monthKWh * _settings.ElectricityRate;
                    if (totals.EnergyKWh > 0)
                        _history.AddInterval(previousTimestamp, now, sample.TotalWatts, maxEnergyIntervalSeconds);
                }

                lock (_gate)
                {
                    if (!IsCurrentRunSnapshot(_running, capturedVersion, _runVersion))
                        return;
                }

                Action<PowerSample> handler = SampleReady;
                if (handler != null)
                    handler(sample);
            }
            finally
            {
                Interlocked.Exchange(ref _tickInProgress, 0);
            }
        }

        internal static bool IsCurrentRunSnapshot(bool running, int capturedVersion, int currentVersion)
        {
            return running && capturedVersion == currentVersion;
        }
    }
}
