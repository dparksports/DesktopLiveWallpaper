using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DesktopLiveWallpaper.Helpers;

namespace DesktopLiveWallpaper.Services
{
    public class YtDlpService
    {
        private const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private string _executablePath;

        public YtDlpService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "DesktopLiveWallpaper", "Bin");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _executablePath = Path.Combine(folder, "yt-dlp.exe");
        }

        public async Task EnsureInstalledAsync()
        {
            if (File.Exists(_executablePath))
                return;

            Log.Write("Downloading yt-dlp...");
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(YtDlpDownloadUrl);
                await File.WriteAllBytesAsync(_executablePath, bytes);
                Log.Write("yt-dlp downloaded successfully.");
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to download yt-dlp: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetStreamUrlAsync(string youtubeUrl)
        {
            await EnsureInstalledAsync();

            var tcs = new TaskCompletionSource<string>();

            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                // -f best: best single file (audio+video combined). 
                // [ext=mp4] prefers mp4 container.
                Arguments = $"-f \"best[ext=mp4]/best\" -g --no-playlist \"{youtubeUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
            // Use StringBuilder for thread safety and performance
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Log.Write($"yt-dlp error: {errorBuilder}");
                    throw new Exception($"yt-dlp exited with code {process.ExitCode}");
                }

                var fullOutput = outputBuilder.ToString();
                // Take the first non-empty line
                var url = "";
                using (var reader = new StringReader(fullOutput))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            url = line.Trim();
                            break; // Logic: Assume first URL is the video or the combined stream
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(url))
                    throw new Exception("yt-dlp returned empty URL");

                // Ensure it's a valid URI
                if (Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    Log.Write("Resolved URL via yt-dlp.");
                    return url;
                }
                
                throw new Exception($"Invalid URL returned: {url}");
            }
            catch (Exception ex)
            {
                Log.Write($"yt-dlp execution failed: {ex.Message}");
                throw;
            }
        }
    }
}
