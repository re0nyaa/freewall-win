using System;
using System.Runtime.InteropServices;

namespace freewall_win
{
    public static class WindowsProxyManager
    {
        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public static void SetProxy(string proxyServer)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key != null)
                {
                    key.SetValue("ProxyEnable", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("ProxyServer", proxyServer, Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("ProxyOverride", "<local>;localhost;127.0.0.1", Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("AutoDetect", 0, Microsoft.Win32.RegistryValueKind.DWord); // Disable WPAD delay
                    RefreshSettings();
                }
            }
            catch { }
        }

        public static void DisableProxy()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key != null)
                {
                    key.SetValue("ProxyEnable", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    RefreshSettings();
                }
            }
            catch { }
        }

        private static void RefreshSettings()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}
