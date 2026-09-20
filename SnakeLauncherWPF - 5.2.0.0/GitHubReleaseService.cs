using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SnakeLauncherWPF
{
    // ==================== GitHub Release 模型 ====================
    public class GitHubRelease
    {
        public long Id { get; set; }
        public string TagName { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        public DateTime PublishedAt { get; set; }
        public List<GitHubReleaseAsset> Assets { get; set; } = new List<GitHubReleaseAsset>();

        /// <summary>解析后的版本号（去掉 v 前缀）</summary>
        public Version ParsedVersion { get; set; }
        public GitHubRepoConfig RepoConfig { get; set; }
    }

    public class GitHubReleaseAsset
    {
        public string Name { get; set; }
        public string BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public int DownloadCount { get; set; }

        /// <summary>★ 从文件名解析出的版本号，用于标识游戏包</summary>
        public Version ParsedVersion { get; set; }

        /// <summary>★ 是否符合 SnakeGameWpf_x.x.x.x.zip 命名规范</summary>
        public bool IsGamePackage { get; set; }
        public GitHubRepoConfig RepoConfig { get; set; }
    }

    // ==================== 原始 JSON 模型 ====================
    internal class RawGitHubRelease
    {
        public long id { get; set; }
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public DateTime published_at { get; set; }
        public List<RawGitHubAsset> assets { get; set; }
    }

    internal class RawGitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
        public string content_type { get; set; }
        public int download_count { get; set; }
    }

    // ==================== 仓库配置 ====================
    public class GitHubRepoConfig
    {
        /// <summary>仓库所有者</summary>
        public string Owner { get; set; }

        /// <summary>仓库名</summary>
        public string Repo { get; set; }

        /// <summary>显示名称</summary>
        public string DisplayName { get; set; }

        /// <summary>是否为 Python 仓库（需要自动安装依赖）</summary>
        public bool IsPythonRepo { get; set; }

        /// <summary>游戏包 zip 文件名正则匹配</summary>
        public System.Text.RegularExpressions.Regex GamePackagePattern { get; set; }

        /// <summary>提取版本号的正则分组索引（默认 1）</summary>
        public int VersionGroupIndex { get; set; } = 1;
    }

    public static class GitHubReleaseService
    {
        // ============================================================
        // ★ 仓库列表配置
        // ============================================================
        public static readonly List<GitHubRepoConfig> Repos = new List<GitHubRepoConfig>
    {
        // 仓库 1：SnakeGameWpf（C# WPF 游戏）
        new GitHubRepoConfig
        {
            Owner = "MMA131845",
            Repo = "SnakeGameWpf",
            DisplayName = "SnakeGameWpf（C#）",
            IsPythonRepo = false,
            GamePackagePattern = new Regex(
                @"^SnakeGameWpf_(\d+(?:\.\d+){1,3})\.zip$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            VersionGroupIndex = 1
        },

        // 仓库 2：Snake-Game-Python-Edition-（Python 游戏）
        new GitHubRepoConfig
        {
            Owner = "MMA131845",
            Repo = "Snake-Game-Python-Edition-",
            DisplayName = "贪吃蛇 Python 版",
            IsPythonRepo = true,
            // 匹配 SnakeGamePython_x.x.x.zip 或 SnakeGamePython_x.x.x.x.zip
            GamePackagePattern = new Regex(
                @"^SnakeGamePython_(\d+(?:\.\d+){1,3})\.zip$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            VersionGroupIndex = 1
        }
    };

        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SnakeLauncherWPF-Downloader/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            return client;
        }

        /// <summary>
        /// 获取指定仓库的所有 Release
        /// </summary>
        public static async Task<List<GitHubRelease>> GetReleasesAsync(
            GitHubRepoConfig repoConfig,
            CancellationToken ct = default)
        {
            string url = $"https://api.github.com/repos/{repoConfig.Owner}/{repoConfig.Repo}/releases?per_page=100";

            using (var response = await _http.GetAsync(url, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var rawList = JsonSerializer.Deserialize<List<RawGitHubRelease>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (rawList == null) return new List<GitHubRelease>();

                var result = new List<GitHubRelease>();

                foreach (var raw in rawList)
                {
                    if (raw.draft || raw.prerelease) continue;

                    var release = new GitHubRelease
                    {
                        Id = raw.id,
                        TagName = raw.tag_name,
                        Name = string.IsNullOrWhiteSpace(raw.name) ? raw.tag_name : raw.name,
                        Body = raw.body ?? "",
                        Draft = raw.draft,
                        Prerelease = raw.prerelease,
                        PublishedAt = raw.published_at,
                        ParsedVersion = ParseTagVersion(raw.tag_name)
                    };

                    if (raw.assets != null)
                    {
                        foreach (var a in raw.assets)
                        {
                            var asset = new GitHubReleaseAsset
                            {
                                Name = a.name,
                                BrowserDownloadUrl = a.browser_download_url,
                                Size = a.size,
                                ContentType = a.content_type,
                                DownloadCount = a.download_count
                            };

                            // 用仓库配置的正则匹配
                            var pkgVersion = ParseAssetVersion(a.name, repoConfig.GamePackagePattern,
                                repoConfig.VersionGroupIndex);
                            asset.ParsedVersion = pkgVersion;
                            asset.IsGamePackage = pkgVersion != null;
                            asset.RepoConfig = repoConfig;

                            release.Assets.Add(asset);
                        }
                    }

                    release.RepoConfig = repoConfig;
                    result.Add(release);
                }

                return result
                    .OrderByDescending(r => r.ParsedVersion ?? new Version(0, 0, 0, 0))
                    .ThenByDescending(r => r.PublishedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// 用指定正则从文件名解析版本号
        /// </summary>
        public static Version ParseAssetVersion(string fileName, Regex pattern, int groupIndex = 1)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            var m = pattern.Match(fileName.Trim());
            if (!m.Success) return null;

            string verStr = m.Groups[groupIndex].Value;
            return ParseVersionString(verStr);
        }

        public static Version ParseTagVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            string clean = tag.Trim().TrimStart('v', 'V');
            return ParseVersionString(clean);
        }

        private static Version ParseVersionString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var m = Regex.Match(s.Trim(), @"^(\d+(?:\.\d+){0,3})");
            if (!m.Success) return null;

            if (!Version.TryParse(m.Groups[1].Value, out var v)) return null;

            int major = v.Major;
            int minor = v.Minor >= 0 ? v.Minor : 0;
            int build = v.Build >= 0 ? v.Build : 0;
            int revision = v.Revision >= 0 ? v.Revision : 0;
            return new Version(major, minor, build, revision);
        }

        public static string ExtractSha256(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            var m = Regex.Match(body,
                @"<!--\s*SHA256\s*:\s*([a-fA-F0-9]{64})\s*-->",
                RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }
    }
}