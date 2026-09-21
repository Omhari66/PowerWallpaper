using System;
using System.Threading;
using System.Windows.Forms;

namespace PowerWallpaper
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                return; // Another instance is already running
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Logger.Log("=== Application Started ===");
            bool isAutostart = args.Length > 0 && args[0] == "--autostart";

            var config = ConfigManager.Load();
            var powerMonitor = new PowerMonitor(config);
            
            powerMonitor.Start();

            Application.Run(new PowerWallpaperContext(config, powerMonitor, isAutostart));
        }
    }
}