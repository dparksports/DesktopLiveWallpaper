using Microsoft.UI.Xaml;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System;
using Windows.Media.Core;
using DesktopLiveWallpaper.Interop;

namespace DesktopLiveWallpaper
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Desktop Live Wallpaper";
            
            // Initialize Scrub Timer
            InitializeTimer();
            
            // Clean borderless window
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                var presenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
                presenter.SetBorderAndTitleBar(true, true); // Restore Standard Title Bar
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
                appWindow.SetPresenter(presenter);
                
                // Set default small size (480p)
                appWindow.Resize(new Windows.Graphics.SizeInt32(854, 480));
            }
            
            // Nuclear Option REMOVED (Reverting to standard window)
            // var currentStyle = Win32.GetWindowLong(windowHandle, Win32.GWL_STYLE);...

            // Load Config
            var config = App.ConfigService.Config;
            
            // Populate History
            VideoPathBox.ItemsSource = config.History;
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
            
            // Dragging: Disabled since we have standard title bar now
            /*
            this.Content.PointerPressed += (s, e) => {
                var properties = e.GetCurrentPoint(null).Properties;
                if (!properties.IsLeftButtonPressed) return;

                // If clicking controls, don't drag
                if (e.OriginalSource is FrameworkElement fe && (fe.Name == "ControlPanel" || fe.DataContext is string)) return;
                
                // P/Invoke to drag window
                Win32.ReleaseCapture();
                Win32.SendMessage(windowHandle, Win32.WM_NCLBUTTONDOWN, new IntPtr(Win32.HT_CAPTION), IntPtr.Zero);
            };
            */

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
            
            // Handle Selection Change in ComboBox
            VideoPathBox.SelectionChanged += (s, e) => {
                if (VideoPathBox.SelectedItem is DesktopLiveWallpaper.Services.HistoryItem item)
                {
                    VideoPathBox.Text = item.Url; // Set Text to URL when picked
                }
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

        private void OnVideoPathBoxKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                OnPlayClick(sender, new RoutedEventArgs());
                // Close dropdown if open? Not strictly necessary.
            }
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

        private async void OnPlayClick(object sender, RoutedEventArgs e)
        {
            var path = VideoPathBox.Text;

            if (string.IsNullOrWhiteSpace(path)) return;
            
            string title = path;
            
            // Fetch Title Logic
            if (path.StartsWith("http"))
            {
                 // Assuming YouTube/Web
                 try {
                     var youtube = new YoutubeExplode.YoutubeClient();
                     var video = await youtube.Videos.GetAsync(path);
                     title = video.Title;
                 } catch { /* keep url as title if fail */ }
            }
            else
            {
                // Local File
                try {
                    title = System.IO.Path.GetFileName(path);
                } catch { }
            }

            // Save Config & History
            App.ConfigService.Config.VideoPath = path;
            App.ConfigService.Config.AddToHistory(path, title);
            App.ConfigService.Save();
            
            // Refresh List
            VideoPathBox.ItemsSource = null;
            VideoPathBox.ItemsSource = App.ConfigService.Config.History;
            VideoPathBox.Text = path; // Restore text
            
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

        // Scrub Bar Logic
        private DispatcherTimer _timer;
        private bool _isDragging = false;

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(0.5);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            if (Player?.MediaPlayer?.PlaybackSession == null) return;
            var session = Player.MediaPlayer.PlaybackSession;
            
            // Update Time Text
            CurrentTimeText.Text = session.Position.ToString(@"mm\:ss");
            
            // Update Slider only if not dragging
            if (!_isDragging && session.NaturalDuration.TotalSeconds > 0)
            {
                TimelineSlider.Maximum = session.NaturalDuration.TotalSeconds;
                TimelineSlider.Value = session.Position.TotalSeconds;
                TotalTimeText.Text = session.NaturalDuration.ToString(@"mm\:ss");
            }
        }

        private void OnTimelineSliderPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isDragging = true;
        }

        private void OnTimelineSliderReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isDragging = false;
            // Final seek to ensure accuracy
            if (Player?.MediaPlayer?.PlaybackSession != null)
            {
                 Player.MediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
            }
        }

        private void OnTimelineSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isDragging && Player?.MediaPlayer?.PlaybackSession != null)
            {
                Player.MediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(e.NewValue);
            }
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            DesktopLiveWallpaper.Helpers.Log.Write("MediaPlayer: Media Opened Successfully");
            
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (TimelineSlider != null)
                {
                    TimelineSlider.Maximum = sender.PlaybackSession.NaturalDuration.TotalSeconds;
                    TotalTimeText.Text = sender.PlaybackSession.NaturalDuration.ToString(@"mm\:ss");
                }
                
                // Resize to fit video aspect ratio
                try 
                {
                    var width = sender.PlaybackSession.NaturalVideoWidth;
                    var height = sender.PlaybackSession.NaturalVideoHeight;
                    
                    if (width > 0 && height > 0)
                    {
                        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                        
                        if (appWindow != null)
                        {
                            var currentSize = appWindow.Size;
                            var aspect = (double)height / width;
                            var newHeight = (int)(currentSize.Width * aspect);
                            if (newHeight < 200) newHeight = 200;
                            appWindow.Resize(new Windows.Graphics.SizeInt32(currentSize.Width, newHeight));
                        }
                    }
                }
                catch (Exception ex)
                {
                     DesktopLiveWallpaper.Helpers.Log.Write($"Resize Error: {ex.Message}");
                }
            });
        }

        private void MediaPlayer_MediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
        {
            DesktopLiveWallpaper.Helpers.Log.Write($"MediaPlayer Error: {args.Error} - {args.ErrorMessage}");
            if (args.ExtendedErrorCode != null)
            {
                DesktopLiveWallpaper.Helpers.Log.Write($"Extended Error: {args.ExtendedErrorCode.Message}");
            }
        }

        private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // if (ControlsGrid != null) ControlsGrid.Opacity = 1;
            FadeInStoryboard.Begin();
        }

        private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (ControlsGrid != null && !VideoPathBox.FocusState.HasFlag(FocusState.Keyboard)) 
            {
               // ControlsGrid.Opacity = 0;
               FadeOutStoryboard.Begin();
            }
        }
        

    }
}
