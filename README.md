贪吃蛇启动器 v4.0.0
<div align="center">
https://img.shields.io/badge/version-4.0.0-brightgreen?style=for-the-badge
https://img.shields.io/badge/.NET-Framework_4.8-512BD4?style=for-the-badge&logo=dotnet
https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows
https://img.shields.io/badge/license-MIT-blue?style=for-the-badge
https://img.shields.io/badge/platform-Windows-0078D4?style=for-the-badge&logo=windows

一款为「自由贪吃蛇」系列打造的现代化启动器

Liquid Glass Edition | 液态玻璃风格 | IPC 实时通信 | 多版本管理 | 内置下载

功能特性 | 快速开始 | 界面说明 | 项目结构 | 技术亮点

</div>
项目简介
贪吃蛇启动器 v4.0.0 是一款使用 WPF (.NET Framework 4.8) 开发的游戏启动器，专为「自由贪吃蛇」系列游戏打造。它负责多版本扫描、游戏启动、进程管理、下载安装，并通过 Named Pipe（命名管道） 与游戏进程建立实时通信，将游戏内的 FPS、得分、击杀数等状态同步到启动器底部的状态栏。

v4.0.0 采用 Liquid Glass（液态玻璃） 视觉风格，内置动态彩色光斑背景、自绘无边框窗口、丝滑的动画过渡，并为不同主题（浅色 / 深色）准备了两套完整的玻璃资源。

核心定位：

多版本管理：自动扫描目录中的 .exe 与 .py 游戏版本

进程守护：启动 / 强杀 / 无响应检测 / 崩溃捕获

实时状态同步：基于 Named Pipe 的 IPC 通道

内置下载：从 HTTP 索引或内嵌资源中安装任意版本

主题系统：浅色 / 深色 + 6 种预设主题色，实时生效

功能特性
四大页面
页面	功能概述
启动	扫描并列出本地所有游戏版本，一键启动，右键重命名 / 打开目录
下载	展示可用版本列表，支持内嵌资源 / 本地 zip / HTTP 远程下载
设置	游戏目录、下载路径、主题模式、主题色、配置导入导出与同步
关于	版本信息、开发者、版权说明
核心亮点
<table> <tr> <td width="50%">
多版本扫描

自动扫描配置目录下的所有版本文件夹

识别 X.Y.Z/dist/贪吃蛇.exe 与 贪吃蛇（X.Y.Z）.py 两种布局

特殊识别 SnakeGame.exe（C# 版本）

同名版本去重，优先保留 EXE

</td> <td width="50%">
实时 IPC 通信

Named Pipe 管道名为 SnakeGameFPSPipe

33ms 级状态推送

状态栏实时显示 FPS、得分、击杀、模式、时长

连接状态灯（灰 / 绿）

</td> </tr> <tr> <td width="50%">
Liquid Glass UI

无边框自绘窗口，WindowChrome 实现原生拖拽与缩放

三个背景彩色光斑，随机时长与半径缓慢漂移

阴影 / 圆角 / 渐变玻璃边框

主题切换实时刷新全部 DynamicResource

</td> <td width="50%">
下载与安装

版本列表自动排序（降序）

点击卡片弹出详情面板（玻璃风格）

下载进度实时显示，可随时取消

解压时进行路径穿越安全检查

</td> </tr> <tr> <td width="50%">
进程管理

启动前检测是否有旧进程运行

2 秒定时健康检查，无响应弹窗提示

每秒采集内存占用与 FPS 历史

崩溃时保存 stdout / stderr 日志并弹窗展示

</td> <td width="50%">
配置系统

JSON 格式，可自定义存储位置（同步文件夹）

支持导出 / 导入配置

窗口尺寸与位置自动记忆

首次启动自动创建默认配置

</td> </tr> </table>
快速开始
环境要求
操作系统：Windows 10 / 11

.NET Framework：4.8 或更高版本

IDE：Visual Studio 2019 / 2022（推荐 2022）

NuGet 包：System.Text.Json 等（见 packages.config）

编译运行
bash
# 克隆仓库
git clone https://github.com/your-username/SnakeLauncher.git
cd SnakeLauncher

# 用 Visual Studio 打开 SnakeLauncherWPF.csproj

# 或使用 MSBuild 命令行
msbuild SnakeLauncherWPF.csproj /p:Configuration=Release
编译完成后运行 bin/Release/SnakeLauncherWPF.exe 即可。

首次使用
启动器会自动检测当前目录并扫描游戏版本

若无游戏版本，可通过「设置 - 游戏目录」添加目录

也可通过「下载」页安装内置版本

点击卡片上的「启动」按钮即可开始游戏

界面说明
顶部标题栏
应用标题：贪吃蛇启动器

中央 Tab 切换：启动 / 下载 / 设置 / 关于

Tab 底部滑动指示条（280ms 缓动动画）

右上角窗口按钮：最小化 / 关闭

双击标题栏可最大化 / 还原

启动页
顶部一行显示当前游戏目录（点击可复制路径）

排序下拉框：默认排序 / 最近游玩 / 最常游玩

「最新 EXE 版本」大卡片，突出显示最新版本

下方网格卡片，每个版本一张卡

卡片右键菜单：重命名、打开所在文件夹

下载页
顶部「刷新列表」按钮

版本卡片列表，展示版本号、显示名、文件大小

点击卡片弹出详情面板

详情面板包含：版本号、大小、更新日志、状态、操作按钮

设置页
游戏目录：列表管理，支持添加 / 删除 / 应用

下载路径：自定义下载 zip 存储位置

主题模式：浅色 / 深色切换（切换后自动重启）

主题色：6 种预设色板 + 当前选中高亮

强调色应用到文字：勾选项

配置管理：导出 / 导入 / 同步文件夹

底部状态栏
启动游戏后自动显示，包含：

FPS

得分

击杀

模式

时长

连接状态灯（灰=未连接 / 绿=已连接）

通知与弹窗
通知横条：右下角滑入，3 秒后滑出（弹性缓动）

详情弹窗：居中缩放动画，用于展示游戏崩溃日志

确认弹窗：自绘是 / 否双按钮

信息弹窗：自绘单按钮

兼容性弹窗：自绘单按钮，提示版本不兼容

操作指南
键盘与鼠标
操作	功能
鼠标左键拖动标题栏	移动窗口
双击标题栏	最大化 / 还原窗口
点击 Tab	切换页面
点击版本卡片	打开详情面板
右键卡片	重命名 / 打开目录
Esc	关闭当前弹窗（部分）
版本兼容性
启动器通过 Named Pipe 与游戏通信，只支持特定版本范围：

游戏类型	版本范围
Python	2.2.0 ~ 2.4.0
Python	2.15.4 ~ 3.0.5
C#	SnakeGame.exe（任意版本）
不在上述范围的版本仍可启动，但启动器会提示「版本不兼容」，部分功能可能无法使用。

技术亮点
1. Liquid Glass 无边框窗口
使用 WindowChrome 实现无边框窗口，同时保留原生拖拽、缩放、最大化行为：

xml
<shell:WindowChrome.WindowChrome>
    <shell:WindowChrome GlassFrameThickness="-1"
                        ResizeBorderThickness="5"
                        CaptionHeight="0"
                        CornerRadius="0"
                        NonClientFrameEdges="None"/>
</shell:WindowChrome.WindowChrome>
CaptionHeight="0" 表示标题栏区域不参与拖动，由代码在 TitleBar_MouseLeftButtonDown 中调用 DragMove() 手动实现，双击切换最大化。

2. 动态背景光斑
主窗口内嵌三个模糊椭圆，通过 TranslateTransform 做无限随机漂移：

csharp
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
每个光斑独立运行，速度与范围各不相同，形成有机的漂浮感。动画完成后自动递归，形成无限循环。

3. Named Pipe IPC
启动器作为管道服务端，游戏作为客户端：

csharp
_server = new NamedPipeServerStream(
    "SnakeGameFPSPipe",
    PipeDirection.In,
    maxNumberOfServerInstances: 1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous
);
await _server.WaitForConnectionAsync();
数据格式为单行文本：

text
FPS:60,SCORE:120,KILLS:5,MODE:classic
游戏每 200ms 推送一次，启动器解析后通过 StatsReceived 事件更新 UI。

4. 自绘对话框系统
为了统一风格并避免原生 MessageBox 样式突兀，本项目实现了三套自绘对话框：

对话框	用途
Compatibility	单按钮提示，用于版本不兼容
Confirm	是 / 否双按钮，用于进程替换确认
Info	单按钮提示，用于主题切换提醒
它们都使用相同模式：全屏半透明遮罩 + 居中面板 + 缩放动画，通过 TaskCompletionSource<bool> 提供 await 支持：

csharp
private Task<bool> ShowConfirmDialogAsync(string message)
{
    _confirmTcs = new TaskCompletionSource<bool>();
    _confirmMessage.Text = message;
    _confirmDialog.Visibility = Visibility.Visible;
    // ...播放动画
    return _confirmTcs.Task;
}
调用方可以这样使用：

csharp
bool killOld = await ShowConfirmDialogAsync("已有游戏在运行，是否关闭旧进程？");
5. 主题系统
ThemeManager.ApplyTheme() 一次性替换 Application.Current.Resources 中的所有 DynamicResource：

csharp
public static void ApplyTheme(ConfigData config)
{
    if (config.ThemeMode == "light") SetLightTheme(resources);
    else SetDarkTheme(resources);

    var accent = (Color)ColorConverter.ConvertFromString(config.AccentColor);
    resources["AccentBrush"] = new SolidColorBrush(accent);
    resources["AccentLightBrush"] = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
    // ...
}
由于所有控件都使用 {DynamicResource ...} 引用画刷，切换主题后 UI 会自动刷新，无需手动遍历。

6. 兼容性范围检查
csharp
private bool IsVersionCompatible(Version version)
{
    if (version.Major == 9999 && version.Minor == 9999 && version.Build == 9999)
        return true; // SnakeGame.exe

    var range1 = new[] { new Version(2, 15, 4), new Version(3, 0, 5) };
    var range2 = new[] { new Version(2, 2, 0),  new Version(2, 4, 0) };

    return (version >= range1[0] && version <= range1[1])
        || (version >= range2[0] && version <= range2[1]);
}
检查失败不阻止启动，仅弹窗提示功能受限，保证向后兼容。

7. 安全解压
安装 zip 时进行路径穿越检查，防止 ../ 逃逸：

csharp
string destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
if (!destPath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("非法路径");
8. 目录自动扫描
正则匹配版本文件夹名与 Python 文件名：

csharp
var exeDirPattern = new Regex(@"^(\d+)\.(\d+)\.(\d+)$");
var pyPattern = new Regex(@"贪吃蛇[（(](\d+)\.(\d+)\.(\d+)[）)]\.py$", RegexOptions.IgnoreCase);
扫描后按版本号分组去重，优先保留 EXE，避免同时显示 EXE 与同名 Python 版本。

更新日志
v4.0.0（当前版本）
重大变更

采用 Liquid Glass（液态玻璃）视觉风格

全局无边框窗口，支持原生缩放与最大化

三个动态背景光斑，随机漂移效果

自绘对话框体系（兼容性 / 确认 / 信息）

主题系统重构，浅色与深色各自维护完整资源

6 种预设主题色，实时切换无需重启

主题色切换时自动弹窗提醒并重启启动器

底部状态栏新增「连接状态灯」

功能增强

下载页支持内嵌资源 / 本地 zip / HTTP 远程三种来源

版本卡片点击弹出玻璃风格详情面板

下载进度可实时取消

启动页新增「最新 EXE 版本」突出卡片

卡片右键菜单支持重命名与打开目录

设置页支持配置导出 / 导入 / 同步文件夹

修复

修复启动画面与主窗口主题不一致问题

修复窗口位置与尺寸未保存

修复下载中断后残留文件未清理

修复扫描到重复版本时 EXE 与 Python 同时显示

修复游戏进程结束后统计未写入

修复主题切换后部分控件颜色未刷新

贡献指南
欢迎提交 Issue 和 Pull Request。

bash
# Fork 后克隆
git clone https://github.com/your-username/SnakeLauncher.git

# 创建功能分支
git checkout -b feature/amazing-feature

# 提交修改
git commit -m "feat: 添加新功能"

# 推送分支
git push origin feature/amazing-feature

# 在 GitHub 上打开 Pull Request
代码规范
使用 PascalCase 命名公开成员，_camelCase 命名私有字段

所有 UI 资源通过 SafeFindBrush / SafeFindStyle 获取，避免空引用

新增主题画刷时同步更新 SetLightTheme 与 SetDarkTheme

新增自绘对话框时使用 TaskCompletionSource<bool> 模式

开源协议
本项目基于 MIT License 开源，详见 LICENSE 文件。

text
MIT License

Copyright (c) 2026 MEIMAOA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction...
致谢
Newtonsoft.Json / System.Text.Json：JSON 序列化

.NET Community：优秀的开发平台

开发：没冇啊、ChatGPT、Gemini、Codex、DeepSeek

所有为本项目提供反馈与建议的玩家

<div align="center">
如果这个项目对你有帮助，欢迎点一个 Star

Made with love by MEIMAOA

https://visitor-badge.laobi.icu/badge?page_id=your-username.SnakeLauncher

</div>
