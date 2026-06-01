using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class InstallerProgram
{
    private const string AppName = "HostPowerMonitor";
    private const string DisplayName = "主机用电监控";
    private const string PayloadResource = "HostPowerMonitorPayload";

    [STAThread]
    private static int Main(string[] args)
    {
        InstallOptions options = InstallOptions.FromArgs(args);
        if (options.Uninstall)
            return Uninstall(options.Silent);
        if (options.Silent)
            return Install(options);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm(options));
        return 0;
    }

    internal static int Install(InstallOptions options)
    {
        try
        {
            Directory.CreateDirectory(options.InstallDir);
            string appExe = Path.Combine(options.InstallDir, AppName + ".exe");
            WritePayload(appExe);

            string setupCopy = Path.Combine(options.InstallDir, AppName + "_Setup.exe");
            try { File.Copy(Application.ExecutablePath, setupCopy, true); }
            catch { setupCopy = Application.ExecutablePath; }

            if (options.StartMenuShortcut)
            {
                string startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", DisplayName);
                Directory.CreateDirectory(startMenuFolder);
                CreateShortcut(Path.Combine(startMenuFolder, DisplayName + ".lnk"), appExe, options.InstallDir);
                CreateShortcut(Path.Combine(startMenuFolder, "卸载 " + DisplayName + ".lnk"), setupCopy, options.InstallDir, "/uninstall");
            }

            if (options.DesktopShortcut)
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DisplayName + ".lnk"), appExe, options.InstallDir);

            ApplyAutoStart(appExe, options.AutoStart);
            WriteUninstallEntry(setupCopy, options.InstallDir);

            if (options.LaunchAfterInstall)
                Process.Start(appExe);

            return 0;
        }
        catch (Exception ex)
        {
            if (!options.Silent)
                MessageBox.Show("安装失败：" + ex.Message, DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int Uninstall(bool silent)
    {
        try
        {
            string installDir = DefaultInstallDir;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                if (key != null)
                {
                    object value = key.GetValue("InstallLocation");
                    if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                        installDir = value.ToString();
                }
            }

            if (!silent)
            {
                DialogResult result = MessageBox.Show("确定卸载主机用电监控？", DisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    return 0;
            }

            ApplyAutoStart(Path.Combine(installDir, AppName + ".exe"), false);
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DisplayName + ".lnk"));
            string startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", DisplayName);
            DeleteShortcut(Path.Combine(startMenuFolder, DisplayName + ".lnk"));
            DeleteShortcut(Path.Combine(startMenuFolder, "卸载 " + DisplayName + ".lnk"));
            TryDeleteDirectory(startMenuFolder);

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName, false); }
            catch { }

            try { File.Delete(Path.Combine(installDir, AppName + ".exe")); }
            catch { }

            if (!silent)
                MessageBox.Show("卸载完成。", DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
                MessageBox.Show("卸载失败：" + ex.Message, DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void WritePayload(string appExe)
    {
        using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
        {
            if (input == null)
                throw new InvalidOperationException("安装包缺少主程序。");
            using (FileStream output = new FileStream(appExe, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
        }
    }

    private static void ApplyAutoStart(string appExe, bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key == null)
                    return;
                if (enabled)
                    key.SetValue(AppName, "\"" + appExe + "\"");
                else
                    key.DeleteValue(AppName, false);
            }
        }
        catch
        {
        }
    }

    private static void WriteUninstallEntry(string setupExe, string installDir)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                if (key == null)
                    return;
                key.SetValue("DisplayName", DisplayName);
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "HostPowerMonitor");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", Path.Combine(installDir, AppName + ".exe"));
                key.SetValue("UninstallString", "\"" + setupExe + "\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch
        {
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        CreateShortcut(shortcutPath, targetPath, workingDir, "");
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string arguments)
    {
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return;
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { DisplayName });
            if (!string.IsNullOrWhiteSpace(arguments))
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
        catch
        {
        }
    }

    private static void DeleteShortcut(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0)
                Directory.Delete(path);
        }
        catch
        {
        }
    }

    internal static string DefaultInstallDir
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
        }
    }
}

internal sealed class InstallOptions
{
    public string InstallDir = InstallerProgram.DefaultInstallDir;
    public bool DesktopShortcut = true;
    public bool StartMenuShortcut = true;
    public bool AutoStart = true;
    public bool LaunchAfterInstall = true;
    public bool Silent;
    public bool Uninstall;

    public static InstallOptions FromArgs(string[] args)
    {
        InstallOptions options = new InstallOptions();
        if (args == null)
            return options;
        foreach (string arg in args)
        {
            if (arg.Equals("/silent", StringComparison.OrdinalIgnoreCase))
                options.Silent = true;
            else if (arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
                options.Uninstall = true;
            else if (arg.Equals("/noDesktop", StringComparison.OrdinalIgnoreCase))
                options.DesktopShortcut = false;
            else if (arg.Equals("/noStartMenu", StringComparison.OrdinalIgnoreCase))
                options.StartMenuShortcut = false;
            else if (arg.Equals("/noAutoStart", StringComparison.OrdinalIgnoreCase))
                options.AutoStart = false;
            else if (arg.Equals("/noLaunch", StringComparison.OrdinalIgnoreCase))
                options.LaunchAfterInstall = false;
            else if (arg.StartsWith("/dir=", StringComparison.OrdinalIgnoreCase))
                options.InstallDir = arg.Substring(5).Trim('"');
        }
        return options;
    }
}

internal sealed class InstallerForm : Form
{
    private readonly InstallOptions _options;
    private TextBox _pathBox;
    private CheckBox _desktop;
    private CheckBox _startMenu;
    private CheckBox _autoStart;
    private CheckBox _launch;
    private Button _installButton;
    private Label _status;

    public InstallerForm(InstallOptions options)
    {
        _options = options;
        Text = "安装 - 主机用电监控";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 360);
        Font = new Font("Microsoft YaHei UI", 9F);
        BuildUi();
    }

    private void BuildUi()
    {
        Label title = new Label();
        title.Text = "主机用电监控";
        title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
        title.Location = new Point(28, 24);
        title.Size = new Size(260, 36);
        Controls.Add(title);

        Label desc = new Label();
        desc.Text = "安装后会常驻托盘，显示整机实时功耗和悬浮气泡。";
        desc.ForeColor = Color.FromArgb(82, 94, 106);
        desc.Location = new Point(31, 64);
        desc.Size = new Size(430, 24);
        Controls.Add(desc);

        Label pathLabel = new Label();
        pathLabel.Text = "安装位置";
        pathLabel.Location = new Point(32, 110);
        pathLabel.AutoSize = true;
        Controls.Add(pathLabel);

        _pathBox = new TextBox();
        _pathBox.Text = _options.InstallDir;
        _pathBox.Location = new Point(34, 134);
        _pathBox.Size = new Size(360, 24);
        Controls.Add(_pathBox);

        Button browse = new Button();
        browse.Text = "浏览";
        browse.Location = new Point(405, 132);
        browse.Size = new Size(80, 28);
        browse.Click += BrowseClicked;
        Controls.Add(browse);

        _desktop = Option("创建桌面快捷方式", _options.DesktopShortcut, 34, 184);
        _startMenu = Option("创建开始菜单快捷方式", _options.StartMenuShortcut, 34, 214);
        _autoStart = Option("开机自启", _options.AutoStart, 34, 244);
        _launch = Option("安装完成后启动", _options.LaunchAfterInstall, 34, 274);

        _status = new Label();
        _status.ForeColor = Color.FromArgb(82, 94, 106);
        _status.Location = new Point(34, 316);
        _status.Size = new Size(300, 24);
        Controls.Add(_status);

        Button cancel = new Button();
        cancel.Text = "取消";
        cancel.Location = new Point(310, 310);
        cancel.Size = new Size(80, 32);
        cancel.Click += delegate { Close(); };
        Controls.Add(cancel);

        _installButton = new Button();
        _installButton.Text = "安装";
        _installButton.Location = new Point(405, 310);
        _installButton.Size = new Size(80, 32);
        _installButton.Click += InstallClicked;
        Controls.Add(_installButton);
    }

    private CheckBox Option(string text, bool value, int x, int y)
    {
        CheckBox box = new CheckBox();
        box.Text = text;
        box.Checked = value;
        box.AutoSize = true;
        box.Location = new Point(x, y);
        Controls.Add(box);
        return box;
    }

    private void BrowseClicked(object sender, EventArgs e)
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "选择安装位置";
            dialog.SelectedPath = _pathBox.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _pathBox.Text = dialog.SelectedPath;
        }
    }

    private void InstallClicked(object sender, EventArgs e)
    {
        _installButton.Enabled = false;
        _status.Text = "正在安装...";
        Refresh();

        _options.InstallDir = _pathBox.Text.Trim();
        _options.DesktopShortcut = _desktop.Checked;
        _options.StartMenuShortcut = _startMenu.Checked;
        _options.AutoStart = _autoStart.Checked;
        _options.LaunchAfterInstall = _launch.Checked;

        int result = InstallerProgram.Install(_options);
        if (result == 0)
        {
            _status.Text = "安装完成";
            MessageBox.Show(this, "安装完成。", "主机用电监控", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        else
        {
            _status.Text = "安装失败";
            _installButton.Enabled = true;
        }
    }
}
