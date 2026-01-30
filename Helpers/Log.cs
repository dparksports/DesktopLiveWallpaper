using System;
using System.IO;

namespace DesktopLiveWallpaper.Helpers
{
    public static class Log
    {
        private static string _logPath;
        public static event Action<string> OnLog;

        static Log()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "DesktopLiveWallpaper");
                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);
                    
                _logPath = System.IO.Path.Combine(folder, "debug.log");
            }
            catch { }
        }

        public static void Write(string message)
        {
            try
            {
                var line = $"{DateTime.Now}: {message}";
                if (_logPath != null)
                    System.IO.File.AppendAllText(_logPath, line + Environment.NewLine);
                
                OnLog?.Invoke(line);
            }
            catch { }
        }
    }
}
