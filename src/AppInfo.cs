using System.Reflection;

[assembly: AssemblyTitle("RDP Guard")]
[assembly: AssemblyDescription("Windows Security audit failure monitor and firewall guard for RDP.")]
[assembly: AssemblyCompany("siberanka")]
[assembly: AssemblyProduct("RDP Guard")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("2026.6.27.8")]
[assembly: AssemblyFileVersion("2026.6.27.8")]

namespace RDPGuard
{
    internal static class AppInfo
    {
        public const string Repository = "siberanka/RDP-Guard";
        public const string RepositoryUrl = "https://github.com/siberanka/RDP-Guard/";
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/siberanka/RDP-Guard/releases/latest";
        public const string Version = "2026.06.27.8";
        public const string VersionTag = "v2026.06.27.8";
    }
}
