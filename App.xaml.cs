using Microsoft.UI.Xaml;
using DesktopLiveWallpaper.Services;
using DesktopLiveWallpaper.Views;
using DesktopLiveWallpaper.Helpers;
using DesktopLiveWallpaper.Interop;
using System;
using WinRT.Interop;
using Microsoft.UI.Windowing;

namespace DesktopLiveWallpaper
{
    public partial class App : Application
    {
        public static WallpaperService WallpaperService { get; private set; }
        public static ConfigService ConfigService { get; private set; }
        public static TrayIconService TrayService { get; private set; }
        
        private MainWindow m_window;
        // private OverlayWindow m_overlay; // Unused

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Log.Write("App Launched");
            
            // 1. Initialize Services
            ConfigService = new ConfigService();
            WallpaperService = new WallpaperService();
            // TrayService calls OnSettings when clicked. We'll map that to bringing the window to front.
            TrayService = new TrayIconService(OnSettings, OnExit);

            Log.Write("Services Initialized");

            // 2. Create Main Window (Video)
            m_window = new MainWindow();
            
            // 3. Get Handle & WindowId
            IntPtr windowHandle = WindowNative.GetWindowHandle(m_window);
            Log.Write($"Main Window Handle: {windowHandle}");

            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // 4. Configure Window (Presenter)
            if (appWindow != null)
            {
                appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                // Force standard window
                var presenter = appWindow.Presenter as OverlappedPresenter;
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
                presenter.SetBorderAndTitleBar(true, true);
            }
            
            // 5. Set Video Source
            string videoPath = ConfigService.Config.VideoPath;
            Log.Write($"Video Path: {videoPath}");
            m_window.SetVideoSourceAsync(videoPath);

            // 6. Activate
            m_window.Activate();
            Log.Write("Window Activated");
            
            // 7. Resize to Virtual Screen (DISABLED for standard window mode)
            // var bounds = ScreenHelper.GetVirtualScreenBounds();
            // Log.Write($"Screen Bounds: {bounds}");
            // Win32.SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, bounds.Width, bounds.Height, Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER | Win32.SWP_NOMOVE); 
            
            // 8. Attach to WorkerW (DISABLED)
            // Log.Write("Attempting to attach to WorkerW...");
            // WallpaperService.Initialize(windowHandle);
            // Log.Write("WorkerW attachment complete (check if successful)");
            
            // 9. Create Overlay (DISABLED) - Removed completely as per instruction
        }
        
        private void OnSettings()
        {
            // Just bring main window to front
            if (m_window != null)
            {
                m_window.Activate();
            }
        }

        private void OnExit()
        {
            TrayService?.Dispose();
            Exit();
        }
        
        public void UpdateVideo(string path)
        {
            if (m_window != null)
            {
                m_window.SetVideoSourceAsync(path);
            }
        }
        
    }
}
