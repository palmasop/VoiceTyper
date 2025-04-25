using System.Text.Json;
using System.Windows.Forms;

namespace VoiceTyper
{
    public class AppSettings
    {
        public string Language { get; set; } = "zh-HK";
        public Keys Hotkey { get; set; } = Keys.OemQuestion;
        public int HotkeyModifiers { get; set; } = 0x0002 | 0x0004; // Default: Ctrl + Shift
        public string AzureRegion { get; set; } = "eastasia";
        public string AzureSubscriptionKey { get; set; } = "";
        public bool IncludePunctuation { get; set; } = true;
        public bool RunAtStartup { get; set; } = false;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceTyper",
            "settings.json"
        );

        public static AppSettings Load()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
} 