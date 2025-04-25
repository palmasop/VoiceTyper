using System.Text.Json;
using System.Windows.Forms;

namespace VoiceTyper
{
    public class AppSettings
    {
        public string Language { get; set; } = "en-US";
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
                    try
                    {
                        string json = File.ReadAllText(SettingsPath);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json);
                        if (settings != null)
                        {
                            // Migrate old settings
                            if (settings.Language == "auto" || settings.Language == "mixed")
                            {
                                Console.WriteLine($"Migrating from old language setting: {settings.Language}");
                                settings.Language = "en-US";
                                settings.Save();
                            }
                            return settings;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing settings file, creating new settings: {ex.Message}");
                        // If the settings file is corrupted, backup the old one
                        string backupPath = SettingsPath + ".bak";
                        try
                        {
                            if (File.Exists(backupPath))
                            {
                                File.Delete(backupPath);
                            }
                            File.Move(SettingsPath, backupPath);
                            Console.WriteLine($"Backed up corrupted settings to: {backupPath}");
                        }
                        catch (Exception backupEx)
                        {
                            Console.WriteLine($"Failed to backup corrupted settings: {backupEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            // Return default settings if anything goes wrong
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

                // Create a backup of the existing settings before saving
                if (File.Exists(SettingsPath))
                {
                    string backupPath = SettingsPath + ".bak";
                    try
                    {
                        File.Copy(SettingsPath, backupPath, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create settings backup: {ex.Message}");
                    }
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Write to a temporary file first
                string tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json);

                // Then move it to the actual settings file (this is more reliable)
                if (File.Exists(SettingsPath))
                {
                    File.Delete(SettingsPath);
                }
                File.Move(tempPath, SettingsPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
} 