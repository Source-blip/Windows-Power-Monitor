using System;
using System.Collections.Generic;
using System.Globalization;
using HostPowerMonitor;

internal static class ModelSanityRunner
{
    private sealed class Scenario
    {
        public string Name;
        public HardwareInventory Inventory;
        public double ExpectedCpuMin;
        public double ExpectedCpuMax;
        public double ExpectedGpuMin;
        public double ExpectedGpuMax;
        public int ExpectedInternalDisks;
    }

    private static int Main()
    {
        List<Scenario> scenarios = new List<Scenario>();
        scenarios.Add(DesktopNvidia());
        scenarios.Add(DesktopAmd());
        scenarios.Add(LaptopIntel());
        scenarios.Add(WorkstationHighEnd());
        scenarios.Add(NoGpuOfficeDesktop());
        scenarios.Add(DesktopBlackwellNvidia());
        scenarios.Add(DesktopRdna4Amd());
        scenarios.Add(LaptopNvidiaDgpu());
        scenarios.Add(IntelArcDesktop());

        int failures = 0;
        foreach (Scenario scenario in scenarios)
        {
            double cpuMax = HardwareDetector.EstimateCpuMaxWatts(scenario.Inventory);
            double gpuMax = 0;
            foreach (GpuInfo gpu in scenario.Inventory.Gpus)
                gpuMax += gpu.EstimatedMaxWatts;

            if (!Between(cpuMax, scenario.ExpectedCpuMin, scenario.ExpectedCpuMax))
            {
                Console.WriteLine("FAIL " + scenario.Name + " cpuMax=" + cpuMax.ToString("0.0", CultureInfo.InvariantCulture));
                failures++;
            }
            if (!Between(gpuMax, scenario.ExpectedGpuMin, scenario.ExpectedGpuMax))
            {
                Console.WriteLine("FAIL " + scenario.Name + " gpuMax=" + gpuMax.ToString("0.0", CultureInfo.InvariantCulture));
                failures++;
            }
            if (scenario.Inventory.InternalDisks.Count != scenario.ExpectedInternalDisks)
            {
                Console.WriteLine("FAIL " + scenario.Name + " disks=" + scenario.Inventory.InternalDisks.Count.ToString(CultureInfo.InvariantCulture));
                failures++;
            }

            Console.WriteLine("SCENARIO " + scenario.Name +
                              " cpuMax=" + cpuMax.ToString("0.0", CultureInfo.InvariantCulture) +
                              " gpuMax=" + gpuMax.ToString("0.0", CultureInfo.InvariantCulture) +
                              " disks=" + scenario.Inventory.InternalDisks.Count.ToString(CultureInfo.InvariantCulture));
        }

        failures += RunDiskFilterTests();
        failures += RunGpuFallbackTests();
        failures += RunNvidiaReaderTests();
        failures += RunNvmlPathTests();
        failures += RunCpuFallbackCacheTests();
        failures += RunMemoryModelTests();
        failures += RunStorageModelTests();
        failures += RunSettingsModelTests();
        failures += RunHighPowerAlertTests();
        failures += RunNumericSafetyTests();
        failures += RunMonitorLifecycleTests();

        Console.WriteLine(failures == 0 ? "RESULT PASS" : "RESULT FAIL");
        return failures == 0 ? 0 : 1;
    }

    private static Scenario DesktopNvidia()
    {
        HardwareInventory inv = BaseDesktop("AMD Ryzen 5 9600X 6-Core Processor", 32, 2);
        inv.Gpus.Add(Gpu("NVIDIA GeForce RTX 5060 Ti", "NVIDIA", false));
        inv.Gpus.Add(Gpu("AMD Radeon(TM) Graphics", "Advanced Micro Devices, Inc.", true));
        inv.InternalDisks.Add(Disk("Samsung SSD 990 PRO 2TB", "NVMe", "SSD"));
        return S("desktop-nvidia-ryzen", inv, 80, 100, 190, 210, 1);
    }

    private static Scenario DesktopAmd()
    {
        HardwareInventory inv = BaseDesktop("AMD Ryzen 7 7800X3D 8-Core Processor", 32, 2);
        inv.Gpus.Add(Gpu("AMD Radeon RX 7800 XT", "Advanced Micro Devices, Inc.", false));
        inv.InternalDisks.Add(Disk("WD_BLACK SN850X", "NVMe", "SSD"));
        return S("desktop-amd-radeon", inv, 100, 130, 250, 275, 1);
    }

    private static Scenario LaptopIntel()
    {
        HardwareInventory inv = BaseLaptop("Intel(R) Core(TM) Ultra 7 155H", 16, 2);
        inv.Gpus.Add(Gpu("Intel(R) Arc(TM) Graphics", "Intel Corporation", true));
        inv.InternalDisks.Add(Disk("KIOXIA NVMe", "NVMe", "SSD"));
        return S("laptop-intel-igpu", inv, 20, 60, 10, 30, 1);
    }

    private static Scenario WorkstationHighEnd()
    {
        HardwareInventory inv = BaseDesktop("Intel(R) Core(TM) i9-14900K", 64, 4);
        inv.Gpus.Add(Gpu("NVIDIA GeForce RTX 4090", "NVIDIA", false));
        inv.InternalDisks.Add(Disk("Crucial T700", "NVMe", "SSD"));
        inv.InternalDisks.Add(Disk("ST8000DM004", "SATA", "Fixed hard disk media"));
        return S("workstation-high-end", inv, 240, 265, 430, 470, 2);
    }

    private static Scenario NoGpuOfficeDesktop()
    {
        HardwareInventory inv = BaseDesktop("Intel(R) Core(TM) i5-12400", 16, 2);
        inv.Gpus.Add(Gpu("Intel(R) UHD Graphics 730", "Intel Corporation", true));
        inv.InternalDisks.Add(Disk("KINGSTON SA400", "SATA", "SSD"));
        return S("office-desktop-igpu", inv, 70, 90, 10, 30, 1);
    }

    private static Scenario DesktopBlackwellNvidia()
    {
        HardwareInventory inv = BaseDesktop("AMD Ryzen 9 9950X 16-Core Processor", 64, 2);
        inv.Gpus.Add(Gpu("NVIDIA GeForce RTX 5090", "NVIDIA", false));
        inv.InternalDisks.Add(Disk("Samsung SSD 990 PRO 4TB", "NVMe", "SSD"));
        return S("desktop-rtx-5090", inv, 160, 180, 560, 590, 1);
    }

    private static Scenario DesktopRdna4Amd()
    {
        HardwareInventory inv = BaseDesktop("AMD Ryzen 7 9700X 8-Core Processor", 32, 2);
        inv.Gpus.Add(Gpu("AMD Radeon RX 9070 XT", "Advanced Micro Devices, Inc.", false));
        inv.InternalDisks.Add(Disk("WD_BLACK SN850X", "NVMe", "SSD"));
        return S("desktop-rx-9070-xt", inv, 95, 115, 295, 315, 1);
    }

    private static Scenario LaptopNvidiaDgpu()
    {
        HardwareInventory inv = BaseLaptop("Intel(R) Core(TM) i7-13700HX", 32, 2);
        inv.Gpus.Add(Gpu("NVIDIA GeForce RTX 4070 Laptop GPU", "NVIDIA", false, true));
        inv.Gpus.Add(Gpu("Intel(R) UHD Graphics", "Intel Corporation", true, true));
        inv.InternalDisks.Add(Disk("SK hynix PC801 NVMe", "NVMe", "SSD"));
        return S("laptop-rtx-4070", inv, 80, 100, 125, 140, 1);
    }

    private static Scenario IntelArcDesktop()
    {
        HardwareInventory inv = BaseDesktop("Intel(R) Core(TM) Ultra 5 245K", 32, 2);
        inv.Gpus.Add(Gpu("Intel(R) Arc(TM) B580 Graphics", "Intel Corporation", false));
        inv.InternalDisks.Add(Disk("Crucial P5 Plus", "NVMe", "SSD"));
        return S("desktop-arc-b580", inv, 240, 260, 180, 200, 1);
    }

    private static HardwareInventory BaseDesktop(string cpu, double memoryGb, int modules)
    {
        HardwareInventory inv = new HardwareInventory();
        inv.OperatingSystem = "Windows 10 Pro";
        inv.CpuName = cpu;
        inv.CpuCores = 8;
        inv.CpuLogicalProcessors = 16;
        inv.MemoryGb = memoryGb;
        inv.MemoryModules = modules;
        inv.IsLaptop = false;
        return inv;
    }

    private static HardwareInventory BaseLaptop(string cpu, double memoryGb, int modules)
    {
        HardwareInventory inv = BaseDesktop(cpu, memoryGb, modules);
        inv.IsLaptop = true;
        return inv;
    }

    private static GpuInfo Gpu(string name, string vendor, bool integrated)
    {
        return Gpu(name, vendor, integrated, false);
    }

    private static GpuInfo Gpu(string name, string vendor, bool integrated, bool laptop)
    {
        GpuInfo gpu = new GpuInfo();
        gpu.Name = name;
        gpu.Vendor = vendor;
        gpu.IsIntegrated = integrated;
        gpu.EstimatedMaxWatts = HardwareDetector.EstimateGpuMaxWatts(name, vendor, integrated, laptop);
        return gpu;
    }

    private static DiskInfo Disk(string model, string interfaceType, string mediaType)
    {
        return Disk(model, interfaceType, mediaType, "", -1);
    }

    private static DiskInfo Disk(string model, string interfaceType, string mediaType, string pnpDeviceId, int busType)
    {
        return Disk(model, interfaceType, mediaType, pnpDeviceId, busType, Gigabytes(512));
    }

    private static DiskInfo Disk(string model, string interfaceType, string mediaType, string pnpDeviceId, int busType, long sizeBytes)
    {
        DiskInfo disk = new DiskInfo();
        disk.Model = model;
        disk.InterfaceType = interfaceType;
        disk.MediaType = mediaType;
        disk.PnpDeviceId = pnpDeviceId;
        disk.BusType = busType;
        disk.SizeBytes = sizeBytes;
        string modelLower = model.ToLowerInvariant();
        string interfaceLower = interfaceType.ToLowerInvariant();
        string mediaLower = mediaType.ToLowerInvariant();
        disk.IsSsdLike = mediaLower.Contains("ssd") || interfaceLower.Contains("nvme") ||
                         modelLower.Contains("emmc") || modelLower.Contains("ufs") ||
                         mediaLower.Contains("emmc") || mediaLower.Contains("ufs") ||
                         interfaceLower.Contains("ufs") || busType == 13 || busType == 19;
        return disk;
    }

    private static int RunDiskFilterTests()
    {
        int failures = 0;
        failures += CheckDisk("internal-nvme", Disk("Samsung SSD 990 PRO 2TB", "NVMe", "SSD", "SCSI\\DISK&VEN_NVME&PROD_SAMSUNG", 17), true);
        failures += CheckDisk("internal-sata-hdd", Disk("ST8000DM004", "SATA", "Fixed hard disk media", "SCSI\\DISK&VEN_ATA&PROD_ST8000DM004", 11), true);
        failures += CheckDisk("usb-direct", Disk("WD My Passport 2627 USB Device", "USB", "Fixed hard disk media", "USBSTOR\\DISK&VEN_WD", 7), false);
        failures += CheckDisk("usb-uasp-portable", Disk("SanDisk Extreme 1TB X3N", "SCSI", "Fixed hard disk media", "SCSI\\DISK&VEN_NVME&PROD_SANDISK_EXTREME", -1), false);
        failures += CheckDisk("virtual-disk", Disk("Msft Virtual Disk", "SCSI", "Fixed hard disk media", "SCSI\\DISK&VEN_MSFT&PROD_VIRTUAL_DISK", 14), false);
        failures += CheckDisk("internal-emmc", Disk("Samsung BJTD4R eMMC", "MMC", "Fixed hard disk media", "SCSI\\DISK&VEN_SAMSUNG&PROD_BJTD4R", 13), true);
        failures += CheckDisk("removable-mmc-card", Disk("Generic MMC Card", "MMC", "Removable media", "SCSI\\DISK&VEN_GENERIC&PROD_MMC_CARD", 13), false);
        failures += CheckDisk("sd-card", Disk("Generic SD Card", "SD", "Removable media", "SCSI\\DISK&VEN_GENERIC&PROD_SD_CARD", 12), false);
        return failures;
    }

    private static int CheckDisk(string name, DiskInfo disk, bool expected)
    {
        bool actual = HardwareDetector.ShouldCountDisk(disk);
        Console.WriteLine("DISKCASE " + name + " count=" + actual.ToString(CultureInfo.InvariantCulture));
        if (actual == expected)
            return 0;
        Console.WriteLine("FAIL " + name + " expectedCount=" + expected.ToString(CultureInfo.InvariantCulture));
        return 1;
    }

    private static int RunGpuFallbackTests()
    {
        int failures = 0;
        GpuInfo integrated = Gpu("AMD Radeon(TM) Graphics", "Advanced Micro Devices, Inc.", true);
        GpuInfo rtx4060 = Gpu("NVIDIA GeForce RTX 4060", "NVIDIA", false);
        GpuInfo rtx4070 = Gpu("NVIDIA GeForce RTX 4070", "NVIDIA", false);

        failures += CheckUtil("igpu-with-measured-dgpu", PowerEstimator.SelectGpuFallbackUtilization(integrated, 80, true, 0, 1), 0);
        failures += CheckUtil("dgpu-unmeasured-hybrid", PowerEstimator.SelectGpuFallbackUtilization(rtx4060, 80, false, 1, 1), 80);
        failures += CheckUtil("igpu-with-unmeasured-dgpu", PowerEstimator.SelectGpuFallbackUtilization(integrated, 80, false, 1, 1), 0);
        failures += CheckUtil("two-unmeasured-dgpus", PowerEstimator.SelectGpuFallbackUtilization(rtx4070, 80, false, 2, 0), 40);
        failures += CheckUtil("igpu-only", PowerEstimator.SelectGpuFallbackUtilization(integrated, 80, false, 0, 1), 50);

        double idleIntegrated = PowerEstimator.EstimateGpuWatts(integrated, 0);
        Console.WriteLine("GPUCASE igpu-idle-watts watts=" + idleIntegrated.ToString("0.0", CultureInfo.InvariantCulture));
        if (!Between(idleIntegrated, 2.0, 3.0))
        {
            Console.WriteLine("FAIL igpu-idle-watts");
            failures++;
        }

        return failures;
    }

    private static int CheckUtil(string name, double actual, double expected)
    {
        Console.WriteLine("GPUCASE " + name + " util=" + actual.ToString("0.0", CultureInfo.InvariantCulture));
        if (Math.Abs(actual - expected) < 0.000001)
            return 0;
        Console.WriteLine("FAIL " + name + " expectedUtil=" + expected.ToString("0.0", CultureInfo.InvariantCulture));
        return 1;
    }

    private static int RunNvidiaReaderTests()
    {
        int failures = 0;
        failures += CheckText("gpu-identity-basic",
            PowerEstimator.NormalizeGpuIdentity("NVIDIA GeForce RTX 5060 Ti"),
            PowerEstimator.NormalizeGpuIdentity("GeForce RTX 5060 Ti"));
        failures += CheckText("gpu-identity-laptop",
            PowerEstimator.NormalizeGpuIdentity("NVIDIA GeForce RTX 4070 Laptop GPU"),
            PowerEstimator.NormalizeGpuIdentity("GeForce RTX 4070"));

        string csv = "NVIDIA GeForce RTX 5060 Ti, 182.34" + Environment.NewLine +
                     "NVIDIA GeForce RTX 4070, [Not Supported]" + Environment.NewLine +
                     "NVIDIA GeForce RTX 4090, 450" + Environment.NewLine +
                     "Broken line";
        Dictionary<string, double> parsed = NvidiaSmiReader.ParsePowerCsv(csv);
        failures += CheckParsedPower("nvidia-smi-5060ti", parsed, "NVIDIA GeForce RTX 5060 Ti", 182.34);
        failures += CheckParsedPower("nvidia-smi-4090", parsed, "NVIDIA GeForce RTX 4090", 450);
        if (parsed.ContainsKey("NVIDIA GeForce RTX 4070"))
        {
            Console.WriteLine("FAIL nvidia-smi-invalid-filter");
            failures++;
        }
        else
            Console.WriteLine("NVIDIACASE nvidia-smi-invalid-filter ok");

        DateTime now = new DateTime(2026, 6, 1, 12, 0, 0);
        failures += CheckBool("gpu-cache-fresh", PowerEstimator.ShouldKeepMeasuredGpuCache(now.AddSeconds(-14), now, 15), true);
        failures += CheckBool("gpu-cache-expired", PowerEstimator.ShouldKeepMeasuredGpuCache(now.AddSeconds(-15), now, 15), false);
        failures += CheckBool("gpu-cache-empty", PowerEstimator.ShouldKeepMeasuredGpuCache(DateTime.MinValue, now, 15), false);
        return failures;
    }

    private static int CheckText(string name, string actual, string expected)
    {
        Console.WriteLine("TEXTCASE " + name + " value=" + actual);
        if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            return 0;
        Console.WriteLine("FAIL " + name + " expected=" + expected);
        return 1;
    }

    private static int CheckParsedPower(string name, Dictionary<string, double> parsed, string key, double expected)
    {
        double actual;
        if (parsed.TryGetValue(key, out actual) && Math.Abs(actual - expected) < 0.000001)
        {
            Console.WriteLine("NVIDIACASE " + name + " watts=" + actual.ToString("0.00", CultureInfo.InvariantCulture));
            return 0;
        }
        Console.WriteLine("FAIL " + name);
        return 1;
    }

    private static int RunNvmlPathTests()
    {
        int failures = 0;
        string[] paths = NvmlReader.CandidateNvmlPaths();
        bool hasSystem32 = false;
        bool hasNvsmi = false;
        Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            if (path.IndexOf("System32", StringComparison.OrdinalIgnoreCase) >= 0 && path.EndsWith("nvml.dll", StringComparison.OrdinalIgnoreCase))
                hasSystem32 = true;
            if (path.IndexOf("NVSMI", StringComparison.OrdinalIgnoreCase) >= 0 && path.EndsWith("nvml.dll", StringComparison.OrdinalIgnoreCase))
                hasNvsmi = true;
            if (seen.ContainsKey(path))
            {
                Console.WriteLine("FAIL nvml-path-duplicate " + path);
                failures++;
            }
            else
                seen[path] = true;
        }
        Console.WriteLine("NVMLCASE candidate-count count=" + paths.Length.ToString(CultureInfo.InvariantCulture));
        if (!hasSystem32)
        {
            Console.WriteLine("FAIL nvml-path-system32");
            failures++;
        }
        if (!hasNvsmi)
        {
            Console.WriteLine("FAIL nvml-path-nvsmi");
            failures++;
        }
        return failures;
    }

    private static int RunCpuFallbackCacheTests()
    {
        int failures = 0;
        DateTime now = new DateTime(2026, 6, 1, 10, 0, 0);
        failures += CheckBool("cpu-cache-fresh", PowerEstimator.IsCacheFresh(now.AddSeconds(-4), now, 5), true);
        failures += CheckBool("cpu-cache-expired", PowerEstimator.IsCacheFresh(now.AddSeconds(-5), now, 5), false);
        failures += CheckBool("cpu-cache-clock-back", PowerEstimator.IsCacheFresh(now.AddSeconds(1), now, 5), false);
        failures += CheckBool("cpu-cache-empty", PowerEstimator.IsCacheFresh(DateTime.MinValue, now, 5), false);
        double average;
        bool ok = PowerEstimator.TryAverageValidPercent(new double[] { 20, 60 }, out average);
        failures += CheckBool("cpu-average-valid", ok, true);
        failures += CheckDouble("cpu-average-value", average, 40);
        ok = PowerEstimator.TryAverageValidPercent(new double[] { -1, double.NaN, 120 }, out average);
        failures += CheckBool("cpu-average-empty", ok, false);
        failures += CheckDouble("cpu-average-empty-value", average, 0);
        return failures;
    }

    private static int RunMemoryModelTests()
    {
        int failures = 0;
        failures += CheckMemory("ddr5-high-speed", MemoryInventory(32, 2, 34, 6200, false), 6.3, 6.4);
        failures += CheckMemory("ddr4-desktop", MemoryInventory(32, 2, 26, 3200, false), 4.9, 5.1);
        failures += CheckMemory("lpddr5-laptop", MemoryInventory(16, 1, 30, 6400, true), 1.2, 1.3);
        failures += CheckMemory("unknown-memory", MemoryInventory(8, 0, 0, 0, false), 2.0, 2.1);
        return failures;
    }

    private static int RunStorageModelTests()
    {
        int failures = 0;
        failures += CheckStorage("desktop-nvme-1tb", StorageInventory(false, Disk("Samsung SSD 980 PRO", "NVMe", "SSD", "SCSI\\DISK&VEN_NVME", 17, Terabytes(1))), 3.0, 3.1);
        failures += CheckStorage("desktop-nvme-4tb", StorageInventory(false, Disk("Samsung SSD 990 PRO 4TB", "NVMe", "SSD", "SCSI\\DISK&VEN_NVME", 17, Terabytes(4))), 3.5, 3.6);
        failures += CheckStorage("desktop-sata-ssd", StorageInventory(false, Disk("KINGSTON SA400", "SATA", "SSD", "SCSI\\DISK&VEN_ATA", 11, Gigabytes(512))), 1.5, 1.6);
        failures += CheckStorage("desktop-hdd-8tb", StorageInventory(false, Disk("ST8000DM004", "SATA", "Fixed hard disk media", "SCSI\\DISK&VEN_ATA", 11, Terabytes(8))), 7.0, 7.1);
        failures += CheckStorage("laptop-hdd-1tb", StorageInventory(true, Disk("WDC WD10SPZX", "SATA", "Fixed hard disk media", "SCSI\\DISK&VEN_ATA", 11, Terabytes(1))), 2.5, 2.6);
        failures += CheckStorage("laptop-emmc", StorageInventory(true, Disk("Samsung BJTD4R eMMC", "MMC", "Fixed hard disk media", "SCSI\\DISK&VEN_SAMSUNG", 13, Gigabytes(128))), 0.7, 0.8);
        failures += CheckStorage("laptop-ufs", StorageInventory(true, Disk("KIOXIA UFS", "UFS", "Fixed hard disk media", "SCSI\\DISK&VEN_KIOXIA", 19, Gigabytes(256))), 0.7, 0.8);
        failures += CheckStorage("desktop-no-disk", StorageInventory(false), 2.0, 2.1);
        failures += CheckStorage("laptop-no-disk", StorageInventory(true), 1.2, 1.3);
        return failures;
    }

    private static int RunSettingsModelTests()
    {
        int failures = 0;
        failures += CheckText("autostart-path-spaces", AppSettings.BuildAutoStartCommand(@"C:\Program Files\HostPowerMonitor\HostPowerMonitor.exe"), @"""C:\Program Files\HostPowerMonitor\HostPowerMonitor.exe""");
        failures += CheckText("autostart-path-prequoted", AppSettings.BuildAutoStartCommand(@"""C:\Tools\HostPowerMonitor.exe"""), @"""C:\Tools\HostPowerMonitor.exe""");
        return failures;
    }

    private static int RunNumericSafetyTests()
    {
        int failures = 0;
        AppSettings settings = new AppSettings();
        settings.MarginPercent = double.NaN;
        settings.ElectricityRate = double.PositiveInfinity;
        settings.HighPowerThresholdWatts = double.NaN;
        settings.SampleSeconds = -5;
        settings.HistoryRetentionDays = 9999;
        settings.Normalize();
        failures += CheckDouble("settings-margin-nan", settings.MarginPercent, 15.0);
        failures += CheckDouble("settings-rate-infinity", settings.ElectricityRate, 0.60);
        failures += CheckDouble("settings-alert-threshold-nan", settings.HighPowerThresholdWatts, 450.0);
        failures += CheckInt("settings-sample-min", settings.SampleSeconds, 1);
        failures += CheckInt("settings-retention-max", settings.HistoryRetentionDays, 365);
        failures += CheckDouble("safe-watts-nan", PowerEstimator.SafeWatts(double.NaN), 0);
        failures += CheckDouble("safe-watts-negative", PowerEstimator.SafeWatts(-12), 0);
        failures += CheckDouble("safe-watts-normal", PowerEstimator.SafeWatts(42.5), 42.5);
        return failures;
    }

    private static int RunHighPowerAlertTests()
    {
        int failures = 0;
        DateTime now = new DateTime(2026, 6, 11, 12, 0, 0);
        failures += CheckBool("alert-disabled", HighPowerAlertPolicy.ShouldNotify(false, 600, 450, now, DateTime.MinValue, false, 10), false);
        failures += CheckBool("alert-below-threshold", HighPowerAlertPolicy.ShouldNotify(true, 300, 450, now, DateTime.MinValue, false, 10), false);
        failures += CheckBool("alert-first-hit", HighPowerAlertPolicy.ShouldNotify(true, 600, 450, now, DateTime.MinValue, false, 10), true);
        failures += CheckBool("alert-already-high", HighPowerAlertPolicy.ShouldNotify(true, 600, 450, now, DateTime.MinValue, true, 10), false);
        failures += CheckBool("alert-cooldown-active", HighPowerAlertPolicy.ShouldNotify(true, 600, 450, now, now.AddMinutes(-3), false, 10), false);
        failures += CheckBool("alert-cooldown-expired", HighPowerAlertPolicy.ShouldNotify(true, 600, 450, now, now.AddMinutes(-11), false, 10), true);
        failures += CheckBool("alert-high-state", HighPowerAlertPolicy.IsHighPowerState(410, 450), true);
        failures += CheckBool("alert-low-state", HighPowerAlertPolicy.IsHighPowerState(390, 450), false);
        return failures;
    }

    private static int RunMonitorLifecycleTests()
    {
        int failures = 0;
        failures += CheckBool("monitor-current-run", MonitorService.IsCurrentRunSnapshot(true, 3, 3), true);
        failures += CheckBool("monitor-stopped-run", MonitorService.IsCurrentRunSnapshot(false, 3, 3), false);
        failures += CheckBool("monitor-stale-run", MonitorService.IsCurrentRunSnapshot(true, 3, 4), false);
        return failures;
    }

    private static HardwareInventory MemoryInventory(double gb, int modules, int type, int speed, bool lowPower)
    {
        HardwareInventory inv = new HardwareInventory();
        inv.MemoryGb = gb;
        inv.MemoryModules = modules;
        inv.MemoryType = type;
        inv.MemorySpeedMhz = speed;
        inv.MemoryIsLowPower = lowPower;
        return inv;
    }

    private static HardwareInventory StorageInventory(bool laptop, params DiskInfo[] disks)
    {
        HardwareInventory inv = new HardwareInventory();
        inv.IsLaptop = laptop;
        foreach (DiskInfo disk in disks)
            inv.InternalDisks.Add(disk);
        return inv;
    }

    private static int CheckMemory(string name, HardwareInventory inventory, double min, double max)
    {
        double watts = PowerEstimator.EstimateMemoryWatts(inventory);
        Console.WriteLine("MEMORYCASE " + name + " watts=" + watts.ToString("0.00", CultureInfo.InvariantCulture));
        if (Between(watts, min, max))
            return 0;
        Console.WriteLine("FAIL " + name);
        return 1;
    }

    private static int CheckStorage(string name, HardwareInventory inventory, double min, double max)
    {
        double watts = PowerEstimator.EstimateStorageWatts(inventory);
        Console.WriteLine("STORAGECASE " + name + " watts=" + watts.ToString("0.00", CultureInfo.InvariantCulture));
        if (Between(watts, min, max))
            return 0;
        Console.WriteLine("FAIL " + name);
        return 1;
    }

    private static int CheckBool(string name, bool actual, bool expected)
    {
        Console.WriteLine("CPUCASE " + name + " value=" + actual.ToString(CultureInfo.InvariantCulture));
        if (actual == expected)
            return 0;
        Console.WriteLine("FAIL " + name + " expected=" + expected.ToString(CultureInfo.InvariantCulture));
        return 1;
    }

    private static int CheckDouble(string name, double actual, double expected)
    {
        Console.WriteLine("NUMERICCASE " + name + " value=" + actual.ToString("0.###", CultureInfo.InvariantCulture));
        if (Math.Abs(actual - expected) < 0.000001)
            return 0;
        Console.WriteLine("FAIL " + name + " expected=" + expected.ToString(CultureInfo.InvariantCulture));
        return 1;
    }

    private static int CheckInt(string name, int actual, int expected)
    {
        Console.WriteLine("NUMERICCASE " + name + " value=" + actual.ToString(CultureInfo.InvariantCulture));
        if (actual == expected)
            return 0;
        Console.WriteLine("FAIL " + name + " expected=" + expected.ToString(CultureInfo.InvariantCulture));
        return 1;
    }

    private static Scenario S(string name, HardwareInventory inv, double cpuMin, double cpuMax, double gpuMin, double gpuMax, int disks)
    {
        Scenario s = new Scenario();
        s.Name = name;
        s.Inventory = inv;
        s.ExpectedCpuMin = cpuMin;
        s.ExpectedCpuMax = cpuMax;
        s.ExpectedGpuMin = gpuMin;
        s.ExpectedGpuMax = gpuMax;
        s.ExpectedInternalDisks = disks;
        return s;
    }

    private static bool Between(double value, double min, double max)
    {
        return value >= min && value <= max;
    }

    private static long Gigabytes(int value)
    {
        return (long)value * 1024L * 1024L * 1024L;
    }

    private static long Terabytes(int value)
    {
        return (long)value * 1024L * 1024L * 1024L * 1024L;
    }
}
