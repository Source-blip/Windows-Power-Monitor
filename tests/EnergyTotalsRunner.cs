using System;
using System.Globalization;
using HostPowerMonitor;

internal static class EnergyTotalsRunner
{
    private static int Main()
    {
        int failures = 0;
        failures += Check("same-day",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 31, 10, 0, 0),
                new DateTime(2026, 5, 31, 10, 0, 2),
                360, 1.0, 2.0, 10),
            1.0002, 2.0002, 0.0002);

        failures += Check("cross-midnight-same-month",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 30, 23, 59, 59),
                new DateTime(2026, 5, 31, 0, 0, 1),
                360, 1.0, 2.0, 10),
            0.0001, 2.0002, 0.0002);

        failures += Check("cross-month",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 31, 23, 59, 59),
                new DateTime(2026, 6, 1, 0, 0, 1),
                360, 1.0, 2.0, 10),
            0.0001, 0.0001, 0.0002);

        failures += Check("sleep-cross-day",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 31, 23, 59, 0),
                new DateTime(2026, 6, 1, 0, 1, 0),
                360, 1.0, 2.0, 10),
            0.0, 0.0, 0.0);

        failures += Check("clock-backward",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 31, 10, 0, 2),
                new DateTime(2026, 5, 31, 10, 0, 0),
                360, 1.0, 2.0, 10),
            1.0, 2.0, 0.0);

        failures += Check("ten-second-refresh-jitter",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 31, 10, 0, 0),
                new DateTime(2026, 5, 31, 10, 0, 12),
                360, 1.0, 2.0, 30),
            1.0012, 2.0012, 0.0012);

        failures += Check("long-resume-gap",
            EnergyAccumulator.Apply(
                new DateTime(2026, 5, 31, 10, 0, 0),
                new DateTime(2026, 5, 31, 10, 2, 0),
                360, 1.0, 2.0, 30),
            1.0, 2.0, 0.0);

        Console.WriteLine(failures == 0 ? "RESULT PASS" : "RESULT FAIL");
        return failures == 0 ? 0 : 1;
    }

    private static int Check(string name, EnergyTotalsUpdate actual, double today, double month, double energy)
    {
        Console.WriteLine("ENERGYCASE " + name +
                          " today=" + actual.TodayKWh.ToString("0.000000", CultureInfo.InvariantCulture) +
                          " month=" + actual.MonthKWh.ToString("0.000000", CultureInfo.InvariantCulture) +
                          " energy=" + actual.EnergyKWh.ToString("0.000000", CultureInfo.InvariantCulture));
        if (Close(actual.TodayKWh, today) && Close(actual.MonthKWh, month) && Close(actual.EnergyKWh, energy))
            return 0;
        Console.WriteLine("FAIL " + name);
        return 1;
    }

    private static bool Close(double actual, double expected)
    {
        return Math.Abs(actual - expected) < 0.0000001;
    }
}
