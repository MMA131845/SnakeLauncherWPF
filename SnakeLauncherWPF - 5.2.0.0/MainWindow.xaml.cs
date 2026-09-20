using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace SnakeLauncherWPF
{
    // ========== 配置类 ==========
    public class ConfigData
    {
        public List<string> Directories { get; set; } = new List<string>();
        public int CurrentDirIndex { get; set; } = 0;
        public string ThemeMode { get; set; } = "light";
        public string AccentColor { get; set; } = "#0078d4";
        public bool ApplyText { get; set; } = true;
        public string WindowSize { get; set; } = "1100x700";
        public string SyncFolder { get; set; } = "";
        public string SaveFolder { get; set; } = "";
        public string UpdateIndexUrl { get; set; } = "http://192.168.190.130:8081/versions.txt";
        public string DownloadPath { get; set; } = "";
        public int WindowWidth { get; set; } = 1100;
        public int WindowHeight { get; set; } = 700;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        // ★ 新增：自动更新相关
        /// <summary>启动时是否自动检查更新</summary>
        public bool AutoCheckUpdate { get; set; } = true;

        /// <summary>用户选择"跳过此版本"时记录的版本号，为空表示不跳过</summary>
        public string SkipUpdateVersion { get; set; } = "";

        /// <summary>上次自动检查更新的时间（UTC），用于限制频率</summary>
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    }

    // ========== 配置管理器 ==========
    // ========== 配置管理器 ==========
    public static class ConfigManager
    {
        private static string _customConfigPath;
        private static readonly object _ioLock = new object();

        public static void SetSyncFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) _customConfigPath = null;
            else _customConfigPath = Path.Combine(folder, "launcher_config.json");
        }

        public static string ConfigPath =>
            _customConfigPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");

        public static ConfigData Load()
        {
            if (!File.Exists(ConfigPath)) return new ConfigData();
            try
            {
                string json;
                // ★ 允许多进程/多线程共享读取
                using (var fs = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    json = sr.ReadToEnd();
                }

                var config = JsonSerializer.Deserialize<ConfigData>(json);
                if (config == null) return new ConfigData();
                if (config.Directories == null) config.Directories = new List<string>();
                if (config.Directories.Count == 0) config.Directories.Add(Directory.GetCurrentDirectory());
                if (config.CurrentDirIndex >= config.Directories.Count) config.CurrentDirIndex = 0;
                if (string.IsNullOrWhiteSpace(config.UpdateIndexUrl))
                    config.UpdateIndexUrl = "http://192.168.190.130:8081/versions.txt";
                if (config.WindowWidth <= 0) config.WindowWidth = 1100;
                if (config.WindowHeight <= 0) config.WindowHeight = 700;
                if (string.IsNullOrWhiteSpace(config.DownloadPath))
                    config.DownloadPath = Path.Combine(Path.GetTempPath(), "SnakeGameDownloads");
                return config;
            }
            catch { return new ConfigData(); }
        }

        public static void Save(ConfigData config)
        {
            // ★ 加锁，避免并发 Save 之间互锁
            lock (_ioLock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(config,
                        new JsonSerializerOptions { WriteIndented = true });

                    // ★ 先写临时文件，再原子替换，避免写入中途被读到半成品
                    string tempPath = ConfigPath + ".tmp";
                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                        FileShare.Read, 4096, FileOptions.WriteThrough))
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        sw.Write(json);
                        sw.Flush();
                        fs.Flush(true);
                    }

                    // 替换时如果目标被占用，重试几次
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            if (File.Exists(ConfigPath))
                                File.Replace(tempPath, ConfigPath, null, ignoreMetadataErrors: true);
                            else
                                File.Move(tempPath, ConfigPath);
                            return;
                        }
                        catch (IOException)
                        {
                            System.Threading.Thread.Sleep(50 * (i + 1));
                        }
                    }

                    // 最后兜底：直接覆盖
                    try
                    {
                        File.Copy(tempPath, ConfigPath, overwrite: true);
                        File.Delete(tempPath);
                    }
                    catch { }
                }
                catch { }
            }
        }

        /// <summary>
        /// ★ 新增：安全导出配置（读入内存后再写目标文件，不直接 Copy 源文件）
        /// </summary>
        public static bool ExportTo(string targetPath)
        {
            try
            {
                if (!File.Exists(ConfigPath)) return false;

                string json;
                using (var fs = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    json = sr.ReadToEnd();
                }

                // 写目标文件（覆盖）
                using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write,
                    FileShare.Read, 4096))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.Write(json);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // ========== 主题管理器 ==========
    public static class ThemeManager
    {
        public static event Action ThemeChanged;

        public static void ApplyTheme(ConfigData config)
        {
            var resources = Application.Current.Resources;
            var accent = (Color)ColorConverter.ConvertFromString(config.AccentColor);

            if (config.ThemeMode == "light")
                SetLightTheme(resources);
            else
                SetDarkTheme(resources);

            resources["AccentBrush"] = new SolidColorBrush(accent);
            resources["AccentLightBrush"] = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
            resources["ButtonHoverBrush"] = new SolidColorBrush(accent) { Opacity = 0.8 };

            // ★ 新增：强调色的 hover / pressed 变体
            resources["AccentHoverBrush"] = new SolidColorBrush(AdjustBrightness(accent, 1.18));
            resources["AccentPressedBrush"] = new SolidColorBrush(AdjustBrightness(accent, 0.78));

            // ★ 新增：根据强调色亮度自动选前景色
            //   亮底色（黄、浅绿）用黑字，暗底色用白字
            double luminance = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255.0;
            resources["AccentForegroundBrush"] = new SolidColorBrush(
                luminance > 0.6 ? Colors.Black : Colors.White);

            if (config.ApplyText)
            {
                resources["ForegroundBrush"] = resources["AccentBrush"];
                resources["MenuForegroundBrush"] = resources["AccentBrush"];
            }

            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// 按系数调整颜色亮度。factor &gt; 1 变亮，factor &lt; 1 变暗。
        /// </summary>
        private static Color AdjustBrightness(Color c, double factor)
        {
            byte r = (byte)Math.Min(255, Math.Max(0, c.R * factor));
            byte g = (byte)Math.Min(255, Math.Max(0, c.G * factor));
            byte b = (byte)Math.Min(255, Math.Max(0, c.B * factor));
            return Color.FromRgb(r, g, b);
        }

        private static void SetLightTheme(ResourceDictionary resources)
        {
            resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
            resources["ForegroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
            resources["SecondaryForegroundBrush"] = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            resources["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(247, 247, 247));
            resources["MenuForegroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
            resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            resources["SeparatorBrush"] = new SolidColorBrush(Color.FromRgb(208, 208, 208));
            resources["ScrollBarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            resources["ScrollBarThumbBrush"] = new SolidColorBrush(Color.FromRgb(192, 192, 192));
            resources["TextBoxBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            resources["TextBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(208, 208, 208));
            resources["GroupBoxHeaderBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));

            resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            resources["ButtonHoverBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            resources["ButtonPressedBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
            resources["ButtonNeutralHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x20, 0, 0, 0));

            resources["SplashShadowColor"] = Color.FromRgb(0x1A, 0x2A, 0x44);

            var lightBg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            lightBg.GradientStops.Add(new GradientStop(Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF), 0));
            lightBg.GradientStops.Add(new GradientStop(Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF), 0.55));
            lightBg.GradientStops.Add(new GradientStop(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF), 1));
            lightBg.Freeze();
            resources["SplashGlassBackgroundBrush"] = lightBg;

            var lightBorder = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            lightBorder.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0));
            lightBorder.GradientStops.Add(new GradientStop(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF), 1));
            lightBorder.Freeze();
            resources["SplashGlassBorderBrush"] = lightBorder;

            resources["SplashBlob1Brush"] = new SolidColorBrush(Color.FromArgb(0x55, 0x77, 0xC4, 0xFF));
            resources["SplashBlob2Brush"] = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xA8, 0xC4));
            resources["SplashTrackBrush"] = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        }

        private static void SetDarkTheme(ResourceDictionary resources)
        {
            resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            resources["ForegroundBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            resources["SecondaryForegroundBrush"] = new SolidColorBrush(Color.FromRgb(160, 160, 160));
            resources["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            resources["MenuForegroundBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(58, 58, 58));
            resources["SeparatorBrush"] = new SolidColorBrush(Color.FromRgb(58, 58, 58));
            resources["ScrollBarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            resources["ScrollBarThumbBrush"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            resources["TextBoxBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            resources["TextBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(58, 58, 58));
            resources["GroupBoxHeaderBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));

            resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            resources["ButtonHoverBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
            resources["ButtonPressedBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
            resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            resources["ButtonNeutralHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

            resources["SplashShadowColor"] = Colors.Black;

            var darkBg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            darkBg.GradientStops.Add(new GradientStop(Color.FromArgb(0xF0, 0x22, 0x24, 0x2E), 0));
            darkBg.GradientStops.Add(new GradientStop(Color.FromArgb(0xE6, 0x1C, 0x1E, 0x28), 0.55));
            darkBg.GradientStops.Add(new GradientStop(Color.FromArgb(0xDD, 0x16, 0x18, 0x20), 1));
            darkBg.Freeze();
            resources["SplashGlassBackgroundBrush"] = darkBg;

            var darkBorder = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            darkBorder.GradientStops.Add(new GradientStop(Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF), 0));
            darkBorder.GradientStops.Add(new GradientStop(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), 1));
            darkBorder.Freeze();
            resources["SplashGlassBorderBrush"] = darkBorder;

            resources["SplashBlob1Brush"] = new SolidColorBrush(Color.FromArgb(0x60, 0x5A, 0x9E, 0xFF));
            resources["SplashBlob2Brush"] = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0x7E, 0xB0));
            resources["SplashTrackBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        }
    }

    // ========== 游戏文件信息 ==========
    public class GameFileInfo
    {
        public Version Version { get; set; }
        public string DisplayName { get; set; }
        public string FullPath { get; set; }
        public bool IsPython { get; set; }
    }

    // ========== 游戏统计 ==========
    public class PlayRecord
    {
        public string GamePath { get; set; }
        public int LaunchCount { get; set; }
        public double TotalPlayMinutes { get; set; }
        public DateTime LastPlayed { get; set; }
    }

    public class GameSessionRecord
    {
        public DateTime StartTime { get; set; }
        public double DurationMinutes { get; set; }
        public int FinalScore { get; set; }
        public int FinalKills { get; set; }
        public string Mode { get; set; }
        public string GamePath { get; set; }
    }

    public static class GameStatsManager
    {
        private static readonly string StatsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game_stats.json");
        private static readonly string HistoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game_history.json");
        private static List<PlayRecord> _records = new List<PlayRecord>();
        private static List<GameSessionRecord> _sessions = new List<GameSessionRecord>();

        /// <summary>
        /// 删除指定路径的所有统计记录
        /// </summary>
        public static void RemoveRecordsByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                _records?.RemoveAll(r => string.Equals(r.GamePath, path, StringComparison.OrdinalIgnoreCase));
                SaveRecords();
            }
            catch { }
        }

        /// <summary>
        /// 删除指定路径的所有对局记录
        /// </summary>
        public static void RemoveSessionsByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                _sessions?.RemoveAll(s => string.Equals(s.GamePath, path, StringComparison.OrdinalIgnoreCase));
                SaveSessions();
            }
            catch { }
        }

        public static List<PlayRecord> LoadRecords()
        {
            try
            {
                if (File.Exists(StatsPath))
                    _records = JsonSerializer.Deserialize<List<PlayRecord>>(File.ReadAllText(StatsPath)) ?? new List<PlayRecord>();
                else _records = new List<PlayRecord>();
            }
            catch { _records = new List<PlayRecord>(); }
            return _records;
        }

        public static List<GameSessionRecord> LoadSessions()
        {
            try
            {
                if (File.Exists(HistoryPath))
                    _sessions = JsonSerializer.Deserialize<List<GameSessionRecord>>(File.ReadAllText(HistoryPath)) ?? new List<GameSessionRecord>();
                else _sessions = new List<GameSessionRecord>();
            }
            catch { _sessions = new List<GameSessionRecord>(); }
            return _sessions;
        }

        public static void SaveRecords()
        {
            try
            {
                string json = JsonSerializer.Serialize(_records,
                    new JsonSerializerOptions { WriteIndented = true });
                using (var fs = new FileStream(StatsPath, FileMode.Create, FileAccess.Write,
                    FileShare.Read, 4096))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.Write(json);
                }
            }
            catch { }
        }

        public static void SaveSessions()
        {
            try
            {
                string json = JsonSerializer.Serialize(_sessions,
                    new JsonSerializerOptions { WriteIndented = true });
                using (var fs = new FileStream(HistoryPath, FileMode.Create, FileAccess.Write,
                    FileShare.Read, 4096))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.Write(json);
                }
            }
            catch { }
        }
        public static PlayRecord GetRecord(string path) => _records?.FirstOrDefault(r => r.GamePath == path);

        public static void RecordLaunch(string path)
        {
            if (_records == null) _records = new List<PlayRecord>();
            var record = GetRecord(path);
            if (record == null) { record = new PlayRecord { GamePath = path }; _records.Add(record); }
            record.LaunchCount++;
            record.LastPlayed = DateTime.Now;
            SaveRecords();
        }

        public static void RecordPlayDuration(string path, double minutes)
        {
            var record = GetRecord(path);
            if (record != null) { record.TotalPlayMinutes += minutes; SaveRecords(); }
        }

        public static void AddSession(GameSessionRecord session)
        {
            if (_sessions == null) _sessions = new List<GameSessionRecord>();
            _sessions.Insert(0, session);
            if (_sessions.Count > 50) _sessions = _sessions.Take(50).ToList();
            SaveSessions();
        }

        public static List<GameSessionRecord> GetSessionsForGame(string path)
        {
            return _sessions?.Where(s => s.GamePath == path).OrderByDescending(s => s.StartTime).ToList();
        }
    }

    // ========== 游戏启动器 ==========
    public static class GameLauncher
    {
        public static Process StartProcess(string filePath, bool isPython)
        {
            var psi = new ProcessStartInfo
            {
                FileName = isPython ? "python.exe" : filePath,
                Arguments = isPython ? $"\"{filePath}\"" : "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(filePath)
            };
            return Process.Start(psi);
        }
    }

    // ========== 下载相关 ==========
    public class VersionInfo
    {
        public string Version { get; set; }
        public string DisplayName { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; }
    }

    public class UpdateIndex
    {
        public VersionInfo Latest { get; set; }
        public List<VersionInfo> History { get; set; }
    }

    public class DownloadProgressEventArgs : EventArgs
    {
        public double ProgressPercentage { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
    }

    public class DownloadManager
    {
        private readonly HttpClient _httpClient;
        public event EventHandler<DownloadProgressEventArgs> ProgressChanged;
        public event EventHandler<string> DownloadCompleted;
        public event EventHandler<string> DownloadFailed;

        public DownloadManager()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        }

        public async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalRead += bytesRead;
                            if (totalBytes > 0)
                            {
                                double progress = (double)totalRead / totalBytes * 100;
                                ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                                {
                                    ProgressPercentage = progress,
                                    DownloadedBytes = totalRead,
                                    TotalBytes = totalBytes
                                });
                            }
                        }
                    }
                }
                DownloadCompleted?.Invoke(this, savePath);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                DownloadFailed?.Invoke(this, ex.Message);
                if (File.Exists(savePath)) { try { File.Delete(savePath); } catch { } }
            }
        }
    }

    public class VersionDownloadState
    {
        public VersionInfo Info { get; set; }
        public bool IsDownloading { get; set; }
        public bool IsDownloaded { get; set; }
        public bool IsInstalled { get; set; }
        public double Progress { get; set; }
        public CancellationTokenSource Cts { get; set; }
        public string DownloadedFilePath { get; set; }
    }

    // ========== 主窗口 ==========
    public partial class MainWindow : Window
    {
        private System.Windows.Threading.DispatcherTimer _notificationTimer;
        private string _currentDetailGamePath;
        private bool _autoUpdateCheckRunning;
        private Version _latestReleaseVersion;
        private bool _autoUpdatePromptShown;
        private UpdateCheckResult _pendingAutoUpdate;   // ★ 加这一行
        private ConfigData _config;
        private List<GameFileInfo> _gameFiles = new List<GameFileInfo>();
        private WrapPanel _cardPanel;
        private Label _currentPathLabel;
        private ToggleButton _sortButton;
        private Popup _sortPopup;
        private ListBox _sortListBox;

        private Process _currentGameProcess;
        private DateTime _gameStartTime;
        private System.Windows.Threading.DispatcherTimer _playTimer;
        private System.Windows.Threading.DispatcherTimer _healthCheckTimer;
        private System.Windows.Threading.DispatcherTimer _performanceTimer;

        private List<int> _fpsHistory = new List<int>();
        private List<long> _memoryHistory = new List<long>();
        private int _lastScore, _lastKills;
        private StringBuilder _logBuilder = new StringBuilder();

        private StackPanel _downloadListPanel;
        private DownloadManager _downloadManager;
        private Dictionary<string, VersionDownloadState> _versionStates = new Dictionary<string, VersionDownloadState>();
        private VersionDownloadState _currentDownloadState;
        private Border _downloadDetailOverlay;
        private TextBlock _popupTitle, _popupSize, _popupNotes, _popupStatus;
        private Button _popupDownloadBtn, _popupCancelBtn;
        private ProgressBar _popupProgressBar;
        private CancellationTokenSource _downloadCts;
        private ScaleTransform _popupScaleTransform;

        private Grid _rootGrid;
        private bool _popupCreated = false;
        private Border _compatibilityDialog;
        private TextBlock _compatibilityMessage;
        private TaskCompletionSource<bool> _compatibilityTcs;
        private Border _confirmDialog;
        private TextBlock _confirmMessage;
        private TaskCompletionSource<bool> _confirmTcs;
        private Border _infoDialog;
        private TextBlock _infoMessage;
        private TaskCompletionSource<bool> _infoTcs;
        private readonly List<Border> _accentSwatches = new List<Border>();
        private readonly Random _blobRandom = new Random();

        // ========== ★ 修复：IPC 模式名汉化映射（大小写不敏感 + 覆盖游戏本体所有模式） ==========
        private static readonly Dictionary<string, string> ModeNameMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "classic", "经典模式" },
            { "survival", "生存模式" },
            { "timed", "淘汰之王" },
            { "endless", "无尽模式" },
            { "battle", "对战模式" },
            { "multiplayer", "多人模式" },
            { "team4v4", "占领模式" },
            { "extreme", "极限模式" },
            { "extraction", "搜打撤" }
        };

        // ========== ★ 下载路径确认对话框 ==========
        private enum DownloadPathChoice
        {
            Confirm,
            Change,
            Cancel
        }

        /// <summary>
        /// 下载前弹出对话框，确认或更改下载路径
        /// </summary>
        private Task<DownloadPathChoice> ShowDownloadPathConfirmAsync(string path)
        {
            var tcs = new TaskCompletionSource<DownloadPathChoice>();

            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x12, 0x1A, 0x2B)),
                Visibility = Visibility.Visible,
                Opacity = 0,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var panel = new Border
            {
                Width = 500,
                MaxHeight = 420,
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(26),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            panel.Effect = new DropShadowEffect
            {
                ShadowDepth = 10,
                BlurRadius = 36,
                Opacity = 0.35,
                Color = Color.FromRgb(0x0F, 0x1B, 0x33)
            };
            var scale = new ScaleTransform(0.8, 0.8);
            panel.RenderTransform = scale;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "确认下载路径",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush")
            };
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            var pathStack = new StackPanel { Margin = new Thickness(0, 12, 0, 18) };
            pathStack.Children.Add(new TextBlock
            {
                Text = "下载的游戏将解压到以下目录：",
                FontSize = 13,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            pathStack.Children.Add(new Border
            {
                Background = SafeFindBrush("BackgroundBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock
                {
                    Text = path,
                    FontSize = 12,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas, Microsoft YaHei UI")
                }
            });
            Grid.SetRow(pathStack, 1);
            grid.Children.Add(pathStack);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            bool closed = false;
            void CloseDialog()
            {
                if (closed) return;
                closed = true;

                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, e) =>
                {
                    try { _rootGrid.Children.Remove(overlay); } catch { }
                };
                var scaleOut = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                overlay.BeginAnimation(OpacityProperty, fadeOut);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            }

            var btnCancel = new Button
            {
                Content = "取消",
                Style = SafeFindStyle("RoundedButton"),
                Width = 90,
                Height = 38,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.Click += (s, e) => { CloseDialog(); tcs.TrySetResult(DownloadPathChoice.Cancel); };

            var btnChange = new Button
            {
                Content = "更改路径",
                Style = SafeFindStyle("RoundedButton"),
                Width = 110,
                Height = 38,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnChange.Click += (s, e) => { CloseDialog(); tcs.TrySetResult(DownloadPathChoice.Change); };

            var btnConfirm = new Button
            {
                Content = "确认下载",
                Style = SafeFindStyle("RoundedButton"),
                Width = 110,
                Height = 38
            };
            btnConfirm.Click += (s, e) => { CloseDialog(); tcs.TrySetResult(DownloadPathChoice.Confirm); };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnChange);
            btnRow.Children.Add(btnConfirm);
            Grid.SetRow(btnRow, 2);
            grid.Children.Add(btnRow);

            panel.Child = grid;
            var overlayGrid = new Grid();
            overlayGrid.Children.Add(panel);
            overlay.Child = overlayGrid;

            _rootGrid.Children.Add(overlay);
            Grid.SetRowSpan(overlay, int.MaxValue);
            Grid.SetColumnSpan(overlay, int.MaxValue);

            // 淡入 + 缩放动画
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleIn = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            overlay.BeginAnimation(OpacityProperty, fadeIn);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

            return tcs.Task;
        }

        /// <summary>
        /// 从 exe 的 FileVersion 读取版本号
        /// </summary>
        private static Version GetExeFileVersion(string exePath)
        {
            try
            {
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(fvi.FileVersion)
                    && Version.TryParse(fvi.FileVersion, out var v))
                {
                    // 补齐为四段
                    int major = v.Major;
                    int minor = v.Minor >= 0 ? v.Minor : 0;
                    int build = v.Build >= 0 ? v.Build : 0;
                    int revision = v.Revision >= 0 ? v.Revision : 0;
                    return new Version(major, minor, build, revision);
                }
            }
            catch { }
            return new Version(0, 0, 0);
        }

        /// <summary>
        /// 检测 ZIP 压缩包根目录是否符合游戏包规范
        /// 支持两种包：
        ///   1) C# exe 包：根目录有 SnakeGame.exe/SnakeGameWpf.exe + .json/.dll
        ///   2) Python 包：根目录有任意 .py 文件
        /// 只检测根目录（不递归子目录）
        /// </summary>
        private static (bool IsValid, string Message, string RootFiles) ValidateGamePackage(string zipPath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    bool hasExe = false;
                    bool hasJson = false;
                    bool hasDll = false;
                    bool hasPy = false;
                    string exeName = null;
                    string pySampleName = null;
                    var rootFiles = new List<string>();

                    foreach (var entry in archive.Entries)
                    {
                        // 只保留根目录文件（FullName 里没有 / 或 \）
                        string fullName = entry.FullName.Replace('\\', '/');
                        if (fullName.Contains("/"))
                            continue;
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string name = entry.Name;
                        string lower = name.ToLowerInvariant();
                        rootFiles.Add(name);

                        if (lower == "snakegame.exe" || lower == "snakegamewpf.exe")
                        {
                            hasExe = true;
                            exeName = name;
                        }
                        else if (lower.EndsWith(".json"))
                        {
                            hasJson = true;
                        }
                        else if (lower.EndsWith(".dll"))
                        {
                            hasDll = true;
                        }
                        else if (lower.EndsWith(".py"))
                        {
                            hasPy = true;
                            if (pySampleName == null) pySampleName = name;
                        }
                    }

                    // 根目录空
                    if (rootFiles.Count == 0)
                    {
                        return (false,
                            "压缩包根目录为空，可能内部还有一层文件夹。",
                            "（无）");
                    }

                    // 判定类型
                    bool isExePackage = hasExe && (hasJson || hasDll);
                    bool isPythonPackage = hasPy;

                    if (!isExePackage && !isPythonPackage)
                    {
                        // 有 exe 但缺 json/dll
                        if (hasExe)
                        {
                            return (false,
                                "压缩包根目录中找到了 SnakeGame.exe，但缺少 .json 或 .dll 文件。",
                                string.Join("、", rootFiles.Take(8)) + (rootFiles.Count > 8 ? " …" : ""));
                        }

                        // 有 .py 但上面 hasPy 已经是 true，不会走到这里
                        // 所以这里是：既没有 exe 也没有 .py
                        return (false,
                            "压缩包根目录中未找到 SnakeGame.exe、SnakeGameWpf.exe 或任何 .py 文件。",
                            string.Join("、", rootFiles.Take(8)) + (rootFiles.Count > 8 ? " …" : ""));
                    }

                    // 通过：组装描述信息
                    var okParts = new List<string>();
                    if (hasExe) okParts.Add(exeName);
                    if (hasPy) okParts.Add(string.IsNullOrEmpty(pySampleName) ? "Python 脚本" : pySampleName);
                    if (hasJson) okParts.Add(".json 配置");
                    if (hasDll) okParts.Add(".dll 依赖");

                    return (true,
                        "根目录文件：" + string.Join("、", okParts),
                        string.Join("、", rootFiles.Take(8)) + (rootFiles.Count > 8 ? " …" : ""));
                }
            }
            catch (Exception ex)
            {
                return (false, $"无法读取压缩包: {ex.Message}", "");
            }
        }

        /// <summary>
        /// 从 GitHub API 拉取所有 Release 并渲染卡片
        /// </summary>
        private async Task LoadGitHubReleasesAsync(GitHubRepoConfig repoConfig)
        {
            if (_downloadListPanel == null) return;

            _downloadListPanel.Children.Clear();
            _downloadListPanel.Children.Add(new TextBlock
            {
                Text = $"正在从 {repoConfig.DisplayName} 获取版本列表...",
                FontSize = 13,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });

            try
            {
                var releases = await GitHubReleaseService.GetReleasesAsync(repoConfig);

                _downloadListPanel.Children.Clear();

                if (releases == null || releases.Count == 0)
                {
                    _downloadListPanel.Children.Add(new TextBlock
                    {
                        Text = $"{repoConfig.DisplayName} 暂无发行版本",
                        FontSize = 13,
                        Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 40, 0, 0)
                    });
                    return;
                }

                _latestReleaseVersion = releases
                    .Where(r => r.ParsedVersion != null)
                    .Select(r => r.ParsedVersion)
                    .OrderByDescending(v => v)
                    .FirstOrDefault();

                foreach (var release in releases)
                {
                    var card = CreateReleaseCard(release);
                    _downloadListPanel.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                _downloadListPanel.Children.Clear();
                _downloadListPanel.Children.Add(new TextBlock
                {
                    Text = $"获取版本列表失败: {ex.Message}",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        /// <summary>
        /// 为一个 GitHub Release 创建可展开的卡片
        /// </summary>
        private Border CreateReleaseCard(GitHubRelease release)
        {
            var card = new Border
            {
                Style = SafeFindStyle("GameCardStyle"),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 12, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ===== 标题行 =====
            var titleBar = new Grid();
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
            titleStack.Children.Add(new TextBlock
            {
                Text = release.TagName ?? "",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });

            if (release.ParsedVersion != null && _latestReleaseVersion != null
                && release.ParsedVersion >= _latestReleaseVersion)
            {
                titleStack.Children.Add(new Border
                {
                    Background = SafeFindBrush("AccentBrush"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "最新",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                });
            }

            titleStack.Children.Add(new TextBlock
            {
                Text = release.PublishedAt.ToString("yyyy-MM-dd"),
                FontSize = 11,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            });

            Grid.SetColumn(titleStack, 0);
            titleBar.Children.Add(titleStack);

            var expandBtn = new Button
            {
                Content = "▾",
                Width = 32,
                Height = 28,
                FontSize = 14,
                Background = Brushes.Transparent,
                Foreground = SafeFindBrush("ForegroundBrush"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(expandBtn, 1);
            titleBar.Children.Add(expandBtn);

            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            // ===== 说明 =====
            var notesText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(release.Body) ? "（无更新说明）" : release.Body,
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 60,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(notesText, 1);
            grid.Children.Add(notesText);

            // ===== assets 容器（默认隐藏） =====
            var assetsPanel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };

            // ★ 只显示符合 SnakeGameWpf_x.x.x.x.zip 命名规范的 asset
            var gameAssets = release.Assets?
                .Where(a => a.IsGamePackage)
                .OrderByDescending(a => a.ParsedVersion ?? new Version(0, 0, 0, 0))
                .ToList() ?? new List<GitHubReleaseAsset>();

            if (gameAssets.Count > 0)
            {
                foreach (var asset in gameAssets)
                {
                    assetsPanel.Children.Add(CreateAssetRow(asset, release));
                }
            }
            else
            {
                assetsPanel.Children.Add(new TextBlock
                {
                    Text = "该版本没有符合 SnakeGameWpf_版本号.zip 命名规范的游戏包",
                    FontSize = 12,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    Margin = new Thickness(0, 4, 0, 4),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            Grid.SetRow(assetsPanel, 2);
            grid.Children.Add(assetsPanel);

            // 展开/收起
            bool expanded = false;
            expandBtn.Click += (s, e) =>
            {
                expanded = !expanded;
                assetsPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                expandBtn.Content = expanded ? "▴" : "▾";
                notesText.MaxHeight = expanded ? double.PositiveInfinity : 60;
                notesText.TextTrimming = expanded ? TextTrimming.None : TextTrimming.CharacterEllipsis;
            };

            card.Child = grid;
            return card;
        }

        /// <summary>
        /// 单个 asset 行：文件名 + 版本 + 大小 + 下载按钮
        /// </summary>
        private Border CreateAssetRow(GitHubReleaseAsset asset, GitHubRelease release)
        {
            var row = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(0x08, 0, 0, 0))
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 左侧：文件名 + 版本号
            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(new TextBlock
            {
                Text = asset.Name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = SafeFindBrush("ForegroundBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (asset.ParsedVersion != null)
            {
                leftStack.Children.Add(new TextBlock
                {
                    Text = $"版本: v{asset.ParsedVersion}",
                    FontSize = 10,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // 中间：大小
            string sizeStr = asset.Size > 0 ? $"{asset.Size / 1048576.0:F1} MB" : "未知";
            var sizeText = new TextBlock
            {
                Text = sizeStr,
                FontSize = 11,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            };
            Grid.SetColumn(sizeText, 1);
            grid.Children.Add(sizeText);

            // 右侧：下载按钮
            var btnDownload = new Button
            {
                Content = "下载",
                Style = SafeFindStyle("RoundedButton"),
                Width = 80,
                Height = 30
            };
            btnDownload.Click += async (s, e) =>
            {
                await DownloadGitHubAssetAsync(asset, release);
            };
            Grid.SetColumn(btnDownload, 2);
            grid.Children.Add(btnDownload);

            row.Child = grid;
            return row;
        }

        /// <summary>
        /// 下载 GitHub Release 的 asset，自动解压到下载目录的 <三位版本号> 文件夹，并让扫描识别到
        /// </summary>
        private async Task DownloadGitHubAssetAsync(GitHubReleaseAsset asset, GitHubRelease release)
        {
            try
            {
                if (!asset.IsGamePackage || asset.ParsedVersion == null)
                {
                    ShowDetailPanel("无法下载",
                        $"文件 {asset.Name} 不符合命名规范。\n\n" +
                        "游戏包必须命名为：SnakeGameWpf_版本号.zip 或 SnakeGamePython_版本号.zip");
                    return;
                }

                // ========== 0. 确认下载路径 ==========
                string downloadDir = _config.DownloadPath;
                if (string.IsNullOrWhiteSpace(downloadDir))
                    downloadDir = Path.Combine(Path.GetTempPath(), "SnakeGameDownloads");

                var pathChoice = await ShowDownloadPathConfirmAsync(downloadDir);

                if (pathChoice == DownloadPathChoice.Cancel)
                {
                    ShowNotification("已取消下载");
                    return;
                }

                if (pathChoice == DownloadPathChoice.Change)
                {
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        dialog.Description = "选择下载存储文件夹";
                        dialog.SelectedPath = downloadDir;
                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            downloadDir = dialog.SelectedPath;
                            _config.DownloadPath = downloadDir;

                            if (!_config.Directories.Any(d =>
                                    string.Equals(d, downloadDir, StringComparison.OrdinalIgnoreCase)))
                            {
                                _config.Directories.Add(downloadDir);
                            }

                            ConfigManager.Save(_config);
                        }
                        else
                        {
                            ShowNotification("已取消下载");
                            return;
                        }
                    }
                }

                // ========== 1. 下载 ==========
                Directory.CreateDirectory(downloadDir);
                string zipPath = Path.Combine(downloadDir, asset.Name);

                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }

                ShowNotification($"正在下载 {asset.Name}...");
                var progress = new Progress<double>(p =>
                {
                    ShowNotification($"下载 {asset.Name}... {p:F1}%");
                });
                await DownloadWithProgressAsync(asset.BrowserDownloadUrl, zipPath, progress);

                // ========== 2. 校验 ==========
                var (isValid, message, rootFiles) = ValidateGamePackage(zipPath);
                if (!isValid)
                {
                    try { File.Delete(zipPath); } catch { }
                    ShowDetailPanel("下载失败",
                        $"压缩包校验未通过：\n{message}\n\n" +
                        $"根目录实际内容：\n{rootFiles}\n\n" +
                        "已删除无效文件。");
                    return;
                }

                // ========== 3. 解压 ==========
                string versionFolder = asset.ParsedVersion.ToString(3);
                string extractDir = Path.Combine(downloadDir, versionFolder);

                if (Directory.Exists(extractDir))
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                        }
                        Directory.Delete(extractDir, true);
                    }
                    catch { }
                }
                Directory.CreateDirectory(extractDir);

                ShowNotification($"正在解压到 {versionFolder}/ ...");
                await Task.Run(() =>
                {
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destPath = Path.GetFullPath(Path.Combine(extractDir, entry.FullName));
                            if (!destPath.StartsWith(extractDir, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("非法路径：包含越界目录");

                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                entry.ExtractToFile(destPath, overwrite: true);
                            }
                        }
                    }
                });

                // ========== 4. 删除 zip ==========
                try { File.Delete(zipPath); } catch { }

                // ============================================================
                // ★ 5. Python 依赖自动安装（仅 Python 仓库）
                // ============================================================
                if (asset.RepoConfig != null && asset.RepoConfig.IsPythonRepo)
                {
                    await EnsurePythonDependenciesAsync();
                }

                // ========== 6. 加入扫描列表 ==========
                bool addedDir = false;
                if (!_config.Directories.Any(d =>
                        string.Equals(d, downloadDir, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.Directories.Add(downloadDir);
                    ConfigManager.Save(_config);
                    addedDir = true;
                }

                // ========== 7. 扫描 ==========
                await ScanGamesAsync();

                string extraMsg = addedDir
                    ? $"\n已将下载目录添加到扫描列表：\n{downloadDir}"
                    : "";
                ShowNotification($"版本 {versionFolder} 安装成功！{extraMsg}");

                SelectTab(0);
            }
            catch (Exception ex)
            {
                ShowDetailPanel("下载失败", ex.Message);
            }
        }

        /// <summary>
        /// ★ 检测并安装 Python 依赖（如果没有）
        /// </summary>
        private async Task EnsurePythonDependenciesAsync()
        {
            ShowNotification("正在检测 Python 环境...");

            // 1. 检查 Python 是否可用
            bool pythonOk = await PythonDependencyManager.IsPythonAvailableAsync();
            if (!pythonOk)
            {
                var result = await ShowConfirmDialogAsync(
                    "未检测到 Python 环境。\n\n" +
                    "该游戏需要 Python 3.8 或更高版本才能运行。\n\n" +
                    "是否打开 Python 官网下载页面？");
                if (result)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://www.python.org/downloads/",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
                return;
            }

            // 2. 检测缺失的依赖
            ShowNotification("正在检测游戏依赖...");
            var missing = await PythonDependencyManager.GetMissingPackagesAsync();

            if (missing.Count == 0)
            {
                ShowNotification("Python 依赖已就绪");
                return;
            }

            // 3. 弹窗确认是否安装
            string pkgList = string.Join("、", missing);
            bool install = await ShowConfirmDialogAsync(
                $"检测到以下 Python 依赖未安装：\n\n{pkgList}\n\n" +
                "是否立即自动安装？\n" +
                "（安装过程可能需要几分钟，请保持网络畅通）");

            if (!install)
            {
                ShowNotification("已跳过依赖安装，游戏可能无法正常运行");
                return;
            }

            // 4. 逐个安装
            foreach (var pkg in missing)
            {
                bool ok = await PythonDependencyManager.InstallPackageAsync(pkg, msg =>
                {
                    Dispatcher.Invoke(() => ShowNotification(msg));
                });

                if (!ok)
                {
                    ShowDetailPanel("依赖安装失败",
                        $"包 {pkg} 安装失败。\n\n" +
                        "请尝试手动运行：\n" +
                        $"pip install {pkg}\n\n" +
                        "或检查网络连接。");
                    return;
                }
            }

            ShowNotification("所有 Python 依赖安装完成！");
        }

        /// <summary>
        /// 带进度回调的下载（复用 UpdateService 的下载逻辑）
        /// </summary>
        private async Task DownloadWithProgressAsync(string url, string savePath, IProgress<double> progress)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(30);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SnakeLauncherWPF-Downloader/1.0");

                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                                progress?.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }
            }
        }

        [Obsolete("已由 DownloadGitHubAssetAsync 直接处理，不再使用")]
        private async Task InstallGitHubAssetAsync(string zipPath, GitHubRelease release, bool skipValidation = false)
        {
            try
            {
                // 如果没校验过，先校验一遍
                if (!skipValidation)
                {
                    var (isValid, message, rootFiles) = ValidateGamePackage(zipPath);
                    if (!isValid)
                    {
                        bool forceInstall = await ShowConfirmDialogAsync(
                            $"压缩包校验未通过：\n{message}\n\n" +
                            $"根目录实际内容：\n{rootFiles}\n\n" +
                            "该文件可能不是有效的 SnakeGameWpf 游戏包。\n\n" +
                            "是否仍要强制解压？");
                        if (!forceInstall)
                        {
                            ShowNotification("已取消安装");
                            return;
                        }
                    }
                }

                string targetDir = _config.Directories[_config.CurrentDirIndex];

                await Task.Run(() =>
                {
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                            if (!destPath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("非法路径：包含越界目录");

                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                // 目录 entry
                                Directory.CreateDirectory(destPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                entry.ExtractToFile(destPath, overwrite: true);
                            }
                        }
                    }
                });

                ShowNotification($"版本 {release.TagName} 安装成功！");
                _ = ScanGamesAsync();
            }
            catch (Exception ex)
            {
                ShowDetailPanel("安装失败", ex.Message);
            }
        }

        // ========== 构造函数 ==========
        public MainWindow()
        {
            try
            {
                _config = ConfigManager.Load();
                if (!string.IsNullOrWhiteSpace(_config.SyncFolder))
                    ConfigManager.SetSyncFolder(_config.SyncFolder);
                ThemeManager.ApplyTheme(_config);
                InitializeComponent();

                this.Width = _config.WindowWidth;
                this.Height = _config.WindowHeight;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                GameStatsManager.LoadRecords();
                GameStatsManager.LoadSessions();

                ThemeManager.ThemeChanged += RefreshCards;
                IpcService.StatsReceived += OnStatsReceived;
                IpcService.Disconnected += OnIpcDisconnected;

                LoadDirectories();
                _ = ScanGamesAsync();

                _playTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _playTimer.Tick += PlayTimer_Tick;

                _healthCheckTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _healthCheckTimer.Tick += HealthCheck_Tick;

                _performanceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _performanceTimer.Tick += PerformanceTimer_Tick;

                _downloadManager = new DownloadManager();

                this.Loaded += OnMainWindowLoaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}\n\n{ex.StackTrace}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // ========== Loaded 事件 ==========
        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.Content == null)
                {
                    MessageBox.Show("窗口内容为空，无法加载。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                var border = this.Content as Border;
                if (border == null)
                {
                    MessageBox.Show($"窗口内容类型不是 Border，而是 {this.Content.GetType().Name}。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                _rootGrid = border.Child as Grid;
                if (_rootGrid == null)
                {
                    MessageBox.Show("根 Border 的 Child 不是 Grid。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                SelectTab(0);

                // ===== 初始化兼容性自绘对话框 =====
                _compatibilityDialog = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x12, 0x1A, 0x2B)),
                    Visibility = Visibility.Collapsed,
                    Opacity = 0,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var panel = new Border
                {
                    Width = 440,
                    MaxHeight = 500,
                    Background = SafeFindBrush("CardBackgroundBrush"),
                    BorderBrush = SafeFindBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(22),
                    Padding = new Thickness(24),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                panel.Effect = new DropShadowEffect { ShadowDepth = 10, BlurRadius = 36, Opacity = 0.35, Color = Color.FromRgb(0x0F, 0x1B, 0x33) };
                var scaleTransform = new ScaleTransform(0.8, 0.8);
                panel.RenderTransform = scaleTransform;

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = "版本不兼容",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = SafeFindBrush("ForegroundBrush")
                };
                _compatibilityMessage = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    Margin = new Thickness(0, 12, 0, 12)
                };
                var btnOk = new Button
                {
                    Content = "确定",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 100,
                    Height = 38,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                btnOk.Click += (s, args) =>
                {
                    CloseCompatibilityDialog();
                    _compatibilityTcs?.TrySetResult(true);
                };

                Grid.SetRow(title, 0);
                Grid.SetRow(_compatibilityMessage, 1);
                Grid.SetRow(btnOk, 2);
                grid.Children.Add(title);
                grid.Children.Add(_compatibilityMessage);
                grid.Children.Add(btnOk);

                panel.Child = grid;
                var overlayGrid = new Grid();
                overlayGrid.Children.Add(panel);
                _compatibilityDialog.Child = overlayGrid;

                _rootGrid.Children.Add(_compatibilityDialog);
                Grid.SetRowSpan(_compatibilityDialog, int.MaxValue);
                Grid.SetColumnSpan(_compatibilityDialog, int.MaxValue);

                // ===== 初始化确认自绘对话框 =====
                _confirmDialog = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x12, 0x1A, 0x2B)),
                    Visibility = Visibility.Collapsed,
                    Opacity = 0,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var confirmPanel = new Border
                {
                    Width = 440,
                    MaxHeight = 500,
                    Background = SafeFindBrush("CardBackgroundBrush"),
                    BorderBrush = SafeFindBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(22),
                    Padding = new Thickness(24),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                confirmPanel.Effect = new DropShadowEffect { ShadowDepth = 10, BlurRadius = 36, Opacity = 0.35, Color = Color.FromRgb(0x0F, 0x1B, 0x33) };
                var confirmScale = new ScaleTransform(0.8, 0.8);
                confirmPanel.RenderTransform = confirmScale;

                var confirmGrid = new Grid();
                confirmGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                confirmGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                confirmGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var confirmTitle = new TextBlock
                {
                    Text = "提示",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = SafeFindBrush("ForegroundBrush")
                };
                _confirmMessage = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    Margin = new Thickness(0, 12, 0, 12)
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 0)
                };

                var btnYes = new Button
                {
                    Content = "是",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 90,
                    Height = 38,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                btnYes.Click += (s, args) =>
                {
                    CloseConfirmDialog();
                    _confirmTcs?.TrySetResult(true);
                };

                var btnNo = new Button
                {
                    Content = "否",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 90,
                    Height = 38
                };
                btnNo.Click += (s, args) =>
                {
                    CloseConfirmDialog();
                    _confirmTcs?.TrySetResult(false);
                };

                buttonPanel.Children.Add(btnYes);
                buttonPanel.Children.Add(btnNo);

                Grid.SetRow(confirmTitle, 0);
                Grid.SetRow(_confirmMessage, 1);
                Grid.SetRow(buttonPanel, 2);
                confirmGrid.Children.Add(confirmTitle);
                confirmGrid.Children.Add(_confirmMessage);
                confirmGrid.Children.Add(buttonPanel);

                confirmPanel.Child = confirmGrid;
                var confirmOverlayGrid = new Grid();
                confirmOverlayGrid.Children.Add(confirmPanel);
                _confirmDialog.Child = confirmOverlayGrid;

                _rootGrid.Children.Add(_confirmDialog);
                Grid.SetRowSpan(_confirmDialog, int.MaxValue);
                Grid.SetColumnSpan(_confirmDialog, int.MaxValue);

                // ===== 初始化信息自绘对话框 =====
                _infoDialog = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x12, 0x1A, 0x2B)),
                    Visibility = Visibility.Collapsed,
                    Opacity = 0,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var infoPanel = new Border
                {
                    Width = 440,
                    MaxHeight = 500,
                    Background = SafeFindBrush("CardBackgroundBrush"),
                    BorderBrush = SafeFindBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(22),
                    Padding = new Thickness(24),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                infoPanel.Effect = new DropShadowEffect { ShadowDepth = 10, BlurRadius = 36, Opacity = 0.35, Color = Color.FromRgb(0x0F, 0x1B, 0x33) };
                var infoScale = new ScaleTransform(0.8, 0.8);
                infoPanel.RenderTransform = infoScale;

                var infoGrid = new Grid();
                infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var infoTitle = new TextBlock
                {
                    Text = "提示",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = SafeFindBrush("ForegroundBrush")
                };
                _infoMessage = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    Margin = new Thickness(0, 12, 0, 12)
                };

                var btnInfoOk = new Button
                {
                    Content = "确定",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 100,
                    Height = 38,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                btnInfoOk.Click += (s, args) =>
                {
                    CloseInfoDialog();
                    _infoTcs?.TrySetResult(true);
                };

                Grid.SetRow(infoTitle, 0);
                Grid.SetRow(_infoMessage, 1);
                Grid.SetRow(btnInfoOk, 2);
                infoGrid.Children.Add(infoTitle);
                infoGrid.Children.Add(_infoMessage);
                infoGrid.Children.Add(btnInfoOk);

                infoPanel.Child = infoGrid;
                var infoOverlayGrid = new Grid();
                infoOverlayGrid.Children.Add(infoPanel);
                _infoDialog.Child = infoOverlayGrid;

                _rootGrid.Children.Add(_infoDialog);
                Grid.SetRowSpan(_infoDialog, int.MaxValue);
                Grid.SetColumnSpan(_infoDialog, int.MaxValue);

                // ===== 启动背景球体的缓慢随机漂移动画 =====
                StartBlobAnimation();

                // ========== ★ 新增：启动后延迟自动检查更新 ==========
                StartAutoUpdateCheck();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载页面时出错: {ex.Message}\n\n{ex.StackTrace}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // ========== 背景球体缓慢随机漂移 ==========
        private void StartBlobAnimation()
        {
            AnimateBlobLoop(BgBlob1Transform, 6.0, 11.0, 150);
            AnimateBlobLoop(BgBlob2Transform, 7.5, 13.0, 130);
            AnimateBlobLoop(BgBlob3Transform, 8.0, 15.0, 170);
        }
        // ================================================================
        // ========== ★ 新增：自动更新检查相关方法 ==========
        // ================================================================

        /// <summary>
        /// 启动后延迟一段时间再检查更新，避免和启动动画争抢资源
        /// </summary>
        private async void StartAutoUpdateCheck()
        {
            if (!_config.AutoCheckUpdate) return;
            if (_autoUpdateCheckRunning) return;

            // 距离上次检查不足 6 小时则跳过
            if ((DateTime.UtcNow - _config.LastUpdateCheck).TotalHours < 6) return;

            _autoUpdateCheckRunning = true;

            try
            {
                // 延迟 5 秒，让主窗口完全渲染完毕
                await Task.Delay(TimeSpan.FromSeconds(5));

                // 再延迟一下，等启动画面完全关闭
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                // 用户正在玩游戏时不要打扰
                if (_currentGameProcess != null && !_currentGameProcess.HasExited) return;

                var update = await UpdateService.CheckForUpdateAsync();
                _config.LastUpdateCheck = DateTime.UtcNow;
                ConfigManager.Save(_config);

                if (update == null || !update.HasUpdate) return;

                // 用户选择过跳过此版本
                if (!string.IsNullOrEmpty(_config.SkipUpdateVersion)
                    && _config.SkipUpdateVersion == update.LatestVersion)
                {
                    return;
                }

                // 已经弹过就不要重复弹
                if (_autoUpdatePromptShown) return;
                _autoUpdatePromptShown = true;

                // 已经在 UI 线程上（StartAutoUpdateCheck 从 OnMainWindowLoaded 启动，await Task.Delay 后仍回到 UI 线程），直接调用即可
                await PromptAutoUpdateAsync(update);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"自动检查更新失败: {ex.Message}");
            }
            finally
            {
                _autoUpdateCheckRunning = false;
            }
        }

        /// <summary>
        /// 发现新版本时的自绘对话框（三选一：稍后 / 跳过 / 立即更新）
        /// </summary>
        private async Task PromptAutoUpdateAsync(UpdateCheckResult update)
        {
            // ===== 构造对话框 =====
            var dialog = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x12, 0x1A, 0x2B)),
                Visibility = Visibility.Collapsed,
                Opacity = 0,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var panel = new Border
            {
                Width = 480,
                MaxHeight = 560,
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(26),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            panel.Effect = new DropShadowEffect
            {
                ShadowDepth = 10,
                BlurRadius = 36,
                Opacity = 0.35,
                Color = Color.FromRgb(0x0F, 0x1B, 0x33)
            };
            var scale = new ScaleTransform(0.8, 0.8);
            panel.RenderTransform = scale;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题
            var title = new TextBlock
            {
                Text = "发现新版本",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush")
            };
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            // 版本信息
            var versionLine = new TextBlock
            {
                Text = $"v{UpdateService.GetCurrentVersion()}  →  v{update.LatestVersion}",
                FontSize = 13,
                Foreground = SafeFindBrush("AccentBrush"),
                Margin = new Thickness(0, 8, 0, 12),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(versionLine, 1);
            grid.Children.Add(versionLine);

            // 更新说明（可滚动）
            var notesScroll = new ScrollViewer
            {
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 14)
            };
            var notesText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                    ? "（无更新说明）"
                    : update.ReleaseNotes,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = SafeFindBrush("ForegroundBrush"),
                LineHeight = 20
            };
            notesScroll.Content = notesText;
            Grid.SetRow(notesScroll, 2);
            grid.Children.Add(notesScroll);

            // 文件大小
            if (update.FileSize > 0)
            {
                var sizeText = new TextBlock
                {
                    Text = $"更新包大小: {update.FileSize / 1048576.0:F1} MB",
                    FontSize = 12,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    Margin = new Thickness(0, 0, 0, 14)
                };
                Grid.SetRow(sizeText, 3);
                grid.Children.Add(sizeText);
            }

            // 按钮行
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var tcs = new TaskCompletionSource<string>();

            var btnLater = new Button
            {
                Content = "稍后提醒",
                Style = SafeFindStyle("RoundedButton"),
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnLater.Click += (s, e) => { CloseDialog(); tcs.TrySetResult("later"); };

            var btnSkip = new Button
            {
                Content = "跳过此版本",
                Style = SafeFindStyle("RoundedButton"),
                Width = 110,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnSkip.Click += (s, e) => { CloseDialog(); tcs.TrySetResult("skip"); };

            var btnUpdate = new Button
            {
                Content = "立即更新",
                Style = SafeFindStyle("RoundedButton"),
                Width = 100,
                Height = 36,
                Background = SafeFindBrush("AccentBrush"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold
            };
            btnUpdate.Click += (s, e) => { CloseDialog(); tcs.TrySetResult("update"); };

            btnRow.Children.Add(btnLater);
            btnRow.Children.Add(btnSkip);
            btnRow.Children.Add(btnUpdate);
            Grid.SetRow(btnRow, 4);
            grid.Children.Add(btnRow);

            panel.Child = grid;
            var overlayGrid = new Grid();
            overlayGrid.Children.Add(panel);
            dialog.Child = overlayGrid;

            _rootGrid.Children.Add(dialog);
            Grid.SetRowSpan(dialog, int.MaxValue);
            Grid.SetColumnSpan(dialog, int.MaxValue);

            // 显示动画
            dialog.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
            var scaleIn = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }
            };
            dialog.BeginAnimation(OpacityProperty, fadeIn);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

            void CloseDialog()
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fadeOut.Completed += (s, e) => _rootGrid.Children.Remove(dialog);
                var scaleOut = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                dialog.BeginAnimation(OpacityProperty, fadeOut);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            }

            string choice = await tcs.Task;

            if (choice == "skip")
            {
                _config.SkipUpdateVersion = update.LatestVersion;
                ConfigManager.Save(_config);
                ShowNotification($"已跳过版本 v{update.LatestVersion}");
                return;
            }

            if (choice == "later")
            {
                ShowNotification("已稍后提醒");
                return;
            }

            // 立即更新
            _pendingAutoUpdate = update;
            await RunUpdateFromAutoCheckAsync(update);
        }

        /// <summary>
        /// 从自动检查直接执行更新（下载 + 校验 + 启动 Updater）
        /// </summary>
        private async Task RunUpdateFromAutoCheckAsync(UpdateCheckResult update)
        {
            if (string.IsNullOrEmpty(update.DownloadUrl))
            {
                ShowNotification("未找到下载链接");
                return;
            }

            try
            {
                ShowNotification("正在下载更新包...");

                var progress = new Progress<double>(p =>
                {
                    ShowNotification($"正在下载... {p:F1}%");
                });

                string zipPath = await UpdateService.DownloadUpdateAsync(
                    update.DownloadUrl, progress, CancellationToken.None);

                if (!string.IsNullOrEmpty(update.Sha256))
                {
                    ShowNotification("正在校验文件完整性...");
                    bool ok = await Task.Run(() => UpdateService.VerifySha256(zipPath, update.Sha256));
                    if (!ok)
                    {
                        try { File.Delete(zipPath); } catch { }
                        ShowDetailPanel("更新失败", "文件校验失败，更新包可能已损坏，请手动到设置页重试。");
                        return;
                    }
                }

                ShowNotification("校验通过，正在启动更新程序...");
                await Task.Delay(600);

                UpdateService.LaunchUpdater(zipPath);
            }
            catch (Exception ex)
            {
                ShowDetailPanel("自动更新失败", ex.Message + "\n\n你可以到\"设置\"页手动重试。");
            }
        }

        private void AnimateBlobLoop(TranslateTransform transform, double minSec, double maxSec, double range)
        {
            if (transform == null) return;

            double targetX = (_blobRandom.NextDouble() - 0.5) * 2 * range;
            double targetY = (_blobRandom.NextDouble() - 0.5) * 2 * range;

            double seconds = minSec + _blobRandom.NextDouble() * (maxSec - minSec);
            var duration = TimeSpan.FromSeconds(seconds);

            double fromX = transform.X;
            double fromY = transform.Y;

            var animX = new DoubleAnimation(fromX, targetX, duration)
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            var animY = new DoubleAnimation(fromY, targetY, duration)
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            animX.Completed += (s, e) => AnimateBlobLoop(transform, minSec, maxSec, range);

            transform.BeginAnimation(TranslateTransform.XProperty, animX);
            transform.BeginAnimation(TranslateTransform.YProperty, animY);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            _config.WindowWidth = (int)this.Width;
            _config.WindowHeight = (int)this.Height;
            _config.WindowLeft = this.Left;
            _config.WindowTop = this.Top;
            ConfigManager.Save(_config);

            _playTimer?.Stop();
            _healthCheckTimer?.Stop();
            _performanceTimer?.Stop();
            _downloadCts?.Cancel();
            IpcService.Stop();
        }

        // ========== 安全获取资源 ==========
        private object SafeFindResource(object key)
        {
            try { return this.TryFindResource(key) ?? Application.Current.TryFindResource(key); }
            catch { return null; }
        }
        private Style SafeFindStyle(object key) => SafeFindResource(key) as Style ?? new Style();
        private Brush SafeFindBrush(object key) => SafeFindResource(key) as Brush ?? Brushes.Transparent;

        // ========== 排序控件 ==========
        private UIElement CreateCustomSortControl()
        {
            _sortButton = new ToggleButton
            {
                Content = "默认排序",
                Style = SafeFindStyle("CustomToggleButtonStyle"),
                Width = 120,
                Height = 34,
            };

            _sortPopup = new Popup
            {
                PlacementTarget = _sortButton,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            _sortListBox = new ListBox
            {
                ItemsSource = new[] { "默认排序", "最近游玩", "最常游玩" },
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                MinWidth = 120,
                ItemContainerStyle = SafeFindStyle("CustomListBoxItemStyle"),
                Padding = new Thickness(4)
            };
            _sortListBox.SelectionChanged += (s, e) =>
            {
                if (_sortListBox.SelectedItem != null)
                {
                    _sortButton.Content = _sortListBox.SelectedItem.ToString();
                    _sortPopup.IsOpen = false;
                    ApplySort();
                }
            };
            _sortPopup.Child = _sortListBox;
            _sortButton.Click += (s, e) => _sortPopup.IsOpen = !_sortPopup.IsOpen;
            return _sortButton;
        }

        // ========== 定时器事件 ==========
        private void PlayTimer_Tick(object sender, EventArgs e) => TxtPlayTime.Text = (DateTime.Now - _gameStartTime).ToString(@"hh\:mm\:ss");

        private void HealthCheck_Tick(object sender, EventArgs e)
        {
            if (_currentGameProcess != null && !_currentGameProcess.HasExited && !_currentGameProcess.Responding)
                Dispatcher.Invoke(() =>
                {
                    if (MessageBox.Show("游戏失去响应，强制终止？", "无响应", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        try { _currentGameProcess.Kill(); } catch { }
                });
        }

        // ========== ★ 修复：模式名转显示名（大小写不敏感 + Trim 防御） ==========
        private static string GetModeDisplayName(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return "--";
            string key = mode.Trim();
            if (string.IsNullOrEmpty(key)) return "--";
            return ModeNameMap.TryGetValue(key, out string chinese) ? chinese : key;
        }

        // ========== IPC 事件 ==========
        private void OnStatsReceived(GameStatsData stats)
        {
            Dispatcher.Invoke(() =>
            {
                TxtFps.Text = stats.Fps.ToString();
                TxtScore.Text = stats.Score.ToString();
                TxtKills.Text = stats.Kills.ToString();
                TxtMode.Text = GetModeDisplayName(stats.Mode);

                StatusLight.Fill = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
                TxtStatus.Text = "已连接";
                _lastScore = stats.Score;
                _lastKills = stats.Kills;
            });
        }

        private void OnIpcDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                _playTimer.Stop(); _healthCheckTimer.Stop(); _performanceTimer.Stop();
                IpcStatusBar.Visibility = Visibility.Collapsed;
            });
        }

        // ========== 游戏启动 ==========
        private async Task LaunchGame(string filePath, bool isPython)
        {
            if (_currentGameProcess != null && !_currentGameProcess.HasExited)
            {
                bool killOld = await ShowConfirmDialogAsync("已有游戏在运行，是否关闭旧进程？");
                if (killOld)
                {
                    try
                    {
                        _currentGameProcess.Exited -= OnGameProcessExited;
                        _currentGameProcess.Kill();
                        await Task.Run(() => _currentGameProcess.WaitForExit(3000));
                        if (!_currentGameProcess.HasExited)
                            _currentGameProcess.Close();
                    }
                    catch (Exception ex) { Debug.WriteLine($"终止旧进程失败: {ex.Message}"); }
                    finally
                    {
                        _currentGameProcess?.Dispose();
                        _currentGameProcess = null;
                        _playTimer?.Stop();
                        _healthCheckTimer?.Stop();
                        _performanceTimer?.Stop();
                        IpcService.Stop();
                        IpcStatusBar.Visibility = Visibility.Collapsed;
                    }
                }
                else return;
            }

            try
            {
                var gameInfo = _gameFiles.FirstOrDefault(g => g.FullPath == filePath && g.IsPython == isPython);
                if (gameInfo == null)
                {
                    MessageBox.Show("无法识别该游戏版本，请确认文件是否正确。", "版本未知", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!IsVersionCompatible(gameInfo.Version))
                {
                    await ShowCompatibilityDialogAsync(
                        "该版本不适配 IPC 通信，无法与启动器同步数据。\n\n" +
                        "请使用以下版本之一：\n" +
                        "• 2.2.0 ～ 2.4.0（Python版本）\n" +
                        "• 2.15.4 ～ 3.0.5（Python版本）\n" +
                        "• SnakeGame（C#版本）\n\n" +
                        "点击确定后仍将启动游戏，但部分功能可能无法使用。");
                }

                _currentGameProcess = GameLauncher.StartProcess(filePath, isPython);
                if (_currentGameProcess == null)
                {
                    MessageBox.Show("游戏进程启动失败，请检查文件路径或权限。", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _gameStartTime = DateTime.Now;
                _lastScore = _lastKills = 0;
                _fpsHistory.Clear();
                _memoryHistory.Clear();
                _logBuilder.Clear();

                GameStatsManager.RecordLaunch(filePath);

                IpcStatusBar.Visibility = Visibility.Visible;
                TxtFps.Text = "--";
                TxtScore.Text = "0";
                TxtKills.Text = "0";
                TxtMode.Text = "--";
                TxtPlayTime.Text = "00:00:00";
                StatusLight.Fill = new SolidColorBrush(Colors.Gray);
                TxtStatus.Text = "等待连接...";

                _ = IpcService.StartAsync();
                _playTimer.Start();
                _healthCheckTimer.Start();
                _performanceTimer.Start();

                _ = Task.Run(() => ReadStream(_currentGameProcess.StandardOutput));
                _ = Task.Run(() => ReadStream(_currentGameProcess.StandardError));

                _currentGameProcess.EnableRaisingEvents = true;
                _currentGameProcess.Exited += OnGameProcessExited;

                ShowNotification("游戏启动成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ReadStream(StreamReader reader)
        {
            try
            {
                while (!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (line != null) Dispatcher.Invoke(() => _logBuilder.AppendLine(line));
                }
            }
            catch { }
        }

        private void OnGameProcessExited(object sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _playTimer.Stop(); _healthCheckTimer.Stop(); _performanceTimer.Stop();
                IpcService.Stop();
                IpcStatusBar.Visibility = Visibility.Collapsed;

                double minutes = (DateTime.Now - _gameStartTime).TotalMinutes;
                if (_currentGameProcess != null)
                {
                    string filePath = _currentGameProcess.StartInfo.FileName;
                    if (_currentGameProcess.StartInfo.Arguments.StartsWith("\""))
                        filePath = _currentGameProcess.StartInfo.Arguments.Trim('"');
                    GameStatsManager.RecordPlayDuration(filePath, minutes);
                    GameStatsManager.AddSession(new GameSessionRecord
                    {
                        StartTime = _gameStartTime,
                        DurationMinutes = minutes,
                        FinalScore = _lastScore,
                        FinalKills = _lastKills,
                        Mode = TxtMode.Text,
                        GamePath = filePath
                    });
                    int exitCode = _currentGameProcess.ExitCode;
                    _currentGameProcess.Dispose();
                    _currentGameProcess = null;
                    if (exitCode != 0)
                    {
                        string log = _logBuilder.ToString();
                        string crashMessage = $"游戏异常退出 (退出码: {exitCode})\n\n--- 游戏输出 ---\n{(string.IsNullOrEmpty(log) ? "无输出" : log)}";
                        ShowDetailPanel("游戏崩溃", crashMessage);
                    }
                }
            });
        }

        private void PerformanceTimer_Tick(object sender, EventArgs e)
        {
            if (_currentGameProcess != null && !_currentGameProcess.HasExited)
            {
                _currentGameProcess.Refresh();
                long memory = _currentGameProcess.WorkingSet64 / (1024 * 1024);
                _memoryHistory.Add(memory);
                int fps = 0;
                Dispatcher.Invoke(() => int.TryParse(TxtFps.Text, out fps));
                _fpsHistory.Add(fps);
                if (_fpsHistory.Count > 60) _fpsHistory.RemoveAt(0);
                if (_memoryHistory.Count > 60) _memoryHistory.RemoveAt(0);
            }
        }

        // ========== 通知与详情面板 ==========
        public void ShowNotification(string message)
        {
            NotificationText.Text = message;

            // 首次创建复用定时器，避免重复 new 造成内存泄漏
            if (_notificationTimer == null)
            {
                _notificationTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _notificationTimer.Tick += (s, ev) =>
                {
                    _notificationTimer.Stop();
                    HideNotification();
                };
            }
            else
            {
                _notificationTimer.Stop();
            }

            // 强制布局一次，拿到实际宽度
            NotificationBar.UpdateLayout();
            double exitOffset = NotificationBar.ActualWidth > 0
                ? NotificationBar.ActualWidth + 40
                : 300;

            // 从右侧初始位置滑入
            NotificationTransform.X = exitOffset;

            var slideIn = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 4 }
            };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));

            NotificationBar.BeginAnimation(OpacityProperty, fadeIn);
            NotificationTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);

            _notificationTimer.Start();
        }

        /// <summary>
        /// 隐藏通知栏（滑出 + 淡出）
        /// </summary>
        private void HideNotification()
        {
            double exitOffset = NotificationBar.ActualWidth > 0
                ? NotificationBar.ActualWidth + 40
                : 300;

            var slideOut = new DoubleAnimation
            {
                To = exitOffset,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));

            NotificationTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            NotificationBar.BeginAnimation(OpacityProperty, fadeOut);
        }

        public void ShowDetailPanel(string title, string message)
        {
            DetailTitle.Text = title;
            DetailTextBox.Text = message;
            DetailOverlay.Visibility = Visibility.Visible;
            var overlayFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
            DetailOverlay.BeginAnimation(OpacityProperty, overlayFadeIn);
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(380))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } };
            var scaleYAnim = scaleXAnim.Clone();
            DetailScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            DetailScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        private void HideDetailPanel()
        {
            var scaleXOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(200))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            var scaleYOut = scaleXOut.Clone();
            var overlayFadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            overlayFadeOut.Completed += (s, e) => DetailOverlay.Visibility = Visibility.Collapsed;
            DetailScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXOut);
            DetailScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYOut);
            DetailOverlay.BeginAnimation(OpacityProperty, overlayFadeOut);
        }

        private void DetailOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { if (e.OriginalSource == DetailOverlay) HideDetailPanel(); }
        private void CloseDetailPanel_Click(object sender, RoutedEventArgs e) => HideDetailPanel();

        private void CloseConfirmDialog()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) => _confirmDialog.Visibility = Visibility.Collapsed;
            var scaleXOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            var scaleYOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

            _confirmDialog.BeginAnimation(OpacityProperty, fadeOut);
            var transform = (_confirmDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXOut);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYOut);
        }

        private Task<bool> ShowConfirmDialogAsync(string message)
        {
            _confirmTcs = new TaskCompletionSource<bool>();
            _confirmMessage.Text = message;

            _confirmDialog.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };

            _confirmDialog.BeginAnimation(OpacityProperty, fadeIn);
            var transform = (_confirmDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            return _confirmTcs.Task;
        }

        private void CloseCompatibilityDialog()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) => _compatibilityDialog.Visibility = Visibility.Collapsed;
            var scaleXOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            var scaleYOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

            _compatibilityDialog.BeginAnimation(OpacityProperty, fadeOut);
            var transform = (_compatibilityDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXOut);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYOut);
        }

        private Task ShowCompatibilityDialogAsync(string message)
        {
            _compatibilityTcs = new TaskCompletionSource<bool>();
            _compatibilityMessage.Text = message;

            _compatibilityDialog.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };

            _compatibilityDialog.BeginAnimation(OpacityProperty, fadeIn);
            var transform = (_compatibilityDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            return _compatibilityTcs.Task;
        }

        private void CloseInfoDialog()
        {
            if (_infoDialog == null) return;
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) => _infoDialog.Visibility = Visibility.Collapsed;
            var scaleXOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            var scaleYOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

            _infoDialog.BeginAnimation(OpacityProperty, fadeOut);
            var transform = (_infoDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXOut);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYOut);
        }

        private Task ShowInfoDialogAsync(string message)
        {
            _infoTcs = new TaskCompletionSource<bool>();
            _infoMessage.Text = message;

            _infoDialog.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };

            _infoDialog.BeginAnimation(OpacityProperty, fadeIn);
            var transform = (_infoDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            return _infoTcs.Task;
        }

        private void RestartLauncher()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法自动重启：{ex.Message}\n\n请手动重新启动启动器。",
                    "重启失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            Application.Current.Shutdown();
        }

        // ========== Tab 切换 ==========
        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            int index = 0;
            switch (btn.Content.ToString())
            {
                case "启动": index = 0; break;
                case "下载": index = 1; break;
                case "数据": index = 2; break;
                case "设置": index = 3; break;
                case "关于": index = 4; break;
            }
            SelectTab(index);
        }

        private void SelectTab(int index)
        {
            try
            {
                _downloadCts?.Cancel(); _downloadCts?.Dispose(); _downloadCts = null;
                CloseDownloadDetailPopup();

                if (BtnTabLaunch == null || BtnTabDownload == null || BtnTabStats == null ||
                    BtnTabSettings == null || BtnTabAbout == null || ContentGrid == null)
                {
                    MessageBox.Show("界面控件未正确初始化。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                BtnTabLaunch.Tag = index == 0 ? "Selected" : "Unselected";
                BtnTabDownload.Tag = index == 1 ? "Selected" : "Unselected";
                BtnTabStats.Tag = index == 2 ? "Selected" : "Unselected";
                BtnTabSettings.Tag = index == 3 ? "Selected" : "Unselected";
                BtnTabAbout.Tag = index == 4 ? "Selected" : "Unselected";

                ContentGrid.Children.Clear();
                switch (index)
                {
                    case 0: ContentGrid.Children.Add(CreateLaunchPage()); break;
                    case 1: ContentGrid.Children.Add(CreateDownloadPage()); break;
                    case 2: ContentGrid.Children.Add(CreateStatsPage()); break;
                    case 3: ContentGrid.Children.Add(CreateSettingsPage()); break;
                    case 4: ContentGrid.Children.Add(CreateAboutPage()); break;
                }

                Button selectedBtn = null;
                switch (index)
                {
                    case 0: selectedBtn = BtnTabLaunch; break;
                    case 1: selectedBtn = BtnTabDownload; break;
                    case 2: selectedBtn = BtnTabStats; break;
                    case 3: selectedBtn = BtnTabSettings; break;
                    case 4: selectedBtn = BtnTabAbout; break;
                }

                if (selectedBtn != null && TabSlider != null)
                {
                    var parentGrid = (selectedBtn.Parent as Panel)?.Parent as Grid;
                    if (parentGrid != null)
                    {
                        Point relativePoint = selectedBtn.TranslatePoint(new Point(0, 0), parentGrid);
                        double left = relativePoint.X;
                        double width = selectedBtn.ActualWidth;
                        TabSlider.BeginAnimation(Border.MarginProperty,
                            new ThicknessAnimation(new Thickness(left, 0, 0, 4), TimeSpan.FromMilliseconds(280))
                            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
                        TabSlider.BeginAnimation(Border.WidthProperty,
                            new DoubleAnimation(width, TimeSpan.FromMilliseconds(280))
                            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换标签页出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== 启动页面 ==========
        private UIElement CreateLaunchPage()
        {
            var mainPanel = new Grid();
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition());

            var topBar = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _currentPathLabel = new Label
            {
                Content = _config.Directories[_config.CurrentDirIndex],
                Foreground = SafeFindBrush("ForegroundBrush"),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _currentPathLabel.MouseLeftButtonUp += (s, e) => { try { Clipboard.SetText(_currentPathLabel.Content.ToString()); } catch { } };

            var sortControl = CreateCustomSortControl();

            var btnRefresh = new Button { Content = "刷新", Style = SafeFindStyle("RoundedButton"), Width = 88, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
            btnRefresh.Click += async (s, e) => await ScanGamesAsync();
            var btnManual = new Button { Content = "手动添加", Style = SafeFindStyle("RoundedButton"), Width = 108, Height = 34 };
            btnManual.Click += (s, e) => ManualAdd();

            Grid.SetColumn(_currentPathLabel, 0);
            Grid.SetColumn(sortControl, 1);
            Grid.SetColumn(btnRefresh, 2);
            Grid.SetColumn(btnManual, 3);
            topBar.Children.Add(_currentPathLabel);
            topBar.Children.Add(sortControl);
            topBar.Children.Add(btnRefresh);
            topBar.Children.Add(btnManual);

            var latestCardPanel = new Border { Margin = new Thickness(0, 0, 0, 16) };
            var latestExe = _gameFiles.Where(f => !f.IsPython).OrderByDescending(f => f.Version).FirstOrDefault();
            if (latestExe != null)
            {
                var card = new Border
                {
                    Style = SafeFindStyle("GameCardStyle"),
                    Padding = new Thickness(20)
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = "最新 EXE 版本", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = SafeFindBrush("SecondaryForegroundBrush") });
                string ver = latestExe.Version.Major == 9999 ? "SnakeGame" : $"{latestExe.Version}";
                stack.Children.Add(new TextBlock { Text = ver, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = SafeFindBrush("ForegroundBrush"), Margin = new Thickness(0, 4, 0, 10) });
                var launchBtn = new Button { Content = "启动游戏", Style = SafeFindStyle("RoundedButton"), Width = 120, Height = 38, HorizontalAlignment = HorizontalAlignment.Left };
                launchBtn.Click += async (s, ev) => await LaunchGame(latestExe.FullPath, false);
                stack.Children.Add(launchBtn);
                card.Child = stack;
                latestCardPanel.Child = card;
            }
            else
            {
                latestCardPanel.Child = new TextBlock
                {
                    Text = "未找到任何 .exe 版本",
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 24, 0, 24)
                };
            }

            _cardPanel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var scrollViewer = new ScrollViewer { Content = _cardPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

            Grid.SetRow(topBar, 0); Grid.SetRow(latestCardPanel, 1); Grid.SetRow(scrollViewer, 2);
            mainPanel.Children.Add(topBar); mainPanel.Children.Add(latestCardPanel); mainPanel.Children.Add(scrollViewer);
            ApplySort(); RefreshCards();
            return mainPanel;
        }

        private void ApplySort()
        {
            string sort = _sortButton?.Content?.ToString() ?? "默认排序";
            switch (sort)
            {
                case "最近游玩":
                    _gameFiles = _gameFiles.OrderByDescending(g => GameStatsManager.GetRecord(g.FullPath)?.LastPlayed ?? DateTime.MinValue).ToList();
                    break;
                case "最常游玩":
                    _gameFiles = _gameFiles.OrderByDescending(g => GameStatsManager.GetRecord(g.FullPath)?.LaunchCount ?? 0).ToList();
                    break;
                default:
                    _gameFiles = _gameFiles.OrderByDescending(g => g.Version).ToList();
                    break;
            }
        }

        private void RefreshCards()
        {
            if (_cardPanel == null) return;
            _cardPanel.Children.Clear();
            if (_gameFiles.Count == 0)
            {
                _cardPanel.Children.Add(new TextBlock
                {
                    Text = "未找到任何游戏版本",
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(20)
                });
                return;
            }
            foreach (var g in _gameFiles)
            {
                string ver = g.Version.Major == 9999 ? "SnakeGame" : $"{g.Version}";
                var card = CreateGameCard(ver, g.DisplayName, g.FullPath, g.IsPython);
                _cardPanel.Children.Add(card);
            }
        }

        private Border CreateGameCard(string versionStr, string displayName, string fullPath, bool isPython)
        {
            var card = new Border { Style = SafeFindStyle("GameCardStyle"), Width = 210, Height = 158, Margin = new Thickness(0, 0, 12, 12) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var versionBlock = new TextBlock
            {
                Text = versionStr,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 6)
            };
            var nameBlock = new TextBlock
            {
                Text = displayName,
                FontSize = 10,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            var launchBtn = new Button { Content = "启动", Style = SafeFindStyle("RoundedButton"), Width = 76, Height = 30 };
            launchBtn.Click += async (s, ev) => await LaunchGame(fullPath, isPython);
            var detailBtn = new Button { Content = "详情", Style = SafeFindStyle("RoundedButton"), Width = 56, Height = 30, Margin = new Thickness(6, 0, 0, 0) };
            detailBtn.Click += (s, ev) => ShowDetailWindow(fullPath);
            btnPanel.Children.Add(launchBtn); btnPanel.Children.Add(detailBtn);

            var contextMenu = new ContextMenu();
            var renameItem = new MenuItem { Header = "重命名" };
            renameItem.Click += (s, ev) => { string newName = Interaction.InputBox("输入新名称", "重命名", displayName); if (!string.IsNullOrWhiteSpace(newName)) { var game = _gameFiles.FirstOrDefault(g => g.FullPath == fullPath); if (game != null) { game.DisplayName = newName; RefreshCards(); } } };
            var openFolderItem = new MenuItem { Header = "打开所在文件夹" };
            openFolderItem.Click += (s, ev) => Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
            contextMenu.Items.Add(renameItem); contextMenu.Items.Add(openFolderItem);
            card.ContextMenu = contextMenu;

            grid.Children.Add(versionBlock); Grid.SetRow(versionBlock, 0);
            grid.Children.Add(nameBlock); Grid.SetRow(nameBlock, 1);
            grid.Children.Add(btnPanel); Grid.SetRow(btnPanel, 2);
            card.Child = grid;
            return card;
        }

        private void ShowDetailWindow(string path)
        {
            var game = _gameFiles.FirstOrDefault(g => g.FullPath == path);
            var stats = GameStatsManager.GetRecord(path);

            // ★ 记录当前查看的游戏路径，供删除按钮使用
            _currentDetailGamePath = path;

            string info = $"版本号：{game?.Version}\n" +
                          $"显示名称：{game?.DisplayName}\n" +
                          $"完整路径：{path}\n" +
                          $"最后游玩：{(stats?.LastPlayed.ToString("g") ?? "从未")}\n" +
                          $"启动次数：{stats?.LaunchCount ?? 0}\n" +
                          $"总时长：{stats?.TotalPlayMinutes:F1} 分钟";

            // 判断游戏文件的父目录
            string parentDir = Path.GetDirectoryName(path);
            string parentDirName = Path.GetFileName(parentDir);

            // 若父目录是版本号目录（比如 7.0.0）或下载目录下的子目录，提示将删除整个目录
            bool hasValidParent = !string.IsNullOrEmpty(parentDir)
                && !string.Equals(parentDir, Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase);

            if (hasValidParent)
            {
                info += $"\n\n⚠ 删除时将同时删除所在文件夹：\n{parentDir}";
            }

            ShowDetailPanel("游戏详情", info);
        }

        /// <summary>
        /// 打开游戏所在文件夹
        /// </summary>
        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentDetailGamePath)) return;
            try
            {
                Process.Start("explorer.exe", $"/select,\"{_currentDetailGamePath}\"");
            }
            catch (Exception ex)
            {
                ShowNotification($"打开失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除游戏文件夹（带二次确认）
        /// </summary>
        private async void BtnDeleteGame_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentDetailGamePath))
            {
                ShowNotification("未选择游戏");
                return;
            }

            string gamePath = _currentDetailGamePath;
            if (!File.Exists(gamePath) && !Directory.Exists(gamePath))
            {
                ShowNotification("文件不存在，可能已被删除");
                HideDetailPanel();
                await ScanGamesAsync();
                return;
            }

            // 计算要删除的目录
            string parentDir = Path.GetDirectoryName(gamePath);
            string root = Path.GetPathRoot(gamePath);

            // 防止删除根目录（比如 C:\ 或 D:\）
            if (string.IsNullOrEmpty(parentDir)
                || string.Equals(parentDir, root, StringComparison.OrdinalIgnoreCase))
            {
                ShowNotification("出于安全考虑，无法删除磁盘根目录");
                return;
            }

            // 防止删除启动器自身目录
            string launcherDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string parentDirTrimmed = parentDir.TrimEnd('\\', '/');
            if (string.Equals(parentDirTrimmed, launcherDir, StringComparison.OrdinalIgnoreCase))
            {
                ShowNotification("出于安全考虑，无法删除启动器自身目录");
                return;
            }

            // 二次确认
            bool confirm = await ShowConfirmDialogAsync(
                $"⚠ 确定要删除此游戏吗？\n\n" +
                $"将删除整个文件夹：\n{parentDir}\n\n" +
                $"此操作不可撤销！");

            if (!confirm) return;

            // 执行删除
            try
            {
                ShowNotification("正在删除...");

                // 先尝试关闭游戏进程（如果正在运行的是这个游戏）
                if (_currentGameProcess != null && !_currentGameProcess.HasExited)
                {
                    try
                    {
                        string runningExe = _currentGameProcess.StartInfo.FileName;
                        if (string.Equals(runningExe, gamePath, StringComparison.OrdinalIgnoreCase))
                        {
                            _currentGameProcess.Exited -= OnGameProcessExited;
                            _currentGameProcess.Kill();
                            await Task.Run(() => _currentGameProcess.WaitForExit(3000));
                        }
                    }
                    catch { }
                }

                // 清除只读属性
                await Task.Run(() =>
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(parentDir, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                        }
                    }
                    catch { }
                });

                // 删除目录（异步执行避免卡 UI）
                bool deleteOk = await Task.Run(() =>
                {
                    try
                    {
                        Directory.Delete(parentDir, true);
                        return true;
                    }
                    catch (IOException ex)
                    {
                        // 尝试更友好的重试
                        System.Threading.Thread.Sleep(500);
                        try
                        {
                            Directory.Delete(parentDir, true);
                            return true;
                        }
                        catch
                        {
                            Debug.WriteLine($"删除失败: {ex.Message}");
                            return false;
                        }
                    }
                    catch { return false; }
                });

                if (deleteOk)
                {
                    // 清理统计记录
                    try
                    {
                        GameStatsManager.RemoveRecordsByPath(gamePath);
                        GameStatsManager.RemoveSessionsByPath(gamePath);
                    }
                    catch { }

                    // 关闭详情面板
                    HideDetailPanel();
                    _currentDetailGamePath = null;

                    // 重新扫描
                    await ScanGamesAsync();

                    ShowNotification("游戏已删除");
                }
                else
                {
                    ShowDetailPanel("删除失败",
                        "无法删除文件夹，可能原因：\n" +
                        "• 文件正在被其他程序占用（如正在运行的游戏）\n" +
                        "• 权限不足\n\n" +
                        $"目录：{parentDir}");
                }
            }
            catch (Exception ex)
            {
                ShowDetailPanel("删除失败", ex.Message);
            }
        }

        private async Task ScanGamesAsync()
        {
            var list = new List<GameFileInfo>();
            var dirs = _config.Directories;

            await Task.Run(() =>
            {
                // ==================== 正则 ====================
                // 版本目录名: 1.2.3（三位）
                var exeDirPattern = new Regex(@"^(\d+)\.(\d+)\.(\d+)$");

                // Python 游戏文件: 贪吃蛇(1.2.3).py
                var pyPattern = new Regex(@"贪吃蛇[（(](\d+)\.(\d+)\.(\d+)[）)]\.py$",
                    RegexOptions.IgnoreCase);

                foreach (string baseDir in dirs)
                {
                    if (!Directory.Exists(baseDir)) continue;

                    string baseDirTrimmed = baseDir.TrimEnd('\\', '/');
                    string baseDirName = Path.GetFileName(baseDirTrimmed);

                    try
                    {
                        // ==================== A. 老规则：<baseDir>/<版本目录>/dist/贪吃蛇.exe ====================
                        foreach (var subDir in Directory.GetDirectories(baseDir))
                        {
                            var dirName = Path.GetFileName(subDir);
                            var match = exeDirPattern.Match(dirName);
                            if (!match.Success) continue;

                            string exePath = Path.Combine(subDir, "dist", "贪吃蛇.exe");
                            if (File.Exists(exePath))
                            {
                                list.Add(new GameFileInfo
                                {
                                    Version = new Version(
                                        int.Parse(match.Groups[1].Value),
                                        int.Parse(match.Groups[2].Value),
                                        int.Parse(match.Groups[3].Value)),
                                    DisplayName = Path.GetFileName(exePath),
                                    FullPath = exePath,
                                    IsPython = false
                                });
                            }
                        }

                        // ==================== B. 老规则：<baseDir>/SnakeGame.exe（无版本目录） ====================
                        string snakePath = Path.Combine(baseDir, "SnakeGame.exe");
                        if (File.Exists(snakePath))
                        {
                            list.Add(new GameFileInfo
                            {
                                Version = new Version(9999, 9999, 9999),
                                DisplayName = "SnakeGame.exe",
                                FullPath = snakePath,
                                IsPython = false
                            });
                        }

                        // ==================== C. 老规则：递归找 *.py ====================
                        foreach (var file in Directory.GetFiles(baseDir, "*.py", SearchOption.AllDirectories))
                        {
                            var match = pyPattern.Match(Path.GetFileName(file));
                            if (!match.Success) continue;

                            list.Add(new GameFileInfo
                            {
                                Version = new Version(
                                    int.Parse(match.Groups[1].Value),
                                    int.Parse(match.Groups[2].Value),
                                    int.Parse(match.Groups[3].Value)),
                                DisplayName = Path.GetFileName(file),
                                FullPath = file,
                                IsPython = true
                            });
                        }

                        // ==================== D. 递归找 SnakeGameWpf.exe ====================
                        // 覆盖结构: <baseDir>/<版本号>/SnakeGameWpf.exe
                        foreach (var exePath in Directory.GetFiles(baseDir,
                                     "SnakeGameWpf.exe", SearchOption.AllDirectories))
                        {
                            AddExeGameFromPath(list, exePath, baseDirTrimmed, baseDirName, exeDirPattern);
                        }

                        // ==================== E. ★ 新增：递归找版本目录里的 SnakeGame.exe ====================
                        // 覆盖结构: <baseDir>/7.0.0/SnakeGame.exe
                        //           <baseDir>/1.0.0/SnakeGame.exe
                        foreach (var exePath in Directory.GetFiles(baseDir,
                                     "SnakeGame.exe", SearchOption.AllDirectories))
                        {
                            // 跳过直接在 baseDir 根目录下的（B 段已处理，避免版本号 9999 被覆盖）
                            string exeDir = Path.GetDirectoryName(exePath)?.TrimEnd('\\', '/');
                            if (string.Equals(exeDir, baseDirTrimmed, StringComparison.OrdinalIgnoreCase))
                                continue;

                            AddExeGameFromPath(list, exePath, baseDirTrimmed, baseDirName, exeDirPattern);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"扫描目录 {baseDir} 出错: {ex.Message}");
                    }
                }

                // ==================== F. 去重 ====================
                // 1) 同一 FullPath 只保留一份
                var byPath = list
                    .GroupBy(g => g.FullPath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                // 2) 同一 Version 优先保留 EXE
                list = byPath
                    .GroupBy(g => g.Version)
                    .Select(g => g.FirstOrDefault(x => !x.IsPython) ?? g.First())
                    .ToList();
            });

            Dispatcher.Invoke(() =>
            {
                _gameFiles = list;
                ApplySort();
                RefreshCards();
            });
        }

        /// <summary>
        /// ★ 新增辅助：从 exe 路径生成 GameFileInfo，版本号优先从父目录名解析
        /// </summary>
        private static void AddExeGameFromPath(
            List<GameFileInfo> list,
            string exePath,
            string baseDirTrimmed,
            string baseDirName,
            Regex exeDirPattern)
        {
            string parentDirName = Path.GetFileName(Path.GetDirectoryName(exePath) ?? "");
            Version ver;

            // 优先从父目录名解析（比如 7.0.0）
            var dirMatch = exeDirPattern.Match(parentDirName);
            if (dirMatch.Success)
            {
                ver = new Version(
                    int.Parse(dirMatch.Groups[1].Value),
                    int.Parse(dirMatch.Groups[2].Value),
                    int.Parse(dirMatch.Groups[3].Value),
                    0);
            }
            else
            {
                // 回退：读 exe 的 FileVersion
                ver = GetExeFileVersion(exePath);
            }

            list.Add(new GameFileInfo
            {
                Version = ver,
                DisplayName = Path.GetFileName(exePath),
                FullPath = exePath,
                IsPython = false
            });
        }

        private void ManualAdd()
        {
            var ofd = new OpenFileDialog { Filter = "贪吃蛇文件|*.exe;*.py|所有文件|*.*" };
            if (ofd.ShowDialog() == true)
            {
                var path = ofd.FileName;
                _gameFiles.Add(new GameFileInfo { Version = new Version(0, 0, 0), DisplayName = Path.GetFileName(path), FullPath = path, IsPython = path.EndsWith(".py") });
                ApplySort(); RefreshCards();
            }
        }

        // ==================== 数据页面 ====================
        private UIElement CreateStatsPage()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var stack = new StackPanel { Margin = new Thickness(4, 4, 4, 12) };

            var records = GameStatsManager.LoadRecords() ?? new List<PlayRecord>();
            var sessions = GameStatsManager.LoadSessions() ?? new List<GameSessionRecord>();

            // ============ 概览区 ============
            var overviewGroup = new GroupBox { Header = "总览", Margin = new Thickness(0, 0, 0, 16) };
            var overviewPanel = new UniformGrid { Rows = 1, Columns = 4 };

            double totalMinutes = records.Sum(r => r.TotalPlayMinutes);
            int totalLaunches = records.Sum(r => r.LaunchCount);
            int bestScore = sessions.Count > 0 ? sessions.Max(s => s.FinalScore) : 0;
            int totalKills = sessions.Sum(s => s.FinalKills);

            overviewPanel.Children.Add(CreateStatCard("累计游玩",
                FormatDurationValue(totalMinutes), FormatDurationUnit(totalMinutes),
                SafeFindBrush("AccentBrush")));
            overviewPanel.Children.Add(CreateStatCard("启动次数",
                totalLaunches.ToString(), "次",
                new SolidColorBrush(Color.FromRgb(0, 153, 102))));
            overviewPanel.Children.Add(CreateStatCard("历史最高分",
                bestScore.ToString(), "分",
                new SolidColorBrush(Color.FromRgb(255, 140, 0))));
            overviewPanel.Children.Add(CreateStatCard("累计击杀",
                totalKills.ToString(), "个",
                new SolidColorBrush(Color.FromRgb(200, 60, 60))));

            overviewGroup.Content = overviewPanel;
            stack.Children.Add(overviewGroup);

            // ============ 最近游玩快捷启动 ============
            var recentRecord = records.OrderByDescending(r => r.LastPlayed).FirstOrDefault();
            if (recentRecord != null && !string.IsNullOrEmpty(recentRecord.GamePath)
                && File.Exists(recentRecord.GamePath))
            {
                var quickStartGroup = new GroupBox { Header = "继续游戏", Margin = new Thickness(0, 0, 0, 16) };
                var quickPanel = new Grid();
                quickPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                quickPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                infoStack.Children.Add(new TextBlock
                {
                    Text = Path.GetFileName(recentRecord.GamePath),
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"上次游玩：{recentRecord.LastPlayed:yyyy-MM-dd HH:mm}  ·  共 {recentRecord.LaunchCount} 次  ·  累计 {FormatDurationValue(recentRecord.TotalPlayMinutes)}{FormatDurationUnit(recentRecord.TotalPlayMinutes)}",
                    FontSize = 12,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    Margin = new Thickness(0, 4, 0, 0)
                });
                Grid.SetColumn(infoStack, 0);
                quickPanel.Children.Add(infoStack);

                var btnContinue = new Button
                {
                    Content = "▶ 继续游戏",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 140,
                    Height = 40,
                    Background = SafeFindBrush("AccentBrush"),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold
                };
                string recentPath = recentRecord.GamePath;
                bool isPython = recentPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
                btnContinue.Click += async (s, e) => await LaunchGame(recentPath, isPython);
                Grid.SetColumn(btnContinue, 1);
                quickPanel.Children.Add(btnContinue);

                quickStartGroup.Content = quickPanel;
                stack.Children.Add(quickStartGroup);
            }

            // ============ 各游戏最佳成绩 ============
            var bestGroup = new GroupBox { Header = "各游戏最佳成绩", Margin = new Thickness(0, 0, 0, 16) };
            var bestStack = new StackPanel();

            if (sessions.Count == 0)
            {
                bestStack.Children.Add(new TextBlock
                {
                    Text = "暂无对局记录，快去玩一局吧！",
                    FontSize = 13,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    Margin = new Thickness(8, 4, 8, 4)
                });
            }
            else
            {
                var byGame = sessions
                    .Where(s => !string.IsNullOrEmpty(s.GamePath))
                    .GroupBy(s => s.GamePath)
                    .Select(g => new
                    {
                        Path = g.Key,
                        Best = g.OrderByDescending(s => s.FinalScore).First(),
                        TotalMinutes = g.Sum(s => s.DurationMinutes),
                        SessionCount = g.Count()
                    })
                    .OrderByDescending(x => x.Best.FinalScore)
                    .ToList();

                foreach (var grp in byGame)
                {
                    bestStack.Children.Add(CreateBestScoreRow(grp.Path, grp.Best, grp.TotalMinutes, grp.SessionCount));
                }
            }

            bestGroup.Content = bestStack;
            stack.Children.Add(bestGroup);

            // ============ 最近对局 ============
            var recentGroup = new GroupBox { Header = "最近对局（最多 15 条）", Margin = new Thickness(0, 0, 0, 16) };
            var recentStack = new StackPanel();

            if (sessions.Count == 0)
            {
                recentStack.Children.Add(new TextBlock
                {
                    Text = "暂无对局记录",
                    FontSize = 13,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    Margin = new Thickness(8, 4, 8, 4)
                });
            }
            else
            {
                foreach (var session in sessions.OrderByDescending(s => s.StartTime).Take(15))
                {
                    recentStack.Children.Add(CreateSessionRow(session));
                }
            }

            recentGroup.Content = recentStack;
            stack.Children.Add(recentGroup);

            scrollViewer.Content = stack;
            return scrollViewer;
        }

        private Border CreateStatCard(string label, string value, string unit, Brush accent)
        {
            var card = new Border
            {
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(5)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush")
            });
            var valuePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            valuePanel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = accent
            });
            if (!string.IsNullOrEmpty(unit))
            {
                valuePanel.Children.Add(new TextBlock
                {
                    Text = unit,
                    FontSize = 12,
                    Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(4, 0, 0, 5)
                });
            }
            stack.Children.Add(valuePanel);
            card.Child = stack;
            return card;
        }

        private Border CreateBestScoreRow(string path, GameSessionRecord best, double totalMinutes, int sessionCount)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 3, 0, 3),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0))
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            nameStack.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(path),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = SafeFindBrush("ForegroundBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = $"共 {sessionCount} 局  ·  {FormatDurationValue(totalMinutes)}{FormatDurationUnit(totalMinutes)}",
                FontSize = 11,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(nameStack, 0);
            grid.Children.Add(nameStack);

            var scoreStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
            scoreStack.Children.Add(new TextBlock
            {
                Text = "最高分",
                FontSize = 10,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            scoreStack.Children.Add(new TextBlock
            {
                Text = best.FinalScore.ToString(),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Grid.SetColumn(scoreStack, 1);
            grid.Children.Add(scoreStack);

            var killStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
            killStack.Children.Add(new TextBlock
            {
                Text = "击杀",
                FontSize = 10,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            killStack.Children.Add(new TextBlock
            {
                Text = best.FinalKills.ToString(),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Grid.SetColumn(killStack, 2);
            grid.Children.Add(killStack);

            var btnStart = new Button
            {
                Content = "▶ 启动",
                Style = SafeFindStyle("RoundedButton"),
                Width = 80,
                Height = 34,
                VerticalAlignment = VerticalAlignment.Center,
                Background = SafeFindBrush("AccentBrush"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            string gamePath = path;
            bool isPython = gamePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
            btnStart.Click += async (s, e) => await LaunchGame(gamePath, isPython);
            Grid.SetColumn(btnStart, 3);
            grid.Children.Add(btnStart);

            row.Child = grid;
            return row;
        }

        private Border CreateSessionRow(GameSessionRecord session)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(0x08, 0, 0, 0))
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            var timeBlock = new TextBlock
            {
                Text = session.StartTime.ToString("MM-dd HH:mm"),
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeBlock, 0);
            grid.Children.Add(timeBlock);

            string gameName = string.IsNullOrEmpty(session.GamePath)
                ? "未知"
                : Path.GetFileNameWithoutExtension(session.GamePath);
            var nameBlock = new TextBlock
            {
                Text = gameName,
                FontSize = 12,
                Foreground = SafeFindBrush("ForegroundBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(nameBlock, 1);
            grid.Children.Add(nameBlock);

            // ★ 修复：使用统一的模式名转换
            string modeDisplay = GetModeDisplayName(session.Mode);
            var modeBlock = new TextBlock
            {
                Text = modeDisplay,
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(modeBlock, 2);
            grid.Children.Add(modeBlock);

            var durBlock = new TextBlock
            {
                Text = $"{session.DurationMinutes:F1} 分",
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(durBlock, 3);
            grid.Children.Add(durBlock);

            var scoreBlock = new TextBlock
            {
                Text = $"{session.FinalScore} 分 / {session.FinalKills} 杀",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = SafeFindBrush("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(scoreBlock, 4);
            grid.Children.Add(scoreBlock);

            row.Child = grid;
            return row;
        }

        private static string FormatDurationValue(double minutes)
        {
            if (minutes < 1) return "0";
            if (minutes < 60) return ((int)Math.Round(minutes)).ToString();
            return (minutes / 60.0).ToString("F1");
        }

        private static string FormatDurationUnit(double minutes)
        {
            if (minutes < 60) return " 分钟";
            return " 小时";
        }

        // ==================== 下载页面 ====================
        private List<VersionInfo> ScanAvailableVersions()
        {
            var versions = new List<VersionInfo>();

            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".zip") && name.Contains(".Resources.SnakeGame_"));

            foreach (var resName in resourceNames)
            {
                var fileName = resName.Split('.').Last();
                var versionPart = fileName.Replace("SnakeGame_", "").Replace(".zip", "");
                var versionStr = versionPart.Replace("_", ".");
                if (!Version.TryParse(versionStr, out var version))
                    continue;

                using (var stream = assembly.GetManifestResourceStream(resName))
                {
                    if (stream == null) continue;
                    long size = stream.Length;
                    versions.Add(new VersionInfo
                    {
                        Version = versionStr,
                        DisplayName = $"贪吃蛇 ({versionStr})",
                        ReleaseNotes = "内置版本",
                        DownloadUrl = null,
                        FileSize = size
                    });
                }
            }

            string localVersionsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Versions");
            if (Directory.Exists(localVersionsDir))
            {
                var zipFiles = Directory.GetFiles(localVersionsDir, "SnakeGame_*.zip");
                foreach (var filePath in zipFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var versionPart = fileName.Replace("SnakeGame_", "").Replace(".zip", "");
                    var versionStr = versionPart.Replace("_", ".");
                    if (!Version.TryParse(versionStr, out var version))
                        continue;

                    if (versions.Any(v => v.Version == versionStr))
                        continue;

                    var fileInfo = new FileInfo(filePath);
                    versions.Add(new VersionInfo
                    {
                        Version = versionStr,
                        DisplayName = $"贪吃蛇 ({versionStr})",
                        ReleaseNotes = "本地版本",
                        DownloadUrl = filePath,
                        FileSize = fileInfo.Length
                    });
                }
            }

            return versions.OrderByDescending(v => Version.Parse(v.Version)).ToList();
        }

        private UIElement CreateDownloadPage()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ===== 顶部工具栏 =====
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };

            // ★ 仓库选择下拉
            var repoCombo = new ComboBox
            {
                Width = 220,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0),
                Style = SafeFindStyle("CustomComboBoxStyle")
            };
            foreach (var repo in GitHubReleaseService.Repos)
            {
                repoCombo.Items.Add(new ComboBoxItem
                {
                    Content = repo.DisplayName,
                    Tag = repo
                });
            }
            repoCombo.SelectedIndex = 0;

            repoCombo.SelectionChanged += async (s, e) =>
            {
                var selected = (repoCombo.SelectedItem as ComboBoxItem)?.Tag as GitHubRepoConfig;
                if (selected != null)
                    await LoadGitHubReleasesAsync(selected);
            };

            var btnRefresh = new Button
            {
                Content = "刷新列表",
                Style = SafeFindStyle("RoundedButton"),
                Width = 108,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnRefresh.Click += async (s, e) =>
            {
                var selected = (repoCombo.SelectedItem as ComboBoxItem)?.Tag as GitHubRepoConfig;
                if (selected != null)
                    await LoadGitHubReleasesAsync(selected);
            };

            var btnOpenRepo = new Button
            {
                Content = "打开仓库",
                Style = SafeFindStyle("RoundedButton"),
                Width = 108,
                Height = 34
            };
            btnOpenRepo.Click += (s, e) =>
            {
                var selected = (repoCombo.SelectedItem as ComboBoxItem)?.Tag as GitHubRepoConfig;
                if (selected == null) return;
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://github.com/{selected.Owner}/{selected.Repo}/releases",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            topPanel.Children.Add(repoCombo);
            topPanel.Children.Add(btnRefresh);
            topPanel.Children.Add(btnOpenRepo);

            // ===== 列表容器 =====
            _downloadListPanel = new StackPanel { Margin = new Thickness(0) };
            var scrollViewer = new ScrollViewer
            {
                Content = _downloadListPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            Grid.SetRow(topPanel, 0);
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(topPanel);
            mainGrid.Children.Add(scrollViewer);

            // 首次进入时异步加载第一个仓库
            _ = LoadGitHubReleasesAsync(GitHubReleaseService.Repos[0]);

            return mainGrid;
        }

        private void LoadVersionCards()
        {
            _downloadListPanel.Children.Clear();
            foreach (var state in _versionStates.Values.OrderByDescending(s => Version.Parse(s.Info.Version)))
            {
                var card = CreateVersionCard(state.Info);
                _downloadListPanel.Children.Add(card);
            }
        }

        private Border CreateVersionCard(VersionInfo info)
        {
            var card = new Border
            {
                Style = SafeFindStyle("GameCardStyle"),
                Height = 68,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid { Margin = new Thickness(16, 0, 16, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var verText = new TextBlock
            {
                Text = $"v{info.Version}",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush")
            };
            var nameText = new TextBlock
            {
                Text = info.DisplayName ?? "",
                FontSize = 11,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            leftStack.Children.Add(verText);
            leftStack.Children.Add(nameText);

            string sizeStr = info.FileSize > 0 ? $"{info.FileSize / 1048576.0:F1} MB" : "未知大小";
            var sizeBlock = new TextBlock
            {
                Text = sizeStr,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = SafeFindBrush("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(leftStack, 0);
            Grid.SetColumn(sizeBlock, 1);
            grid.Children.Add(leftStack);
            grid.Children.Add(sizeBlock);
            card.Child = grid;

            card.MouseLeftButtonUp += (s, e) => ShowVersionDetailPopup(info);
            return card;
        }

        private void EnsurePopupCreated()
        {
            if (_popupCreated) return;
            if (_rootGrid == null) return;

            _downloadDetailOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x12, 0x1A, 0x2B)),
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Opacity = 0
            };
            _downloadDetailOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == _downloadDetailOverlay)
                    CloseDownloadDetailPopup();
            };

            var panel = new Border
            {
                Width = 440,
                MaxHeight = 520,
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            panel.Effect = new DropShadowEffect { ShadowDepth = 10, BlurRadius = 36, Opacity = 0.35, Color = Color.FromRgb(0x0F, 0x1B, 0x33) };
            _popupScaleTransform = new ScaleTransform(0.8, 0.8);
            panel.RenderTransform = _popupScaleTransform;

            var panelGrid = new Grid();
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _popupTitle = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = SafeFindBrush("ForegroundBrush") };

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                FontSize = 14,
                Background = Brushes.Transparent,
                Foreground = SafeFindBrush("ForegroundBrush"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => CloseDownloadDetailPopup();

            Grid.SetColumn(_popupTitle, 0);
            Grid.SetColumn(closeBtn, 1);
            titleBar.Children.Add(_popupTitle);
            titleBar.Children.Add(closeBtn);

            var contentStack = new StackPanel();
            _popupSize = new TextBlock { FontSize = 14, Foreground = SafeFindBrush("ForegroundBrush"), Margin = new Thickness(0, 5, 0, 0) };
            _popupNotes = new TextBlock { FontSize = 13, Foreground = SafeFindBrush("SecondaryForegroundBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
            _popupStatus = new TextBlock { FontSize = 13, Foreground = SafeFindBrush("ForegroundBrush"), Margin = new Thickness(0, 14, 0, 12) };

            _popupDownloadBtn = new Button { Content = "下载", Style = SafeFindStyle("RoundedButton"), Width = 160, Height = 42, Margin = new Thickness(0, 12, 0, 0) };
            _popupDownloadBtn.Click += PopupDownloadBtn_Click;

            _popupProgressBar = new ProgressBar
            {
                Height = 20,
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = Visibility.Collapsed,
                Style = SafeFindStyle("CustomProgressBarStyle")
            };
            _popupCancelBtn = new Button { Content = "取消", Style = SafeFindStyle("RoundedButton"), Width = 90, Height = 34, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 12, 0, 0) };
            _popupCancelBtn.Click += (s, e) => _currentDownloadState?.Cts?.Cancel();

            contentStack.Children.Add(_popupSize);
            contentStack.Children.Add(_popupNotes);
            contentStack.Children.Add(_popupStatus);
            contentStack.Children.Add(_popupDownloadBtn);
            contentStack.Children.Add(_popupProgressBar);
            contentStack.Children.Add(_popupCancelBtn);

            var scrollViewer = new ScrollViewer
            {
                Content = contentStack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            Grid.SetRow(titleBar, 0);
            Grid.SetRow(scrollViewer, 1);
            panelGrid.Children.Add(titleBar);
            panelGrid.Children.Add(scrollViewer);
            panel.Child = panelGrid;

            var overlayGrid = new Grid();
            overlayGrid.Children.Add(panel);
            _downloadDetailOverlay.Child = overlayGrid;

            _rootGrid.Children.Add(_downloadDetailOverlay);
            int rowSpan = Math.Max(1, _rootGrid.RowDefinitions.Count);
            int colSpan = Math.Max(1, _rootGrid.ColumnDefinitions.Count);
            Grid.SetRowSpan(_downloadDetailOverlay, rowSpan);
            Grid.SetColumnSpan(_downloadDetailOverlay, colSpan);

            _popupCreated = true;
        }

        private void ShowVersionDetailPopup(VersionInfo info)
        {
            if (!_versionStates.ContainsKey(info.Version))
                _versionStates[info.Version] = new VersionDownloadState { Info = info };
            _currentDownloadState = _versionStates[info.Version];

            _popupTitle.Text = $"版本 {info.Version}";
            _popupSize.Text = $"大小: {(info.FileSize > 0 ? $"{info.FileSize / 1048576.0:F1} MB" : "未知")}";
            _popupNotes.Text = info.ReleaseNotes ?? "暂无更新日志";
            RefreshPopupButtons();

            _downloadDetailOverlay.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };

            _downloadDetailOverlay.BeginAnimation(OpacityProperty, fadeIn);
            _popupScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            _popupScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        private void CloseDownloadDetailPopup()
        {
            if (_downloadDetailOverlay == null) return;
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) => _downloadDetailOverlay.Visibility = Visibility.Collapsed;
            var scaleXOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            var scaleYOut = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

            _downloadDetailOverlay.BeginAnimation(OpacityProperty, fadeOut);
            _popupScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXOut);
            _popupScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYOut);
        }

        private void RefreshPopupButtons()
        {
            if (_currentDownloadState == null) return;
            var state = _currentDownloadState;

            if (state.IsInstalled)
            {
                _popupStatus.Text = "✓ 已安装";
                _popupDownloadBtn.Content = "重新安装";
                _popupDownloadBtn.IsEnabled = true;
                _popupProgressBar.Visibility = Visibility.Collapsed;
                _popupCancelBtn.Visibility = Visibility.Collapsed;
            }
            else if (state.IsDownloaded)
            {
                _popupStatus.Text = "已下载，可安装";
                _popupDownloadBtn.Content = "安装";
                _popupDownloadBtn.IsEnabled = true;
                _popupProgressBar.Visibility = Visibility.Collapsed;
                _popupCancelBtn.Visibility = Visibility.Collapsed;
            }
            else if (state.IsDownloading)
            {
                _popupStatus.Text = $"下载中... {state.Progress:F1}%";
                _popupDownloadBtn.IsEnabled = false;
                _popupProgressBar.Visibility = Visibility.Visible;
                _popupCancelBtn.Visibility = Visibility.Visible;
                var anim = new DoubleAnimation(state.Progress, TimeSpan.FromMilliseconds(220));
                _popupProgressBar.BeginAnimation(ProgressBar.ValueProperty, anim);
            }
            else
            {
                _popupStatus.Text = "未下载";
                _popupDownloadBtn.Content = "下载";
                _popupDownloadBtn.IsEnabled = true;
                _popupProgressBar.Visibility = Visibility.Collapsed;
                _popupCancelBtn.Visibility = Visibility.Collapsed;
            }
        }

        private async void PopupDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDownloadState == null) return;
            var state = _currentDownloadState;

            if (state.IsInstalled)
            {
                state.IsInstalled = false;
                state.IsDownloaded = false;
                RefreshPopupButtons();
                await StartDownload(state);
            }
            else if (state.IsDownloaded)
            {
                InstallDownloadedPackage(state.DownloadedFilePath, state.Info);
                state.IsInstalled = true;
                state.IsDownloaded = false;
                RefreshPopupButtons();
            }
            else
            {
                await StartDownload(state);
            }
        }

        private async Task<bool> ExtractEmbeddedResourceAsync(string version, string savePath)
        {
            string resourceVersion = version.Replace('.', '_');
            string resourceName = $"SnakeLauncherWPF.Resources.SnakeGame_{resourceVersion}.zip";
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return false;
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await stream.CopyToAsync(fileStream);
                }
                return true;
            }
        }

        private async Task StartDownload(VersionDownloadState state)
        {
            state.IsDownloading = true;
            state.Progress = 0;
            state.Cts = new CancellationTokenSource();
            RefreshPopupButtons();

            string downloadDir = _config.DownloadPath;
            if (string.IsNullOrWhiteSpace(downloadDir))
                downloadDir = Path.Combine(Path.GetTempPath(), "SnakeGameDownloads");
            Directory.CreateDirectory(downloadDir);
            string tempPath = Path.Combine(downloadDir, $"snake_update_{state.Info.Version}.zip");

            if (File.Exists(tempPath))
            {
                state.IsDownloading = false;
                state.IsDownloaded = true;
                state.DownloadedFilePath = tempPath;
                if (_currentDownloadState == state) RefreshPopupButtons();
                return;
            }

            bool success = false;

            if (string.IsNullOrEmpty(state.Info.DownloadUrl) || state.Info.DownloadUrl.StartsWith("SnakeGame_"))
            {
                success = await ExtractEmbeddedResourceAsync(state.Info.Version, tempPath);
            }

            if (!success && !string.IsNullOrEmpty(state.Info.DownloadUrl) && File.Exists(state.Info.DownloadUrl))
            {
                File.Copy(state.Info.DownloadUrl, tempPath, true);
                success = true;
            }

            if (!success && state.Info.DownloadUrl?.StartsWith("http") == true)
            {
                await NetworkDownload(state, tempPath);
                return;
            }

            if (success)
            {
                state.IsDownloading = false;
                state.IsDownloaded = true;
                state.DownloadedFilePath = tempPath;
                if (_currentDownloadState == state) RefreshPopupButtons();
            }
            else
            {
                state.IsDownloading = false;
                _popupStatus.Text = "未找到版本文件";
                if (_currentDownloadState == state) RefreshPopupButtons();
            }
        }

        private async Task NetworkDownload(VersionDownloadState state, string tempPath)
        {
            void OnProgress(object _, DownloadProgressEventArgs args)
            {
                Dispatcher.Invoke(() =>
                {
                    state.Progress = args.ProgressPercentage;
                    if (_currentDownloadState == state) RefreshPopupButtons();
                });
            }
            void OnCompleted(object _, string path)
            {
                Dispatcher.Invoke(() =>
                {
                    state.IsDownloading = false;
                    state.IsDownloaded = true;
                    state.DownloadedFilePath = path;
                    if (_currentDownloadState == state) RefreshPopupButtons();
                });
                Cleanup();
            }
            void OnFailed(object _, string error)
            {
                Dispatcher.Invoke(() =>
                {
                    state.IsDownloading = false;
                    _popupStatus.Text = "下载失败: " + error;
                    if (_currentDownloadState == state) RefreshPopupButtons();
                });
                Cleanup();
            }
            void Cleanup()
            {
                _downloadManager.ProgressChanged -= OnProgress;
                _downloadManager.DownloadCompleted -= OnCompleted;
                _downloadManager.DownloadFailed -= OnFailed;
                state.Cts?.Dispose();
                state.Cts = null;
            }

            _downloadManager.ProgressChanged += OnProgress;
            _downloadManager.DownloadCompleted += OnCompleted;
            _downloadManager.DownloadFailed += OnFailed;

            await _downloadManager.DownloadFileAsync(state.Info.DownloadUrl, tempPath, state.Cts.Token);
        }

        private void InstallDownloadedPackage(string zipPath, VersionInfo info)
        {
            string targetDir = _config.Directories[_config.CurrentDirIndex];
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                        if (!destPath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("非法路径");
                        if (string.IsNullOrEmpty(entry.Name))
                            Directory.CreateDirectory(destPath);
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                            entry.ExtractToFile(destPath, overwrite: true);
                        }
                    }
                }
                ShowNotification($"版本 {info.Version} 安装成功！");
                _ = ScanGamesAsync();
            }
            catch (Exception ex) { ShowDetailPanel("安装失败", ex.Message); }
        }

        // ========== 设置页面 ==========
        private UIElement CreateSettingsPage()
        {
            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(4, 4, 4, 12) };

            // ==================== 1. 游戏目录 ====================
            var dirGroup = new GroupBox { Header = "游戏目录", Margin = new Thickness(0, 0, 0, 18) };
            var dirListBox = new ListBox
            {
                Height = 140,
                Margin = new Thickness(0, 10, 0, 10),
                ItemContainerStyle = SafeFindStyle("CustomListBoxItemStyle")
            };
            foreach (var d in _config.Directories) dirListBox.Items.Add(d);
            dirListBox.SelectedIndex = _config.CurrentDirIndex;

            var dirBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var btnAdd = new Button { Content = "添加目录", Style = SafeFindStyle("RoundedButton"), Margin = new Thickness(0, 0, 8, 0) };
            btnAdd.Click += (s, e) => AddDirectory(dirListBox);
            var btnRemove = new Button { Content = "删除选中", Style = SafeFindStyle("RoundedButton"), Margin = new Thickness(0, 0, 8, 0) };
            btnRemove.Click += (s, e) => RemoveDirectory(dirListBox);
            var btnApply = new Button { Content = "应用选中", Style = SafeFindStyle("RoundedButton") };
            btnApply.Click += (s, e) => ApplyDirectory(dirListBox);
            dirBtnPanel.Children.Add(btnAdd);
            dirBtnPanel.Children.Add(btnRemove);
            dirBtnPanel.Children.Add(btnApply);

            var dirContentPanel = new StackPanel();
            dirContentPanel.Children.Add(dirListBox);
            dirContentPanel.Children.Add(dirBtnPanel);
            dirGroup.Content = dirContentPanel;

            // ==================== 2. 下载路径 ====================
            var dlGroup = new GroupBox { Header = "下载路径", Margin = new Thickness(0, 0, 0, 18) };
            var dlPanel = new StackPanel();
            var dlPathText = new TextBlock
            {
                Text = _config.DownloadPath,
                Foreground = SafeFindBrush("ForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 10),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12
            };
            var btnSetDlPath = new Button
            {
                Content = "选择文件夹",
                Style = SafeFindStyle("RoundedButton"),
                Width = 130,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnSetDlPath.Click += (s, e) =>
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "选择下载存储文件夹";
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _config.DownloadPath = dialog.SelectedPath;
                        dlPathText.Text = _config.DownloadPath;
                        ConfigManager.Save(_config);
                    }
                }
            };
            dlPanel.Children.Add(dlPathText);
            dlPanel.Children.Add(btnSetDlPath);
            dlGroup.Content = dlPanel;

            // ==================== 3. 主题模式 ====================
            var themeGroup = new GroupBox { Header = "主题模式", Margin = new Thickness(0, 0, 0, 18) };
            var themePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnLight = new Button { Content = "浅色", Style = SafeFindStyle("RoundedButton"), Width = 90, Margin = new Thickness(0, 0, 10, 0) };
            btnLight.Click += (s, e) => SetTheme("light");
            var btnDark = new Button { Content = "深色", Style = SafeFindStyle("RoundedButton"), Width = 90 };
            btnDark.Click += (s, e) => SetTheme("dark");
            themePanel.Children.Add(btnLight);
            themePanel.Children.Add(btnDark);
            themeGroup.Content = themePanel;

            // ==================== 4. 主题色 ====================
            var colorGroup = new GroupBox { Header = "主题色", Margin = new Thickness(0, 0, 0, 18) };
            var colorPanel = new WrapPanel();
            _accentSwatches.Clear();

            var presetColors = new Dictionary<string, string>
    {
        { "#0078d4", "蓝色" }, { "#ff4343", "红色" }, { "#00cc6a", "绿色" },
        { "#886ce4", "紫色" }, { "#ff763b", "橙色" }, { "#00b7c3", "青色" }
    };
            foreach (var kv in presetColors)
            {
                var border = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(10),
                    Background = (SolidColorBrush)new BrushConverter().ConvertFromString(kv.Key),
                    Margin = new Thickness(4, 4, 8, 4),
                    Cursor = Cursors.Hand,
                    Tag = kv.Key
                };
                border.Effect = new DropShadowEffect
                {
                    ShadowDepth = 2,
                    BlurRadius = 8,
                    Opacity = 0.35,
                    Color = (Color)ColorConverter.ConvertFromString(kv.Key)
                };

                border.MouseLeftButtonUp += (s, e) =>
                {
                    _config.AccentColor = kv.Key;
                    ApplyTheme();
                    RefreshAccentSwatches();
                };

                _accentSwatches.Add(border);
                colorPanel.Children.Add(border);
            }

            RefreshAccentSwatches();
            colorGroup.Content = colorPanel;

            // ==================== 5. 强调色应用到文字 ====================
            var applyCheck = new CheckBox
            {
                Content = "强调色应用到文字",
                IsChecked = _config.ApplyText,
                Margin = new Thickness(4, 0, 0, 18)
            };
            applyCheck.Checked += (s, e) => { _config.ApplyText = true; ApplyTheme(); };
            applyCheck.Unchecked += (s, e) => { _config.ApplyText = false; ApplyTheme(); };

            // ==================== 6. 配置管理 ====================
            var configGroup = new GroupBox { Header = "配置管理", Margin = new Thickness(0, 0, 0, 18) };
            var configPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnExport = new Button
            {
                Content = "导出配置",
                Style = SafeFindStyle("RoundedButton"),
                Width = 110,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnExport.Click += (s, e) =>
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "JSON文件|*.json",
                    FileName = "launcher_config.json"
                };
                if (sfd.ShowDialog() == true)
                {
                    // ★ 用 ConfigManager.ExportTo，避免 File.Copy 独占冲突
                    if (ConfigManager.ExportTo(sfd.FileName))
                        ShowNotification("配置导出成功");
                    else
                        ShowNotification("配置导出失败（文件被占用）");
                }
            };

            var btnImport = new Button
            {
                Content = "导入配置",
                Style = SafeFindStyle("RoundedButton"),
                Width = 110,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnImport.Click += (s, e) =>
            {
                var ofd = new OpenFileDialog { Filter = "JSON文件|*.json" };
                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        // ★ 读入内存后再写入 ConfigPath，避免直接覆盖被占用
                        string json;
                        using (var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs, Encoding.UTF8))
                        {
                            json = sr.ReadToEnd();
                        }

                        // 写到目标位置
                        lock (typeof(ConfigManager))
                        {
                            using (var fs = new FileStream(ConfigManager.ConfigPath, FileMode.Create,
                                FileAccess.Write, FileShare.Read, 4096))
                            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                            {
                                sw.Write(json);
                            }
                        }

                        _config = ConfigManager.Load();
                        ConfigManager.SetSyncFolder(_config.SyncFolder);
                        ApplyTheme();
                        LoadDirectories();
                        SelectTab(0);
                        ShowNotification("配置导入成功");
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"导入失败: {ex.Message}");
                    }
                }
            };

            // ★ 补回 btnSync 定义（同步文件夹按钮）
            var btnSync = new Button
            {
                Content = "同步文件夹",
                Style = SafeFindStyle("RoundedButton"),
                Width = 130
            };
            btnSync.Click += (s, e) =>
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _config.SyncFolder = dialog.SelectedPath;
                        ConfigManager.SetSyncFolder(dialog.SelectedPath);
                        ConfigManager.Save(_config);
                        ShowNotification("同步文件夹已设置");
                    }
                }
            };

            configPanel.Children.Add(btnExport);
            configPanel.Children.Add(btnImport);
            configPanel.Children.Add(btnSync);
            configGroup.Content = configPanel;

            // ==================== 7. 启动器更新 ====================
            var updateGroup = new GroupBox { Header = "启动器更新", Margin = new Thickness(0, 0, 0, 18) };
            var updatePanel = new StackPanel();

            var currentVersion = UpdateService.GetCurrentVersion();
            var updateInfoText = new TextBlock
            {
                Text = $"当前版本: v{currentVersion}",
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 12
            };

            // ★ 自动检查开关
            var autoCheckBox = new CheckBox
            {
                Content = "启动后自动检查更新（每 6 小时最多检查一次）",
                IsChecked = _config.AutoCheckUpdate,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = SafeFindBrush("ForegroundBrush"),
                FontSize = 12
            };
            autoCheckBox.Checked += (s, e) =>
            {
                _config.AutoCheckUpdate = true;
                ConfigManager.Save(_config);
                ShowNotification("已开启自动检查更新");
            };
            autoCheckBox.Unchecked += (s, e) =>
            {
                _config.AutoCheckUpdate = false;
                ConfigManager.Save(_config);
                ShowNotification("已关闭自动检查更新");
            };

            // ★ 清除跳过记录
            var btnClearSkip = new Button
            {
                Content = "清除跳过记录",
                Style = SafeFindStyle("RoundedButton"),
                Width = 130,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12)
            };
            btnClearSkip.Click += (s, e) =>
            {
                _config.SkipUpdateVersion = "";
                ConfigManager.Save(_config);
                ShowNotification("已清除跳过记录");
            };

            var updateStatusText = new TextBlock
            {
                Text = "",
                Foreground = SafeFindBrush("ForegroundBrush"),
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            var updateProgressBar = new ProgressBar
            {
                Height = 16,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
                Maximum = 100
            };

            var updateBtnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var btnCheckUpdate = new Button
            {
                Content = "检查更新",
                Style = SafeFindStyle("RoundedButton"),
                Width = 120,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var btnReleasePage = new Button
            {
                Content = "打开发布页",
                Style = SafeFindStyle("RoundedButton"),
                Width = 120,
                Height = 34
            };
            btnReleasePage.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = UpdateService.GetReleasePageUrl(),
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowNotification($"打开失败: {ex.Message}");
                }
            };

            btnCheckUpdate.Click += async (s, e) =>
            {
                btnCheckUpdate.IsEnabled = false;
                btnReleasePage.IsEnabled = false;
                updateStatusText.Text = "正在检查更新...";
                updateProgressBar.Visibility = Visibility.Collapsed;
                updateProgressBar.Value = 0;

                UpdateCheckResult update = null;
                try
                {
                    update = await UpdateService.CheckForUpdateAsync();
                }
                catch (Exception ex)
                {
                    updateStatusText.Text = $"检查更新失败: {ex.Message}";
                    btnCheckUpdate.IsEnabled = true;
                    btnReleasePage.IsEnabled = true;
                    return;
                }

                if (update == null || !update.HasUpdate)
                {
                    updateStatusText.Text = $"✓ 当前已是最新版本 (v{UpdateService.GetCurrentVersion()})";
                    btnCheckUpdate.IsEnabled = true;
                    btnReleasePage.IsEnabled = true;
                    return;
                }

                updateStatusText.Text = $"发现新版本 v{update.LatestVersion}";

                string sizeStr = update.FileSize > 0
                    ? $"{update.FileSize / 1048576.0:F1} MB"
                    : "未知大小";

                var confirm = await ShowConfirmDialogAsync(
                    $"发现新版本 v{update.LatestVersion}\n\n" +
                    $"更新说明:\n{(string.IsNullOrEmpty(update.ReleaseNotes) ? "无" : update.ReleaseNotes)}\n\n" +
                    $"文件大小: {sizeStr}\n\n" +
                    "是否立即更新？\n更新完成后启动器将自动重启。");

                if (!confirm)
                {
                    updateStatusText.Text = "已取消更新";
                    btnCheckUpdate.IsEnabled = true;
                    btnReleasePage.IsEnabled = true;
                    return;
                }

                if (string.IsNullOrEmpty(update.DownloadUrl))
                {
                    updateStatusText.Text = "✗ 错误: 未找到可用的下载链接";
                    btnCheckUpdate.IsEnabled = true;
                    btnReleasePage.IsEnabled = true;
                    return;
                }

                try
                {
                    updateProgressBar.Visibility = Visibility.Visible;
                    updateStatusText.Text = "正在下载更新包...";

                    var progress = new Progress<double>(p =>
                    {
                        updateProgressBar.Value = p;
                        updateStatusText.Text = $"正在下载... {p:F1}%";
                    });

                    string zipPath = await UpdateService.DownloadUpdateAsync(
                        update.DownloadUrl, progress, CancellationToken.None);

                    if (!string.IsNullOrEmpty(update.Sha256))
                    {
                        updateStatusText.Text = "正在校验文件完整性...";
                        bool ok = await Task.Run(() => UpdateService.VerifySha256(zipPath, update.Sha256));
                        if (!ok)
                        {
                            try { File.Delete(zipPath); } catch { }
                            updateStatusText.Text = "✗ 文件校验失败，更新包可能已损坏，请重试";
                            updateProgressBar.Visibility = Visibility.Collapsed;
                            btnCheckUpdate.IsEnabled = true;
                            btnReleasePage.IsEnabled = true;
                            return;
                        }
                    }

                    updateStatusText.Text = "✓ 校验通过，正在启动更新程序...";
                    updateProgressBar.Value = 100;
                    await Task.Delay(600);

                    UpdateService.LaunchUpdater(zipPath);
                }
                catch (Exception ex)
                {
                    updateStatusText.Text = $"更新失败: {ex.Message}";
                    updateProgressBar.Visibility = Visibility.Collapsed;
                    btnCheckUpdate.IsEnabled = true;
                    btnReleasePage.IsEnabled = true;
                }
            };

            updateBtnPanel.Children.Add(btnCheckUpdate);
            updateBtnPanel.Children.Add(btnReleasePage);

            updatePanel.Children.Add(updateInfoText);
            updatePanel.Children.Add(autoCheckBox);
            updatePanel.Children.Add(btnClearSkip);
            updatePanel.Children.Add(updateBtnPanel);
            updatePanel.Children.Add(updateProgressBar);
            updatePanel.Children.Add(updateStatusText);
            updateGroup.Content = updatePanel;

            // ==================== 组装 ====================
            stack.Children.Add(dirGroup);
            stack.Children.Add(dlGroup);
            stack.Children.Add(themeGroup);
            stack.Children.Add(colorGroup);
            stack.Children.Add(applyCheck);
            stack.Children.Add(configGroup);
            stack.Children.Add(updateGroup);

            scrollViewer.Content = stack;
            return scrollViewer;
        }

        private void AddDirectory(ListBox listBox)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择游戏目录";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;
                    if (!_config.Directories.Contains(path))
                    {
                        _config.Directories.Add(path);
                        listBox.Items.Add(path);
                        ConfigManager.Save(_config);
                    }
                }
            }
        }

        private void RefreshAccentSwatches()
        {
            foreach (var b in _accentSwatches)
            {
                if ((b.Tag as string) == _config.AccentColor)
                {
                    b.BorderBrush = SafeFindBrush("ForegroundBrush");
                    b.BorderThickness = new Thickness(2);
                }
                else
                {
                    b.BorderBrush = null;
                    b.BorderThickness = new Thickness(0);
                }
            }
        }

        private void RemoveDirectory(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0 && _config.Directories.Count > 1)
            {
                int idx = listBox.SelectedIndex;
                _config.Directories.RemoveAt(idx);
                listBox.Items.RemoveAt(idx);
                if (_config.CurrentDirIndex >= _config.Directories.Count) _config.CurrentDirIndex = _config.Directories.Count - 1;
                ConfigManager.Save(_config);
                _ = ScanGamesAsync();
                if (_currentPathLabel != null && _config.CurrentDirIndex < _config.Directories.Count)
                    _currentPathLabel.Content = _config.Directories[_config.CurrentDirIndex];
            }
        }

        private void ApplyDirectory(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex < _config.Directories.Count)
            {
                _config.CurrentDirIndex = listBox.SelectedIndex;
                ConfigManager.Save(_config);
                if (_currentPathLabel != null) _currentPathLabel.Content = _config.Directories[_config.CurrentDirIndex];
                _ = ScanGamesAsync();
            }
        }

        private async void SetTheme(string mode)
        {
            if (_config.ThemeMode == mode) return;

            _config.ThemeMode = mode;
            ApplyTheme();

            string message = mode == "dark"
                ? "已切换为深色模式。\n\n启动画面需要重启启动器后才能应用深色效果。\n\n点击“确定”后启动器将立即重启。"
                : "已切换为浅色模式。\n\n启动画面需要重启启动器后才能应用浅色效果。\n\n点击“确定”后启动器将立即重启。";

            await ShowInfoDialogAsync(message);

            RestartLauncher();
        }
        private void ApplyTheme() { ThemeManager.ApplyTheme(_config); ConfigManager.Save(_config); }

        private static BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }

        private UIElement CreateAboutPage()
        {
            var grid = new Grid();
            var stack = new StackPanel { Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            stack.Children.Add(new TextBlock
            {
                Text = "贪吃蛇启动器",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("ForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"v{UpdateService.GetCurrentVersion().ToString(3)} · Liquid Glass Edition",
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 24)
            });

            try
            {
                var bmp = Properties.Resources.MMA1;
                var img = new Image
                {
                    Source = BitmapToImageSource(bmp),
                    Width = 180,
                    Height = 180,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                var imgBorder = new Border
                {
                    CornerRadius = new CornerRadius(24),
                    ClipToBounds = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = new DropShadowEffect { ShadowDepth = 6, BlurRadius = 24, Opacity = 0.22, Color = Color.FromRgb(0x1A, 0x2A, 0x44) }
                };
                imgBorder.Child = img;
                stack.Children.Add(imgBorder);
            }
            catch { }

            stack.Children.Add(new TextBlock
            {
                Text = "开发：没冇啊，ChatGPT，Gemini，Codex，deep seek",
                FontSize = 13,
                Foreground = SafeFindBrush("ForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "© 2026 MEIMAOA保留权利",
                FontSize = 12,
                Foreground = SafeFindBrush("SecondaryForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            grid.Children.Add(stack);
            return grid;
        }

        private bool IsVersionCompatible(Version version)
        {
            if (version.Major == 9999 && version.Minor == 9999 && version.Build == 9999)
                return true;

            var v1 = new Version(2, 15, 4);
            var v2 = new Version(3, 0, 5);
            if (version >= v1 && version <= v2)
                return true;

            var v3 = new Version(2, 2, 0);
            var v4 = new Version(2, 4, 0);
            if (version >= v3 && version <= v4)
                return true;

            return false;
        }

        private void LoadDirectories()
        {
            if (_config.Directories == null || _config.Directories.Count == 0)
                _config.Directories = new List<string> { Directory.GetCurrentDirectory() };

            if (_config.CurrentDirIndex >= _config.Directories.Count)
                _config.CurrentDirIndex = 0;

            // ★ 新增：确保下载目录在扫描列表里
            try
            {
                string downloadDir = _config.DownloadPath;
                if (!string.IsNullOrWhiteSpace(downloadDir)
                    && !_config.Directories.Any(d =>
                        string.Equals(d, downloadDir, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.Directories.Add(downloadDir);
                    ConfigManager.Save(_config);
                }
            }
            catch { }
        }

        // ========== 窗口控制 ==========
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    return;
                }
                DragMove();
            }
        }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}