using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml.Linq;

namespace RDPGuard
{
    public sealed class AuditFailureScanner
    {
        private static readonly XNamespace EventNamespace = "http://schemas.microsoft.com/win/2004/08/events/event";
        private const int LogonTypePropertyIndex = 10;
        private const int IpAddressPropertyIndex = 19;
        private const int ReaderBatchSize = 64;
        private const int YieldEveryRecords = 500;

        public ScanResult Scan(DateTime fromUtc, DateTime toUtc)
        {
            if (toUtc <= fromUtc)
            {
                AppLogger.Debug("Audit scan skipped because time range is empty.");
                return new ScanResult();
            }

            var stopwatch = Stopwatch.StartNew();
            var lookbackMillis = Math.Max(1000, (long)Math.Ceiling((DateTime.UtcNow - fromUtc).TotalMilliseconds) + 5000);
            var query = "*[System[(EventID=4625) and TimeCreated[timediff(@SystemTime) <= " + lookbackMillis + "]]]";
            AppLogger.Debug("Audit scan start: fromUtc=" + fromUtc.ToString("o") + ", toUtc=" + toUtc.ToString("o") + ", lookbackMillis=" + lookbackMillis);
            var eventQuery = new EventLogQuery("Security", PathType.LogName, query)
            {
                ReverseDirection = false,
                TolerateQueryErrors = true
            };

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var inspected = 0;

            using (var reader = new EventLogReader(eventQuery))
            {
                reader.BatchSize = ReaderBatchSize;
                EventRecord record;
                var recordsRead = 0;
                while ((record = reader.ReadEvent()) != null)
                {
                    recordsRead++;
                    if (recordsRead % YieldEveryRecords == 0)
                    {
                        Thread.Sleep(1);
                    }

                    using (record)
                    {
                        if (!record.TimeCreated.HasValue)
                        {
                            continue;
                        }

                        var createdUtc = record.TimeCreated.Value.ToUniversalTime();
                        if (createdUtc < fromUtc || createdUtc > toUtc)
                        {
                            continue;
                        }

                        inspected++;
                        if (!IsRemoteFailure(record, out var ip))
                        {
                            continue;
                        }

                        if (!IsBlockableRemoteIp(ip))
                        {
                            continue;
                        }

                        counts[ip] = counts.TryGetValue(ip, out var current) ? current + 1 : 1;
                    }
                }
            }

            return BuildResult(inspected, counts, stopwatch);
        }

        private static ScanResult BuildResult(int inspected, Dictionary<string, int> counts, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            AppLogger.Debug("Audit scan finished: inspectedEvents=" + inspected + ", uniqueIps=" + counts.Count + ", elapsedMs=" + stopwatch.ElapsedMilliseconds);
            return new ScanResult
            {
                EventsInspected = inspected,
                CountsByIp = counts
            };
        }

        private static bool IsRemoteFailure(EventRecord record, out string ipAddress)
        {
            ipAddress = null;

            if (TryReadRemoteFailureFromProperties(record, out ipAddress, out var isRemoteFailure))
            {
                return isRemoteFailure;
            }

            try
            {
                var xml = XDocument.Parse(record.ToXml());
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in xml
                    .Descendants(EventNamespace + "Data")
                    .Where(node => node.Attribute("Name") != null))
                {
                    data[(string)node.Attribute("Name")] = (node.Value ?? string.Empty).Trim();
                }

                ipAddress = data.TryGetValue("IpAddress", out var ip) ? ip : null;

                if (!data.TryGetValue("LogonType", out var logonTypeText) || !int.TryParse(logonTypeText, out var logonType))
                {
                    return false;
                }

                // RDP failures commonly appear as RemoteInteractive(10) or NLA/network(3).
                return logonType == 3 || logonType == 10;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadRemoteFailureFromProperties(EventRecord record, out string ipAddress, out bool isRemoteFailure)
        {
            ipAddress = null;
            isRemoteFailure = false;

            try
            {
                var properties = record.Properties;
                if (properties == null || properties.Count <= IpAddressPropertyIndex)
                {
                    return false;
                }

                var logonTypeText = Convert.ToString(properties[LogonTypePropertyIndex].Value);
                if (!int.TryParse(logonTypeText, out var logonType))
                {
                    return false;
                }

                ipAddress = Convert.ToString(properties[IpAddressPropertyIndex].Value);
                isRemoteFailure = logonType == 3 || logonType == 10;
                if (isRemoteFailure && !IsBlockableRemoteIp(ipAddress))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                ipAddress = null;
                isRemoteFailure = false;
                return false;
            }
        }

        private static bool IsBlockableRemoteIp(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-")
            {
                return false;
            }

            if (!IPAddress.TryParse(value.Trim(), out var ip))
            {
                return false;
            }

            if (IPAddress.IsLoopback(ip))
            {
                return false;
            }

            return true;
        }
    }

    public sealed class ScanResult
    {
        public int EventsInspected { get; set; }
        public Dictionary<string, int> CountsByIp { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
