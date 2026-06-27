using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RDPGuard
{
    public sealed class FirewallManager
    {
        private const string Prefix = "RDP_GUARD_";
        private const int MaxRemoteIpArgumentLength = 22000;

        public FirewallBlockResult BlockIps(IEnumerable<string> ipAddresses)
        {
            var ips = NormalizeIps(ipAddresses).ToList();

            if (ips.Count == 0)
            {
                throw new ArgumentException("No valid IP address was provided for blocking.", nameof(ipAddresses));
            }

            var baseRuleName = CreateRuleName();
            var chunks = SplitIntoSafeRuleChunks(ips);
            var rules = new List<FirewallRuleScope>();
            var createdRuleNames = new List<string>();
            if (chunks.Count > 1)
            {
                AppLogger.Write("Firewall block add split into " + chunks.Count + " rules because the remote IP list is too large for one safe netsh call.");
            }

            try
            {
                for (var index = 0; index < chunks.Count; index++)
                {
                    var chunk = chunks[index];
                    var ruleName = chunks.Count == 1
                        ? baseRuleName
                        : baseRuleName + "_" + (index + 1).ToString("000");

                    AppLogger.Debug("Firewall block add: rule=" + ruleName + ", ipCount=" + chunk.Count + ", sample=" + FormatSample(chunk, 10));
                    RunNetsh("advfirewall firewall add rule name=\"" + ruleName + "\" dir=in action=block remoteip=" + string.Join(",", chunk) + " protocol=any profile=any enable=yes");
                    createdRuleNames.Add(ruleName);
                    rules.Add(new FirewallRuleScope
                    {
                        RuleName = ruleName,
                        IpAddresses = chunk
                    });
                }
            }
            catch
            {
                RollBackCreatedRules(createdRuleNames);
                throw;
            }


            return new FirewallBlockResult(rules);
        }

        public HashSet<string> FindAlreadyBlockedIps(IEnumerable<string> ipAddresses)
        {
            var ips = NormalizeIps(ipAddresses).ToList();
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ips.Count == 0)
            {
                return result;
            }

            var scopes = GetInboundBlockRemoteAddressScopes().ToList();
            var matcher = new FirewallScopeMatcher(scopes);
            foreach (var ip in ips)
            {
                if (matcher.Contains(ip))
                {
                    result.Add(ip);
                }
            }

            AppLogger.Debug("Firewall existing block lookup: inputIps=" + ips.Count + ", scopes=" + scopes.Count + ", matches=" + result.Count);
            return result;
        }

        public void UpdateRuleRemoteIps(string ruleName, IEnumerable<string> ipAddresses)
        {
            if (!IsManagedRuleName(ruleName))
            {
                return;
            }

            var ips = NormalizeIps(ipAddresses).ToList();
            if (ips.Count == 0)
            {
                RemoveRule(ruleName);
                return;
            }

            AppLogger.Debug("Firewall scope update: rule=" + ruleName + ", ipCount=" + ips.Count + ", sample=" + FormatSample(ips, 10));
            RunNetsh("advfirewall firewall set rule name=\"" + ruleName + "\" new remoteip=" + string.Join(",", ips));
        }

        public void RemoveRule(string ruleName)
        {
            if (!IsManagedRuleName(ruleName))
            {
                return;
            }

            AppLogger.Debug("Firewall rule delete: rule=" + ruleName);
            RunNetsh("advfirewall firewall delete rule name=\"" + ruleName + "\"", allowFailure: true);
        }

        public void RemoveRules(IEnumerable<string> ruleNames)
        {
            foreach (var ruleName in (ruleNames ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                RemoveRule(ruleName);
            }
        }

        public static bool IsManagedRuleName(string ruleName)
        {
            if (string.IsNullOrWhiteSpace(ruleName) ||
                !ruleName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ruleName.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
        }

        private static IEnumerable<string> NormalizeIps(IEnumerable<string> ipAddresses)
        {
            return (ipAddresses ?? Enumerable.Empty<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => IPAddress.TryParse(value, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        }

        private static string CreateRuleName()
        {
            return Prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        }

        private static List<List<string>> SplitIntoSafeRuleChunks(IReadOnlyList<string> ips)
        {
            var chunks = new List<List<string>>();
            var current = new List<string>();
            var currentLength = 0;

            foreach (var ip in ips)
            {
                var addedLength = ip.Length + (current.Count == 0 ? 0 : 1);
                if (current.Count > 0 && currentLength + addedLength > MaxRemoteIpArgumentLength)
                {
                    chunks.Add(current);
                    current = new List<string>();
                    currentLength = 0;
                    addedLength = ip.Length;
                }

                current.Add(ip);
                currentLength += addedLength;
            }

            if (current.Count > 0)
            {
                chunks.Add(current);
            }

            return chunks;
        }

        private static void RunNetsh(string arguments, bool allowFailure = false)
        {
            var result = ProcessRunner.Run("netsh.exe", arguments, 30000);
            if (result.ExitCode != 0 && !allowFailure)
            {
                throw new InvalidOperationException("netsh exit code " + result.ExitCode + ": " + result.CombinedOutput);
            }
        }

        private static void RollBackCreatedRules(IEnumerable<string> ruleNames)
        {
            var names = (ruleNames ?? Enumerable.Empty<string>())
                .Where(IsManagedRuleName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                return;
            }

            AppLogger.Write("Firewall block add failed; rolling back " + names.Count + " partially-created rule(s).");
            foreach (var ruleName in names)
            {
                try
                {
                    RunNetsh("advfirewall firewall delete rule name=\"" + ruleName + "\"", allowFailure: true);
                }
                catch (Exception ex)
                {
                    AppLogger.WriteException("Firewall rollback failed for " + ruleName, ex);
                }
            }
        }

        private static IEnumerable<string> GetInboundBlockRemoteAddressScopes()
        {
            var stopwatch = Stopwatch.StartNew();
            var scopes = new List<string>();
            var ruleCount = 0;
            var broadBlockRuleCount = 0;
            object policy = null;
            object rulesObject = null;

            try
            {
                var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (policyType == null)
                {
                    throw new InvalidOperationException("Windows Firewall COM API is unavailable.");
                }

                policy = Activator.CreateInstance(policyType);
                rulesObject = GetProperty(policy, "Rules");
                var rules = rulesObject as IEnumerable;
                if (rules == null)
                {
                    throw new InvalidOperationException("Windows Firewall COM API did not return a rule collection.");
                }

                foreach (var rule in rules)
                {
                    ruleCount++;
                    try
                    {
                        if (!IsBroadInboundBlockRule(rule))
                        {
                            continue;
                        }

                        broadBlockRuleCount++;
                        var remoteAddresses = Convert.ToString(GetProperty(rule, "RemoteAddresses"));
                        if (string.IsNullOrWhiteSpace(remoteAddresses))
                        {
                            continue;
                        }

                        scopes.AddRange(remoteAddresses
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(item => item.Trim())
                            .Where(item => item.Length > 0));
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WriteException("Firewall COM rule parse failed", ex);
                    }
                    finally
                    {
                        ReleaseComObject(rule);
                    }
                }
            }
            finally
            {
                ReleaseComObject(rulesObject);
                ReleaseComObject(policy);
            }

            stopwatch.Stop();
            scopes = scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            AppLogger.Debug("Firewall COM scope read: totalRules=" + ruleCount +
                            ", broadBlockRules=" + broadBlockRuleCount +
                            ", scopeCount=" + scopes.Count +
                            ", elapsedMs=" + stopwatch.ElapsedMilliseconds +
                            ", sample=" + FormatSample(scopes, 10));
            return scopes;
        }

        private static bool IsBroadInboundBlockRule(object rule)
        {
            var enabled = Convert.ToBoolean(GetProperty(rule, "Enabled"));
            var direction = Convert.ToInt32(GetProperty(rule, "Direction"));
            var action = Convert.ToInt32(GetProperty(rule, "Action"));
            var protocol = Convert.ToInt32(GetProperty(rule, "Protocol"));
            var applicationName = Convert.ToString(GetProperty(rule, "ApplicationName"));
            var serviceName = Convert.ToString(GetProperty(rule, "ServiceName"));

            return enabled &&
                   direction == 1 &&
                   action == 0 &&
                   protocol == 256 &&
                   string.IsNullOrWhiteSpace(applicationName) &&
                   string.IsNullOrWhiteSpace(serviceName);
        }

        private static object GetProperty(object instance, string propertyName)
        {
            return instance.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty,
                null,
                instance,
                null);
        }

        private static void ReleaseComObject(object value)
        {
            if (value == null)
            {
                return;
            }

            try
            {
                if (Marshal.IsComObject(value))
                {
                    Marshal.FinalReleaseComObject(value);
                }
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Firewall COM release failed", ex);
            }
        }

        private static string FormatSample(IEnumerable<string> values, int limit)
        {
            return string.Join(", ", (values ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Take(limit));
        }

        private sealed class FirewallScopeMatcher
        {
            private readonly HashSet<IPAddress> _singleIps = new HashSet<IPAddress>();
            private readonly List<ScopeEntry> _rangeEntries = new List<ScopeEntry>();
            private bool _matchesAll;

            public FirewallScopeMatcher(IEnumerable<string> scopes)
            {
                foreach (var scope in scopes ?? Enumerable.Empty<string>())
                {
                    if (!ScopeEntry.TryParse(scope, out var entry))
                    {
                        continue;
                    }

                    if (entry.IsAny)
                    {
                        _matchesAll = true;
                        continue;
                    }

                    if (entry.IsSingle)
                    {
                        _singleIps.Add(entry.SingleAddress);
                        continue;
                    }

                    _rangeEntries.Add(entry);
                }
            }

            public bool Contains(string ipText)
            {
                if (!IPAddress.TryParse(ipText, out var ip))
                {
                    return false;
                }

                if (_matchesAll || _singleIps.Contains(ip))
                {
                    return true;
                }

                return _rangeEntries.Any(entry => entry.Contains(ip));
            }
        }

        private sealed class ScopeEntry
        {
            private readonly IPAddress _start;
            private readonly IPAddress _end;
            private readonly int _prefixLength;
            private readonly EntryKind _kind;

            private ScopeEntry(EntryKind kind, IPAddress start = null, IPAddress end = null, int prefixLength = 0)
            {
                _kind = kind;
                _start = start;
                _end = end;
                _prefixLength = prefixLength;
            }

            public static bool TryParse(string value, out ScopeEntry entry)
            {
                entry = null;
                value = (value ?? string.Empty).Trim();
                if (value.Length == 0)
                {
                    return false;
                }

                if (string.Equals(value, "*", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "Any", StringComparison.OrdinalIgnoreCase))
                {
                    entry = new ScopeEntry(EntryKind.Any);
                    return true;
                }

                var dashIndex = value.IndexOf('-');
                if (dashIndex > 0)
                {
                    var first = value.Substring(0, dashIndex);
                    var last = value.Substring(dashIndex + 1);
                    if (IPAddress.TryParse(first, out var start) && IPAddress.TryParse(last, out var end) && start.AddressFamily == end.AddressFamily)
                    {
                        entry = new ScopeEntry(EntryKind.Range, start, end);
                        return true;
                    }

                    return false;
                }

                var slashIndex = value.IndexOf('/');
                if (slashIndex > 0)
                {
                    var addressPart = value.Substring(0, slashIndex);
                    var prefixPart = value.Substring(slashIndex + 1);
                    if (IPAddress.TryParse(addressPart, out var network) && TryParsePrefix(prefixPart, network.AddressFamily, out var prefixLength))
                    {
                        entry = new ScopeEntry(EntryKind.Cidr, network, prefixLength: prefixLength);
                        return true;
                    }

                    return false;
                }

                if (IPAddress.TryParse(value, out var ip))
                {
                    entry = new ScopeEntry(EntryKind.Single, ip);
                    return true;
                }

                return false;
            }

            public bool IsAny => _kind == EntryKind.Any;

            public bool IsSingle => _kind == EntryKind.Single;

            public IPAddress SingleAddress => _start;

            public bool Contains(IPAddress ip)
            {
                if (_kind == EntryKind.Any)
                {
                    return true;
                }

                if (_start == null || ip.AddressFamily != _start.AddressFamily)
                {
                    return false;
                }

                if (_kind == EntryKind.Single)
                {
                    return ip.Equals(_start);
                }

                if (_kind == EntryKind.Range)
                {
                    return CompareBytes(ip.GetAddressBytes(), _start.GetAddressBytes()) >= 0 &&
                           CompareBytes(ip.GetAddressBytes(), _end.GetAddressBytes()) <= 0;
                }

                return ContainsCidr(ip, _start, _prefixLength);
            }

            private static bool TryParsePrefix(string value, AddressFamily addressFamily, out int prefixLength)
            {
                if (int.TryParse(value, out prefixLength))
                {
                    var maxPrefix = addressFamily == AddressFamily.InterNetwork ? 32 : 128;
                    return prefixLength >= 0 && prefixLength <= maxPrefix;
                }

                if (addressFamily == AddressFamily.InterNetwork && IPAddress.TryParse(value, out var mask))
                {
                    var bytes = mask.GetAddressBytes();
                    if (bytes.Length != 4)
                    {
                        prefixLength = 0;
                        return false;
                    }

                    prefixLength = 0;
                    var zeroSeen = false;
                    foreach (var b in bytes)
                    {
                        for (var bit = 7; bit >= 0; bit--)
                        {
                            var isOne = (b & (1 << bit)) != 0;
                            if (isOne && zeroSeen)
                            {
                                return false;
                            }

                            if (isOne)
                            {
                                prefixLength++;
                            }
                            else
                            {
                                zeroSeen = true;
                            }
                        }
                    }

                    return true;
                }

                prefixLength = 0;
                return false;
            }

            private static bool ContainsCidr(IPAddress ip, IPAddress network, int prefixLength)
            {
                var ipBytes = ip.GetAddressBytes();
                var networkBytes = network.GetAddressBytes();
                var fullBytes = prefixLength / 8;
                var remainingBits = prefixLength % 8;

                for (var index = 0; index < fullBytes; index++)
                {
                    if (ipBytes[index] != networkBytes[index])
                    {
                        return false;
                    }
                }

                if (remainingBits == 0)
                {
                    return true;
                }

                var mask = (byte)(0xFF << (8 - remainingBits));
                return (ipBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
            }

            private static int CompareBytes(byte[] left, byte[] right)
            {
                for (var index = 0; index < left.Length; index++)
                {
                    var diff = left[index].CompareTo(right[index]);
                    if (diff != 0)
                    {
                        return diff;
                    }
                }

                return 0;
            }
        }

        private enum EntryKind
        {
            Any,
            Single,
            Range,
            Cidr
        }
    }

    public sealed class FirewallBlockResult
    {
        public FirewallBlockResult(List<FirewallRuleScope> rules)
        {
            Rules = rules ?? new List<FirewallRuleScope>();
        }

        public string RuleName => Rules.Count > 0 ? Rules[0].RuleName : string.Empty;

        public List<FirewallRuleScope> Rules { get; }
    }

    public sealed class FirewallRuleScope
    {
        public string RuleName { get; set; }
        public List<string> IpAddresses { get; set; } = new List<string>();
    }
}
