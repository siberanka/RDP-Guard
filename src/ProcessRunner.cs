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

                    throw new TimeoutException(fileName + " " + timeoutMilliseconds + " ms icinde tamamlanmadi.");
                }

                outputDone.WaitOne(2000);
                errorDone.WaitOne(2000);

                return new ProcessRunResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString()
                };
            }
        }
    }

    internal sealed class ProcessRunResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }

        public string CombinedOutput => ((Error ?? string.Empty) + (Output ?? string.Empty)).Trim();
    }
}
