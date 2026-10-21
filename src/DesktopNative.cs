using System;
using System.Runtime.InteropServices;

namespace PowerUI
{
    internal static class DesktopNative
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Margins { internal int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct PowerStatus
        {
            internal byte AC, Battery, Percent, Saver;
            internal uint Life, FullLife;
        }
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(IntPtr handle, ref Margins margins);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr handle);
        [DllImport("kernel32.dll")] internal static extern bool GetSystemPowerStatus(out PowerStatus status);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr handle);
        [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr handle, IntPtr dc, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr handle, out Rect rect);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
    }
}
