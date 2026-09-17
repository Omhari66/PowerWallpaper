using System;
using System.Threading;
using System.Windows.Forms;

namespace PowerWallpaper
{
    static class Program
    {
        static Mutex _mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");

        [STAThread]
        static void Main(string[] args)
        {
            if (_mutex.WaitOne(TimeSpan.Zero, true))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Logger.Log("=== Application Started ===");
                bool isAutostart = args.Length > 0 && args[0] == "--autostart";

                var config = ConfigManager.Load();
                var powerMonitor = new PowerMonitor(config);
                
                powerMonitor.Start();

                Application.Run(new PowerWallpaperContext(config, powerMonitor, isAutostart));
                
                _mutex.ReleaseMutex();
            }
            else
            {
                // Another instance is running, exit silently
            }
        }
    }
}