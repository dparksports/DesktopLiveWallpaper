using DesktopLiveWallpaper.Interop;
using System;

namespace DesktopLiveWallpaper.Services
{
    public class WallpaperService
    {
        private IntPtr _workerW = IntPtr.Zero;
        private IntPtr _attachedWindow = IntPtr.Zero;

        public void Initialize(IntPtr windowHandle)
        {
            _attachedWindow = windowHandle;
            
            // 1. Spawn WorkerW
            IntPtr progman = Win32.FindWindow("Progman", null);
            IntPtr result = IntPtr.Zero;
            Win32.SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0, 1000, out result);
            DesktopLiveWallpaper.Helpers.Log.Write($"Sent 0x052C to Progman ({progman}). Result: {result}");

            // 2. Spawn WorkerW by sending 0x052C to Progman
            // We loop a few times to ensure it works, as sometimes it takes a moment or needs a retry.
            IntPtr workerW = IntPtr.Zero;
            
            for (int i = 0; i < 3; i++)
            {
                DesktopLiveWallpaper.Helpers.Log.Write($"[Attempt {i+1}] Sending 0x052C to Progman...");
                // Use 0 for SMTO_NORMAL
                Win32.SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0, 1000, out _);
                
                // Give it a moment to spawn
                System.Threading.Thread.Sleep(100);

                // 3. Find the correct WorkerW
                // The correct WorkerW is the one that is the Next Sibling of the WorkerW containing SHELLDLL_DefView.
                // OR: On W10/W11, often there's a WorkerW with SHELLDLL_DefView, and a NEW WorkerW is created BEHIND it? 
                
                // Let's EnumWindows to find it.
                Win32.EnumWindows((hwnd, lParam) =>
                {
                    IntPtr shellDll = Win32.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellDll != IntPtr.Zero)
                    {
                        // Found the WorkerW with icons. The wallpaper WorkerW is its Next Sibling (Z-order wise, i.e. Behind it).
                        workerW = Win32.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                        
                        DesktopLiveWallpaper.Helpers.Log.Write($"[Enum] Found SHELLDLL_DefView at {shellDll} inside {hwnd}");
                        DesktopLiveWallpaper.Helpers.Log.Write($"[Enum] WorkerW Search (Next Sibling of {hwnd}): {workerW}");
                        
                        // Stop enumerating
                        return false;
                    }
                    return true;

                }, IntPtr.Zero);

                if (workerW != IntPtr.Zero)
                {
                     DesktopLiveWallpaper.Helpers.Log.Write($"SUCCESS: Found target WorkerW: {workerW}");
                     _workerW = workerW; // Critical Fix: Assign to class field
                     break;
                }
            }

            // 4. Fallback search (sometimes it's just an isolated WorkerW without the sibling chain clearly visible?)
            if (workerW == IntPtr.Zero)
            {
                DesktopLiveWallpaper.Helpers.Log.Write("[Fallback] Searching for any isolated WorkerW...");
                Win32.EnumWindows((hwnd, lParam) =>
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    Win32.GetClassName(hwnd, sb, 256);
                    if (sb.ToString() == "WorkerW")
                    {
                         IntPtr shell = Win32.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                         if (shell == IntPtr.Zero && Win32.IsWindowVisible(hwnd))
                         {
                             DesktopLiveWallpaper.Helpers.Log.Write($"[Fallback] Found candidate WorkerW (No ShellDLL): {hwnd}");
                             _workerW = hwnd;
                             // Don't stop, but usually the first one is fine?
                         }
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (_workerW != IntPtr.Zero)
            {
                DesktopLiveWallpaper.Helpers.Log.Write($"Attaching {windowHandle} to WorkerW {_workerW}");
                Win32.SetParent(windowHandle, _workerW);
                // Force it to be the bottom-most child of WorkerW, just in case
                Win32.SetWindowPos(windowHandle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOMOVE | Win32.SWP_NOACTIVATE);
            }
            else
            {
                // FAILED to find split WorkerW. 
                // This means the user likely has "Animate controls and elements inside windows" disabled.
                // In this state, SHELLDLL_DefView owns the background and is Opaque.
                // We cannot be behind it (on Progman) because it covers us.
                // We MUST be a child of SHELLDLL_DefView itself to be visible.
                
                DesktopLiveWallpaper.Helpers.Log.Write($"FAILED to find WorkerW. Falling back to SHELLDLL_DefView injection.");
                
                IntPtr shellDll = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDll != IntPtr.Zero)
                {
                    DesktopLiveWallpaper.Helpers.Log.Write($"Found SHELLDLL_DefView at {shellDll}. Attaching directly to it.");
                    Win32.SetParent(windowHandle, shellDll);
                    
                    // Send to bottom (Behind SysListView32 icons)
                    Win32.SetWindowPos(windowHandle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOMOVE | Win32.SWP_NOACTIVATE);
                }
                else
                {
                    // Absolute worst case: Attach to Progman and hope for the best (likely invisible)
                    DesktopLiveWallpaper.Helpers.Log.Write("Could not find SHELLDLL_DefView either. Attaching to Progman (Likely Invisible).");
                    Win32.SetParent(windowHandle, progman);
                    Win32.SetWindowPos(windowHandle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOMOVE | Win32.SWP_NOACTIVATE);
                }
            }
        }
        
        public void Detach()
        {
             if (_attachedWindow != IntPtr.Zero)
             {
                 Win32.SetParent(_attachedWindow, IntPtr.Zero);
             }
        }
    }
}
