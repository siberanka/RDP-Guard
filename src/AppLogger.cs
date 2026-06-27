using System;
using System.IO;

namespace RDPGuard
{
    internal static class AppLogger
    {
        private static readonly object Sync = new object();
        private const long MaxLogBytes = 1024 * 1024;

        public static void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message;
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
                Write(context + ": unknown exception");
                return;
            }

            Write(context + ": " + exception);
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

            File.Copy(AppConfig.LogPath, AppConfig.LogPath + ".1", true);
            File.WriteAllText(AppConfig.LogPath, string.Empty);
        }
    }
}
