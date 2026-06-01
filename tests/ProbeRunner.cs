using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HostPowerMonitor;

internal static class ProbeRunner
{
    private static int Main(string[] args)
    {
        int rounds = 8;
        if (args.Length > 0)
            int.TryParse(args[0], out rounds);
        if (rounds < 3)
            rounds = 3;

        AppSettings settings = new AppSettings();
        settings.MarginPercent = 15;
        settings.ElectricityRate = 0.60;

        HardwareInventory inventory = HardwareDetector.Detect();
        PrintInventory(inventory);

        List<double> totals = new List<double>();
        int measuredRounds = 0;
        int failureCount = 0;

        using (PowerEstimator estimator = new PowerEstimator(inventory, settings))
        {
            for (int i = 0; i < rounds; i++)
            {
                PowerSample sample = estimator.ReadSample(DateTime.Now);
                totals.Add(sample.TotalWatts);
                int measured = 0;
                foreach (ComponentPower component in sample.Components)
                {
                    if (component.Source == PowerSourceKind.Measured)
                        measured++;
                    if (double.IsNaN(component.Watts) || double.IsInfinity(component.Watts) || component.Watts < 0 || component.Watts > 1200)
                    {
                        Console.WriteLine("FAIL component range: " + component.Name + " " + component.Watts.ToString(CultureInfo.InvariantCulture));
                        failureCount++;
                    }
                }
                if (measured > 0)
                    measuredRounds++;

                Console.WriteLine("ROUND " + (i + 1).ToString(CultureInfo.InvariantCulture) +
                                  " total=" + sample.TotalWatts.ToString("0.0", CultureInfo.InvariantCulture) +
                                  "W cpu=" + sample.CpuWatts.ToString("0.0", CultureInfo.InvariantCulture) +
                                  "W gpu=" + sample.GpuWatts.ToString("0.0", CultureInfo.InvariantCulture) +
                                  "W base=" + sample.TotalBeforeMarginWatts.ToString("0.0", CultureInfo.InvariantCulture) +
                                  "W " + sample.Summary);
                Thread.Sleep(1100);
            }
        }

        double min = Min(totals);
        double max = Max(totals);
        double avg = Average(totals);
        Console.WriteLine("SUMMARY rounds=" + rounds.ToString(CultureInfo.InvariantCulture) +
                          " min=" + min.ToString("0.0", CultureInfo.InvariantCulture) +
                          "W avg=" + avg.ToString("0.0", CultureInfo.InvariantCulture) +
                          "W max=" + max.ToString("0.0", CultureInfo.InvariantCulture) + "W");

        if (inventory.Gpus.Count == 0 && string.IsNullOrEmpty(inventory.CpuName))
        {
            Console.WriteLine("FAIL no CPU/GPU inventory was detected");
            failureCount++;
        }
        if (avg <= 5 || avg > 1200)
        {
            Console.WriteLine("FAIL total watts out of realistic desktop/laptop range");
            failureCount++;
        }
        if (measuredRounds == 0)
            Console.WriteLine("WARN no direct power sensor was available; all readings used estimates");

        if (failureCount > 0)
        {
            Console.WriteLine("RESULT FAIL");
            return 1;
        }

        Console.WriteLine("RESULT PASS");
        return 0;
    }

    private static void PrintInventory(HardwareInventory inventory)
    {
        Console.WriteLine("OS=" + inventory.OperatingSystem);
        Console.WriteLine("MODEL=" + inventory.ComputerModel);
        Console.WriteLine("BOARD=" + inventory.BaseBoard);
        Console.WriteLine("CPU=" + inventory.CpuName);
        Console.WriteLine("MEMORY_GB=" + inventory.MemoryGb.ToString("0.0", CultureInfo.InvariantCulture) +
                          " MODULES=" + inventory.MemoryModules.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < inventory.Gpus.Count; i++)
            Console.WriteLine("GPU" + i.ToString(CultureInfo.InvariantCulture) + "=" + inventory.Gpus[i].Name +
                              " MAX_EST=" + inventory.Gpus[i].EstimatedMaxWatts.ToString("0", CultureInfo.InvariantCulture) + "W");
        for (int i = 0; i < inventory.InternalDisks.Count; i++)
            Console.WriteLine("DISK" + i.ToString(CultureInfo.InvariantCulture) + "=" + inventory.InternalDisks[i].Model +
                              " IF=" + inventory.InternalDisks[i].InterfaceType);
    }

    private static double Min(List<double> values)
    {
        double min = double.MaxValue;
        foreach (double value in values)
            if (value < min)
                min = value;
        return min;
    }

    private static double Max(List<double> values)
    {
        double max = double.MinValue;
        foreach (double value in values)
            if (value > max)
                max = value;
        return max;
    }

    private static double Average(List<double> values)
    {
        double sum = 0;
        foreach (double value in values)
            sum += value;
        return values.Count == 0 ? 0 : sum / values.Count;
    }
}
