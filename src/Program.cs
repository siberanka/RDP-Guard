using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
            InstallExceptionHandlers();
            TrySetBalancedPriority();
            TryRegisterApplicationRestart();
            AppLogger.WriteStartupSnapshot(args);
            Application.ApplicationExit += (_, __) => AppLogger.Write("Application exit.");

            _mutex = new Mutex(true, "Global\\RDPGuard_7BB1E64C_1E2A_4F88_B88E_0D9E7F238C88", out var createdNew);
            if (!createdNew)
            {
                try
                {
                    EventWaitHandle.OpenExisting(InstanceEventName).Set();
                }
                catch
                {
                    AppLogger.Warning("Second instance detected but existing instance signal failed.");
                    MessageBox.Show("RDP Guard is already running.", "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return;
            }

            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceEventName);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            if (!IsAdministrator())
            {
                AppLogger.Warning("Application is not running as administrator; exiting before UI startup.");
                MessageBox.Show("RDP Guard must run as administrator to read the Security log and manage Windows Firewall.", "RDP Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var startInWindow = args.Any(arg => string.Equals(arg, "--window", StringComparison.OrdinalIgnoreCase));
            try
            {
                Application.Run(new MainForm(!startInWindow, _showWindowEvent));
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Fatal Application.Run failure", ex);
                throw;
            }
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static void InstallExceptionHandlers()
        {
            Application.ThreadException += (_, e) =>
            {
                AppLogger.WriteException("Unhandled UI thread exception", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                AppLogger.WriteException("Unhandled AppDomain exception", e.ExceptionObject as Exception);
            };
        }

        private static void TrySetBalancedPriority()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                }

                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                AppLogger.Write("Process priority set to AboveNormal.");
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Process priority could not be adjusted", ex);
            }
        }

        private static void TryRegisterApplicationRestart()
        {
            try
            {
                var result = RegisterApplicationRestart("--tray", 0);
                AppLogger.Write("Application restart registration result: " + result);
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Application restart registration failed", ex);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterApplicationRestart(string commandLineArgs, int flags);
    }
}
