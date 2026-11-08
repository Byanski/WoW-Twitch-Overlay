using System;
using System.IO;
using System.Text.Json;

namespace TwitchWoWOverlay
{
    public class AppSettings
    {
        public string LastChannel { get; set; } = "shroud";
        public double Opacity { get; set; } = 0.95;
        public double Volume { get; set; } = 1.0;
        public bool IsMuted { get; set; } = false;
        public double WindowWidth { get; set; } = 640;
        public double WindowHeight { get; set; } = 390;
        public double? WindowTop { get; set; } = null;
        public double? WindowLeft { get; set; } = null;
        public string ViewMode { get; set; } = "popout"; // "popout", "full", "chat"
        public bool AlwaysOnTop { get; set; } = true;
        public bool HideInAltTab { get; set; } = false;

        private static string SettingsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TwitchWoWOverlay"
        );

        private static string SettingsFilePath => Path.Combine(SettingsFolder, "config.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch
            {
                // Fallback to default if error
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Silently continue if cannot save
            }
        }
    }
}
