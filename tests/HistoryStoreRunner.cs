using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using HostPowerMonitor;

internal static class HistoryStoreRunner
{
    private static int Main()
    {
        int failures = 0;
        string folder = AppSettings.AppFolder;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "history.csv");
        string backup = path + ".testbak";

        try
        {
            if (File.Exists(backup))
                File.Delete(backup);
            if (File.Exists(path))
                File.Move(path, backup);

            DateTime now = DateTime.Now;
            DateTime validToday1 = now.AddSeconds(-50);
            DateTime validToday2 = now.AddSeconds(-20);
            DateTime yesterday = now.Date.AddDays(-1).AddHours(23).AddMinutes(59);
            DateTime future = now.AddMinutes(5);
            string[] lines = new string[]
            {
                Row(validToday1, 0.001),
                Row(validToday2, 0.002),
                Row(yesterday, 0.003),
                Row(future, 0.004),
                Row(now.AddSeconds(-10), -0.001),
                Row(now.AddSeconds(-5), 0.2),
                "bad,line"
            };
            File.WriteAllLines(path, lines, Encoding.UTF8);

            HistoryStore store = new HistoryStore(30);
            Totals totals = store.LoadTotals(now);
            double expectedToday = 0;
            if (validToday1.Date == now.Date)
                expectedToday += 0.001;
            if (validToday2.Date == now.Date)
                expectedToday += 0.002;
            double expectedMonth = expectedToday;
            if (yesterday.Year == now.Year && yesterday.Month == now.Month)
                expectedMonth += 0.003;
            failures += Check("today", totals.TodayKWh, expectedToday);
            failures += Check("month", totals.MonthKWh, expectedMonth);

            failures += RunConcurrentWriteTest(path, now);
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(backup))
                    File.Move(backup, path);
            }
            catch
            {
            }
        }

        Console.WriteLine(failures == 0 ? "RESULT PASS" : "RESULT FAIL");
        return failures == 0 ? 0 : 1;
    }

    private static int RunConcurrentWriteTest(string path, DateTime now)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);

            HistoryStore store = new HistoryStore(30);
            DateTime baseTime = now.Date.AddHours(1);
            if (baseTime > now)
                baseTime = now.Date;
            int threadCount = 4;
            int iterations = 20;
            Exception threadError = null;
            Thread[] threads = new Thread[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                threads[t] = new Thread(delegate()
                {
                    try
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            DateTime start = baseTime.AddSeconds(i);
                            store.AddInterval(start, start.AddSeconds(1), 360, 10);
                            if (i % 5 == 0)
                                store.Flush();
                        }
                    }
                    catch (Exception ex)
                    {
                        threadError = ex;
                    }
                });
                threads[t].Start();
            }

            for (int t = 0; t < threadCount; t++)
                threads[t].Join();

            if (threadError != null)
            {
                Console.WriteLine("FAIL concurrent-write exception=" + threadError.GetType().Name);
                return 1;
            }

            store.Flush();
            Totals totals = store.LoadTotals(baseTime.AddMinutes(2));
            double expected = threadCount * iterations * 360.0 * (1.0 / 3600.0) / 1000.0;
            return Check("concurrent-write", totals.TodayKWh, expected);
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL concurrent-write outer=" + ex.GetType().Name);
            return 1;
        }
    }

    private static string Row(DateTime minute, double energy)
    {
        return minute.ToString("o", CultureInfo.InvariantCulture) +
               ",300.000,250.000,350.000," +
               energy.ToString("0.00000000", CultureInfo.InvariantCulture);
    }

    private static int Check(string name, double actual, double expected)
    {
        Console.WriteLine("HISTORYCASE " + name + "=" + actual.ToString("0.000000", CultureInfo.InvariantCulture));
        if (Math.Abs(actual - expected) < 0.0000001)
            return 0;
        Console.WriteLine("FAIL " + name + " expected=" + expected.ToString("0.000000", CultureInfo.InvariantCulture));
        return 1;
    }
}
