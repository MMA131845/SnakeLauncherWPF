# 贪吃蛇启动器 v4.1.0

<div align="center">

![Version](https://img.shields.io/badge/version-4.1.0-brightgreen?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-Framework_4.8-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/license-MIT-blue?style=for-the-badge)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=for-the-badge&logo=windows)

# 贪吃蛇启动器 (SnakeLauncherWPF)

**一款为「自由贪吃蛇」系列打造的现代化启动器**

Liquid Glass 设计 | IPC 实时通信 | 多版本管理 | 反作弊系统 | 自动更新 | 多语言

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
|------|----------|
| **启动** | 扫描并列出本地所有游戏版本，一键启动，右键重命名 / 打开目录 |
| **下载** | 从 GitHub Releases 拉取版本列表，支持两个仓库切换下载 |
| **数据** | 累计游玩时长、启动次数、历史最高分、累计击杀、会话记录 |
| **设置** | 游戏目录、下载路径、主题、主题色、语言、配置导入导出、更新检查 |
| **关于** | 版本信息、开发者、版权说明 |

### 核心亮点

#### 双分支版本管理

- 同时识别 C# 与 Python 版游戏
- 支持 5 种目录布局自动扫描
- 从父目录或文件名解析版本号
- 同名版本去重，优先保留 EXE
- 支持手动添加

#### 实时 IPC 通信

- Named Pipe 管道名 `SnakeGameFPSPipe`
- 500ms 级状态推送
- 状态栏实时显示 FPS、得分、击杀、模式、时长
- 连接状态灯（灰 / 绿）
- 断开自动重连

#### 反作弊系统

- 游戏 EXE 文件 SHA256 白名单校验
- IPC 消息 HMAC-SHA256 签名验证
- 时间戳防重放（30 秒容忍窗口）
- 分数 / 击杀增速合理性检测
- 检测到作弊弹通知，不中断游戏

#### Python 依赖管理

- 自动检测 Python 3.8+ 环境
- 检测 pygame / pywin32 是否安装
- 一键自动 pip 安装
- 失败时提供手动安装指引
- 未装 Python 时引导跳转官网

#### Liquid Glass UI

- 无边框窗口，WindowChrome 原生拖拽缩放
- 三个背景彩色光斑缓慢漂移
- 阴影 / 圆角 / 渐变玻璃边框
- 主题切换实时刷新全部 DynamicResource
- 强调色自动计算前景色（黑 / 白）

#### 启动器自更新

- 基于 GitHub Releases 检查新版本
- SHA256 完整性校验
- 独立 Updater 进程完成文件替换
- 失败自动回滚
- 备份目录延迟清理

#### 多语言支持

- 简体中文 / English / Deutsch / Français / Русский
- 切换即时生效，无需重启
- 语言文件内嵌，零外部依赖

#### 数据统计

- 累计游玩时长 / 启动次数
- 历史最高分 / 累计击杀
- 按游戏分组的最佳成绩
- 最近 15 局详细记录
- 快速继续上次游戏

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
- 最近对局记录：最多显示 15 条

### 设置页

- 游戏目录：添加 / 删除 / 应用选中目录
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
|------|------|
| 鼠标左键拖动标题栏 | 移动窗口 |
| 双击标题栏 | 最大化 / 还原窗口 |
| 点击 Tab | 切换页面 |
| 点击版本卡片 | 打开详情面板 |
| 右键卡片 | 重命名 / 打开目录 |
| Esc | 关闭当前弹窗（部分） |

### 版本兼容性

启动器通过 Named Pipe 与游戏通信，只支持特定版本范围：

| 游戏类型 | 版本范围 |
|----------|----------|
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

```
SnakeLauncherWPF/
│
├── 启动器主程序
│   ├── App.xaml(.cs)                        全局资源 + 启动流程 + 全局异常捕获
│   ├── MainWindow.xaml(.cs)                 无边框窗口 + 五大页面 + 进程管理
│   ├── SplashWindow.xaml(.cs)               启动画面（3 秒展示后淡出）
│   ├── Lang.cs                              多语言资源（简中 / 英 / 德 / 法 / 俄）
│   ├── IpcService.cs                        Named Pipe 服务器 + 反作弊集成
│   ├── AntiCheat.cs                         HMAC 校验 + 会话合理性追踪
│   ├── PythonDependencyManager.cs           Python 环境与依赖检测 / 安装
│   ├── GitHubReleaseService.cs              GitHub Releases 多仓库拉取
│   ├── UpdateService.cs                     启动器自更新服务
│   └── ConfigData / ThemeManager / GameStatsManager（内嵌于 MainWindow.xaml.cs）
│
├── 更新程序（独立进程）
│   ├── SnakeLauncherWPF.Updater.csproj
│   ├── App.xaml(.cs)                        主题跟随主程序
│   ├── MainWindow.xaml(.cs)                 更新进度 UI
│   ├── UpdateEngine.cs                      解压 / 备份 / 覆盖 / 回滚引擎
│   └── Program.cs                           （保留的传统入口）
│
├── 项目配置
│   ├── SnakeLauncherWPF.csproj              Visual Studio 工程文件
│   ├── packages.config                      NuGet 包引用
│   └── App.config                           运行时配置
│
└── 资源文件
    └── Resources/                           内嵌游戏包 ZIP 资源
```

---

## 技术亮点

### 1. Liquid Glass 无边框窗口

主窗口采用 `WindowStyle="None"` + `AllowsTransparency="True"`，配合 `WindowChrome` 实现原生拖拽缩放。背景由三个彩色椭圆光斑组成，每个光斑独立执行随机漂移动画（6~15 秒周期，SineEase 缓动），通过 `TranslateTransform` 持续改变位置。

### 2. 主题系统与强调色

`ThemeManager` 在运行时动态替换 `Application.Current.Resources` 中的所有画刷。强调色会自动计算亮度并选择黑 / 白前景色：

```csharp
double luminance = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255.0;
resources["AccentForegroundBrush"] = new SolidColorBrush(
    luminance > 0.6 ? Colors.Black : Colors.White);
```

### 3. Named Pipe IPC 通信

`IpcService` 在后台线程启动 `NamedPipeServerStream`（管道名 `SnakeGameFPSPipe`），持续读取游戏推送的状态行，解析后通过 `Dispatcher.Invoke` 更新 UI。每次启动会话重置 `SessionTracker`，支持自动重试解决管道占用问题。

### 4. HMAC-SHA256 消息签名

`AntiCheat` 使用硬编码 32 字节密钥对 payload 签名，校验时同时检查时间戳，超过 30 秒容忍窗口即视为重放攻击。

### 5. 会话数据合理性检测

追踪每帧的分数与击杀变化率，异常增速即告警。阈值设为分数 800/秒、击杀 8/秒，正常游戏不可能触及。

### 6. Python 依赖自动管理

启动 Python 版游戏前自动检测依赖，使用 `python -m pip show {packageName}` 检查包是否已安装。安装时先升级 pip 再安装目标包，超时 120 秒。

### 7. 启动器自更新

`UpdateService` 从 GitHub Releases API 拉取最新版本，下载后校验 SHA256（从 Release body 的 `<!-- SHA256: ... -->` 注释中提取）。校验通过后把 Updater 复制到临时目录再启动，避免 Updater 锁定自身。

### 8. 独立更新引擎

`UpdateEngine` 支持完整的更新流程，任何一步失败都能回滚。更新阶段包括：等待主进程退出、清理旧备份、解压、备份、复制文件、启动新版本、完成。

### 9. 多语言内嵌字典

采用内嵌字典，零外部依赖。切换语言后触发 `LanguageChanged` 事件，主窗口重建顶部栏、按钮、当前标签页。全部 5 种语言覆盖 200 多个键。

### 10. 智能版本扫描

`ScanGamesAsync` 支持 5 种目录布局，版本号来源优先级为：父目录名匹配 → EXE 文件版本信息 → 文件名中的版本号正则。扫描后按路径去重、按版本号去重（优先保留非 Python）。

### 11. 安全删除

删除游戏时进行多重安全检查，拒绝删除磁盘根目录和启动器自身目录，同时判断父目录是否还有其他游戏文件。

### 12. 配置原子写入

`ConfigManager.Save` 采用临时文件 + 原子替换策略，避免写入中断导致配置文件损坏。

### 13. ZIP 结构严格校验

下载的游戏包必须满足命名规范和内容规范。C# 包必须同时包含 EXE、主 DLL、Newtonsoft.Json.dll、runtimeconfig.json，缺一即判定为非法包。

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

---

## 配套游戏项目

本启动器为「自由贪吃蛇」系列游戏提供统一管理。配套游戏 **SnakeGameWpf v7.0.0** 是一款从 WinForms 全面迁移到 WPF 的现代化贪吃蛇游戏，拥有五种游戏模式、局域网联机、主题系统和成就体系。

| 项目 | 仓库地址 | 技术栈 |
|------|----------|--------|
| 启动器 | [SnakeLauncherWPF](https://github.com/MMA131845/SnakeLauncherWPF) | WPF (.NET Framework 4.8) |
| 游戏 | [SnakeGameWpf](https://github.com/MMA131845/SnakeGameWpf) | WPF (.NET 10) |

游戏支持经典模式、淘汰之王、占领模式、极限模式和搜打撤五种模式，通过 Named Pipe 向启动器上报 FPS、得分、击杀等状态数据。启动器的 IPC 状态栏、数据统计页面和反作弊系统均依赖游戏端的配合。

---

## 致谢

- 开发：没冇啊
- 代码：ChatGPT、Gemini、Codex、DeepSeek、没冇啊
- 美术设计：没冇啊
- QA：没冇啊
- 特别感谢：所有支持本游戏的玩家
- [.NET Community](https://dotnet.microsoft.com/)：优秀的开发平台

---

**如果这个项目对你有帮助，欢迎点一个 Star**

Made with love by MEIMAOA

## 开源协议

本项目基于 MIT License 开源，详见 [LICENSE](https://github.com/MMA131845/SnakeLauncherWPF/blob/main/LICENSE)。

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
