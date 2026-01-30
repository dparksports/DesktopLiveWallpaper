using System;
using System.Runtime.InteropServices;
using DesktopLiveWallpaper.Interop;

namespace DesktopLiveWallpaper.Helpers
{
    public static class ScreenHelper
    {
        public static (int X, int Y, int Width, int Height) GetVirtualScreenBounds()
        {
            int x = Win32.GetSystemMetrics(76); // SM_XVIRTUALSCREEN
            int y = Win32.GetSystemMetrics(77); // SM_YVIRTUALSCREEN
            int w = Win32.GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
            int h = Win32.GetSystemMetrics(79); // SM_CYVIRTUALSCREEN
            return (x, y, w, h);
        }
    }
    
    // Extension to Win32 for SystemMetrics
    public partial class Win32Extensions
    {
         [DllImport("user32.dll")]
         public static extern int GetSystemMetrics(int nIndex);
    }
}
