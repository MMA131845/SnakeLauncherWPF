# 贪吃蛇启动器 v4.1.0

<div align="center">

![Version](https://img.shields.io/badge/version-4.1.0-brightgreen?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-Framework_4.8-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/license-MIT-blue?style=for-the-badge)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=for-the-badge&logo=windows)

**一款为「自由贪吃蛇」系列打造的现代化启动器**

Liquid Glass 设计 | IPC 实时通信 | 多版本管理 | 反作弊系统 | 自动更新 | 多语言

[项目简介](#项目简介) | [功能特性](#功能特性) | [快速开始](#快速开始) | [界面说明](#界面说明) | [项目结构](#项目结构) | [技术亮点](#技术亮点)

</div>

---

## 项目简介

**贪吃蛇启动器 v4.1.0** 是一款使用 **WPF (.NET Framework 4.8)** 开发的游戏启动器，统一管理「自由贪吃蛇」系列的 **C# WPF 版** 与 **Python 版** 两大分支。它负责多版本扫描、游戏启动、进程守护、依赖自动安装、下载更新，并通过 **Named Pipe（命名管道）** 与游戏建立实时通信，将 FPS、得分、击杀数等状态同步到启动器底部状态栏。

v4.1.0 在 v4.0.0 的基础上，新增了 **反作弊系统**、**Python 依赖自动安装**、**启动器自动更新**、**多语言支持**（简体中文 / English / Deutsch / Français / Русский）以及 **数据统计页面**。

核心定位：

- **双分支统一管理**：同时支持 C# WPF 版和 Python 版游戏
- **多版本扫描**：自动识别多种命名规范与目录布局
- **进程守护**：启动检测、无响应监控、崩溃捕获
- **依赖自动化**：Python 环境检测、pygame / pywin32 自动安装
- **反作弊系统**：HMAC 签名 + 时间戳 + 会话数据合理性检测
- **自动更新**：基于 GitHub Releases 的自更新链路

---

## 功能特性

### 五大页面

| 页面 | 功能概述 |
|:----:|----------|
| 启动 | 扫描并列出本地所有游戏版本，一键启动，右键重命名 / 打开目录 |
| 下载 | 从 GitHub Releases 拉取版本列表，支持两个仓库切换下载 |
| 数据 | 累计游玩时长、启动次数、历史最高分、累计击杀、会话记录 |
| 设置 | 游戏目录、下载路径、主题、主题色、语言、配置导入导出、更新检查 |
| 关于 | 版本信息、开发者、版权说明 |

### 核心亮点

<table>
<tr>
<td width="50%">

**双分支版本管理**

- 同时识别 C# 与 Python 版游戏
- 支持 5 种目录布局自动扫描
- 从父目录或文件名解析版本号
- 同名版本去重，优先保留 EXE
- 支持手动添加

</td>
<td width="50%">

**实时 IPC 通信**

- Named Pipe 管道名 `SnakeGameFPSPipe`
- 500ms 级状态推送
- 状态栏实时显示 FPS、得分、击杀、模式、时长
- 连接状态灯（灰 / 绿）
- 断开自动重连

</td>
</tr>
<tr>
<td width="50%">

**反作弊系统**

- 游戏 EXE 文件 SHA256 白名单校验
- IPC 消息 HMAC-SHA256 签名验证
- 时间戳防重放（30 秒容忍窗口）
- 分数 / 击杀增速合理性检测
- 检测到作弊弹通知，不中断游戏

</td>
<td width="50%">

**Python 依赖管理**

- 自动检测 Python 3.8+ 环境
- 检测 pygame / pywin32 是否安装
- 一键自动 pip 安装
- 失败时提供手动安装指引
- 未装 Python 时引导跳转官网

</td>
</tr>
<tr>
<td width="50%">

**Liquid Glass UI**

- 无边框窗口，WindowChrome 原生拖拽缩放
- 三个背景彩色光斑缓慢漂移
- 阴影 / 圆角 / 渐变玻璃边框
- 主题切换实时刷新全部 DynamicResource
- 强调色自动计算前景色（黑 / 白）

</td>
<td width="50%">

**启动器自更新**

- 基于 GitHub Releases 检查新版本
- SHA256 完整性校验
- 独立 Updater 进程完成文件替换
- 失败自动回滚
- 备份目录延迟清理

</td>
</tr>
<tr>
<td width="50%">

**多语言支持**

- 简体中文 / English / Deutsch
- Français / Русский
- 切换即时生效，无需重启
- 语言文件内嵌，零外部依赖

</td>
<td width="50%">

**数据统计**

- 累计游玩时长 / 启动次数
- 历史最高分 / 累计击杀
- 按游戏分组的最佳成绩
- 最近 15 局详细记录
- 快速继续上次游戏

</td>
</tr>
</table>

---

## 快速开始

### 环境要求

- **操作系统**：Windows 10 / 11
- **.NET Framework**：4.8 或更高版本
- **IDE**：Visual Studio 2019 / 2022（推荐 2022）
- **可选**：Python 3.8+（仅当运行 Python 版游戏时需要）

### 编译运行

```bash
# 克隆仓库
git clone https://github.com/MMA131845/SnakeLauncherWPF.git
cd SnakeLauncherWPF

# 用 Visual Studio 打开 SnakeLauncherWPF.sln

# 或使用 MSBuild 命令行
msbuild SnakeLauncherWPF.csproj /p:Configuration=Release
```

编译完成后运行 `bin/Release/SnakeLauncherWPF.exe` 即可。

### 首次使用

1. 启动器会自动检测当前目录并扫描游戏版本
2. 若无游戏版本，可通过「设置 - 游戏目录」添加目录
3. 也可通过「下载」页从 GitHub 安装任意版本
4. 点击卡片上的「启动」按钮即可开始游戏
5. 若运行 Python 版游戏，启动器会自动检测并安装依赖

---

## 界面说明

### 顶部标题栏

- 应用标题：贪吃蛇启动器
- 中央 Tab 切换：启动 / 下载 / 数据 / 设置 / 关于
- Tab 底部滑动指示条（280ms 缓动动画）
- 右上角窗口按钮：最小化 / 关闭
- 双击标题栏可最大化 / 还原

### 启动页

- 顶部一行显示当前游戏目录（点击可复制路径）
- 排序下拉框：默认排序 / 最近游玩 / 最常游玩
- 「最新 EXE 版本」大卡片，突出显示最新版本
- 下方网格卡片，每个版本一张卡
- 卡片右键菜单：重命名、打开所在文件夹
- 卡片底部按钮：启动、详情

### 详情弹窗

点击「详情」按钮后弹出，包含：

- 版本号 / 显示名称 / 完整路径
- 最后游玩时间 / 启动次数 / 累计时长
- 底部操作按钮：打开所在文件夹 / 删除此游戏
- 删除时自动判断父目录是否有其他游戏，智能选择删除方式
- 安全检查：拒绝删除磁盘根目录和启动器自身目录

### 下载页

- 顶部仓库切换下拉框
  - SnakeGameWpf（C# 版）
  - 贪吃蛇 Python 版
- 「刷新列表」「打开仓库」按钮
- 版本卡片列表，展示 Tag、发布时间、更新日志
- 展开后显示资源列表，每个资源可单独下载
- 下载前弹出路径确认对话框：确认 / 更改路径 / 取消
- 下载完成后自动校验 ZIP 结构、解压、加入扫描列表

### 数据页

- 总览卡片：累计游玩 / 启动次数 / 历史最高分 / 累计击杀
- 继续游戏卡片：显示最近一局的游戏名与时间
- 各游戏最佳成绩：按游戏分组，展示最高分、击杀、总局数
- 最近对局列表：时间 / 游戏 / 模式 / 时长 / 分数 / 击杀

### 设置页

- 游戏目录：列表管理，支持添加 / 删除 / 应用
- 下载路径：自定义下载 zip 存储位置
- 主题模式：浅色 / 深色切换
- 语言：简中 / 英 / 德 / 法 / 俄
- 主题色：6 种预设色板 + 当前选中高亮
- 强调色应用到文字：勾选项
- 配置管理：导出 / 导入 / 同步文件夹
- 启动器更新：当前版本、自动检查开关、清除跳过记录、检查更新、打开发布页

### 底部状态栏

启动游戏后自动显示，包含：

- FPS
- 得分
- 击杀
- 模式（自动翻译为中文模式名）
- 时长
- 连接状态灯（灰 / 绿）

### 通知与弹窗

- **通知横条**：右下角滑入，3 秒后滑出（弹性缓动）
- **详情弹窗**：居中缩放动画，用于展示游戏崩溃日志
- **确认弹窗**：自绘是 / 否双按钮
- **信息弹窗**：自绘单按钮
- **兼容性弹窗**：自绘单按钮，提示版本不兼容
- **下载路径弹窗**：自绘三按钮，确认 / 更改 / 取消
- **自动更新弹窗**：自绘三按钮，稍后 / 跳过此版本 / 立即更新

---

## 操作指南

### 键盘与鼠标

| 操作 | 功能 |
|:----:|------|
| 鼠标左键拖动标题栏 | 移动窗口 |
| 双击标题栏 | 最大化 / 还原窗口 |
| 点击 Tab | 切换页面 |
| 点击版本卡片 | 打开详情面板 |
| 右键卡片 | 重命名 / 打开目录 |
| Esc | 关闭当前弹窗（部分） |

### 版本兼容性

启动器通过 Named Pipe 与游戏通信，只支持特定版本范围：

| 游戏类型 | 版本范围 |
|:--------:|:--------:|
| Python | 2.2.0 ~ 2.4.0 |
| Python | 2.15.4 ~ 3.0.5 |
| Python | v5.x 系列（新增 IPC 签名） |
| C# | SnakeGame.exe（任意版本） |

不在上述范围的版本仍可启动，但启动器会提示「版本不兼容」，部分功能可能无法使用。

### 反作弊说明

启动器在 `game_hashes.json` 中维护一份游戏 EXE 的白名单哈希：

- 启动前校验 EXE 文件 SHA256，不匹配则阻止启动
- 若你是自己修改过的版本，可同步更新 `game_hashes.json`
- Python 版游戏暂不做 EXE 校验

IPC 消息通过 HMAC-SHA256 签名 + 30 秒时间戳窗口防重放：

- 签名密钥硬编码在游戏端与启动器端，必须完全一致
- 检测到非法消息会弹通知，但不会中断游戏
- 会话数据合理性：分数每秒增长超过 800、击杀每秒超过 8 会告警

---

## 项目结构

```text
SnakeLauncherWPF/
│
├── 启动器主程序
│   ├── App.xaml(.cs)                 全局资源 + 启动流程 + 全局异常捕获
│   ├── MainWindow.xaml(.cs)          无边框窗口 + 五大页面 + 进程管理
│   ├── SplashWindow.xaml(.cs)        启动画面（3 秒展示后淡出）
│   ├── Lang.cs                       多语言资源（简中 / 英 / 德 / 法 / 俄）
│   ├── IpcService.cs                 Named Pipe 服务器 + 反作弊集成
│   ├── AntiCheat.cs                  HMAC 校验 + 会话合理性追踪
│   ├── PythonDependencyManager.cs    Python 环境与依赖检测 / 安装
│   ├── GitHubReleaseService.cs       GitHub Releases 多仓库拉取
│   ├── UpdateService.cs              启动器自更新服务
│   └── ConfigData / ThemeManager / GameStatsManager（内嵌于 MainWindow.xaml.cs）
│
├── 更新程序（独立进程）
│   ├── SnakeLauncherWPF.Updater.csproj
│   ├── App.xaml(.cs)                 主题跟随主程序
│   ├── MainWindow.xaml(.cs)          更新进度 UI
│   ├── UpdateEngine.cs               解压 / 备份 / 覆盖 / 回滚引擎
│   └── Program.cs                    （保留的传统入口）
│
├── 项目配置
│   ├── SnakeLauncherWPF.csproj       Visual Studio 工程文件
│   ├── packages.config               NuGet 依赖清单
│   └── Properties/                   程序集信息与资源
│
├── 内嵌资源
│   ├── Resources/SnakeGame_*.zip     内嵌版本包
│   └── Versions/SnakeGame_*.zip      本地版本包
│
└── 运行时生成
    ├── launcher_config.json          启动器配置（JSON）
    ├── game_stats.json               游戏统计（启动次数 / 时长）
    ├── game_history.json             会话历史（最近 50 条）
    ├── game_hashes.json              反作弊哈希白名单
    └── _backup_*/                    更新时的旧文件备份
```

---

## 技术亮点

### 1. Liquid Glass 无边框窗口

使用 `WindowChrome` 实现无边框窗口，同时保留原生拖拽、缩放、最大化行为：

```xml
<shell:WindowChrome.WindowChrome>
    <shell:WindowChrome GlassFrameThickness="-1"
                        ResizeBorderThickness="5"
                        CaptionHeight="0"
                        CornerRadius="0"
                        NonClientFrameEdges="None"/>
</shell:WindowChrome.WindowChrome>
```

`CaptionHeight="0"` 表示标题栏区域不参与拖动，由代码在 `TitleBar_MouseLeftButtonDown` 中调用 `DragMove()` 手动实现，双击切换最大化。

### 2. 动态背景光斑

主窗口内嵌三个模糊椭圆，通过 `TranslateTransform` 做无限随机漂移：

```csharp
private void AnimateBlobLoop(TranslateTransform transform, double minSec, double maxSec, double range)
{
    double targetX = (_blobRandom.NextDouble() - 0.5) * 2 * range;
    double targetY = (_blobRandom.NextDouble() - 0.5) * 2 * range;
    double seconds = minSec + _blobRandom.NextDouble() * (maxSec - minSec);

    var animX = new DoubleAnimation(transform.X, targetX, TimeSpan.FromSeconds(seconds))
    {
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
    };
    animX.Completed += (s, e) => AnimateBlobLoop(transform, minSec, maxSec, range);
    transform.BeginAnimation(TranslateTransform.XProperty, animX);
}
```

每个光斑独立运行，速度与范围各不相同，动画完成后自动递归，形成无限循环。

### 3. Named Pipe IPC 与反作弊

启动器作为管道服务端，游戏作为客户端：

```csharp
_server = new NamedPipeServerStream(
    "SnakeGameFPSPipe",
    PipeDirection.In,
    maxNumberOfServerInstances: 1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous
);
await _server.WaitForConnectionAsync();
```

数据格式为单行文本，带时间戳与 HMAC 签名：

```text
FPS:60,SCORE:120,KILLS:5,MODE:classic,TS:1735780000,SIG:abc123...
```

启动器接收后进行三重校验：

```csharp
if (!AntiCheat.VerifyIpcLine(line, out string cleanPayload, out string reason))
{
    CheatDetected?.Invoke(reason);
    continue;
}

var stats = ParseLine(cleanPayload);
SessionTracker.Feed(ts, stats.Score, stats.Kills);
if (SessionTracker.IsSuspicious)
    CheatDetected?.Invoke(SessionTracker.SuspiciousReason);
```

### 4. HMAC-SHA256 消息签名

`AntiCheat` 使用硬编码 32 字节密钥对 payload 签名：

```csharp
private static string ComputeHmac(string payload)
{
    using (var h = new HMACSHA256(RootKey))
    {
        byte[] mac = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var sb = new StringBuilder(mac.Length * 2);
        foreach (var b in mac) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
```

校验时同时检查时间戳，超过 30 秒容忍窗口即视为重放攻击：

```csharp
long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
if (Math.Abs(now - ts) > TimestampToleranceSeconds)
{
    errorReason = "时间戳过期（可能重放）";
    return false;
}
```

### 5. 会话数据合理性检测

追踪每帧的分数与击杀变化率，异常增速即告警：

```csharp
public void Feed(long ts, int score, int kills)
{
    if (_lastTs > 0 && ts > _lastTs)
    {
        double dt = ts - _lastTs;
        if (dt > 0)
        {
            double scoreRate = (score - _lastScore) / dt;
            double killRate = (kills - _lastKills) / dt;

            if (scoreRate > MaxScoreRatePerSecond)
            {
                IsSuspicious = true;
                SuspiciousReason = $"分数增速异常 ({scoreRate:F0}/秒)";
            }
            // ...
        }
    }
}
```

阈值设为分数 800/秒、击杀 8/秒，正常游戏不可能触及。

### 6. Python 依赖自动管理

启动 Python 版游戏前自动检测依赖：

```csharp
public static async Task<bool> IsPackageInstalledAsync(string packageName, string pythonExe = "python")
{
    var psi = new ProcessStartInfo
    {
        FileName = pythonExe,
        Arguments = $"-m pip show {packageName}",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    using (var proc = Process.Start(psi))
    {
        string output = await proc.StandardOutput.ReadToEndAsync();
        await Task.Run(() => proc.WaitForExit(10000));
        return proc.ExitCode == 0
            && output.IndexOf($"Name: {packageName}", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
```

安装时先升级 pip 再安装目标包，超时 120 秒。失败后返回详细错误信息，启动器弹窗展示 `pip install xxx` 手动命令。

### 7. 启动器自更新

`UpdateService` 从 GitHub Releases API 拉取最新版本：

```csharp
string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
```

下载后校验 SHA256（可选，从 Release body 的 `<!-- SHA256: xxx -->` 注释中提取）：

```csharp
public static bool VerifySha256(string filePath, string expectedSha256)
{
    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var sha = SHA256.Create())
    {
        byte[] hashBytes = sha.ComputeHash(stream);
        string computed = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return string.Equals(computed, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }
}
```

校验通过后把 Updater 复制到临时目录再启动，避免 Updater 锁定自身。Updater 完成文件替换后自动重启主程序。

### 8. 独立更新引擎

`UpdateEngine` 支持完整的更新流程，任何一步失败都能回滚：

```csharp
public enum UpdateStage
{
    WaitingForMainExit,
    CleaningOldBackups,
    Extracting,
    BackingUp,
    CopyingFiles,
    Launching,
    Completed
}
```

关键设计：

- 等待主进程完全退出，最多 60 秒
- 清理上次更新残留的 `_backup_*` 和 `*.old_*`
- 备份所有将被覆盖的旧文件
- 逐文件复制并上报进度
- 任何一步异常都尝试 `TryRollback`
- 更新成功后不删备份，由新主程序启动时清理

### 9. 多仓库 GitHub 拉取

`GitHubReleaseService` 支持同时管理多个仓库：

```csharp
public static readonly List<GitHubRepoConfig> Repos = new List<GitHubRepoConfig>
{
    new GitHubRepoConfig
    {
        Owner = "MMA131845",
        Repo = "SnakeGameWpf",
        DisplayName = "SnakeGameWpf（C#）",
        IsPythonRepo = false,
        GamePackagePattern = new Regex(@"^SnakeGameWpf_(\d+(?:\.\d+){1,3})\.zip$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    },
    new GitHubRepoConfig
    {
        Owner = "MMA131845",
        Repo = "Snake-Game-Python-Edition-",
        DisplayName = "贪吃蛇 Python 版",
        IsPythonRepo = true,
        GamePackagePattern = new Regex(@"^SnakeGamePython_(\d+(?:\.\d+){1,3})\.zip$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    }
};
```

每个 Asset 的名称用正则解析出版本号，`IsGamePackage` 标记是否符合规范。拉取后按版本号降序、发布时间降序排列。

### 10. 自绘对话框系统

统一风格的对话框体系，避免原生 MessageBox 突兀：

| 对话框 | 用途 |
|:------:|------|
| Compatibility | 单按钮提示，用于版本不兼容、文件完整性失败 |
| Confirm | 是 / 否双按钮，用于进程替换、删除确认 |
| Info | 单按钮提示，用于主题切换提醒 |
| DownloadPath | 三按钮，用于下载路径确认 |
| AutoUpdate | 三按钮，用于新版本提示 |

它们都使用相同模式：全屏半透明遮罩 + 居中面板 + 缩放动画，通过 `TaskCompletionSource<bool>` 提供 `await` 支持。

### 11. 多语言系统

`Lang.cs` 采用内嵌字典，零外部依赖：

```csharp
public static string T(string key)
{
    if (string.IsNullOrEmpty(key)) return "";
    if (_dict != null && _dict.TryGetValue(key, out var v) && v != null) return v;
    return key;
}
```

支持带参数格式化：

```csharp
public static string T(string key, params object[] args)
{
    var s = T(key);
    if (args == null || args.Length == 0) return s;
    try { return string.Format(s, args); }
    catch { return s; }
}
```

切换语言后触发 `LanguageChanged` 事件，主窗口重建顶部栏、按钮、当前标签页。全部 5 种语言覆盖 200 多个键。

### 12. 智能版本扫描

`ScanGamesAsync` 支持 5 种目录布局：

```csharp
// A. <baseDir>/<版本目录>/dist/贪吃蛇.exe
// B. <baseDir>/SnakeGame.exe
// C. <baseDir>/**.py（递归）
// D. <baseDir>/**.SnakeGameWpf.exe（递归）
// E. <baseDir>/<版本目录>/SnakeGame.exe
```

版本号来源优先级：

1. 父目录名匹配 `\d+\.\d+\.\d+`
2. EXE 文件版本信息
3. 文件名中的版本号正则

扫描后按路径去重、按版本号去重（优先保留非 Python），保证同一版本只显示一张卡片。

### 13. 安全删除

删除游戏时进行多重安全检查：

```csharp
string parentDir = Path.GetDirectoryName(gamePath);
string root = Path.GetPathRoot(gamePath);

if (string.IsNullOrEmpty(parentDir)
    || string.Equals(parentDir, root, StringComparison.OrdinalIgnoreCase))
{
    ShowNotification(Lang.T("Detail.RootRefused"));
    return;
}

string launcherDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
if (string.Equals(parentDir.TrimEnd('\\', '/'), launcherDir, StringComparison.OrdinalIgnoreCase))
{
    ShowNotification(Lang.T("Detail.LauncherDirRefused"));
    return;
}
```

同时判断父目录是否还有其他游戏文件，若无则删除整个目录，否则只删除游戏文件。

### 14. 配置原子写入

`ConfigManager.Save` 采用临时文件 + 原子替换策略，避免写入中断导致配置文件损坏：

```csharp
string tempPath = ConfigPath + ".tmp";
using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
{
    sw.Write(json);
    sw.Flush();
    fs.Flush(true);
}

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
```

### 15. ZIP 结构严格校验

下载的游戏包必须满足命名规范和内容规范：

```csharp
// Python 包：根目录有任意 .py 文件
// C# 包：必须同时包含以下四项
//   - SnakeGame.exe / SnakeGameWpf.exe
//   - SnakeGame.dll / SnakeGameWpf.dll
//   - Newtonsoft.Json.dll
//   - SnakeGame.runtimeconfig.json
```

缺一即判定为非法包，避免用户下载到残缺版本导致游戏无法启动。

---

## 更新日志

### v4.1.0（当前版本）

**新增功能**

- 反作弊系统：EXE 哈希白名单 + IPC HMAC 签名 + 时间戳防重放 + 会话数据合理性
- Python 依赖自动检测与安装（pygame / pywin32）
- 启动器自更新（GitHub Releases + SHA256 校验 + 独立 Updater）
- 多语言支持（简中 / English / Deutsch / Français / Русский）
- 数据统计页面（时长 / 启动次数 / 最高分 / 击杀 / 会话记录）
- 详情弹窗新增删除按钮，含安全检查
- 下载页支持多仓库切换（C# 版 / Python 版）
- 下载前弹出路径确认对话框
- ZIP 结构严格校验
- 配置原子写入，避免文件损坏

**修复**

- 修复配置文件被占用导致写入失败
- 修复部分游戏无法被扫描到
- 修复主题切换后部分控件未刷新
- 修复下载中断残留临时文件
- 修复游戏进程结束后统计未写入

### v4.0.0

**重大变更**

- 采用 Liquid Glass（液态玻璃）视觉风格
- 全局无边框窗口，支持原生缩放与最大化
- 三个动态背景光斑，随机漂移效果
- 自绘对话框体系（兼容性 / 确认 / 信息）
- 主题系统重构，浅色与深色各自维护完整资源
- 6 种预设主题色，实时切换无需重启
- 底部状态栏新增「连接状态灯」

**功能增强**

- 下载页支持内嵌资源 / 本地 zip / HTTP 远程三种来源
- 版本卡片点击弹出玻璃风格详情面板
- 启动页新增「最新 EXE 版本」突出卡片
- 卡片右键菜单支持重命名与打开目录

---

## 贡献指南

欢迎提交 Issue 和 Pull Request。

```bash
# Fork 后克隆
git clone https://github.com/MMA131845/SnakeLauncherWPF.git

# 创建功能分支
git checkout -b feature/amazing-feature

# 提交修改
git commit -m "feat: 添加新功能"

# 推送分支
git push origin feature/amazing-feature

# 在 GitHub 上打开 Pull Request
```

### 代码规范

- 使用 `PascalCase` 命名公开成员，`_camelCase` 命名私有字段
- 所有 UI 资源通过 `SafeFindBrush` / `SafeFindStyle` 获取，避免空引用
- 新增主题画刷时同步更新 `SetLightTheme` 与 `SetDarkTheme`
- 新增自绘对话框时使用 `TaskCompletionSource<bool>` 模式
- 新增字符串时同步更新 `Lang.cs` 中的 5 种语言字典
- 涉及文件 IO 时优先使用 `FileShare.ReadWrite` 避免占用

---

## 开源协议

本项目基于 **MIT License** 开源，详见 [LICENSE](LICENSE) 文件。

```text
MIT License

Copyright (c) 2026 MEIMAOA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction...
```

---

## 致谢

- 开发：没冇啊
- 代码：ChatGPT、Gemini、Codex、DeepSeek、没冇啊
- 美术设计：没冇啊
- QA：没冇啊
- 特别感谢：所有支持本游戏的玩家
- [.NET Community](https://dotnet.microsoft.com/)：优秀的开发平台

---

<div align="center">

**如果这个项目对你有帮助，欢迎点一个 Star**

Made with love by MEIMAOA

![Visitors](https://visitor-badge.laobi.icu/badge?page_id=MMA131845.SnakeLauncherWPF)

</div>
