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
            CurrentLivelyStatus = LivelyStatus.Starting;

            // Resolve path before jumping to background thread
            string resolvedPath = wallpaperPath;
            if (!File.Exists(resolvedPath))
            {
                string absPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, wallpaperPath));
                if (File.Exists(absPath)) resolvedPath = absPath;
            }

            // Run everything on a background thread so the UI/event thread is NEVER blocked
            System.Threading.Tasks.Task.Run(() => StartLivelyBackground(resolvedPath));
        }

        private static void StartLivelyBackground(string wallpaperPath)
        {
            try
            {
                if (!File.Exists(LivelyExecutablePath))
                {
                    Logger.Log($"Error: Lively not found at {LivelyExecutablePath}");
                    CurrentLivelyStatus = LivelyStatus.Error;
                    CurrentWallpaperStatus = WallpaperStatus.Error;
                    return;
                }

                if (!IsLivelyRunning())
                {
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

                    // Poll until Lively spawns its child processes (proves it's initialized)
                    // Cap at 15 seconds total
                    Logger.Log("Waiting for Lively to initialize...");
                    int waited = 0;
                    while (waited < 15000)
                    {
                        Thread.Sleep(500);
                        waited += 500;
                        var procs = GetLivelyOwnedProcesses();
                        if (procs.Count >= 2)
                        {
                            Logger.Log($"Lively initialized after {waited}ms ({procs.Count} processes).");
                            Thread.Sleep(1000); // extra settle time
                            break;
                        }
                    }
                    if (waited >= 15000)
                        Logger.Log("Warning: Lively init timeout after 15s, attempting setwp anyway.");
                }

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
                    Logger.Log($"Live wallpaper not found: {wallpaperPath}");
                    CurrentWallpaperStatus = WallpaperStatus.Error;
                }

                CurrentLivelyStatus = LivelyStatus.Running;
                Logger.Log("AC mode transition complete.");
            }
            catch (Exception ex)
            {
                Logger.Log($"StartLively background error: {ex.Message}");
                CurrentLivelyStatus = LivelyStatus.Error;
            }
        }

        public static void KillLivelySafely()
        {
            CurrentLivelyStatus = LivelyStatus.Stopping;
            Logger.Log("Attempting to close Lively completely...");

            // Run on background thread — never block the UI/power-event thread
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var livelyProcesses = GetLivelyOwnedProcesses();
                    if (livelyProcesses.Count == 0)
                    {
                        Logger.Log("Lively was not running.");
                        CurrentLivelyStatus = LivelyStatus.NotRunning;
                        return;
                    }

                    Logger.Log($"Force terminating {livelyProcesses.Count} Lively-owned processes...");
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

                    // Wait up to 1s for processes to actually die
                    Thread.Sleep(800);

                    // Verify they're gone
                    var remaining = GetLivelyOwnedProcesses();
                    if (remaining.Count == 0)
                    {
                        Logger.Log("Battery mode transition complete.");
                    }
                    else
                    {
                        Logger.Log($"Warning: {remaining.Count} Lively process(es) still alive after kill.");
                    }

                    CurrentLivelyStatus = LivelyStatus.NotRunning;
                }
                catch (Exception ex)
                {
                    Logger.Log($"KillLively background error: {ex.Message}");
                }
            });
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
