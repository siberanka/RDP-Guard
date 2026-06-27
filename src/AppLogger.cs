using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace RDPGuard
{
    internal static class AppLogger
    {
        private static readonly object Sync = new object();
        private static readonly DateTime ProcessStartedUtc = DateTime.UtcNow;
        private static readonly int ProcessId = GetCurrentProcessId();
        private const long MaxLogBytes = 2 * 1024 * 1024;
        private const int MaxRolledFiles = 4;
        private const int MaxInlineText = 4000;

        public static void Write(string message)
        {
            Write(LogLevel.Info, message);
        }

        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public static void Warning(string message)
        {
            Write(LogLevel.Warning, message);
        }

        public static void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        public static void Write(LogLevel level, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                var line = BuildLine(level, message);
                lock (Sync)
                {
                    Directory.CreateDirectory(AppConfig.DataDirectory);
                    RotateIfNeeded();
                    File.AppendAllText(AppConfig.LogPath, line + Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        public static void WriteException(string context, Exception exception)
        {
            if (exception == null)
            {
                Error(context + ": unknown exception");
                return;
            }

            Error(context + ": " + exception);
        }

        public static void WriteStartupSnapshot(string[] args)
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    Debug("Startup snapshot: version=" + assembly.GetName().Version +
                          ", exe=" + assembly.Location +
                          ", args=" + string.Join(" ", args ?? new string[0]) +
                          ", os=" + Environment.OSVersion +
                          ", clr=" + Environment.Version +
                          ", machine=" + Environment.MachineName +
                          ", user=" + Environment.UserName +
                          ", pid=" + process.Id +
                          ", priority=" + process.PriorityClass +
                          ", process64=" + Environment.Is64BitProcess +
                          ", os64=" + Environment.Is64BitOperatingSystem +
                          ", workingSet=" + process.WorkingSet64 +
                          ", privateMemory=" + process.PrivateMemorySize64 +
                          ", config=" + AppConfig.ConfigPath +
                          ", log=" + AppConfig.LogPath);
                }
            }
            catch (Exception ex)
            {
                WriteException("Startup snapshot failed", ex);
            }
        }

        private static void RotateIfNeeded()
        {
            if (!File.Exists(AppConfig.LogPath))
            {
                return;
            }

            if (new FileInfo(AppConfig.LogPath).Length <= MaxLogBytes)
            {
                return;
            }

            for (var index = MaxRolledFiles - 1; index >= 1; index--)
            {
                var source = AppConfig.LogPath + "." + index;
                var target = AppConfig.LogPath + "." + (index + 1);
                if (File.Exists(source))
                {
                    File.Copy(source, target, true);
                }
            }

            File.Copy(AppConfig.LogPath, AppConfig.LogPath + ".1", true);
            File.WriteAllText(AppConfig.LogPath, string.Empty);

            var overflow = AppConfig.LogPath + "." + (MaxRolledFiles + 1);
            if (File.Exists(overflow))
            {
                File.Delete(overflow);
            }
        }

        private static string BuildLine(LogLevel level, string message)
        {
            var safeMessage = Truncate(message);
            var uptime = DateTime.UtcNow - ProcessStartedUtc;
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                   " [" + level.ToString().ToUpperInvariant() + "]" +
                   " [pid:" + ProcessId + "]" +
                   " [tid:" + Thread.CurrentThread.ManagedThreadId + "]" +
                   " [up:" + (long)uptime.TotalSeconds + "s]  " +
                   safeMessage;
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaxInlineText)
            {
                return value;
            }

            return value.Substring(0, MaxInlineText) + "... <truncated " + (value.Length - MaxInlineText) + " chars>";
        }

        private static int GetCurrentProcessId()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    return process.Id;
                }
            }
            catch
            {
                return 0;
            }
        }
    }

    internal enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
