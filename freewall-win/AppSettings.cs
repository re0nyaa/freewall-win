using System;
using System.IO;
using System.Text.Json;

namespace freewall_win
{
    public class AppSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "freewall-win",
            "settings.json"
        );

        public static AppSettings Instance { get; set; } = Load();

        public bool AutoStartProtection { get; set; } = false;
        public bool LaunchAtStartup { get; set; } = false;

        // Engine: "spoofdpi" (default) or "goodbyedpi"
        public string Engine { get; set; } = "spoofdpi";

        // SpoofDPI settings (macOS freewall preset: 9.9.9.9:9953 + chunk 1)
        public int SpoofDpiPort { get; set; } = 8080;
        public string SpoofDpiDnsAddr { get; set; } = "9.9.9.9";
        public int SpoofDpiDnsPort { get; set; } = 9953;
        public bool SpoofDpiEnableDoh { get; set; } = false;
        public int SpoofDpiWindowSize { get; set; } = 1; // 1 byte chunk

        // GoodbyeDPI settings
        public string ModePreset { get; set; } = "-1";
        public bool UseCustomDns { get; set; } = false;
        public string CustomDnsIp { get; set; } = "9.9.9.9";
        public int CustomDnsPort { get; set; } = 53;

        public string CustomArguments { get; set; } = "";
        public string CustomBinaryPath { get; set; } = "";

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        // Update old yandex default if still present
                        if (settings.CustomDnsIp == "77.88.8.8" && settings.CustomDnsPort == 1253)
                        {
                            settings.CustomDnsIp = "9.9.9.9";
                            settings.CustomDnsPort = 9953;
                        }
                        return settings;
                    }
                }
            }
            catch { }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
