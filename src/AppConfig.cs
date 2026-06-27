using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace RDPGuard
{
    public sealed class AppConfig
    {
        public const int DefaultFailureThreshold = 3;
        public const int MinFailureThreshold = 1;
        public const int MaxFailureThreshold = 1000;
        public const int DefaultCheckIntervalMinutes = 15;
        public const int MinCheckIntervalMinutes = 1;
        public const int MaxCheckIntervalMinutes = 1440;

        public int FailureThreshold { get; set; } = DefaultFailureThreshold;
        public int CheckIntervalMinutes { get; set; } = DefaultCheckIntervalMinutes;
        public bool MonitorEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; }
        public bool BlockOutbound { get; set; }
        public string LanguageCode { get; set; } = "en";
        public DateTime LastCheckedUtc { get; set; } = DateTime.MinValue;
        public List<string> Whitelist { get; set; } = new List<string> { "127.0.0.1", "::1" };
        public List<BlockedIpRecord> BlockedIps { get; set; } = new List<BlockedIpRecord>();

        [XmlIgnore]
        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RDPGuard");

        [XmlIgnore]
        public static string ConfigPath => Path.Combine(DataDirectory, "settings.xml");

        [XmlIgnore]
        public static string LogPath => Path.Combine(DataDirectory, "rdpguard.log");

        public static AppConfig Load()
        {
            Directory.CreateDirectory(DataDirectory);

            if (!File.Exists(ConfigPath))
            {
                var config = new AppConfig();
                config.Save();
                return config;
            }

            try
            {
                using (var stream = File.OpenRead(ConfigPath))
                {
                    var serializer = new XmlSerializer(typeof(AppConfig));
                    var config = (AppConfig)serializer.Deserialize(stream);
                    config.Normalize();
                    return config;
                }
            }
            catch
            {
                var backupPath = ConfigPath + ".broken_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(ConfigPath, backupPath, true);
                var config = new AppConfig();
                config.Save();
                return config;
            }
        }

        public void Save()
        {
            Normalize();
            Directory.CreateDirectory(DataDirectory);

            var tempPath = ConfigPath + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                var serializer = new XmlSerializer(typeof(AppConfig));
                serializer.Serialize(stream, this);
            }

            if (File.Exists(ConfigPath))
            {
                File.Replace(tempPath, ConfigPath, null);
            }
            else
            {
                File.Move(tempPath, ConfigPath);
            }
        }

        public AppConfig Clone()
        {
            Normalize();
            return new AppConfig
            {
                FailureThreshold = FailureThreshold,
                CheckIntervalMinutes = CheckIntervalMinutes,
                MonitorEnabled = MonitorEnabled,
                StartWithWindows = StartWithWindows,
                BlockOutbound = BlockOutbound,
                LanguageCode = LanguageCode,
                LastCheckedUtc = LastCheckedUtc,
                Whitelist = new List<string>(Whitelist ?? new List<string>()),
                BlockedIps = (BlockedIps ?? new List<BlockedIpRecord>())
                    .Select(item => new BlockedIpRecord
                    {
                        IpAddress = item.IpAddress,
                        BlockedAtUtc = item.BlockedAtUtc,
                        FailureCount = item.FailureCount,
                        RuleName = item.RuleName,
                        InboundRuleName = item.InboundRuleName,
                        OutboundRuleName = item.OutboundRuleName
                    })
                    .ToList()
            };
        }

        public void Normalize()
        {
            FailureThreshold = Clamp(FailureThreshold, MinFailureThreshold, MaxFailureThreshold);
            CheckIntervalMinutes = Clamp(CheckIntervalMinutes, MinCheckIntervalMinutes, MaxCheckIntervalMinutes);
            LanguageCode = Localization.NormalizeLanguageCode(LanguageCode);

            Whitelist = (Whitelist ?? new List<string>())
                .Select(item => (item ?? string.Empty).Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!Whitelist.Contains("127.0.0.1", StringComparer.OrdinalIgnoreCase))
            {
                Whitelist.Add("127.0.0.1");
            }

            if (!Whitelist.Contains("::1", StringComparer.OrdinalIgnoreCase))
            {
                Whitelist.Add("::1");
            }

            BlockedIps = (BlockedIps ?? new List<BlockedIpRecord>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.IpAddress))
                .Select(item =>
                {
                    item.IpAddress = item.IpAddress.Trim();
                    if (string.IsNullOrWhiteSpace(item.RuleName))
                    {
                        item.RuleName = !string.IsNullOrWhiteSpace(item.InboundRuleName)
                            ? item.InboundRuleName
                            : item.OutboundRuleName;
                    }

                    return item;
                })
                .GroupBy(item => item.IpAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.BlockedAtUtc).First())
                .OrderByDescending(item => item.BlockedAtUtc)
                .ToList();
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }

    public sealed class BlockedIpRecord
    {
        public string IpAddress { get; set; }
        public DateTime BlockedAtUtc { get; set; }
        public int FailureCount { get; set; }
        public string RuleName { get; set; }
        public string InboundRuleName { get; set; }
        public string OutboundRuleName { get; set; }
    }
}
