using System;

namespace RDPGuard
{
    public sealed class StartupManager
    {
        private const string TaskName = "RDP Guard";

        public bool IsEnabled()
        {
            var result = RunSchtasks("/Query /TN \"" + TaskName + "\"", true);
            return result.ExitCode == 0;
        }

        public void SetEnabled(bool enabled, string executablePath)
        {
            if (enabled)
            {
                var taskRun = "\\\"" + executablePath + "\\\" --tray";
                var args = "/Create /TN \"" + TaskName + "\" /SC ONLOGON /RL HIGHEST /F /TR \"" + taskRun + "\"";
                var result = RunSchtasks(args, false);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(result.CombinedOutput);
                }
            }
            else
            {
                RunSchtasks("/Delete /TN \"" + TaskName + "\" /F", true);
            }
        }

        private static ProcessResult RunSchtasks(string arguments, bool allowFailure)
        {
            var run = ProcessRunner.Run("schtasks.exe", arguments, 30000);
            if (run.ExitCode != 0 && !allowFailure)
            {
                throw new InvalidOperationException(run.CombinedOutput);
            }

            return new ProcessResult
            {
                ExitCode = run.ExitCode,
                CombinedOutput = run.CombinedOutput
            };
        }

        private sealed class ProcessResult
        {
            public int ExitCode { get; set; }
            public string CombinedOutput { get; set; }
        }
    }
}
