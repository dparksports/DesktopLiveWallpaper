using Microsoft.UI.Xaml;
using System;
using DesktopLiveWallpaper.Services;

namespace DesktopLiveWallpaper.Views
{
    public sealed partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            this.InitializeComponent();
            Title = "Settings";
            
            // Ensure visible
            this.Activate();
             // WinUI 3 doesn't have Topmost property easily on Window usually, 
             // but let's try standard activation first.
             // Or use OverlappedPresenter to force it.
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter == null)
                {
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
                    presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                }
                presenter.IsAlwaysOnTop = true;
            }
            
            // Load current config
            var config = App.ConfigService.Config;
            VideoPathBox.Text = config.VideoPath;
            VolumeSlider.Value = config.Volume;
            MuteCheckBox.IsChecked = config.IsMuted;

            // Subscribe to logs
            DesktopLiveWallpaper.Helpers.Log.OnLog += Log_OnLog;
        }

        private void Log_OnLog(string msg)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                LogConsole.Text += msg + Environment.NewLine;
                // Auto scroll?
            });
        }

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            
            // Initialize the picker with the window handle (Required for WinUI 3 Desktop)
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".avi");

            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                VideoPathBox.Text = file.Path;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var config = App.ConfigService.Config;
            config.VideoPath = VideoPathBox.Text;
            config.Volume = VolumeSlider.Value;
            config.IsMuted = MuteCheckBox.IsChecked ?? true;
            
            App.ConfigService.Save();
            this.Close();
            
            App.ConfigService.Save();
            // this.Close(); // Keep open per user request
            
            // Reload Main Window
            // We need a way to access MainWindow instance or just restart app roughly.
            // Or better, assume user restarts or we handle it?
            // Let's implement static helper or access via App.
            // But m_window is private.
            // For now, user has to restart app to apply changes fully or we can just hope it picks up next time?
            // Wait, changing video requires reloading the player.
            
            // Hacky way: Force restart
            // Application.Current.Exit(); // This closes everything.
            
            // Better: Prompt user to restart or handle it?
            // Let's rely on the user restarting for now OR improve App structure later.
            // Actually, let's just show a MessageDialog that settings saved?
            // Or just do nothing and tell user.
            
            // IMPROVEMENT: Force update via static event?
            // App.UpdateVideoSource(config.VideoPath);
            // Let's add that to App.xaml.cs
            if (Application.Current is App app)
            {
                 app.UpdateVideo(config.VideoPath);
            }
        }
    }
}
