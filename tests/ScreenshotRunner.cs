using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using HostPowerMonitor;

internal static class ScreenshotRunner
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args == null || args.Length < 2)
        {
            Console.WriteLine("usage: ScreenshotRunner <main.png> <bubble.png>");
            return 1;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppSettings settings = new AppSettings();
        settings.MarginPercent = 15;
        settings.ElectricityRate = 0.60;
        settings.BubbleVisible = true;
        settings.HighPowerAlert = true;
        settings.HighPowerThresholdWatts = 450;
        settings.BubbleX = 20;
        settings.BubbleY = 20;

        HardwareInventory inventory = new HardwareInventory();
        inventory.OperatingSystem = "Microsoft Windows 11";
        inventory.ComputerModel = "MS-Terminator B850M";
        inventory.BaseBoard = "MS-Terminator B850M";
        inventory.CpuName = "AMD Ryzen 5 9600X 6-Core Processor";
        inventory.MemoryGb = 32;
        inventory.MemoryModules = 2;
        inventory.Gpus.Add(new GpuInfo { Name = "NVIDIA GeForce RTX 5060 Ti", Vendor = "NVIDIA", EstimatedMaxWatts = 180 });
        inventory.Gpus.Add(new GpuInfo { Name = "AMD Radeon(TM) Graphics", Vendor = "AMD", IsIntegrated = true, EstimatedMaxWatts = 18 });
        inventory.InternalDisks.Add(new DiskInfo { Model = "Samsung SSD 990 PRO 2TB", InterfaceType = "NVMe", MediaType = "SSD", SizeBytes = 2L * 1024L * 1024L * 1024L * 1024L, BusType = 17, IsSsdLike = true });

        PowerSample sample = new PowerSample();
        sample.Timestamp = DateTime.Now;
        sample.CpuWatts = 98;
        sample.GpuWatts = 166;
        sample.MemoryWatts = 24;
        sample.StorageWatts = 18;
        sample.PlatformWatts = 42;
        sample.TotalBeforeMarginWatts = 348;
        sample.TotalWatts = 348;
        sample.TodayKWh = 0.10;
        sample.MonthKWh = 38.42;
        sample.TodayCost = sample.TodayKWh * settings.ElectricityRate;
        sample.MonthCost = sample.MonthKWh * settings.ElectricityRate;
        sample.Summary = "2 measured, 4 estimated";
        sample.Components.Add(new ComponentPower { Name = "CPU", Watts = sample.CpuWatts, Source = PowerSourceKind.Measured });
        sample.Components.Add(new ComponentPower { Name = "GPU", Watts = sample.GpuWatts, Source = PowerSourceKind.Measured });
        sample.Components.Add(new ComponentPower { Name = "Memory", Watts = sample.MemoryWatts, Source = PowerSourceKind.Estimated });
        sample.Components.Add(new ComponentPower { Name = "Storage", Watts = sample.StorageWatts, Source = PowerSourceKind.Estimated });
        sample.Components.Add(new ComponentPower { Name = "Board", Watts = sample.PlatformWatts, Source = PowerSourceKind.Estimated });

        using (MainForm main = new MainForm(settings, inventory))
        {
            for (int i = 0; i < 60; i++)
            {
                double trend = 120 + i * 4.2 + Math.Sin(i / 3.0) * 22;
                if (i > 38)
                    trend += (i - 38) * 3.8;
                sample.TotalWatts = trend;
                main.UpdateSample(sample);
            }
            sample.TotalWatts = 348;
            main.UpdateSample(sample);
            SaveControl(main, args[0]);
        }

        using (BubbleForm bubble = new BubbleForm(settings))
        {
            bubble.UpdateSample(sample);
            SaveControl(bubble, args[1]);
        }

        Console.WriteLine("SCREENSHOT main=" + args[0]);
        Console.WriteLine("SCREENSHOT bubble=" + args[1]);
        return 0;
    }

    private static void SaveControl(Control control, string path)
    {
        if (control is BubbleForm)
        {
            SaveBubblePreview((Form)control, path);
            return;
        }

        Form form = control as Form;
        if (form != null)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(40, 40);
            form.Show();
        }
        control.CreateControl();
        control.Refresh();
        control.Update();
        Application.DoEvents();
        using (Bitmap bitmap = new Bitmap(control.Width, control.Height))
        {
            control.DrawToBitmap(bitmap, new Rectangle(0, 0, control.Width, control.Height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        if (form != null)
            form.Hide();
    }

    private static void SaveBubblePreview(Form bubble, string path)
    {
        bubble.BackColor = Color.FromArgb(237, 242, 248);
        bubble.TransparencyKey = Color.Empty;
        bubble.CreateControl();
        bubble.Refresh();
        bubble.Update();
        Application.DoEvents();
        using (Bitmap bitmap = new Bitmap(bubble.Width, bubble.Height))
        {
            bubble.DrawToBitmap(bitmap, new Rectangle(0, 0, bubble.Width, bubble.Height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
