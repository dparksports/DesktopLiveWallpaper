using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace DesktopLiveWallpaper.Views
{
    public sealed partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            this.InitializeComponent();
            Title = "Overlay";
            // Make window transparent and borderless
            // In WinUI 3, this requires some AppWindow manipulation or P/Invoke
            // We will address this in the "production polish" or startup logic
            // For now, standard window.
        }

        private void OnNotepadClick(object sender, RoutedEventArgs e)
        {
            Process.Start("notepad.exe");
        }

        private void OnSceneClick(object sender, RoutedEventArgs e)
        {
            // Logic to change scene
        }
    }
}
