using Guardian.Native;
using Microsoft.Win32;

namespace Guardian.Services;

public sealed class SystemProxyManager : ISystemProxyManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public void Configure(ProxySettings settings)
    {
        if (settings.Host is null)
        {
            throw new ArgumentNullException(nameof(settings.Host));
        }

        using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true))
        {
            if (key is null)
            {
                throw new InvalidOperationException("Failed to access registry key for proxy settings");
            }

            // Preserve existing proxy override settings
            var proxyOverride = key.GetValue("ProxyOverride") ?? "";

            key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            key.SetValue("ProxyServer", $"{settings.Host}:{settings.Port}", RegistryValueKind.String);
            key.SetValue("ProxyOverride", proxyOverride, RegistryValueKind.String);
        }

        RefreshSystemProxySettings();
    }

    public void Disable()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true))
        {
            if (key is null)
            {
                throw new InvalidOperationException("Failed to access registry key for proxy settings");
            }

            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        }

        RefreshSystemProxySettings();
    }

    private static void RefreshSystemProxySettings()
    {
        // Notifica o WinINet que as configurações foram alteradas
        NativeMethods.InternetSetOption(IntPtr.Zero, NativeMethods.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        NativeMethods.InternetSetOption(IntPtr.Zero, NativeMethods.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

        // Envie uma mensagem em braodcast notificando que as configurações foram alteradas
        NativeMethods.SendMessageTimeout(
            hWnd: NativeMethods.HWND_BROADCAST,
            Msg: NativeMethods.WM_SETTINGCHANGE,
            wParam: IntPtr.Zero,
            lParam: IntPtr.Zero,
            fuFlags: NativeMethods.SMTO_ABORTIFHUNG,
            uTimeout: 5000,
            out _);
    }
}