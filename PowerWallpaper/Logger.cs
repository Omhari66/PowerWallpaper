using System;
using System.IO;

namespace PowerWallpaper
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "power-wallpaper.log");
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
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
