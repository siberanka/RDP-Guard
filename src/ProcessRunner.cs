using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RDPGuard
{
    internal static class ProcessRunner
    {
        public static ProcessRunResult Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            var stopwatch = Stopwatch.StartNew();
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

                AppLogger.Debug("Process start: file=" + fileName + ", timeoutMs=" + timeoutMilliseconds + ", args=" + SanitizeArguments(fileName, arguments));
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                    catch
                    {
                    }

                    WaitForReader(outputTask);
                    WaitForReader(errorTask);
                    stopwatch.Stop();
                    AppLogger.Error("Process timeout: file=" + fileName + ", elapsedMs=" + stopwatch.ElapsedMilliseconds + ", args=" + SanitizeArguments(fileName, arguments));
                    throw new TimeoutException(fileName + " did not finish within " + timeoutMilliseconds + " ms.");
                }

                WaitForReader(outputTask);
                WaitForReader(errorTask);
                stopwatch.Stop();
                var output = GetReaderResult(outputTask);
                var error = GetReaderResult(errorTask);

                AppLogger.Debug("Process exit: file=" + fileName +
                                ", exitCode=" + process.ExitCode +
                                ", elapsedMs=" + stopwatch.ElapsedMilliseconds +
                                ", stdoutChars=" + output.Length +
                                ", stderrChars=" + error.Length);

                return new ProcessRunResult
                {
                    ExitCode = process.ExitCode,
                    Output = output,
                    Error = error,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private static void WaitForReader(Task<string> readerTask)
        {
            try
            {
                readerTask.Wait(2000);
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Process stream reader wait failed", ex);
            }
        }

        private static string GetReaderResult(Task<string> readerTask)
        {
            try
            {
                return readerTask.IsCompleted ? (readerTask.Result ?? string.Empty) : string.Empty;
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Process stream reader result failed", ex);
                return string.Empty;
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
