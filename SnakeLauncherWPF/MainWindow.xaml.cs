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
        public bool ApplyText { get; set; } = false;
        public string WindowSize { get; set; } = "1100x700";
        public string SyncFolder { get; set; } = "";
        public string SaveFolder { get; set; } = "";
        public string UpdateIndexUrl { get; set; } = "http://192.168.190.130:8081/versions.txt";
        public string DownloadPath { get; set; } = "";
        public int WindowWidth { get; set; } = 1100;
        public int WindowHeight { get; set; } = 700;
        // 新增：窗口位置
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
    }

    // ========== 配置管理器 ==========
    public static class ConfigManager
    {
        private static string _customConfigPath;
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
                string json = File.ReadAllText(ConfigPath);
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
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }

    // ========== 主题管理器（支持深色/浅色和自定义主题色） ==========
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

            if (config.ApplyText)
            {
                resources["ForegroundBrush"] = resources["AccentBrush"];
                resources["MenuForegroundBrush"] = resources["AccentBrush"];
            }

            ThemeChanged?.Invoke();
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
        private static List<PlayRecord> _records;
        private static List<GameSessionRecord> _sessions;

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
            try { File.WriteAllText(StatsPath, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true })); } catch { }
        }
        public static void SaveSessions()
        {
            try { File.WriteAllText(HistoryPath, JsonSerializer.Serialize(_sessions, new JsonSerializerOptions { WriteIndented = true })); } catch { }
        }
        public static PlayRecord GetRecord(string path) => _records?.FirstOrDefault(r => r.GamePath == path);

        public static void RecordLaunch(string path)
        {
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
        // 兼容性自绘对话框
        private Border _compatibilityDialog;
        private TextBlock _compatibilityMessage;
        private TaskCompletionSource<bool> _compatibilityTcs;
        // 确认对话框（用于“已有游戏在运行”等需要用户确认的场景）
        private Border _confirmDialog;
        private TextBlock _confirmMessage;
        private TaskCompletionSource<bool> _confirmTcs;

        private static readonly Dictionary<string, string> ModeNameMap = new Dictionary<string, string>
        {
            { "Classic", "经典模式" },
            { "Survival", "生存模式" },
            { "Timed", "限时模式" },
            { "Endless", "无尽模式" },
            { "Battle", "对战模式" },
            { "Multiplayer", "多人模式" }
        };

        // ========== 构造函数 ==========
        public MainWindow()
        {
            try
            {
                // 1. 加载配置（必须放在最前面，所有后续逻辑都依赖配置）
                _config = ConfigManager.Load();

                // 2. 同步文件夹设置
                if (!string.IsNullOrWhiteSpace(_config.SyncFolder))
                    ConfigManager.SetSyncFolder(_config.SyncFolder);

                // 3. 应用主题（基于加载的配置）
                ThemeManager.ApplyTheme(_config);

                // 4. 初始化界面组件
                InitializeComponent();

                // 5. 恢复窗口大小
                this.Width = _config.WindowWidth;
                this.Height = _config.WindowHeight;

                // 6. 恢复窗口位置，无记录则默认居中
                // 每次启动窗口居中（忽略之前保存的位置）
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // 7. 加载游戏统计数据
                GameStatsManager.LoadRecords();
                GameStatsManager.LoadSessions();

                // 8. 订阅全局事件
                ThemeManager.ThemeChanged += RefreshCards;
                IpcService.StatsReceived += OnStatsReceived;
                IpcService.Disconnected += OnIpcDisconnected;

                // 9. 初始化目录并扫描游戏
                LoadDirectories();
                _ = ScanGamesAsync();

                // 10. 初始化各类定时器
                _playTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _playTimer.Tick += PlayTimer_Tick;

                _healthCheckTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _healthCheckTimer.Tick += HealthCheck_Tick;

                _performanceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _performanceTimer.Tick += PerformanceTimer_Tick;

                // 11. 初始化下载管理器
                _downloadManager = new DownloadManager();

                // 12. 窗口加载完成事件
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
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
                    Visibility = Visibility.Collapsed,
                    Opacity = 0,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var panel = new Border
                {
                    Width = 420,
                    MaxHeight = 480,
                    Background = SafeFindBrush("CardBackgroundBrush"),
                    BorderBrush = SafeFindBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(20),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
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
                    Foreground = SafeFindBrush("AccentBrush")
                };
                _compatibilityMessage = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    Margin = new Thickness(0, 10, 0, 10)
                };
                var btnOk = new Button
                {
                    Content = "确定",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 80,
                    Height = 32,
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
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
                    Visibility = Visibility.Collapsed,
                    Opacity = 0,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var confirmPanel = new Border
                {
                    Width = 420,
                    MaxHeight = 480,
                    Background = SafeFindBrush("CardBackgroundBrush"),
                    BorderBrush = SafeFindBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(20),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
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
                    Foreground = SafeFindBrush("AccentBrush")
                };
                _confirmMessage = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = SafeFindBrush("ForegroundBrush"),
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var btnYes = new Button
                {
                    Content = "是",
                    Style = SafeFindStyle("RoundedButton"),
                    Width = 80,
                    Height = 32,
                    Margin = new Thickness(0, 0, 10, 0)
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
                    Width = 80,
                    Height = 32,
                    Background = SafeFindBrush("TextBoxBackgroundBrush"),
                    Foreground = SafeFindBrush("ForegroundBrush")
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载页面时出错: {ex.Message}\n\n{ex.StackTrace}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            // 保存窗口大小与位置
            _config.WindowWidth = (int)this.Width;
            _config.WindowHeight = (int)this.Height;
            _config.WindowLeft = this.Left;
            _config.WindowTop = this.Top;
            ConfigManager.Save(_config);

            // 原有后续代码不变
            _playTimer?.Stop();
            _healthCheckTimer?.Stop();
            _performanceTimer?.Stop();
            _downloadCts?.Cancel();
            IpcService.Stop();
        }

        // ========== 安全获取资源（备用） ==========
        private object SafeFindResource(object key)
        {
            try
            {
                return this.TryFindResource(key) ?? Application.Current.TryFindResource(key);
            }
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
                Width = 110,
                Height = 28,
            };

            _sortPopup = new Popup
            {
                PlacementTarget = _sortButton,
                Placement = PlacementMode.Bottom,
                StaysOpen = false
            };

            _sortListBox = new ListBox
            {
                ItemsSource = new[] { "默认排序", "最近游玩", "最常游玩" },
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                MinWidth = 110,
                ItemContainerStyle = SafeFindStyle("CustomListBoxItemStyle")
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

        // ========== IPC 事件 ==========
        private void OnStatsReceived(GameStatsData stats)
        {
            Dispatcher.Invoke(() =>
            {
                TxtFps.Text = stats.Fps.ToString();
                TxtScore.Text = stats.Score.ToString();
                TxtKills.Text = stats.Kills.ToString();

                string modeDisplay = stats.Mode;
                if (ModeNameMap.TryGetValue(stats.Mode, out string chineseMode))
                    modeDisplay = chineseMode;
                TxtMode.Text = modeDisplay;

                StatusLight.Fill = new SolidColorBrush(Colors.Green);
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
            // ========== 1. 检查是否已有游戏在运行 ==========
            if (_currentGameProcess != null && !_currentGameProcess.HasExited)
            {
                bool killOld = await ShowConfirmDialogAsync("已有游戏在运行，是否关闭旧进程？");
                if (killOld)
                {
                    try
                    {
                        // 取消事件绑定，避免自动清理干扰
                        _currentGameProcess.Exited -= OnGameProcessExited;
                        _currentGameProcess.Kill();

                        // 等待进程退出（最多3秒）
                        await Task.Run(() => _currentGameProcess.WaitForExit(3000));

                        // 如果仍未退出，强制关闭句柄
                        if (!_currentGameProcess.HasExited)
                        {
                            _currentGameProcess.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但不阻塞启动
                        Debug.WriteLine($"终止旧进程失败: {ex.Message}");
                    }
                    finally
                    {
                        // 释放进程资源
                        _currentGameProcess?.Dispose();
                        _currentGameProcess = null;

                        // 停止所有相关定时器和 IPC
                        _playTimer?.Stop();
                        _healthCheckTimer?.Stop();
                        _performanceTimer?.Stop();
                        IpcService.Stop();
                        IpcStatusBar.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    return; // 用户取消，不启动新游戏
                }
            }

            try
            {
                // ========== 2. 查找对应的游戏信息 ==========
                var gameInfo = _gameFiles.FirstOrDefault(g => g.FullPath == filePath && g.IsPython == isPython);
                if (gameInfo == null)
                {
                    MessageBox.Show(
                        "无法识别该游戏版本，请确认文件是否正确。",
                        "版本未知",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return; // 确保返回，不继续执行
                }

                // ========== 3. 版本兼容性检查 ==========
                if (!IsVersionCompatible(gameInfo.Version))
                {
                    await ShowCompatibilityDialogAsync(
                        "该版本不适配 IPC 通信，无法与启动器同步数据。\n\n" +
                        "请使用以下版本之一：\n" +
                        "• 2.2.0 ～ 2.4.0（Python版本）\n" +
                        "• 2.15.4 ～ 3.0.5（Python版本）\n" +
                        "• SnakeGame（C#版本）\n\n" +
                        "点击确定后仍将启动游戏，但部分功能可能无法使用。");
                    // 不再 return，继续启动
                }

                // ========== 4. 启动游戏进程 ==========
                _currentGameProcess = GameLauncher.StartProcess(filePath, isPython);
                if (_currentGameProcess == null)
                {
                    MessageBox.Show("游戏进程启动失败，请检查文件路径或权限。", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ========== 5. 记录启动信息 ==========
                _gameStartTime = DateTime.Now;
                _lastScore = _lastKills = 0;
                _fpsHistory.Clear();
                _memoryHistory.Clear();
                _logBuilder.Clear();

                GameStatsManager.RecordLaunch(filePath);

                // ========== 6. 显示 IPC 状态栏 ==========
                IpcStatusBar.Visibility = Visibility.Visible;
                TxtFps.Text = "--";
                TxtScore.Text = "0";
                TxtKills.Text = "0";
                TxtMode.Text = "--";
                TxtPlayTime.Text = "00:00:00";
                StatusLight.Fill = new SolidColorBrush(Colors.Gray);
                TxtStatus.Text = "等待连接...";

                // ========== 7. 启动 IPC 服务和定时器 ==========
                _ = IpcService.StartAsync();
                _playTimer.Start();
                _healthCheckTimer.Start();
                _performanceTimer.Start();

                // ========== 8. 异步读取输出流 ==========
                _ = Task.Run(() => ReadStream(_currentGameProcess.StandardOutput));
                _ = Task.Run(() => ReadStream(_currentGameProcess.StandardError));

                // ========== 9. 注册进程退出事件 ==========
                _currentGameProcess.EnableRaisingEvents = true;
                _currentGameProcess.Exited += OnGameProcessExited;

                // ========== 10. 成功通知 ==========
                ShowNotification("游戏启动成功");
            }
            catch (Exception ex)
            {
                // 异常时弹出错误并记录
                MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // 也可以调用 ShowDetailPanel 记录详细堆栈
                // ShowDetailPanel("启动失败", ex.Message + "\n\n" + ex.StackTrace);
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
            var slideIn = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 3 } };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            NotificationBar.BeginAnimation(OpacityProperty, fadeIn);
            NotificationTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                var slideOut = new DoubleAnimation { To = 300, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                NotificationTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                NotificationBar.BeginAnimation(OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        public void ShowDetailPanel(string title, string message)
        {
            DetailTitle.Text = title;
            DetailTextBox.Text = message;
            DetailOverlay.Visibility = Visibility.Visible;
            var overlayFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            DetailOverlay.BeginAnimation(OpacityProperty, overlayFadeIn);
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(350))
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
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
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
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };

            _compatibilityDialog.BeginAnimation(OpacityProperty, fadeIn);
            var transform = (_compatibilityDialog.Child as Grid)?.Children[0]?.RenderTransform as ScaleTransform;
            transform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            transform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            return _compatibilityTcs.Task;
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
                case "设置": index = 2; break;
                case "关于": index = 3; break;
            }
            SelectTab(index);
        }

        private void SelectTab(int index)
        {
            try
            {
                _downloadCts?.Cancel(); _downloadCts?.Dispose(); _downloadCts = null;
                CloseDownloadDetailPopup();

                if (BtnTabLaunch == null || BtnTabDownload == null || BtnTabSettings == null || BtnTabAbout == null || ContentGrid == null)
                {
                    MessageBox.Show("界面控件未正确初始化。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                BtnTabLaunch.Tag = index == 0 ? "Selected" : "Unselected";
                BtnTabDownload.Tag = index == 1 ? "Selected" : "Unselected";
                BtnTabSettings.Tag = index == 2 ? "Selected" : "Unselected";
                BtnTabAbout.Tag = index == 3 ? "Selected" : "Unselected";

                ContentGrid.Children.Clear();
                switch (index)
                {
                    case 0: ContentGrid.Children.Add(CreateLaunchPage()); break;
                    case 1: ContentGrid.Children.Add(CreateDownloadPage()); break;
                    case 2: ContentGrid.Children.Add(CreateSettingsPage()); break;
                    case 3: ContentGrid.Children.Add(CreateAboutPage()); break;
                }

                Button selectedBtn = null;
                switch (index)
                {
                    case 0: selectedBtn = BtnTabLaunch; break;
                    case 1: selectedBtn = BtnTabDownload; break;
                    case 2: selectedBtn = BtnTabSettings; break;
                    case 3: selectedBtn = BtnTabAbout; break;
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
                            new ThicknessAnimation(new Thickness(left, 0, 0, 0), TimeSpan.FromMilliseconds(250))
                            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
                        TabSlider.BeginAnimation(Border.WidthProperty,
                            new DoubleAnimation(width, TimeSpan.FromMilliseconds(250))
                            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
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

            var topBar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _currentPathLabel = new Label
            {
                Content = _config.Directories[_config.CurrentDirIndex],
                Foreground = SafeFindBrush("AccentBrush"),
                Cursor = Cursors.Hand,
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            };
            _currentPathLabel.MouseLeftButtonUp += (s, e) => { try { Clipboard.SetText(_currentPathLabel.Content.ToString()); } catch { } };

            var sortControl = CreateCustomSortControl();

            var btnRefresh = new Button { Content = "刷新", Style = SafeFindStyle("RoundedButton"), Width = 80, Height = 32 };
            btnRefresh.Click += async (s, e) => await ScanGamesAsync();
            var btnManual = new Button { Content = "手动添加", Style = SafeFindStyle("RoundedButton"), Width = 100, Height = 32 };
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
                    Background = SafeFindBrush("CardBackgroundBrush"),
                    BorderBrush = SafeFindBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Effect = new DropShadowEffect { ShadowDepth = 2, BlurRadius = 6, Opacity = 0.1 }
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = "最新 EXE 版本", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = SafeFindBrush("ForegroundBrush") });
                string ver = latestExe.Version.Major == 9999 ? "SnakeGame" : $"{latestExe.Version}";
                stack.Children.Add(new TextBlock { Text = ver, FontSize = 12, Foreground = SafeFindBrush("ForegroundBrush"), Margin = new Thickness(0, 4, 0, 8) });
                var launchBtn = new Button { Content = "启动", Style = SafeFindStyle("RoundedButton"), Width = 100, Height = 34 };
                launchBtn.Click += (s, ev) => LaunchGame(latestExe.FullPath, false);
                stack.Children.Add(launchBtn);
                card.Child = stack;
                latestCardPanel.Child = card;
            }
            else latestCardPanel.Child = new TextBlock { Text = "未找到任何 .exe 版本", Foreground = SafeFindBrush("ForegroundBrush"), TextAlignment = TextAlignment.Center };

            _cardPanel = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
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
            if (_gameFiles.Count == 0) { _cardPanel.Children.Add(new TextBlock { Text = "未找到任何游戏版本", Foreground = SafeFindBrush("ForegroundBrush"), TextAlignment = TextAlignment.Center }); return; }
            foreach (var g in _gameFiles)
            {
                string ver = g.Version.Major == 9999 ? "SnakeGame" : $"{g.Version}";
                var card = CreateGameCard(ver, g.DisplayName, g.FullPath, g.IsPython);
                _cardPanel.Children.Add(card);
            }
        }

        private Border CreateGameCard(string versionStr, string displayName, string fullPath, bool isPython)
        {
            var card = new Border { Style = SafeFindStyle("GameCardStyle"), Width = 196, Height = 150 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var versionBlock = new TextBlock { Text = versionStr, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = SafeFindBrush("ForegroundBrush"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            var nameBlock = new TextBlock { Text = displayName, FontSize = 10, Foreground = SafeFindBrush("ForegroundBrush"), TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 4) };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            var launchBtn = new Button { Content = "启动", Style = SafeFindStyle("RoundedButton"), Width = 70, Height = 28 };
            launchBtn.Click += (s, ev) => LaunchGame(fullPath, isPython);
            var detailBtn = new Button { Content = "详情", Style = SafeFindStyle("RoundedButton"), Width = 50, Height = 28, Margin = new Thickness(4, 0, 0, 0) };
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
            string info = $"版本号：{game?.Version}\n完整路径：{path}\n最后游玩：{(stats?.LastPlayed.ToString("g") ?? "从未")}\n启动次数：{stats?.LaunchCount ?? 0}\n总时长：{stats?.TotalPlayMinutes:F1} 分钟";
            ShowDetailPanel("游戏详情", info);
        }

        private async Task ScanGamesAsync()
        {
            var list = new List<GameFileInfo>();
            var dirs = _config.Directories;
            await Task.Run(() =>
            {
                var exeDirPattern = new Regex(@"^(\d+)\.(\d+)\.(\d+)$");
                var pyPattern = new Regex(@"贪吃蛇[（(](\d+)\.(\d+)\.(\d+)[）)]\.py$", RegexOptions.IgnoreCase);
                foreach (string baseDir in dirs)
                {
                    if (Directory.Exists(baseDir))
                    {
                        // 扫描目录格式的 exe 版本
                        foreach (var dir in Directory.GetDirectories(baseDir))
                        {
                            var match = exeDirPattern.Match(Path.GetFileName(dir));
                            if (match.Success)
                            {
                                string exePath = Path.Combine(dir, "dist", "贪吃蛇.exe");
                                if (File.Exists(exePath))
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

                        // 扫描根目录 SnakeGame.exe
                        string snakePath = Path.Combine(baseDir, "SnakeGame.exe");
                        if (File.Exists(snakePath))
                            list.Add(new GameFileInfo
                            {
                                Version = new Version(9999, 9999, 9999),
                                DisplayName = "SnakeGame.exe",
                                FullPath = snakePath,
                                IsPython = false
                            });

                        // 扫描所有 py 版本
                        foreach (var file in Directory.GetFiles(baseDir, "*.py", SearchOption.AllDirectories))
                        {
                            var match = pyPattern.Match(Path.GetFileName(file));
                            if (match.Success)
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
                    }
                }

                // ========== 新增：同一版本优先保留 exe，去重 ==========
                list = list
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

        // ==================== 下载页面 ====================

        /// <summary>
        /// 扫描所有可用的版本（先嵌入资源，再本地文件夹）
        /// </summary>
        private List<VersionInfo> ScanAvailableVersions()
        {
            var versions = new List<VersionInfo>();

            // ---- 1. 从嵌入资源中扫描 ----
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
    .Where(name => name.EndsWith(".zip") && name.Contains(".Resources.SnakeGame_"));

            foreach (var resName in resourceNames)
            {
                // 提取文件名（最后一部分）
                var fileName = resName.Split('.').Last(); // 例如 SnakeGame_1.1.0.zip 或 SnakeGame_1_1_0.zip
                                                          // 去掉前缀 "SnakeGame_" 和后缀 ".zip"
                var versionPart = fileName.Replace("SnakeGame_", "").Replace(".zip", "");
                // 将下划线替换为点（如果存在）
                var versionStr = versionPart.Replace("_", ".");
                if (!Version.TryParse(versionStr, out var version))
                    continue;

                // 获取资源大小
                using (var stream = assembly.GetManifestResourceStream(resName))
                {
                    if (stream == null) continue;
                    long size = stream.Length;
                    versions.Add(new VersionInfo
                    {
                        Version = versionStr,
                        DisplayName = $"贪吃蛇 ({versionStr})",
                        ReleaseNotes = "内置版本", // 可自定义
                        DownloadUrl = null,       // 无需 URL，使用嵌入资源
                        FileSize = size
                    });
                }
            }

            // ---- 2. 从本地 Versions 文件夹扫描（作为补充） ----
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

                    // 避免重复（如果嵌入资源已有同名版本，跳过）
                    if (versions.Any(v => v.Version == versionStr))
                        continue;

                    var fileInfo = new FileInfo(filePath);
                    versions.Add(new VersionInfo
                    {
                        Version = versionStr,
                        DisplayName = $"贪吃蛇 ({versionStr})",
                        ReleaseNotes = "本地版本",
                        DownloadUrl = filePath, // 使用本地路径作为来源标记
                        FileSize = fileInfo.Length
                    });
                }
            }

            // 按版本降序排序
            return versions.OrderByDescending(v => Version.Parse(v.Version)).ToList();
        }
        private UIElement CreateDownloadPage()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0) };
            var btnRefresh = new Button { Content = "刷新列表", Style = SafeFindStyle("RoundedButton"), Width = 100, Height = 32 };
            btnRefresh.Click += (s, e) => LoadVersionCards();
            topPanel.Children.Add(btnRefresh);

            _downloadListPanel = new StackPanel { Margin = new Thickness(10, 10, 10, 10) };
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

            // 清空并重新加载版本状态
            _versionStates.Clear();
            var versions = ScanAvailableVersions();
            foreach (var v in versions)
                _versionStates[v.Version] = new VersionDownloadState { Info = v };

            EnsurePopupCreated();
            LoadVersionCards();
            return mainGrid;
        }

        private void LoadVersionCards()
        {
            _downloadListPanel.Children.Clear();
            // 遍历 _versionStates 中的版本信息
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
                Height = 60,
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid { Margin = new Thickness(12, 0, 12, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var verText = new TextBlock
            {
                Text = $"v{info.Version}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = SafeFindBrush("AccentBrush")
            };
            var nameText = new TextBlock
            {
                Text = info.DisplayName ?? "",
                FontSize = 11,
                Foreground = SafeFindBrush("ForegroundBrush"),
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
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
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
                Width = 420,
                MaxHeight = 480,
                Background = SafeFindBrush("CardBackgroundBrush"),
                BorderBrush = SafeFindBrush("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            _popupScaleTransform = new ScaleTransform(0.8, 0.8);
            panel.RenderTransform = _popupScaleTransform;

            var panelGrid = new Grid();
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _popupTitle = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = SafeFindBrush("AccentBrush") };

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                FontSize = 16,
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
            _popupNotes = new TextBlock { FontSize = 13, Foreground = SafeFindBrush("ForegroundBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
            _popupStatus = new TextBlock { FontSize = 13, Foreground = SafeFindBrush("ForegroundBrush"), Margin = new Thickness(0, 10, 0, 10) };

            _popupDownloadBtn = new Button { Content = "下载", Style = SafeFindStyle("RoundedButton"), Width = 140, Height = 40, Margin = new Thickness(0, 10, 0, 0) };
            _popupDownloadBtn.Click += PopupDownloadBtn_Click;

            _popupProgressBar = new ProgressBar
            {
                Height = 20,
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed,
                Style = SafeFindStyle("CustomProgressBarStyle")
            };
            _popupCancelBtn = new Button { Content = "取消", Style = SafeFindStyle("RoundedButton"), Width = 80, Height = 32, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
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
            var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
            var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
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
                var anim = new DoubleAnimation(state.Progress, TimeSpan.FromMilliseconds(200));
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

            // 如果已有缓存，直接使用
            if (File.Exists(tempPath))
            {
                state.IsDownloading = false;
                state.IsDownloaded = true;
                state.DownloadedFilePath = tempPath;
                if (_currentDownloadState == state) RefreshPopupButtons();
                return;
            }

            // ---- 判断来源 ----
            bool success = false;

            // 1. 尝试从嵌入资源提取
            if (string.IsNullOrEmpty(state.Info.DownloadUrl) || state.Info.DownloadUrl.StartsWith("SnakeGame_"))
            {
                success = await ExtractEmbeddedResourceAsync(state.Info.Version, tempPath);
            }

            // 2. 如果是本地文件路径，直接复制
            if (!success && !string.IsNullOrEmpty(state.Info.DownloadUrl) && File.Exists(state.Info.DownloadUrl))
            {
                File.Copy(state.Info.DownloadUrl, tempPath, true);
                success = true;
            }

            // 3. 否则尝试网络下载（如果 DownloadUrl 是 http 开头）
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
            // 从 _downloadManager 下载
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
            var stack = new StackPanel { Margin = new Thickness(20) };

            var dirGroup = new GroupBox { Header = "游戏目录", Margin = new Thickness(0, 0, 0, 16) };
            var dirListBox = new ListBox
            {
                Height = 120,
                Margin = new Thickness(0, 8, 0, 8),
                ItemContainerStyle = SafeFindStyle("CustomListBoxItemStyle")
            };
            foreach (var d in _config.Directories) dirListBox.Items.Add(d);
            dirListBox.SelectedIndex = _config.CurrentDirIndex;
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var btnAdd = new Button { Content = "添加目录", Style = SafeFindStyle("RoundedButton"), Margin = new Thickness(0, 0, 8, 0) };
            btnAdd.Click += (s, e) => AddDirectory(dirListBox);
            var btnRemove = new Button { Content = "删除选中", Style = SafeFindStyle("RoundedButton"), Margin = new Thickness(0, 0, 8, 0) };
            btnRemove.Click += (s, e) => RemoveDirectory(dirListBox);
            var btnApply = new Button { Content = "应用选中", Style = SafeFindStyle("RoundedButton") };
            btnApply.Click += (s, e) => ApplyDirectory(dirListBox);
            btnPanel.Children.Add(btnAdd); btnPanel.Children.Add(btnRemove); btnPanel.Children.Add(btnApply);
            dirGroup.Content = new StackPanel { Children = { dirListBox, btnPanel } };

            var dlGroup = new GroupBox { Header = "下载路径", Margin = new Thickness(0, 0, 0, 16) };
            var dlPanel = new StackPanel();
            var dlPathText = new TextBlock
            {
                Text = _config.DownloadPath,
                Foreground = SafeFindBrush("AccentBrush"),
                Margin = new Thickness(0, 0, 0, 8),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var btnSetDlPath = new Button { Content = "选择文件夹", Style = SafeFindStyle("RoundedButton"), Width = 120, Height = 32 };
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

            var themeGroup = new GroupBox { Header = "主题模式", Margin = new Thickness(0, 0, 0, 16) };
            var themePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnLight = new Button { Content = "浅色", Style = SafeFindStyle("RoundedButton"), Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            btnLight.Click += (s, e) => SetTheme("light");
            var btnDark = new Button { Content = "深色", Style = SafeFindStyle("RoundedButton"), Width = 80 };
            btnDark.Click += (s, e) => SetTheme("dark");
            themePanel.Children.Add(btnLight); themePanel.Children.Add(btnDark);
            themeGroup.Content = themePanel;

            var colorGroup = new GroupBox { Header = "主题色", Margin = new Thickness(0, 0, 0, 16) };
            var colorPanel = new WrapPanel();
            var presetColors = new Dictionary<string, string> { { "#0078d4", "蓝色" }, { "#ff4343", "红色" }, { "#00cc6a", "绿色" }, { "#886ce4", "紫色" }, { "#ff763b", "橙色" }, { "#00b7c3", "青色" } };
            foreach (var kv in presetColors)
            {
                var border = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(6), Background = (SolidColorBrush)new BrushConverter().ConvertFromString(kv.Key), Margin = new Thickness(4, 0, 4, 0) };
                if (kv.Key == _config.AccentColor) border.BorderBrush = Brushes.Black;
                border.MouseLeftButtonUp += (s, e) => { _config.AccentColor = kv.Key; ApplyTheme(); };
                colorPanel.Children.Add(border);
            }
            colorGroup.Content = colorPanel;

            var applyCheck = new CheckBox { Content = "强调色应用到文字", IsChecked = _config.ApplyText, Margin = new Thickness(0, 0, 0, 16) };
            applyCheck.Checked += (s, e) => { _config.ApplyText = true; ApplyTheme(); };
            applyCheck.Unchecked += (s, e) => { _config.ApplyText = false; ApplyTheme(); };

            var configGroup = new GroupBox { Header = "配置管理", Margin = new Thickness(0, 0, 0, 16) };
            var configPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnExport = new Button { Content = "导出配置", Style = SafeFindStyle("RoundedButton"), Width = 100, Margin = new Thickness(0, 0, 8, 0) };
            btnExport.Click += (s, e) => { var sfd = new SaveFileDialog { Filter = "JSON文件|*.json", FileName = "launcher_config.json" }; if (sfd.ShowDialog() == true) { File.Copy(ConfigManager.ConfigPath, sfd.FileName, true); ShowNotification("配置导出成功"); } };
            var btnImport = new Button { Content = "导入配置", Style = SafeFindStyle("RoundedButton"), Width = 100, Margin = new Thickness(0, 0, 8, 0) };
            btnImport.Click += (s, e) => { var ofd = new OpenFileDialog { Filter = "JSON文件|*.json" }; if (ofd.ShowDialog() == true) { File.Copy(ofd.FileName, ConfigManager.ConfigPath, true); _config = ConfigManager.Load(); ConfigManager.SetSyncFolder(_config.SyncFolder); ApplyTheme(); LoadDirectories(); SelectTab(0); ShowNotification("配置导入成功"); } };
            var btnSync = new Button { Content = "同步文件夹", Style = SafeFindStyle("RoundedButton"), Width = 130 };
            btnSync.Click += (s, e) => { using (var dialog = new System.Windows.Forms.FolderBrowserDialog()) { if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) { _config.SyncFolder = dialog.SelectedPath; ConfigManager.SetSyncFolder(dialog.SelectedPath); ConfigManager.Save(_config); ShowNotification("同步文件夹已设置"); } } };
            configPanel.Children.Add(btnExport); configPanel.Children.Add(btnImport); configPanel.Children.Add(btnSync);
            configGroup.Content = configPanel;

            stack.Children.Add(dirGroup);
            stack.Children.Add(dlGroup);
            stack.Children.Add(themeGroup);
            stack.Children.Add(colorGroup);
            stack.Children.Add(applyCheck);
            stack.Children.Add(configGroup);
            return stack;
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

        private void RemoveDirectory(ListBox listBox)
        {
            if (listBox.SelectedIndex >= 0 && _config.Directories.Count > 1)
            {
                int idx = listBox.SelectedIndex;
                _config.Directories.RemoveAt(idx);
                listBox.Items.RemoveAt(idx);
                if (_config.CurrentDirIndex >= _config.Directories.Count) _config.CurrentDirIndex = _config.Directories.Count - 1;
                ConfigManager.Save(_config);
                _ = Task.Run(() => ScanGamesAsync());
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
                _ = Task.Run(() => ScanGamesAsync());
            }
        }

        private void SetTheme(string mode) { _config.ThemeMode = mode; ApplyTheme(); }
        private void ApplyTheme() { ThemeManager.ApplyTheme(_config); ConfigManager.Save(_config); }

        private static BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            using (var ms = new MemoryStream()) { bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png); ms.Position = 0; var bi = new BitmapImage(); bi.BeginInit(); bi.StreamSource = ms; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze(); return bi; }
        }

        private UIElement CreateAboutPage()
        {
            var grid = new Grid();
            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock { Text = "贪吃蛇启动器 v3.0.0", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = SafeFindBrush("ForegroundBrush") });
            stack.Children.Add(new TextBlock { Text = "开发：没冇啊", Foreground = SafeFindBrush("ForegroundBrush") });
            stack.Children.Add(new TextBlock { Text = "PCL2 风格界面", Foreground = SafeFindBrush("ForegroundBrush") });
            try
            {
                var bmp = Properties.Resources.MMA1;
                var img = new Image { Source = BitmapToImageSource(bmp), Width = 200, Height = 200, Margin = new Thickness(10, 0, 10, 0) };
                stack.Children.Add(img);
            }
            catch { }
            grid.Children.Add(stack);
            return grid;
        }

        private bool IsVersionCompatible(Version version)
        {
            // SnakeGame 特殊版本（9999.9999.9999）
            if (version.Major == 9999 && version.Minor == 9999 && version.Build == 9999)
                return true;

            // 区间 [2.15.4, 3.0.5]
            var v1 = new Version(2, 15, 4);
            var v2 = new Version(3, 0, 5);
            if (version >= v1 && version <= v2)
                return true;

            // 区间 [2.2.0, 2.4.0]
            var v3 = new Version(2, 2, 0);
            var v4 = new Version(2, 4, 0);
            if (version >= v3 && version <= v4)
                return true;

            return false;
        }

        private void LoadDirectories()
        {
            if (_config.Directories == null || _config.Directories.Count == 0) _config.Directories = new List<string> { Directory.GetCurrentDirectory() };
            if (_config.CurrentDirIndex >= _config.Directories.Count) _config.CurrentDirIndex = 0;
        }

        // ========== 窗口控制 ==========
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}