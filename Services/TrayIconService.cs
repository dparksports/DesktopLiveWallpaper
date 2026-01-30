using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Drawing;

namespace DesktopLiveWallpaper.Services
{
    public class TrayIconService : IDisposable
    {
        private TaskbarIcon _taskbarIcon;
        private Action _onSettings;
        private Action _onExit;

        public TrayIconService(Action onSettings, Action onExit)
        {
            _onSettings = onSettings;
            _onExit = onExit;
            Initialize();
        }

        private void Initialize()
        {
            // Run on UI thread
            if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() != null)
            {
                CreateIcon();
            }
        }

        private void CreateIcon()
        {
            _taskbarIcon = new TaskbarIcon();
            _taskbarIcon.Icon = SystemIcons.Application;
            _taskbarIcon.ToolTipText = "Desktop Live Wallpaper";
            
            var menu = new MenuFlyout();
            
            var settingsItem = new MenuFlyoutItem { Text = "Settings" };
            settingsItem.Click += (s, e) => _onSettings?.Invoke();
            menu.Items.Add(settingsItem);
            
            menu.Items.Add(new MenuFlyoutSeparator());
            
            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += (s, e) => _onExit?.Invoke();
            menu.Items.Add(exitItem);

            _taskbarIcon.ContextFlyout = menu;
            _taskbarIcon.ForceCreate();
        }

        public void Dispose()
        {
            _taskbarIcon?.Dispose();
        }
    }
}
