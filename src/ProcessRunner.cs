using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace RDPGuard
{
    internal static class ProcessRunner
    {
        public static ProcessRunResult Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            var stopwatch = Stopwatch.StartNew();
            var output = new StringBuilder();
            var error = new StringBuilder();
            using (var outputDone = new ManualResetEvent(false))
            using (var errorDone = new ManualResetEvent(false))
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        outputDone.Set();
                        return;
                    }

                    output.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        errorDone.Set();
                        return;
                    }

                    error.AppendLine(e.Data);
                };

                AppLogger.Debug("Process start: file=" + fileName + ", timeoutMs=" + timeoutMilliseconds + ", args=" + SanitizeArguments(fileName, arguments));
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    stopwatch.Stop();
                    AppLogger.Error("Process timeout: file=" + fileName + ", elapsedMs=" + stopwatch.ElapsedMilliseconds + ", args=" + SanitizeArguments(fileName, arguments));
                    throw new TimeoutException(fileName + " did not finish within " + timeoutMilliseconds + " ms.");
                }

                outputDone.WaitOne(2000);
                errorDone.WaitOne(2000);
                stopwatch.Stop();

                AppLogger.Debug("Process exit: file=" + fileName +
                                ", exitCode=" + process.ExitCode +
                                ", elapsedMs=" + stopwatch.ElapsedMilliseconds +
                                ", stdoutChars=" + output.Length +
                                ", stderrChars=" + error.Length);

                return new ProcessRunResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private static string SanitizeArguments(string fileName, string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return string.Empty;
            }

            if (string.Equals(fileName, "powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                return arguments.Length > 300 ? arguments.Substring(0, 300) + "... <truncated>" : arguments;
            }

            return arguments.Length > 600 ? arguments.Substring(0, 600) + "... <truncated>" : arguments;
        }
    }

    internal sealed class ProcessRunResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public long ElapsedMilliseconds { get; set; }

        public string CombinedOutput => ((Error ?? string.Empty) + (Output ?? string.Empty)).Trim();
    }
}
