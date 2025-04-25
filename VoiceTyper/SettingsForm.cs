using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VoiceTyper
{
    public class SettingsForm : Form
    {
        private ComboBox languageComboBox = new();
        private Button hotkeyButton = new();
        private Button resetLanguageButton = new();
        private Button resetHotkeyButton = new();
        private Label titleLabel = new();
        private Label languageLabel = new();
        private Label hotkeyLabel = new();
        private Label hotkeyValueLabel = new();
        private Label azureRegionLabel = new();
        private TextBox azureRegionTextBox = new();
        private Label azureKeyLabel = new();
        private TextBox azureKeyTextBox = new();
        private Label validationLabel = new();
        private CheckBox punctuationCheckBox = new();
        private CheckBox startupCheckBox = new();

        public delegate void LanguageChangedHandler(string newLanguage);
        public delegate void HotkeyChangedHandler(Keys newHotkey, int newModifiers);
        public delegate void HotkeyCapturingStateHandler(bool isCapturing);
        public delegate void AzureConfigChangedHandler(string region, string key);
        public delegate void PunctuationChangedHandler(bool includePunctuation);
        public delegate void StartupChangedHandler(bool runAtStartup);

        public event LanguageChangedHandler? LanguageChanged;
        public event HotkeyChangedHandler? HotkeyChanged;
        public event HotkeyCapturingStateHandler? HotkeyCapturingStateChanged;
        public event AzureConfigChangedHandler? AzureConfigChanged;
        public event PunctuationChangedHandler? PunctuationChanged;
        public event StartupChangedHandler? StartupChanged;

        public string SelectedLanguage { get; private set; }
        public Keys SelectedHotkey { get; private set; }
        public int SelectedModifiers { get; private set; }
        public string AzureRegion { get; private set; }
        public string AzureKey { get; private set; }

        private bool isCapturingHotkey = false;
        private Keys currentKey = Keys.None;
        private int currentModifiers = 0;
        private Keys lastKey = Keys.None;
        private int lastModifiers = 0;
        private HashSet<Keys> pressedKeys = new();
        private System.Windows.Forms.Timer? keyTimer;
        private bool isAzureConfigValid = true;
        private bool includePunctuation;
        private bool runAtStartup;

        // Default values
        private const string DEFAULT_LANGUAGE = "zh-HK";
        private const Keys DEFAULT_HOTKEY = Keys.OemQuestion;
        private const int DEFAULT_MODIFIERS = 0x0002 | 0x0004; // Ctrl + Shift

        private Dictionary<string, string> languages = new Dictionary<string, string>
        {
            // East Asian Languages
            { "zh-HK", "Chinese (Cantonese)" },
            { "zh-CN", "Chinese (Mandarin)" },
            { "zh-TW", "Chinese (Traditional)" },
            { "ja-JP", "Japanese" },
            { "ko-KR", "Korean" },

            // European Languages
            { "en-US", "English (US)" },
            { "fr-FR", "French" },
            { "de-DE", "German" },
            { "es-ES", "Spanish" },
            { "it-IT", "Italian" },
            { "pt-BR", "Portuguese" },
            { "ru-RU", "Russian" },

            // Southeast Asian Languages
            { "th-TH", "Thai" },
            { "vi-VN", "Vietnamese" },
            { "id-ID", "Indonesian" },
            { "ms-MY", "Malay" }
        };

        public SettingsForm(string currentLanguage, Keys currentHotkey, int currentModifiers, string azureRegion, string azureKey, bool includePunctuation, bool runAtStartup)
        {
            SelectedLanguage = currentLanguage;
            SelectedHotkey = currentHotkey;
            SelectedModifiers = currentModifiers;
            AzureRegion = azureRegion;
            AzureKey = azureKey;
            this.includePunctuation = includePunctuation;
            this.runAtStartup = runAtStartup;
            currentKey = currentHotkey;
            this.currentModifiers = currentModifiers;
            lastKey = currentHotkey;
            lastModifiers = currentModifiers;

            // Load custom icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "icon.png");
                if (File.Exists(iconPath))
                {
                    using (var bitmap = new Bitmap(iconPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        this.Icon = Icon.FromHandle(hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load icon for settings form: {ex.Message}");
            }

            InitializeComponents();
            LoadLanguages();
            UpdateHotkeyDisplay();
        }

        private void InitializeComponents()
        {
            this.Text = "VoiceTyper Settings";
            this.Size = new Size(400, 380);  // Increased height to accommodate new checkboxes
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Padding = new Padding(20);

            // Title
            titleLabel = new Label
            {
                Text = "VoiceTyper Settings",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(360, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Language selection
            languageLabel = new Label
            {
                Text = "Language:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 70),
                Size = new Size(100, 25)
            };

            languageComboBox = new ComboBox
            {
                Location = new Point(130, 70),
                Size = new Size(180, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10)
            };

            resetLanguageButton = new Button
            {
                Text = "Reset",
                Location = new Point(320, 70),
                Size = new Size(60, 25),
                Font = new Font("Segoe UI", 8),
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat
            };

            // Hotkey information
            hotkeyLabel = new Label
            {
                Text = "Hotkey:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 115),
                Size = new Size(100, 25)
            };

            hotkeyValueLabel = new Label
            {
                Text = "Ctrl + Shift + /",
                Font = new Font("Segoe UI", 10),
                Location = new Point(130, 115),
                Size = new Size(150, 25)
            };

            hotkeyButton = new Button
            {
                Text = "Set Hotkey",
                Location = new Point(290, 115),
                Size = new Size(90, 25),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat
            };

            resetHotkeyButton = new Button
            {
                Text = "Reset",
                Location = new Point(320, 150),
                Size = new Size(60, 25),
                Font = new Font("Segoe UI", 8),
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat
            };

            // Azure Region configuration
            azureRegionLabel = new Label
            {
                Text = "Azure Region:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 160),
                Size = new Size(100, 25)
            };

            azureRegionTextBox = new TextBox
            {
                Text = AzureRegion,
                Font = new Font("Segoe UI", 10),
                Location = new Point(130, 160),
                Size = new Size(180, 25)
            };

            // Azure Key configuration
            azureKeyLabel = new Label
            {
                Text = "Azure Key:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 195),
                Size = new Size(100, 25)
            };

            azureKeyTextBox = new TextBox
            {
                Text = AzureKey,
                Font = new Font("Segoe UI", 10),
                Location = new Point(130, 195),
                Size = new Size(180, 25),
                PasswordChar = '•'  // Mask the key for security
            };

            // Add validation message label
            validationLabel = new Label
            {
                Text = "Please enter valid Azure settings",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9),
                Location = new Point(130, 225),
                Size = new Size(200, 20),
                Visible = false
            };

            // Add punctuation checkbox
            punctuationCheckBox = new CheckBox
            {
                Text = "Include punctuation",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 260),
                Size = new Size(200, 25),
                Checked = includePunctuation
            };

            // Add startup checkbox
            startupCheckBox = new CheckBox
            {
                Text = "Run at Startup",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 290),
                Size = new Size(200, 25),
                Checked = runAtStartup
            };

            // Add controls
            this.Controls.AddRange(new Control[] {
                titleLabel,
                languageLabel, languageComboBox, resetLanguageButton,
                hotkeyLabel, hotkeyValueLabel, hotkeyButton, resetHotkeyButton,
                azureRegionLabel, azureRegionTextBox,
                azureKeyLabel, azureKeyTextBox,
                validationLabel,
                punctuationCheckBox,
                startupCheckBox
            });

            // Event handlers
            resetLanguageButton.Click += (s, e) =>
            {
                SelectedLanguage = DEFAULT_LANGUAGE;
                foreach (var item in languageComboBox.Items)
                {
                    if (item.ToString()?.Contains("Cantonese") == true)
                    {
                        languageComboBox.SelectedItem = item;
                        break;
                    }
                }
                LanguageChanged?.Invoke(SelectedLanguage);
            };

            resetHotkeyButton.Click += (s, e) =>
            {
                currentKey = DEFAULT_HOTKEY;
                currentModifiers = DEFAULT_MODIFIERS;
                UpdateHotkeyDisplay();
                SelectedHotkey = currentKey;
                SelectedModifiers = currentModifiers;
                HotkeyChanged?.Invoke(SelectedHotkey, SelectedModifiers);
            };

            hotkeyButton.Click += (s, e) =>
            {
                if (isCapturingHotkey)
                {
                    // Cancel the capture
                    currentKey = lastKey;
                    currentModifiers = lastModifiers;
                    UpdateHotkeyDisplay();
                    StopHotkeyCapture();
                }
                else
                {
                    StartHotkeyCapture();
                }
            };

            languageComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (languageComboBox.SelectedItem != null)
                {
                    string selectedDisplayName = languageComboBox.SelectedItem.ToString() ?? "";
                    SelectedLanguage = languages.FirstOrDefault(x => x.Value == selectedDisplayName).Key;
                    LanguageChanged?.Invoke(SelectedLanguage);
                }
            };

            // Add event handlers for Azure configuration changes
            azureRegionTextBox.TextChanged += (s, e) =>
            {
                AzureRegion = azureRegionTextBox.Text;
                ValidateAzureConfig();
                validationLabel.Visible = !isAzureConfigValid;
                AzureConfigChanged?.Invoke(AzureRegion, AzureKey);
            };

            azureKeyTextBox.TextChanged += (s, e) =>
            {
                AzureKey = azureKeyTextBox.Text;
                ValidateAzureConfig();
                validationLabel.Visible = !isAzureConfigValid;
                AzureConfigChanged?.Invoke(AzureRegion, AzureKey);
            };

            // Initialize the key timer
            keyTimer = new System.Windows.Forms.Timer();
            keyTimer.Interval = 500; // 500ms delay
            keyTimer.Tick += (s, e) => 
            {
                keyTimer?.Stop();
                if (isCapturingHotkey)
                {
                    ConfirmHotkeyCombination();
                }
            };

            // Add event handler for punctuation checkbox
            punctuationCheckBox.CheckedChanged += (s, e) =>
            {
                PunctuationChanged?.Invoke(punctuationCheckBox.Checked);
            };

            // Add event handler for startup checkbox
            startupCheckBox.CheckedChanged += (s, e) =>
            {
                StartupChanged?.Invoke(startupCheckBox.Checked);
            };
        }

        private void StartHotkeyCapture()
        {
            isCapturingHotkey = true;
            HotkeyCapturingStateChanged?.Invoke(true);
            pressedKeys.Clear();
            lastKey = currentKey;
            lastModifiers = currentModifiers;
            hotkeyValueLabel.Text = "Press key combination...";
            hotkeyButton.Text = "Cancel";
            hotkeyButton.BackColor = Color.FromArgb(255, 200, 200); // Light red for cancel
            this.KeyPreview = true;
            this.KeyDown += SettingsForm_KeyDown;
            this.KeyUp += SettingsForm_KeyUp;
        }

        private void SettingsForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!isCapturingHotkey) return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                // Cancel and restore previous hotkey
                currentKey = lastKey;
                currentModifiers = lastModifiers;
                UpdateHotkeyDisplay();
                StopHotkeyCapture();
                return;
            }

            // Add the key to pressed keys
            pressedKeys.Add(e.KeyCode);

            // Update the display
            UpdateHotkeyDisplayFromPressedKeys();

            // Check if we have a valid combination
            var mainKey = pressedKeys.FirstOrDefault(k => 
                k != Keys.ControlKey && k != Keys.ShiftKey && k != Keys.Alt);

            if (mainKey != Keys.None)
            {
                // Set modifiers
                var newModifiers = 0;
                if (pressedKeys.Contains(Keys.ControlKey)) newModifiers |= 0x0002;
                if (pressedKeys.Contains(Keys.ShiftKey)) newModifiers |= 0x0004;
                if (pressedKeys.Contains(Keys.Alt)) newModifiers |= 0x0001;

                // Validate the combination
                int modifierCount = 0;
                if ((newModifiers & 0x0002) != 0) modifierCount++;
                if ((newModifiers & 0x0004) != 0) modifierCount++;
                if ((newModifiers & 0x0001) != 0) modifierCount++;

                if (modifierCount > 0)
                {
                    currentKey = mainKey;
                    currentModifiers = newModifiers;
                    SelectedHotkey = currentKey;
                    SelectedModifiers = currentModifiers;
                    UpdateHotkeyDisplay();
                    HotkeyChanged?.Invoke(SelectedHotkey, SelectedModifiers);
                    StopHotkeyCapture();
                }
            }
        }

        private void SettingsForm_KeyUp(object? sender, KeyEventArgs e)
        {
            if (!isCapturingHotkey) return;
            e.Handled = true;
            e.SuppressKeyPress = true;

            // Remove the key from pressed keys
            pressedKeys.Remove(e.KeyCode);

            // Update display with remaining pressed keys
            UpdateHotkeyDisplayFromPressedKeys();
        }

        private void ConfirmHotkeyCombination()
        {
            if (pressedKeys.Count > 0)
            {
                // Update the current key and modifiers
                var mainKey = pressedKeys.Last();
                var modifiers = 0;
                
                if (pressedKeys.Contains(Keys.ControlKey)) modifiers |= 0x0002;
                if (pressedKeys.Contains(Keys.ShiftKey)) modifiers |= 0x0004;
                if (pressedKeys.Contains(Keys.Menu)) modifiers |= 0x0001;

                if (modifiers != 0) // Only accept if at least one modifier is pressed
                {
                    currentKey = mainKey;
                    currentModifiers = modifiers;
                    SelectedHotkey = currentKey;
                    SelectedModifiers = currentModifiers;
                    HotkeyChanged?.Invoke(SelectedHotkey, SelectedModifiers);
                }
            }

            StopHotkeyCapture();
            UpdateHotkeyDisplay();
        }

        private HashSet<Keys> lastPressedKeys = new();

        private void UpdateHotkeyDisplayFromPressedKeys()
        {
            if (pressedKeys.Count > 0)
            {
                lastPressedKeys = new HashSet<Keys>(pressedKeys);
            }

            string modifiers = "";
            if (pressedKeys.Contains(Keys.ControlKey)) modifiers += "Ctrl + ";
            if (pressedKeys.Contains(Keys.ShiftKey)) modifiers += "Shift + ";
            if (pressedKeys.Contains(Keys.Alt)) modifiers += "Alt + ";

            // Get the main key
            var mainKey = pressedKeys.FirstOrDefault(k => 
                k != Keys.ControlKey && k != Keys.ShiftKey && k != Keys.Alt);

            if (mainKey != Keys.None)
            {
                string keyName = FormatKeyName(mainKey);
                hotkeyValueLabel.Text = modifiers + keyName;
                }
                else
                {
                hotkeyValueLabel.Text = modifiers.TrimEnd(' ', '+');
            }
        }

        private string FormatKeyName(Keys key)
        {
            string keyName = key.ToString();
            if (keyName.StartsWith("Oem"))
            {
                keyName = keyName.Replace("Oem", "");
                if (keyName == "Question") keyName = "/";
                else if (keyName == "Period") keyName = ".";
                else if (keyName == "Comma") keyName = ",";
                else if (keyName == "Minus") keyName = "-";
                else if (keyName == "Plus") keyName = "+";
            }
            return keyName;
        }

        private void StopHotkeyCapture()
        {
            isCapturingHotkey = false;
            HotkeyCapturingStateChanged?.Invoke(false);
            hotkeyButton.Text = "Set Hotkey";
            hotkeyButton.BackColor = Color.FromArgb(240, 240, 240); // Reset to original color
            this.KeyPreview = false;
            this.KeyDown -= SettingsForm_KeyDown;
            this.KeyUp -= SettingsForm_KeyUp;
            keyTimer?.Stop();
            pressedKeys.Clear();
        }

        private void UpdateHotkeyDisplay()
        {
            string modifiers = "";
            if ((currentModifiers & 0x0002) != 0) modifiers += "Ctrl + ";
            if ((currentModifiers & 0x0004) != 0) modifiers += "Shift + ";
            if ((currentModifiers & 0x0001) != 0) modifiers += "Alt + ";

            hotkeyValueLabel.Text = modifiers + FormatKeyName(currentKey);
        }

        private void LoadLanguages()
        {
            foreach (var lang in languages)
            {
                languageComboBox.Items.Add(lang.Value);
            }

            // Set the selected language
            string displayName = languages[SelectedLanguage];
            languageComboBox.SelectedItem = displayName;
        }

        public bool ValidateAzureConfig()
        {
            bool isValid = true;
            Color errorColor = Color.FromArgb(255, 230, 230); // Light red
            Color normalColor = Color.White;

            // Validate region
            if (string.IsNullOrWhiteSpace(AzureRegion))
            {
                azureRegionTextBox.BackColor = errorColor;
                isValid = false;
            }
            else
            {
                azureRegionTextBox.BackColor = normalColor;
            }

            // Validate key
            if (string.IsNullOrWhiteSpace(AzureKey))
            {
                azureKeyTextBox.BackColor = errorColor;
                isValid = false;
            }
            else
            {
                azureKeyTextBox.BackColor = normalColor;
            }

            isAzureConfigValid = isValid;
            return isValid;
        }
    }
} 