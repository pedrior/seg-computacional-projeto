using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Guardian.Native;

[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Native methods")]
internal static partial class NativeMethods
{
    public const nint HWND_BROADCAST = 0xFFFF;
    
    public const uint WM_SETTINGCHANGE = 0x001A;
    
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    
    public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    
    public const int INTERNET_OPTION_REFRESH = 37;
    
    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutA", SetLastError = true)]
    public static partial IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);
    
    [LibraryImport("wininet.dll", EntryPoint = "InternetSetOptionA", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}