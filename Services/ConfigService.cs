using System;
using System.IO;
using System.Text.Json;

namespace DesktopLiveWallpaper.Services
{
    public class HistoryItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        
        public override string ToString() => Title; 
    }

    public class AppConfig
    {
        public string VideoPath { get; set; } = "";
        public double Volume { get; set; } = 0.5;
        public bool IsMuted { get; set; } = false;
        public System.Collections.Generic.List<HistoryItem> History { get; set; } = new System.Collections.Generic.List<HistoryItem>();

        public void AddToHistory(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (string.IsNullOrWhiteSpace(title)) title = url; 

            var existing = History.Find(x => x.Url == url);
            if (existing != null)
            {
                History.Remove(existing);
            }

            History.Insert(0, new HistoryItem { Url = url, Title = title });

            if (History.Count > 10)
            {
                History.RemoveRange(10, History.Count - 10);
            }
        }
    }
    
    public class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopLiveWallpaper", "config.json");

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
