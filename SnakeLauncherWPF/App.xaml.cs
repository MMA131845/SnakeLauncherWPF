using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SnakeLauncherWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ===== 全局异常捕获 =====
            this.DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show($"UI线程异常: {ex.Exception.Message}\n\n{ex.Exception.StackTrace}", "致命错误");
                ex.Handled = true;
                Shutdown();
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                MessageBox.Show($"后台任务异常: {ex.Exception.Message}", "错误");
                ex.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                string msg = (ex.ExceptionObject as Exception)?.Message ?? "未知错误";
                MessageBox.Show($"未处理异常: {msg}", "崩溃");
                Environment.Exit(1);
            };

            // ===== 加载配置并应用主题（先于启动画面） =====
            var config = ConfigManager.Load();
            ThemeManager.ApplyTheme(config);

            // ===== 显示启动画面（此时资源已更新） =====
            var splash = new SplashWindow();
            splash.Show();

            // ===== 延迟3秒后创建主窗口 =====
            _ = DelayedMainWindow(splash);
        }

        private async Task DelayedMainWindow(SplashWindow splash)
        {
            await Task.Delay(3000); // 展示启动画面3秒

            // 主窗口构造（包含配置、扫描等耗时操作）
            var mainWindow = new MainWindow();

            // 主窗口加载完成后关闭启动画面（淡出动画）
            mainWindow.Loaded += (s, args) =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                fadeOut.Completed += (o, ev) => splash.Close();
                splash.BeginAnimation(Window.OpacityProperty, fadeOut);
            };

            mainWindow.Show();
        }
    }
}