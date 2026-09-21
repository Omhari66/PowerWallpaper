using System;
using System.Threading;
using System.Windows.Forms;

namespace PowerWallpaper
{
    static class Program
    {
        // Keep a reference so the GC never collects the Mutex while the app is running
        private static Mutex? _singleInstanceMutex;

        [STAThread]
        static void Main(string[] args)
        {
            // initiallyOwned: false — we create it then try to acquire separately
            _singleInstanceMutex = new Mutex(false, "PowerWallpaper_SingleInstance_9A3F2B1C");

            bool acquired = false;
            try
            {
                acquired = _singleInstanceMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                // Previous instance was killed without releasing. We have it now.
                acquired = true;
            }

            if (!acquired)
            {
                // Another instance is genuinely running — exit silently
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                // Log ALL unhandled exceptions so silent crashes become visible
                Application.ThreadException += (s, e) =>
                    Logger.Log($"FATAL ThreadException: {e.Exception}");
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    Logger.Log($"FATAL UnhandledException: {e.ExceptionObject}");

                Logger.Log("=== Application Started ===");
                bool isAutostart = args.Length > 0 && args[0] == "--autostart";

                var config = ConfigManager.Load();
                var powerMonitor = new PowerMonitor(config);

                powerMonitor.Start();

                Application.Run(new PowerWallpaperContext(config, powerMonitor, isAutostart));
            }
            catch (Exception ex)
            {
                Logger.Log($"FATAL startup exception: {ex}");
            }
            finally
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            }
        }
    }
}