using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using HostPowerMonitor;

internal static class UiSmokeRunner
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppSettings settings = new AppSettings();
        HardwareInventory inventory = new HardwareInventory();

        using (MainForm form = new MainForm(settings, inventory))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(40, 40);
            form.Show();
            form.CreateControl();
            Application.DoEvents();

            Panel settingsPanel = GetPrivateField<Panel>(form, "_settingsPanel");
            if (settingsPanel == null)
                return Fail("settings panel missing");

            Control settingsNav = FindSidebarLabel(form, "设置");
            Control overviewNav = FindSidebarLabel(form, "概览");
            if (settingsNav == null)
                return Fail("settings nav missing");
            if (overviewNav == null)
                return Fail("overview nav missing");

            Click(settingsNav);
            Application.DoEvents();
            if (!settingsPanel.Visible)
                return Fail("settings nav did not open settings panel");

            Click(overviewNav);
            Application.DoEvents();
            if (settingsPanel.Visible)
                return Fail("overview nav did not close settings panel");

            Label settingsButton = GetPrivateField<Label>(form, "_settingsButton");
            if (settingsButton == null || settingsButton.Visible)
                return Fail("right settings button should be hidden");

            Label power = GetPrivateField<Label>(form, "_currentPower");
            Label unit = GetPrivateField<Label>(form, "_currentUnit");
            if (power == null || unit == null)
                return Fail("power readout controls missing");
            if (unit.Left <= power.Left)
                return Fail("power unit is not positioned after value");

            Console.WriteLine("RESULT PASS");
            return 0;
        }
    }

    private static T GetPrivateField<T>(object target, string name) where T : class
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return field == null ? null : field.GetValue(target) as T;
    }

    private static Control FindSidebarLabel(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            Label label = child as Label;
            if (label != null && label.Text == text && IsSidebarLabel(label))
                return label;
            Control found = FindSidebarLabel(child, text);
            if (found != null)
                return found;
        }
        return null;
    }

    private static bool IsSidebarLabel(Control control)
    {
        Control item = control.Parent;
        Control sidebar = item == null ? null : item.Parent;
        return sidebar != null && sidebar.Left <= 25 && sidebar.Top >= 80 && sidebar.Width <= 190;
    }

    private static Control FindLabel(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            Label label = child as Label;
            if (label != null && label.Text == text)
                return label;
            Control found = FindLabel(child, text);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void Click(Control control)
    {
        MethodInfo onClick = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        onClick.Invoke(control, new object[] { EventArgs.Empty });
    }

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL " + message);
        Console.WriteLine("RESULT FAIL");
        return 1;
    }
}
