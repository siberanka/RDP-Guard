using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace RDPGuard
{
    internal sealed class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string CurrentTag { get; set; }
        public string LatestTag { get; set; }
        public string LatestUrl { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal static class UpdateChecker
    {
        private static readonly Regex TagRegex = new Regex("\"tag_name\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex UrlRegex = new Regex("\"html_url\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Compiled);

        public static UpdateCheckResult CheckLatest()
        {
            var currentTag = AppInfo.VersionTag;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(AppInfo.LatestReleaseApiUrl);
                request.Method = "GET";
                request.Accept = "application/vnd.github+json";
                request.UserAgent = "RDPGuard/" + AppInfo.Version;
                request.Timeout = 7000;
                request.ReadWriteTimeout = 7000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var latestTag = Extract(json, TagRegex);
                    if (string.IsNullOrWhiteSpace(latestTag))
                    {
                        throw new InvalidOperationException("The release response did not contain a tag.");
                    }

                    var latestUrl = Extract(json, UrlRegex);
                    return new UpdateCheckResult
                    {
                        Success = true,
                        CurrentTag = currentTag,
                        LatestTag = latestTag,
                        LatestUrl = string.IsNullOrWhiteSpace(latestUrl) ? AppInfo.RepositoryUrl : latestUrl,
                        IsUpdateAvailable = CompareTags(latestTag, currentTag) > 0
                    };
                }
            }
            catch (Exception ex)
            {
                AppLogger.WriteException("Update check failed", ex);
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentTag = currentTag,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static string Extract(string value, Regex regex)
        {
            var match = regex.Match(value ?? string.Empty);
            return match.Success ? Regex.Unescape(match.Groups["value"].Value) : string.Empty;
        }

        private static int CompareTags(string left, string right)
        {
            var leftParts = ParseTag(left);
            var rightParts = ParseTag(right);
            var length = Math.Max(leftParts.Length, rightParts.Length);
            for (var index = 0; index < length; index++)
            {
                var leftValue = index < leftParts.Length ? leftParts[index] : 0;
                var rightValue = index < rightParts.Length ? rightParts[index] : 0;
                var comparison = leftValue.CompareTo(rightValue);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static int[] ParseTag(string tag)
        {
            var matches = Regex.Matches(tag ?? string.Empty, "\\d+");
            var result = new int[matches.Count];
            for (var index = 0; index < matches.Count; index++)
            {
                result[index] = int.TryParse(matches[index].Value, out var value) ? value : 0;
            }

            return result;
        }
    }
}
