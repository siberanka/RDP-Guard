using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RDPGuard
{
    public sealed class GuardService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly AuditFailureScanner _scanner = new AuditFailureScanner();
        private readonly FirewallManager _firewall = new FirewallManager();
        private Timer _timer;
        private bool _isChecking;
        private bool _disposed;

        public GuardService(AppConfig config)
        {
            Config = config;
        }

        public AppConfig Config { get; private set; }

        public event EventHandler<string> Log;
        public event EventHandler ConfigChanged;
        public event EventHandler<CheckCompletedEventArgs> CheckCompleted;

        public AppConfig GetConfigSnapshot()
        {
            lock (_sync)
            {
                return Config.Clone();
            }
        }

        public void Start()
        {
            RemoveWhitelistedBlocks();

            var started = false;
            var interval = 0;
            lock (_sync)
            {
                StopTimerOnly();
                if (_disposed || !Config.MonitorEnabled)
                {
                    return;
                }

                ScheduleTimerLocked(TimeSpan.FromSeconds(3));
                interval = Config.CheckIntervalMinutes;
                started = true;
            }

            if (started)
            {
                OnLog("Protection active. Check interval: " + interval + " min.");
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopTimerOnly();
                Config.MonitorEnabled = false;
                Config.Save();
            }

            OnConfigChanged();
            OnLog("Protection stopped.");
        }

        public void ApplyConfig(AppConfig config)
        {
            lock (_sync)
            {
                Config = config;
                Config.Save();
            }

            RemoveWhitelistedBlocks();
            OnConfigChanged();
            Start();
        }

        public void CheckNow()
        {
            CheckNowCore(resetScheduleAfterCheck: true);
        }

        private void CheckNowFromTimer()
        {
            CheckNowCore(resetScheduleAfterCheck: false);
        }

        private void CheckNowCore(bool resetScheduleAfterCheck)
        {
            var skippedBecauseRunning = false;

            lock (_sync)
            {
                if (_disposed || !Config.MonitorEnabled)
                {
                    return;
                }

                if (resetScheduleAfterCheck)
                {
                    StopTimerOnly();
                }

                if (_isChecking)
                {
                    skippedBecauseRunning = true;
                }
                else
                {
                    StopTimerOnly();
                    _isChecking = true;
                }
            }

            if (skippedBecauseRunning)
            {
                OnLog("A check is already running; the new request was skipped.");
                return;
            }

            try
            {
                RunCheck();
            }
            catch (Exception ex)
            {
                OnLog("Error: " + ex.Message);
            }
            finally
            {
                lock (_sync)
                {
                    _isChecking = false;
                    if (!_disposed && Config.MonitorEnabled)
                    {
                        ScheduleTimerLocked(TimeSpan.FromMinutes(Config.CheckIntervalMinutes));
                    }
                }
            }
        }

        public void Unblock(string ipAddress)
        {
            RemoveBlockedIps(new[] { ipAddress }, "Block removed");
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                StopTimerOnly();
            }
        }

        private void RunCheck()
        {
            AppConfig snapshot;
            lock (_sync)
            {
                snapshot = Config.Clone();
            }

            var nowUtc = DateTime.UtcNow;
            var fromUtc = snapshot.LastCheckedUtc == DateTime.MinValue
                ? nowUtc.AddMinutes(-snapshot.CheckIntervalMinutes)
                : snapshot.LastCheckedUtc;

            if (fromUtc > nowUtc)
            {
                fromUtc = nowUtc.AddMinutes(-snapshot.CheckIntervalMinutes);
            }

            OnLog("Check started: " + fromUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + " - " + nowUtc.ToLocalTime().ToString("HH:mm:ss"));

            var scan = _scanner.Scan(fromUtc, nowUtc);
            var whitelist = new WhitelistMatcher(snapshot.Whitelist);
            var blocked = new List<BlockedIpRecord>();
            var candidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var firewallBlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var firewallReadSucceeded = false;

            try
            {
                var ipsToVerify = snapshot.BlockedIps
                    .Select(item => item.IpAddress)
                    .Concat(scan.CountsByIp.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                firewallBlocked = _firewall.FindAlreadyBlockedIps(ipsToVerify);
                firewallReadSucceeded = true;
            }
            catch (Exception ex)
            {
                OnLog("Existing firewall rules could not be read; firewall de-duplication was skipped: " + ex.Message);
            }

            if (firewallReadSucceeded)
            {
                var staleRecords = snapshot.BlockedIps
                    .Where(item => !firewallBlocked.Contains(item.IpAddress))
                    .Where(item => !whitelist.IsWhitelisted(item.IpAddress))
                    .GroupBy(item => item.IpAddress, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new KeyValuePair<string, int>(group.Key, Math.Max(snapshot.FailureThreshold, group.Max(item => item.FailureCount))))
                    .ToList();

                RemoveStaleConfiguredBlocks(staleRecords.Select(item => item.Key));
                foreach (var item in staleRecords)
                {
                    candidates[item.Key] = item.Value;
                }
            }

            HashSet<string> alreadyBlocked;
            lock (_sync)
            {
                alreadyBlocked = new HashSet<string>(Config.BlockedIps.Select(item => item.IpAddress), StringComparer.OrdinalIgnoreCase);
            }

            foreach (var pair in scan.CountsByIp.OrderByDescending(item => item.Value))
            {
                var ip = pair.Key;
                var count = pair.Value;

                if (count < snapshot.FailureThreshold)
                {
                    continue;
                }

                if (whitelist.IsWhitelisted(ip))
                {
                    OnLog("Whitelisted IP skipped: " + ip + " (" + count + ")");
                    continue;
                }

                if (alreadyBlocked.Contains(ip))
                {
                    OnLog("Already blocked by RDP Guard: " + ip + " (" + count + ")");
                    continue;
                }

                if (firewallBlocked.Contains(ip))
                {
                    OnLog("Already blocked in firewall: " + ip + " (" + count + ")");
                    continue;
                }

                candidates[ip] = candidates.TryGetValue(ip, out var existing)
                    ? Math.Max(existing, count)
                    : count;
                alreadyBlocked.Add(ip);
            }

            if (candidates.Count > 0)
            {
                var firewallRule = _firewall.BlockIps(candidates.Keys);
                var record = new BlockedIpRecord
                {
                    BlockedAtUtc = DateTime.UtcNow,
                    RuleName = firewallRule.RuleName,
                    InboundRuleName = firewallRule.RuleName,
                    OutboundRuleName = string.Empty
                };

                foreach (var item in candidates)
                {
                    blocked.Add(new BlockedIpRecord
                    {
                        IpAddress = item.Key,
                        BlockedAtUtc = record.BlockedAtUtc,
                        FailureCount = item.Value,
                        RuleName = record.RuleName,
                        InboundRuleName = record.InboundRuleName,
                        OutboundRuleName = string.Empty
                    });
                }

                OnLog("Blocked in one firewall rule: " + firewallRule.RuleName + " (" + candidates.Count + " IP)");
            }

            lock (_sync)
            {
                Config.LastCheckedUtc = nowUtc;
                Config.BlockedIps.AddRange(blocked);
                Config.Normalize();
                Config.Save();
            }

            OnConfigChanged();
            CheckCompleted?.Invoke(this, new CheckCompletedEventArgs(scan.EventsInspected, scan.CountsByIp.Count, blocked.Count));
            OnLog("Check finished. Events: " + scan.EventsInspected + ", IPs: " + scan.CountsByIp.Count + ", new blocks: " + blocked.Count);
        }

        private void RemoveWhitelistedBlocks()
        {
            var whitelist = new WhitelistMatcher(Config.Whitelist);
            var ips = Config.BlockedIps
                .Where(item => whitelist.IsWhitelisted(item.IpAddress))
                .Select(item => item.IpAddress)
                .ToList();
            RemoveBlockedIps(ips, "Block removed because of whitelist");
        }

        private void RemoveBlockedIps(IEnumerable<string> ipAddresses, string logPrefix)
        {
            var requested = new HashSet<string>(
                (ipAddresses ?? Enumerable.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim()),
                StringComparer.OrdinalIgnoreCase);

            if (requested.Count == 0)
            {
                return;
            }

            List<string> removedIps;
            var warnings = new List<string>();
            lock (_sync)
            {
                var matches = Config.BlockedIps
                    .Where(item => requested.Contains(item.IpAddress))
                    .ToList();

                if (matches.Count == 0)
                {
                    return;
                }

                var affectedRules = matches
                    .SelectMany(GetRuleNames)
                    .Where(rule => !string.IsNullOrWhiteSpace(rule))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Config.BlockedIps = Config.BlockedIps
                    .Where(item => !requested.Contains(item.IpAddress))
                    .ToList();

                foreach (var ruleName in affectedRules)
                {
                    var remainingIps = Config.BlockedIps
                        .Where(item => GetRuleNames(item).Contains(ruleName, StringComparer.OrdinalIgnoreCase))
                        .Select(item => item.IpAddress)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (remainingIps.Count > 0 && FirewallManager.IsManagedRuleName(ruleName))
                    {
                        try
                        {
                            _firewall.UpdateRuleRemoteIps(ruleName, remainingIps);
                        }
                        catch (Exception ex)
                        {
                            warnings.Add("Rule scope could not be updated (" + ruleName + "): " + ex.Message);
                        }
                    }
                    else if (FirewallManager.IsManagedRuleName(ruleName))
                    {
                        try
                        {
                            _firewall.RemoveRule(ruleName);
                        }
                        catch (Exception ex)
                        {
                            warnings.Add("Rule could not be deleted (" + ruleName + "): " + ex.Message);
                        }
                    }
                }

                removedIps = matches.Select(item => item.IpAddress).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Config.Save();
            }

            OnConfigChanged();
            foreach (var warning in warnings)
            {
                OnLog(warning);
            }

            foreach (var ip in removedIps)
            {
                OnLog(logPrefix + ": " + ip);
            }
        }

        private void RemoveStaleConfiguredBlocks(IEnumerable<string> ipAddresses)
        {
            var requested = new HashSet<string>(
                (ipAddresses ?? Enumerable.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim()),
                StringComparer.OrdinalIgnoreCase);

            if (requested.Count == 0)
            {
                return;
            }

            List<string> removedIps;
            lock (_sync)
            {
                var matches = Config.BlockedIps
                    .Where(item => requested.Contains(item.IpAddress))
                    .ToList();

                if (matches.Count == 0)
                {
                    return;
                }

                Config.BlockedIps = Config.BlockedIps
                    .Where(item => !requested.Contains(item.IpAddress))
                    .ToList();

                removedIps = matches
                    .Select(item => item.IpAddress)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Config.Save();
            }

            OnConfigChanged();
            foreach (var ip in removedIps)
            {
                OnLog("No longer blocked in firewall; will be evaluated again: " + ip);
            }
        }

        private static IEnumerable<string> GetRuleNames(BlockedIpRecord item)
        {
            if (item == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(item.RuleName))
            {
                yield return item.RuleName;
            }

            if (!string.IsNullOrWhiteSpace(item.InboundRuleName))
            {
                yield return item.InboundRuleName;
            }

            if (!string.IsNullOrWhiteSpace(item.OutboundRuleName))
            {
                yield return item.OutboundRuleName;
            }
        }

        private void StopTimerOnly()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private void ScheduleTimerLocked(TimeSpan dueTime)
        {
            StopTimerOnly();
            var safeDueTime = dueTime < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : dueTime;
            _timer = new Timer(_ => CheckNowFromTimer(), null, safeDueTime, Timeout.InfiniteTimeSpan);
        }

        private void OnLog(string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message;
            AppLogger.Write(message);
            Log?.Invoke(this, line);
        }

        private void OnConfigChanged()
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }

    }

    public sealed class CheckCompletedEventArgs : EventArgs
    {
        public CheckCompletedEventArgs(int inspectedEvents, int uniqueIps, int blockedIps)
        {
            InspectedEvents = inspectedEvents;
            UniqueIps = uniqueIps;
            BlockedIps = blockedIps;
        }

        public int InspectedEvents { get; }
        public int UniqueIps { get; }
        public int BlockedIps { get; }
    }
}
