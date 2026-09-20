using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SnakeLauncherWPF
{
    /// <summary>
    /// Python 依赖检测与自动安装
    /// </summary>
    public static class PythonDependencyManager
    {
        /// <summary>Python 游戏所需依赖包</summary>
        public static readonly string[] RequiredPackages =
        {
            "pygame",
            "pywin32"   // 仅 v5.x IPC 功能需要，但统一安装无害
        };

        /// <summary>
        /// 检测依赖是否已安装
        /// </summary>
        /// <param name="packageName">包名，如 "pygame"</param>
        /// <param name="pythonExe">python.exe 路径，默认 "python"</param>
        /// <returns>已安装返回 true</returns>
        public static async Task<bool> IsPackageInstalledAsync(string packageName, string pythonExe = "python")
        {
            try
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

                    // pip show 成功输出中会有 "Name: xxx" / "Version: xxx"
                    return proc.ExitCode == 0
                        && output.IndexOf($"Name: {packageName}", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检测所有依赖是否已安装
        /// </summary>
        /// <returns>缺失的包名列表</returns>
        public static async Task<System.Collections.Generic.List<string>> GetMissingPackagesAsync(
            string pythonExe = "python")
        {
            var missing = new System.Collections.Generic.List<string>();

            foreach (var pkg in RequiredPackages)
            {
                bool installed = await IsPackageInstalledAsync(pkg, pythonExe);
                if (!installed) missing.Add(pkg);
            }

            return missing;
        }

        /// <summary>
        /// 安装指定包
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <param name="progress">进度回调（文本）</param>
        /// <param name="pythonExe">python.exe 路径</param>
        /// <returns>安装成功返回 true</returns>
        public static async Task<bool> InstallPackageAsync(
            string packageName,
            Action<string> progress = null,
            string pythonExe = "python")
        {
            try
            {
                // 升级 pip（可选，提升安装成功率）
                progress?.Invoke("正在升级 pip...");
                try
                {
                    var upgradePsi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = "-m pip install --upgrade pip",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var proc = Process.Start(upgradePsi))
                    {
                        await Task.Run(() => proc.WaitForExit(30000));
                    }
                }
                catch { /* 升级失败不影响后续 */ }

                // 安装目标包
                progress?.Invoke($"正在安装 {packageName}...");

                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"-m pip install {packageName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    string error = await proc.StandardError.ReadToEndAsync();
                    await Task.Run(() => proc.WaitForExit(120000));

                    if (proc.ExitCode == 0)
                    {
                        progress?.Invoke($"{packageName} 安装成功");
                        return true;
                    }
                    else
                    {
                        progress?.Invoke($"{packageName} 安装失败: {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Invoke($"安装 {packageName} 出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查 python 是否可用
        /// </summary>
        public static async Task<bool> IsPythonAvailableAsync(string pythonExe = "python")
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    await Task.Run(() => proc.WaitForExit(5000));
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}