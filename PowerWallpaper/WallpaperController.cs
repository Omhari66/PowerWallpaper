using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Management;
using System.Collections.Generic;

namespace PowerWallpaper
{
    public enum LivelyStatus
    {
        Unknown,
        NotRunning,
        Starting,
        Running,
        Stopping,
        Error
    }

    public enum WallpaperStatus
    {
        Unknown,
        LiveActive,
        DarkActive,
        Transitioning,
        Error
    }

    public static class WallpaperController
    {
        public static LivelyStatus CurrentLivelyStatus { get; private set; } = LivelyStatus.Unknown;
        public static WallpaperStatus CurrentWallpaperStatus { get; private set; } = WallpaperStatus.Unknown;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        private static string _livelyExecutablePath = null;
        public static string LivelyExecutablePath 
        { 
            get 
            {
                if (_livelyExecutablePath == null)
                {
                    _livelyExecutablePath = FindLivelyExecutable();
                }
                return _livelyExecutablePath;
            }
        }

        private static string FindLivelyExecutable()
        {
            // Try Store Appx
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"(Get-AppxPackage 12030rocksdanister.LivelyWallpaper).InstallLocation\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                
                if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
                {
                    string path = Path.Combine(output, "Build", "Lively.exe");
                    if (File.Exists(path)) return path;
                }
            }
            catch { }
            
            // Fallback to legacy/desktop installer
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", "Lively Wallpaper", "Lively.exe");
        }

        public static bool SetWindowsStaticWallpaper(string path)
        {
            CurrentWallpaperStatus = WallpaperStatus.Transitioning;
            
            if (!File.Exists(path))
            {
                string absPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
                if (!File.Exists(absPath))
                {
                    Logger.Log($"Static wallpaper not found: {path} (Resolved: {absPath})");
                    CurrentWallpaperStatus = WallpaperStatus.Error;
                    return false;
                }
                path = absPath;
            }

            Logger.Log("Setting Windows static wallpaper...");
            int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            
            if (result != 0)
            {
                CurrentWallpaperStatus = WallpaperStatus.DarkActive;
                return true;
            }
            
            CurrentWallpaperStatus = WallpaperStatus.Error;
            return false;
        }

        public static void UpdateLivelyStatus()
        {
            if (CurrentLivelyStatus == LivelyStatus.Starting || CurrentLivelyStatus == LivelyStatus.Stopping) return; // Prevent overwriting transition states
            CurrentLivelyStatus = IsLivelyRunning() ? LivelyStatus.Running : LivelyStatus.NotRunning;
        }

        public static bool IsLivelyRunning()
        {
            var p = Process.GetProcessesByName("Lively").FirstOrDefault();
            return p != null && !p.HasExited;
        }

        public static void StartLively(string wallpaperPath)
        {
            CurrentWallpaperStatus = WallpaperStatus.Transitioning;
            
            if (!File.Exists(LivelyExecutablePath))
            {
                Logger.Log($"Error: Lively not found at {LivelyExecutablePath}");
                return;
            }

            if (!File.Exists(wallpaperPath))
            {
                // Resolve relative to application directory
                string absPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, wallpaperPath));
                if (!File.Exists(absPath))
                {
                    Logger.Log($"Live wallpaper not found: {wallpaperPath} (Resolved: {absPath})");
                    // We can still start Lively, but won't set wp
                }
                else
                {
                    wallpaperPath = absPath;
                }
            }

            if (!IsLivelyRunning())
            {
                CurrentLivelyStatus = LivelyStatus.Starting;
                Logger.Log("Lively is not running. Starting Lively...");
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = LivelyExecutablePath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to start Lively: {ex.Message}");
                    CurrentLivelyStatus = LivelyStatus.Error;
                    CurrentWallpaperStatus = WallpaperStatus.Error;
                    return;
                }
            }

            // Wait for Lively to be ready before sending command
            WaitForLivelyReady();

            if (File.Exists(wallpaperPath))
            {
                Logger.Log($"Loading live wallpaper: {wallpaperPath}");
                try
                {
                    var psiCmd = new ProcessStartInfo
                    {
                        FileName = LivelyExecutablePath,
                        Arguments = $"setwp --file \"{wallpaperPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psiCmd);
                    CurrentWallpaperStatus = WallpaperStatus.LiveActive;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to send wallpaper command: {ex.Message}");
                    CurrentWallpaperStatus = WallpaperStatus.Error;
                }
            }
            else
            {
                // Just started Lively, but no wp file to set
                CurrentWallpaperStatus = WallpaperStatus.Error;
            }
            CurrentLivelyStatus = LivelyStatus.Running;
        }

        private static void WaitForLivelyReady()
        {
            // Wait up to 10 seconds for Lively to stabilize
            for (int i = 0; i < 20; i++)
            {
                var processes = GetLivelyOwnedProcesses();
                // If it spawned its UI or Core process, it's getting ready
                if (processes.Count > 1) 
                {
                    Thread.Sleep(1000); // Give it one more second after spawning children
                    return;
                }
                Thread.Sleep(500);
            }
            Logger.Log("Warning: Lively startup wait timeout.");
        }

        public static void KillLivelySafely()
        {
            CurrentLivelyStatus = LivelyStatus.Stopping;
            Logger.Log("Attempting to close Lively completely...");

            // First, ask it to shutdown gracefully via CLI if it supports it
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = LivelyExecutablePath,
                    Arguments = "--shutdown true",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch { }

            // Wait a bit for graceful exit
            Thread.Sleep(2000);

            var livelyProcesses = GetLivelyOwnedProcesses();
            if (livelyProcesses.Count == 0)
            {
                Logger.Log("Lively exited gracefully.");
                return;
            }

            Logger.Log($"Force terminating {livelyProcesses.Count} remaining Lively-owned processes...");
            foreach (var p in livelyProcesses)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        Logger.Log($"Killing {p.ProcessName} (PID: {p.Id})");
                        p.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to kill {p.ProcessName}: {ex.Message}");
                }
            }
            
            CurrentLivelyStatus = LivelyStatus.NotRunning;
        }

        private static List<Process> GetLivelyOwnedProcesses()
        {
            var livelyProcesses = new List<Process>();
            var allLocalProcesses = Process.GetProcesses();
            
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath FROM Win32_Process"))
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        var path = item["ExecutablePath"]?.ToString();
                        var pidObj = item["ProcessId"];
                        
                        if (path != null && pidObj != null)
                        {
                            // Strict string match: Only processes installed in the Lively app directory
                            string installDir = Path.GetDirectoryName(Path.GetDirectoryName(LivelyExecutablePath));
                            if (path.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                            {
                                int pid = Convert.ToInt32(pidObj);
                                var proc = allLocalProcesses.FirstOrDefault(p => p.Id == pid);
                                if (proc != null)
                                {
                                    livelyProcesses.Add(proc);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"WMI Query failed: {ex.Message}");
                // Fallback: Extremely conservative kill, ONLY Lively.exe, never mpv
                var fallback = Process.GetProcessesByName("Lively").ToList();
                livelyProcesses.AddRange(fallback);
            }

            return livelyProcesses;
        }
    }
}
