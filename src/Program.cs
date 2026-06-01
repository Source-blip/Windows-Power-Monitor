using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HostPowerMonitor
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "HostPowerMonitor.SingleInstance", out created))
            {
                if (!created)
                {
                    SingleInstanceMessenger.SignalShowMain();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool showMain = args != null && args.Length > 0 && args[0].Equals("--show", StringComparison.OrdinalIgnoreCase);
                using (TrayAppContext context = new TrayAppContext(Application.ExecutablePath, showMain))
                    Application.Run(context);
            }
        }
    }

    internal static class SingleInstanceMessenger
    {
        private const string MessageName = "HostPowerMonitor.ShowMain.1";
        private static readonly int MessageId = RegisterWindowMessage(MessageName);
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        public static int ShowMainMessage
        {
            get { return MessageId; }
        }

        public static void SignalShowMain()
        {
            if (MessageId == 0)
                return;
            try
            {
                PostMessage(HwndBroadcast, MessageId, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
