using System;
using System.IO;

namespace PowerWallpaper
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "power-wallpaper.log");
        private static readonly string LogBak  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "power-wallpaper.log.bak");
        private static readonly object _lock = new object();
        private const long MaxLogBytes = 512 * 1024; // 500 KB

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    // Rotate log if it's grown too large
                    if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxLogBytes)
                    {
                        if (File.Exists(LogBak)) File.Delete(LogBak);
                        File.Move(LogFile, LogBak);
                    }

                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    string logEntry = $"[{timestamp}] {message}";
                    File.AppendAllText(LogFile, logEntry + Environment.NewLine);
                    Console.WriteLine(logEntry);
                }
                catch
                {
                    // Fail silently for logging
                }
            }
        }
    }
}
