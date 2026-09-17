using System;
using System.IO;
using System.Text.Json;

namespace PowerWallpaper
{
    public class Config
    {
        public string ChargingWallpaper { get; set; } = "wallpapers/charging.mp4";
        public string BatteryWallpaper { get; set; } = "wallpapers/black.jpg";
        public bool StartWithWindows { get; set; } = false;
        public bool PauseAutomation { get; set; } = false;
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = "config.json";

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<Config>(json) ?? new Config();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading config: {ex.Message}");
            }
            
            // Create default
            var config = new Config();
            Save(config);
            return config;
        }

        public static void Save(Config config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving config: {ex.Message}");
            }
        }
    }
}
