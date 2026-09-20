using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SnakeLauncherWPF
{
    // ==================== GitHub Release 响应模型 ====================
    internal class GitHubReleaseInfo
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public string published_at { get; set; }
        public GitHubAsset[] assets { get; set; }
    }

    internal class GitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
        public string content_type { get; set; }
    }

    // ==================== 检查更新结果模型 ====================
    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; }
        public string CurrentVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; }
        public string ReleasePageUrl { get; set; }
        public string ErrorMessage { get; set; }
    }

    // ==================== 更新服务 ====================
    public static class UpdateService
    {
        // ============================================================
        // ★ 需要修改的配置
        // ============================================================
        private const string GitHubOwner = "MMA131845";
        private const string GitHubRepo = "SnakeLauncherWPF";
        private const string AssetNameKeyword = "SnakeLauncherWPF";
        private const string MainExeName = "SnakeLauncherWPF.exe";
        private const string UpdaterExeName = "SnakeLauncherWPF.Updater.exe";

        // ============================================================
        // 内部字段
        // ============================================================
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseProxy = true
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SnakeLauncherWPF-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
            return client;
        }

        // ============================================================
        // 公开 API
        // ============================================================

        public static string GetReleasePageUrl()
        {
            return $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";
        }

        /// <summary>
        /// 获取当前本地程序集版本（四段格式）
        /// </summary>
        public static Version GetCurrentVersion()
        {
            try
            {
                var fileVersion = FileVersionInfo
                    .GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                    .FileVersion;

                if (!string.IsNullOrWhiteSpace(fileVersion)
                    && Version.TryParse(fileVersion, out var v1))
                {
                    return Normalize(v1);
                }
            }
            catch { }

            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null) return Normalize(v);
            }
            catch { }

            return new Version(0, 0, 0, 0);
        }

        private static Version Normalize(Version v)
        {
            int major = v.Major;
            int minor = v.Minor >= 0 ? v.Minor : 0;
            int build = v.Build >= 0 ? v.Build : 0;
            int revision = v.Revision >= 0 ? v.Revision : 0;
            return new Version(major, minor, build, revision);
        }

        /// <summary>
        /// 检查是否有可用更新
        /// </summary>
        public static async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = GetCurrentVersion().ToString(),
                ReleasePageUrl = GetReleasePageUrl()
            };

            try
            {
                string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                using (var response = await _http.GetAsync(apiUrl).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        result.ErrorMessage = $"GitHub API 返回 {(int)response.StatusCode} {response.ReasonPhrase}";
                        return result;
                    }

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var release = JsonSerializer.Deserialize<GitHubReleaseInfo>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (release == null || string.IsNullOrWhiteSpace(release.tag_name))
                    {
                        result.ErrorMessage = "未获取到有效的 Release 信息";
                        return result;
                    }

                    if (release.draft || release.prerelease)
                    {
                        result.ErrorMessage = "最新 Release 为草稿或预发布，已跳过";
                        return result;
                    }

                    string tagClean = release.tag_name.Trim().TrimStart('v', 'V');
                    Version latestVersion = ParseVersion(tagClean);
                    if (latestVersion == null)
                    {
                        result.ErrorMessage = $"无法解析版本号: {release.tag_name}";
                        return result;
                    }

                    result.LatestVersion = latestVersion.ToString();
                    result.ReleaseNotes = release.body ?? "";

                    Version currentVersion = GetCurrentVersion();
                    if (latestVersion <= currentVersion)
                    {
                        result.HasUpdate = false;
                        return result;
                    }

                    if (release.assets != null && release.assets.Length > 0)
                    {
                        foreach (var asset in release.assets)
                        {
                            if (string.IsNullOrEmpty(asset.name)) continue;
                            if (!asset.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                            if (asset.name.IndexOf(AssetNameKeyword, StringComparison.OrdinalIgnoreCase) < 0) continue;

                            result.DownloadUrl = asset.browser_download_url;
                            result.FileSize = asset.size;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(result.DownloadUrl))
                    {
                        result.ErrorMessage = "Release 中未找到匹配的 zip 资源";
                        return result;
                    }

                    if (!string.IsNullOrEmpty(result.ReleaseNotes))
                    {
                        var m = Regex.Match(result.ReleaseNotes,
                            @"<!--\s*SHA256\s*:\s*([a-fA-F0-9]{64})\s*-->",
                            RegexOptions.IgnoreCase);
                        if (m.Success)
                            result.Sha256 = m.Groups[1].Value.ToLowerInvariant();
                    }

                    result.HasUpdate = true;
                    result.ErrorMessage = null;
                    return result;
                }
            }
            catch (TaskCanceledException)
            {
                result.ErrorMessage = "检查更新超时，请检查网络";
                return result;
            }
            catch (HttpRequestException ex)
            {
                result.ErrorMessage = $"网络错误: {ex.Message}";
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"检查更新失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 下载更新包到临时目录
        /// </summary>
        public static async Task<string> DownloadUpdateAsync(
            string downloadUrl,
            IProgress<double> progress,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new ArgumentException("下载地址为空", nameof(downloadUrl));

            string tempDir = Path.Combine(Path.GetTempPath(), "SnakeLauncherUpdate");
            Directory.CreateDirectory(tempDir);

            try
            {
                foreach (var old in Directory.GetFiles(tempDir, "update_*.zip"))
                {
                    try { File.Delete(old); } catch { }
                }
            }
            catch { }

            string tempPath = Path.Combine(tempDir,
                $"update_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}.zip");

            using (var response = await _http.GetAsync(downloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using (var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = new FileStream(tempPath, FileMode.Create,
                    FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)
                        .ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;

                        if (totalBytes > 0 && progress != null)
                        {
                            double percent = (double)totalRead / totalBytes * 100.0;
                            if (percent > 100) percent = 100;
                            progress.Report(percent);
                        }
                    }

                    await fileStream.FlushAsync(ct).ConfigureAwait(false);
                }
            }

            progress?.Report(100.0);
            return tempPath;
        }

        /// <summary>
        /// 校验文件的 SHA256 是否匹配
        /// </summary>
        public static bool VerifySha256(string filePath, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256))
                return true;

            if (!File.Exists(filePath))
                return false;

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read, 81920, useAsync: false))
                using (var sha = SHA256.Create())
                {
                    byte[] hashBytes = sha.ComputeHash(stream);
                    string computed = BitConverter.ToString(hashBytes)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();

                    return string.Equals(computed, expectedSha256.Trim().ToLowerInvariant(),
                        StringComparison.Ordinal);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 计算文件的 SHA256（辅助方法）
        /// </summary>
        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read, 81920, useAsync: false))
                using (var sha = SHA256.Create())
                {
                    byte[] hashBytes = sha.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 启动 Updater.exe 并退出当前主程序
        /// 关键：先把 Updater 复制到临时目录再启动，避免 Updater 锁定自身导致无法自更新
        /// </summary>
        public static void LaunchUpdater(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath))
                throw new ArgumentException("zip 路径为空", nameof(zipPath));

            if (!File.Exists(zipPath))
                throw new FileNotFoundException("更新包不存在", zipPath);

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

            string currentExeName = MainExeName;
            try
            {
                var mainModule = Process.GetCurrentProcess().MainModule;
                if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                    currentExeName = Path.GetFileName(mainModule.FileName);
            }
            catch { }

            string updaterPathInApp = Path.Combine(appDir, UpdaterExeName);
            if (!File.Exists(updaterPathInApp))
            {
                throw new FileNotFoundException(
                    $"{UpdaterExeName} 不存在，无法完成更新。\n" +
                    "请确认发布包中包含了 Updater 程序。", updaterPathInApp);
            }

            // 把 Updater 复制到临时目录，从那里启动，避免锁定安装目录里的 Updater.exe
            string tempDir = Path.Combine(
                Path.GetTempPath(),
                "SnakeLauncherUpdater_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);

            string updaterTempPath = Path.Combine(tempDir, UpdaterExeName);

            try
            {
                // 1) 复制 Updater 主程序到临时目录
                File.Copy(updaterPathInApp, updaterTempPath, overwrite: true);

                // 2) 复制 Updater 的 .config
                string updaterConfigSrc = updaterPathInApp + ".config";
                if (File.Exists(updaterConfigSrc))
                {
                    try { File.Copy(updaterConfigSrc, updaterTempPath + ".config", overwrite: true); } catch { }
                }

                // 3) 复制安装目录下所有 .dll 依赖
                foreach (var dll in Directory.GetFiles(appDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string name = Path.GetFileName(dll);
                        File.Copy(dll, Path.Combine(tempDir, name), overwrite: true);
                    }
                    catch { }
                }

                // 4) 把安装目录里的 Updater.exe 重命名为 .old_时间戳
                try
                {
                    if (File.Exists(updaterPathInApp))
                    {
                        string renamedUpdater = updaterPathInApp + ".old_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Move(updaterPathInApp, renamedUpdater);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"重命名 Updater.exe 失败（可能被锁）: {ex.Message}");
                }

                // 5) 同样处理 Updater 的 .config
                try
                {
                    string cfgPath = updaterPathInApp + ".config";
                    if (File.Exists(cfgPath))
                    {
                        string renamedCfg = cfgPath + ".old_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Move(cfgPath, renamedCfg);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"准备更新程序失败: {ex.Message}", ex);
            }

            // 读取当前主题和强调色，传给 Updater
            string themeMode = "light";
            string accentColor = "#0078d4";
            try
            {
                var cfg = ConfigManager.Load();
                if (cfg != null)
                {
                    if (!string.IsNullOrWhiteSpace(cfg.ThemeMode))
                        themeMode = cfg.ThemeMode;
                    if (!string.IsNullOrWhiteSpace(cfg.AccentColor))
                        accentColor = cfg.AccentColor;
                }
            }
            catch { }

            string arguments =
                $"--app-dir \"{appDir}\" " +
                $"--zip \"{zipPath}\" " +
                $"--exe \"{currentExeName}\" " +
                $"--temp-dir \"{tempDir}\" " +
                $"--theme \"{themeMode}\" " +
                $"--accent \"{accentColor}\"";

            var psi = new ProcessStartInfo
            {
                FileName = updaterTempPath,
                Arguments = arguments,
                UseShellExecute = true,
                WorkingDirectory = tempDir,
                CreateNoWindow = false
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"启动更新程序失败: {ex.Message}", ex);
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// 清理主程序临时目录中超过 24 小时的更新文件
        /// </summary>
        public static void CleanupTempFiles()
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "SnakeLauncherUpdate");
                if (!Directory.Exists(tempDir)) return;

                var cutoff = DateTime.Now.AddHours(-24);
                foreach (var file in Directory.GetFiles(tempDir))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                            File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 清理 Updater 临时运行目录（24 小时前遗留的）
        /// </summary>
        public static void CleanupUpdaterTempDirs()
        {
            try
            {
                string tempRoot = Path.GetTempPath();
                if (!Directory.Exists(tempRoot)) return;

                foreach (var dir in Directory.GetDirectories(tempRoot, "SnakeLauncherUpdater_*"))
                {
                    try
                    {
                        if (Directory.GetCreationTime(dir) < DateTime.Now.AddHours(-24))
                            Directory.Delete(dir, true);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// ★ 新增：清理安装目录里的更新残留
        /// - _backup_* 目录（旧版本备份）
        /// - *.old_* 文件（Updater.exe 重命名留下的旧文件）
        /// 在每次主程序启动时调用
        /// </summary>
        public static void CleanupOldBackups()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

                // 1) 清理 _backup_* 目录
                foreach (var dir in Directory.GetDirectories(appDir, "_backup_*"))
                {
                    try
                    {
                        // 清除只读属性（有些备份文件可能带只读）
                        foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                        }
                        Directory.Delete(dir, true);
                    }
                    catch { /* 正在使用则跳过，下次再删 */ }
                }

                // 2) 清理 *.old_* 文件（Updater 重命名 Updater.exe 时留下的）
                foreach (var file in Directory.GetFiles(appDir, "*.old_*"))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ============================================================
        // 内部辅助
        // ============================================================

        /// <summary>
        /// 将版本字符串解析为四段 Version
        /// </summary>
        private static Version ParseVersion(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();

            if (!Version.TryParse(s, out Version v))
            {
                var m = Regex.Match(s, @"^(\d+(?:\.\d+){0,3})");
                if (!m.Success) return null;
                if (!Version.TryParse(m.Groups[1].Value, out v)) return null;
            }

            int major = v.Major;
            int minor = v.Minor >= 0 ? v.Minor : 0;
            int build = v.Build >= 0 ? v.Build : 0;
            int revision = v.Revision >= 0 ? v.Revision : 0;

            return new Version(major, minor, build, revision);
        }
    }
}