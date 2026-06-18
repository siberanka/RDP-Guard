using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace RDPGuard
{
    internal static class Program
    {
        private const string InstanceEventName = "Global\\RDPGuard_ShowWindow_7BB1E64C_1E2A_4F88_B88E_0D9E7F238C88";
        private static Mutex _mutex;
        private static EventWaitHandle _showWindowEvent;

        [STAThread]
        private static void Main(string[] args)
        {
            _mutex = new Mutex(true, "Global\\RDPGuard_7BB1E64C_1E2A_4F88_B88E_0D9E7F238C88", out var createdNew);
            if (!createdNew)
            {
                try
                {
                    EventWaitHandle.OpenExisting(InstanceEventName).Set();
                }
                catch
                {
                    MessageBox.Show("RDP Guard is already running.", "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return;
            }

            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceEventName);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdministrator())
            {
                MessageBox.Show("RDP Guard must run as administrator to read the Security log and manage Windows Firewall.", "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var startInWindow = args.Any(arg => string.Equals(arg, "--window", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(!startInWindow, _showWindowEvent));
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
