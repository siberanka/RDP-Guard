using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace RDPGuard
{
    public sealed class WhitelistMatcher
    {
        private readonly List<Entry> _entries;

        public WhitelistMatcher(IEnumerable<string> values)
        {
            _entries = (values ?? Enumerable.Empty<string>())
                .Select(value => Entry.TryParse(value, out var entry) ? entry : null)
                .Where(entry => entry != null)
                .ToList();
        }

        public bool IsWhitelisted(string ipText)
        {
            if (!IPAddress.TryParse(ipText, out var ip))
            {
                return true;
            }

            return _entries.Any(entry => entry.Contains(ip));
        }

        private sealed class Entry
        {
            private readonly IPAddress _network;
            private readonly int _prefixLength;
            private readonly bool _isCidr;

            private Entry(IPAddress network, int prefixLength, bool isCidr)
            {
                _network = network;
                _prefixLength = prefixLength;
                _isCidr = isCidr;
            }

            public static bool TryParse(string value, out Entry entry)
            {
                entry = null;
                value = (value ?? string.Empty).Trim();
                if (value.Length == 0)
                {
                    return false;
                }

                var slash = value.IndexOf('/');
                if (slash < 0)
                {
                    if (!IPAddress.TryParse(value, out var singleIp))
                    {
                        return false;
                    }

                    entry = new Entry(singleIp, singleIp.AddressFamily == AddressFamily.InterNetwork ? 32 : 128, false);
                    return true;
                }

                var addressPart = value.Substring(0, slash);
                var prefixPart = value.Substring(slash + 1);
                if (!IPAddress.TryParse(addressPart, out var network) || !int.TryParse(prefixPart, out var prefixLength))
                {
                    return false;
                }

                var maxPrefix = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                if (prefixLength < 0 || prefixLength > maxPrefix)
                {
                    return false;
                }

                entry = new Entry(network, prefixLength, true);
                return true;
            }

            public bool Contains(IPAddress ip)
            {
                if (ip.AddressFamily != _network.AddressFamily)
                {
                    return false;
                }

                if (!_isCidr)
                {
                    return ip.Equals(_network);
                }

                var ipBytes = ip.GetAddressBytes();
                var networkBytes = _network.GetAddressBytes();
                var fullBytes = _prefixLength / 8;
                var remainingBits = _prefixLength % 8;

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
        }
    }
}
