using Microsoft.UI.Xaml;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System;
using Windows.Media.Core;

namespace DesktopLiveWallpaper
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Desktop Live Wallpaper";
            
            // Ensure standard window behavior (TitleBar + Resizable)
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                var presenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
                presenter.IsResizable = true;
                presenter.IsAlwaysOnTop = false; 
                appWindow.SetPresenter(presenter);
            }

            // Load Config
            var config = App.ConfigService.Config;
            VideoPathBox.Text = config.VideoPath;
            VolumeSlider.Value = config.Volume;
            MuteCheckBox.IsChecked = config.IsMuted;
            
            // Subscribe to Log
            DesktopLiveWallpaper.Helpers.Log.OnLog += Log_OnLog;
            
            // Apply volume immediately
            if (Player != null && Player.MediaPlayer != null)
            {
                 Player.MediaPlayer.Volume = config.IsMuted ? 0 : config.Volume;
            }
            
            // Listen to UI changes
            VolumeSlider.ValueChanged += (s, e) => { 
                if (Player?.MediaPlayer != null && MuteCheckBox.IsChecked == false) Player.MediaPlayer.Volume = e.NewValue; 
                App.ConfigService.Config.Volume = e.NewValue;
                App.ConfigService.Save();
            };
            MuteCheckBox.Checked += (s, e) => { 
                if (Player?.MediaPlayer != null) Player.MediaPlayer.Volume = 0; 
                App.ConfigService.Config.IsMuted = true;
                App.ConfigService.Save();
            };
            MuteCheckBox.Unchecked += (s, e) => { 
                if (Player?.MediaPlayer != null) Player.MediaPlayer.Volume = VolumeSlider.Value; 
                App.ConfigService.Config.IsMuted = false;
                App.ConfigService.Save();
            };
        }
        
        private void Log_OnLog(string msg)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (LogConsole != null)
                {
                    LogConsole.Text += msg + Environment.NewLine;
                }
            });
        }

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".mov");
            
            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                VideoPathBox.Text = file.Path;
                OnPlayClick(null, null); // Auto-play
            }
        }

        private void OnPlayClick(object sender, RoutedEventArgs e)
        {
            var path = VideoPathBox.Text;
            if (string.IsNullOrWhiteSpace(path)) return;
            
            // Save Config
            App.ConfigService.Config.VideoPath = path;
            App.ConfigService.Save();
            
            // Play
            SetVideoSourceAsync(path);
        }

        public async void SetVideoSourceAsync(string pathOrUrl)
        {
            try
            {
                Uri sourceUri = null;

                // Check for YouTube
                if (pathOrUrl.Contains("youtube.com") || pathOrUrl.Contains("youtu.be"))
                {
                    DesktopLiveWallpaper.Helpers.Log.Write($"Detected YouTube URL. Using yt-dlp directly...");
                    try 
                    {
                        var ytdlp = new Services.YtDlpService();
                        var url = await ytdlp.GetStreamUrlAsync(pathOrUrl);
                        sourceUri = new Uri(url);
                        DesktopLiveWallpaper.Helpers.Log.Write($"Resolved via yt-dlp: {sourceUri}");
                    }
                    catch (Exception ex)
                    {
                        DesktopLiveWallpaper.Helpers.Log.Write($"yt-dlp failed: {ex.Message}. Trying internal resolver...");
                        
                         try 
                        {
                            var youtube = new YoutubeExplode.YoutubeClient();
                            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(pathOrUrl);
                            var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                            if (streamInfo != null)
                            {
                                sourceUri = new Uri(streamInfo.Url);
                                DesktopLiveWallpaper.Helpers.Log.Write($"Resolved via YoutubeExplode: {sourceUri}");
                            }
                        }
                        catch (Exception ex2)
                        {
                            var msg = ex2.Message;
                            if (msg.Contains("403") || msg.Contains("Forbidden"))
                            {
                                msg += " [Likely COPYRIGHT or AGE RESTRICTED content. Try a different video.]";
                            }
                            DesktopLiveWallpaper.Helpers.Log.Write($"Internal resolver failed: {msg}");
                        }
                    }
                }
                
                if (sourceUri == null)
                {
                    // Fallback to standard URI creation
                    if (!Uri.TryCreate(pathOrUrl, UriKind.Absolute, out sourceUri))
                    {
                         // Handle local file path if valid
                         try {
                            sourceUri = new Uri(pathOrUrl);
                         } catch { }
                    }
                }

                if (sourceUri != null)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        SetVideoSource(sourceUri);
                    });
                }
            }
            catch (Exception ex)
            {
                DesktopLiveWallpaper.Helpers.Log.Write($"SetVideoSourceAsync Error: {ex.Message}");
            }
        }

        public void SetVideoSource(Uri source)
        {
            if (Player != null)
            {
                DesktopLiveWallpaper.Helpers.Log.Write($"Setting Player Source: {source}");
                
                // Reset handlers to avoid duplicates if called multiple times (though simple way is valid)
                Player.MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
                Player.MediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                
                Player.Source = MediaSource.CreateFromUri(source);
                Player.MediaPlayer.IsLoopingEnabled = true;
                
                Player.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
                Player.MediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
                
                Player.MediaPlayer.Play();
            }
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            DesktopLiveWallpaper.Helpers.Log.Write("MediaPlayer: Media Opened Successfully");
        }

        private void MediaPlayer_MediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
        {
            DesktopLiveWallpaper.Helpers.Log.Write($"MediaPlayer Error: {args.Error} - {args.ErrorMessage}");
            if (args.ExtendedErrorCode != null)
            {
                DesktopLiveWallpaper.Helpers.Log.Write($"Extended Error: {args.ExtendedErrorCode.Message}");
            }
        }
    }
}
