using System;
using System.IO;
using System.Text.Json;

namespace DesktopLiveWallpaper.Services
{
    public class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopLiveWallpaper", "config.json");

        public class AppConfig
        {
            // Defaulting to fireplace. User can browse for Jazz.
            public string VideoPath { get; set; } = "https://storage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4"; 
            public double Volume { get; set; } = 0.5;
            public bool IsMuted { get; set; } = false;
        }

        public AppConfig Config { get; private set; }

        public ConfigService()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    Config = JsonSerializer.Deserialize<AppConfig>(json);
                }
                catch
                {
                    Config = new AppConfig();
                }
            }
            else
            {
                Config = new AppConfig();
            }
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(Config);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
