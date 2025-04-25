using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Collections.Generic;
using WindowsInput = InputSimulatorStandard.InputSimulator;
using IInputSimulator = InputSimulatorStandard.IInputSimulator;
using System.Linq;
using Microsoft.Win32;
using System.Text.Json;

namespace VoiceTyper
{
    public partial class MainForm : Form
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_ALT = 0x0001;
        private const int HOTKEY_ID = 1;

        private Form? detectingTooltip;
        private bool isRecording = false;
        private SpeechRecognizer? recognizer;
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private IInputSimulator inputSimulator = new WindowsInput();
        private bool isGenerating = false;
        private DebugLogForm debugForm;
        private bool isHotkeyEnabled = true;
        private AppSettings settings;

        private readonly Dictionary<string, string> languageCodes = new Dictionary<string, string>
        {
            { "yue", "zh-HK" },  // Cantonese (Hong Kong)
            { "zh", "zh-CN" },   // Mandarin (Simplified)
            { "en", "en-US" }    // English (US)
        };

        private readonly Dictionary<string, string> languageDisplayNames = new Dictionary<string, string>
        {
            { "zh-HK", "Chinese (Cantonese)" },
            { "zh-CN", "Chinese (Mandarin)" },
            { "en-US", "English" }
        };

        private ToolStripMenuItem? languageMenu;
        private string subscriptionKey;
        private string region;

        [DllImport("user32.dll")]
        static extern bool GetCaretPos(out Point lpPoint);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        public MainForm()
        {
            // Initialize debug form first
            debugForm = new DebugLogForm();
            Console.SetOut(new FormConsoleWriter(debugForm));
            
            Console.WriteLine("=== VoiceTyper Starting ===");

            // Load settings
            settings = AppSettings.Load();

            // Initialize Azure settings from environment variables or settings
            subscriptionKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY") ?? settings.AzureSubscriptionKey;
            region = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION") ?? settings.AzureRegion;

            if (string.IsNullOrEmpty(subscriptionKey))
            {
                subscriptionKey = "03FWe8Fq7vsuY7xJUxl0pojB5ceX9EbC5jUtqPqfGOmQJqPWb9c2JQQJ99BDAC3pKaRXJ3w3AAAYACOGLi2u";
            }

            InitializeComponent();
            
            // Set window title
            this.Text = "VoiceTyper";
            
            // Completely hide the form
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.Opacity = 0;
            this.Size = new Size(1, 1);
            this.WindowState = FormWindowState.Minimized;

            // Initialize components
            InitializeTrayIcon();
            InitializeSpeechRecognizer();
            InitializeDetectingTooltip();

            try
            {
                bool hotkeyRegistered = RegisterHotKey(this.Handle, HOTKEY_ID, settings.HotkeyModifiers, (int)settings.Hotkey);
                if (!hotkeyRegistered)
                {
                    Console.WriteLine("Failed to register hotkey");
                    MessageBox.Show("Failed to register hotkey. The application will still work with the button.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                MessageBox.Show($"Error registering hotkey: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!this.IsHandleCreated)
            {
                CreateHandle();
                value = false;
            }
            base.SetVisibleCore(false);
        }

        private void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            
            // Create language menu first
            InitializeLanguageMenu();
            
            // Then add other menu items
            trayMenu.Items.Add("Settings", null, (s, e) => ShowSettings());
            trayMenu.Items.Add("Debug Logs", null, (s, e) => debugForm.Show());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, async (s, e) => 
            {
                // Ensure we properly cleanup and exit
                try 
                {
                    // First stop recognition
                    if (recognizer != null)
                    {
                        await recognizer.StopContinuousRecognitionAsync();
                    }

                    // Then unregister hotkey
                    UnregisterHotKey(this.Handle, HOTKEY_ID);
                    
                    // Then dispose recognizer
                    recognizer?.Dispose();

                    // Then dispose UI elements
                    trayIcon?.Dispose();

                    // Finally dispose debug form and exit
                    debugForm?.Dispose();

                    Application.Exit();
                }
                catch
                {
                    // Force exit if anything goes wrong
                    Environment.Exit(1);
                }
            });

            // Load custom icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "icon.png");
                Console.WriteLine($"Attempting to load icon from: {iconPath}");

                if (File.Exists(iconPath))
                {
                    using (var bitmap = new Bitmap(iconPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        var icon = Icon.FromHandle(hIcon);
                        trayIcon = new NotifyIcon()
                        {
                            Icon = icon,
                            ContextMenuStrip = trayMenu,
                            Visible = true,
                            Text = "VoiceTyper"
                        };

                        // Also set the form icon
                        this.Icon = icon;
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Icon file not found at: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load custom icon: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Fallback to system icon if custom icon fails to load
                trayIcon = new NotifyIcon()
                {
                    Icon = SystemIcons.Application,
                    ContextMenuStrip = trayMenu,
                    Visible = true,
                    Text = "VoiceTyper"
                };
            }

            // Add auto-close behavior to context menu
            trayMenu.LostFocus += (s, e) => 
            {
                // Only close if we're not hovering over any menu item
                if (!IsMouseOverMenu(trayMenu))
                {
                    trayMenu.Close();
                }
            };

            trayMenu.MouseLeave += (s, e) => 
            {
                // Add a small delay to allow clicking menu items
                Task.Delay(100).ContinueWith(t => 
                {
                    if (!IsMouseOverMenu(trayMenu))
                    {
                        this.BeginInvoke(new Action(() => trayMenu.Close()));
                    }
                });
            };

            // Double click to open settings
            trayIcon.DoubleClick += (s, e) => ShowSettings();
        }

        private bool IsMouseOverMenu(ToolStrip menu)
        {
            // Check main menu bounds
            if (menu.Bounds.Contains(Cursor.Position))
                return true;

            // Check all dropdown menus
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems && menuItem.DropDown.Visible)
                {
                    if (menuItem.DropDown.Bounds.Contains(Cursor.Position))
                        return true;
                }
            }

            return false;
        }

        private void UpdateLanguageMenuCheckedState(string selectedLanguage)
        {
            if (languageMenu?.DropDownItems == null) return;

            foreach (ToolStripMenuItem item in languageMenu.DropDownItems.OfType<ToolStripMenuItem>())
            {
                if (item.Tag is string langCode)
                {
                    item.Checked = languageCodes.TryGetValue(langCode, out string? azureCode) && azureCode == selectedLanguage;
                }
            }
        }

        public void ShowSettings()
        {
            // Stop voice capture if it's active
            if (isRecording)
            {
                StopAndTranscribe();
            }

            var settingsForm = new SettingsForm(
                settings.Language, 
                settings.Hotkey, 
                settings.HotkeyModifiers,
                settings.AzureRegion,
                settings.AzureSubscriptionKey,
                settings.IncludePunctuation,
                settings.RunAtStartup
            );
            
            // Handle settings changes
            settingsForm.LanguageChanged += (newLanguage) =>
            {
                if (settings.Language != newLanguage)
                {
                    settings.Language = newLanguage;
                    settings.Save();
                    UpdateLanguageMenuCheckedState(newLanguage);
                    recognizer?.Dispose();
                    InitializeSpeechRecognizer();
                }
            };

            settingsForm.HotkeyChanged += (newHotkey, newModifiers) =>
            {
                if (settings.Hotkey != newHotkey || settings.HotkeyModifiers != newModifiers)
                {
                    if (isHotkeyEnabled)
                    {
                        UnregisterHotKey(this.Handle, HOTKEY_ID);
                    }
                    settings.Hotkey = newHotkey;
                    settings.HotkeyModifiers = newModifiers;
                    settings.Save();
                    if (isHotkeyEnabled)
                    {
                        RegisterHotKey(this.Handle, HOTKEY_ID, settings.HotkeyModifiers, (int)settings.Hotkey);
                    }
                }
            };

            settingsForm.HotkeyCapturingStateChanged += (isCapturing) =>
            {
                if (isCapturing && isHotkeyEnabled)
                {
                    UnregisterHotKey(this.Handle, HOTKEY_ID);
                    isHotkeyEnabled = false;
                }
                else if (!isCapturing && !isHotkeyEnabled)
                {
                    RegisterHotKey(this.Handle, HOTKEY_ID, settings.HotkeyModifiers, (int)settings.Hotkey);
                    isHotkeyEnabled = true;
                }
            };

            settingsForm.AzureConfigChanged += (newRegion, newKey) =>
            {
                bool needRestart = false;

                if (settings.AzureRegion != newRegion)
                {
                    settings.AzureRegion = newRegion;
                    region = newRegion;
                    needRestart = true;
                }

                if (settings.AzureSubscriptionKey != newKey)
                {
                    settings.AzureSubscriptionKey = newKey;
                    subscriptionKey = newKey;
                    needRestart = true;
                }

                if (needRestart)
                {
                    settings.Save();
                    recognizer?.Dispose();
                    InitializeSpeechRecognizer();
                }
            };

            settingsForm.PunctuationChanged += (includePunctuation) =>
            {
                if (settings.IncludePunctuation != includePunctuation)
                {
                    settings.IncludePunctuation = includePunctuation;
                    settings.Save();
                    recognizer?.Dispose();
                    InitializeSpeechRecognizer();
                }
            };

            settingsForm.StartupChanged += (runAtStartup) =>
            {
                if (settings.RunAtStartup != runAtStartup)
                {
                    settings.RunAtStartup = runAtStartup;
                    settings.Save();
                    UpdateStartupRegistry(runAtStartup);
                }
            };

            settingsForm.Show();
        }

        private void UpdateStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    true))
                {
                    if (key != null)
                    {
                        string appPath = Application.ExecutablePath;
                        if (enable)
                        {
                            key.SetValue("VoiceTyper", appPath);
                        }
                        else
                        {
                            key.DeleteValue("VoiceTyper", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error managing startup settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeLanguageMenu()
        {
            languageMenu = new ToolStripMenuItem("Language");

            foreach (var lang in languageCodes)
            {
                string displayName = "Unknown";
                if (languageCodes.TryGetValue(lang.Key, out string? azureCode) && 
                    languageDisplayNames.TryGetValue(azureCode, out string? name))
                {
                    displayName = name;
                }

                var menuItem = new ToolStripMenuItem(displayName);
                menuItem.Tag = lang.Key;
                menuItem.Click += LanguageMenuItem_Click;
                languageMenu.DropDownItems.Add(menuItem);
            }

            // Add language menu at the beginning
            trayMenu?.Items.Insert(0, languageMenu);

            // Set initial checked state
            UpdateLanguageMenuCheckedState(settings.Language);
        }

        private void LanguageMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string langCode)
            {
                // Uncheck all items
                if (languageMenu?.DropDownItems != null)
                {
                    foreach (ToolStripMenuItem item in languageMenu.DropDownItems.OfType<ToolStripMenuItem>())
                    {
                        item.Checked = false;
                    }
                }

                // Check the selected item
                menuItem.Checked = true;

                if (languageCodes.TryGetValue(langCode, out string? azureCode))
                {
                    settings.Language = azureCode;
                    settings.Save();
                    recognizer?.Dispose();
                    InitializeSpeechRecognizer();
                }
            }
        }

        private void InitializeSpeechRecognizer()
        {
            try
            {
                Console.WriteLine("Initializing speech recognizer...");
                var config = SpeechConfig.FromSubscription(subscriptionKey, region);
                config.SpeechRecognitionLanguage = settings.Language;
                
                Console.WriteLine($"Using language: {settings.Language}");
                config.SetProperty("SpeechServiceConnection_LatencyOptimizationEnabled", "1");
                config.SetProperty("SpeechServiceConnection_ContinuousDictation", "1");
                
                // Configure punctuation settings
                config.SetServiceProperty("punctuation", 
                    settings.IncludePunctuation ? "automatic" : "explicit", 
                    ServicePropertyChannel.UriQueryParameter);

                var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                recognizer = new SpeechRecognizer(config, audioConfig);

                recognizer.Recognized += (s, e) =>
                {
                    Console.WriteLine($"Recognition result: {e.Result.Reason}");
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        var text = e.Result.Text;
                        // Get the raw recognition result without automatic formatting
                        var rawText = e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                        if (!string.IsNullOrEmpty(rawText))
                        {
                            try
                            {
                                // Parse the JSON to get the display text
                                var jsonDoc = System.Text.Json.JsonDocument.Parse(rawText);
                                if (jsonDoc.RootElement.TryGetProperty("DisplayText", out var displayText))
                                {
                                    text = displayText.GetString() ?? text;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing recognition result: {ex.Message}");
                            }
                        }

                        Console.WriteLine($"Recognized text: {text}");
                        if (!string.IsNullOrEmpty(text))
                        {
                            try
                            {
                                this.BeginInvoke(new Action(() => 
                                {
                                    Console.WriteLine($"Typing text: {text}");
                                    inputSimulator.Keyboard.TextEntry(text + " ");
                                    UpdateStatus(false);
                                }));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error typing text: {ex.Message}");
                            }
                        }
                    }
                };

                recognizer.Recognizing += (s, e) =>
                {
                    // Only log the interim result, don't type it
                    if (!string.IsNullOrEmpty(e.Result.Text))
                    {
                        Console.WriteLine($"Recognizing in progress: {e.Result.Text}");
                        this.BeginInvoke(new Action(() =>
                        {
                            UpdateStatus(true);
                        }));
                    }
                };

                recognizer.SessionStarted += (s, e) =>
                {
                    Console.WriteLine("Speech recognition session started");
                    this.BeginInvoke(new Action(() =>
                    {
                        UpdateStatus(false);
                    }));
                };

                recognizer.SessionStopped += (s, e) =>
                {
                    Console.WriteLine("Speech recognition session stopped");
                };

                recognizer.Canceled += (s, e) =>
                {
                    Console.WriteLine($"Speech recognition canceled: {e.ErrorCode} - {e.ErrorDetails}");
                };

                Console.WriteLine("Speech recognizer initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing speech recognizer: {ex.Message}");
                MessageBox.Show($"Error initializing speech recognizer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeDetectingTooltip()
        {
            detectingTooltip = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(400, 60),
                BackColor = Color.FromArgb(40, 40, 40),
                ShowInTaskbar = false,
                TopMost = true,
                Opacity = 0.9
            };

            var label = new Label
            {
                Text = GetStatusText(),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            detectingTooltip.Controls.Add(label);
            CenterTooltip();
        }

        private string GetStatusText()
        {
            string hotkeyText = "";
            
            // Add modifiers
            if ((settings.HotkeyModifiers & MOD_CONTROL) != 0) hotkeyText += "Ctrl+";
            if ((settings.HotkeyModifiers & MOD_SHIFT) != 0) hotkeyText += "Shift+";
            if ((settings.HotkeyModifiers & MOD_ALT) != 0) hotkeyText += "Alt+";
            
            // Add main key
            string keyText = settings.Hotkey.ToString();
            if (keyText.StartsWith("Oem"))
            {
                keyText = keyText.Replace("OemQuestion", "/")
                                .Replace("OemPeriod", ".")
                                .Replace("OemComma", ",")
                                .Replace("OemMinus", "-")
                                .Replace("OemPlus", "+");
            }
            hotkeyText += keyText;

            if (isGenerating)
            {
                return $"💭 Generating...\n(Press {hotkeyText} to stop)";
            }
            return $"🎙️ Speak to type\n(Press {hotkeyText} to stop)";
        }

        private void UpdateStatus(bool generating)
        {
            if (detectingTooltip?.Controls[0] is Label label)
            {
                isGenerating = generating;
                label.Text = GetStatusText();
            }
        }

        private void CenterTooltip()
        {
            if (detectingTooltip != null)
            {
                // Get the screen that contains the cursor
                Screen currentScreen = Screen.FromPoint(Cursor.Position);
                
                // Calculate center position
                int x = currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Width - detectingTooltip.Width) / 2;
                int y = currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Height - detectingTooltip.Height) / 2;
                
                detectingTooltip.Location = new Point(x, y);
            }
        }

        private async void StartVoiceInput()
        {
            // Validate Azure settings first
            if (string.IsNullOrWhiteSpace(settings.AzureSubscriptionKey) || string.IsNullOrWhiteSpace(settings.AzureRegion))
            {
                var result = MessageBox.Show(
                    "Azure Speech Service settings are not configured. Would you like to configure them now?",
                    "Configuration Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    var settingsForm = new SettingsForm(
                        settings.Language,
                        settings.Hotkey,
                        settings.HotkeyModifiers,
                        settings.AzureRegion,
                        settings.AzureSubscriptionKey,
                        settings.IncludePunctuation,
                        settings.RunAtStartup
                    );

                    // Highlight invalid fields
                    settingsForm.ValidateAzureConfig();
                    
                    // Handle settings changes
                    settingsForm.LanguageChanged += (newLanguage) =>
                    {
                        if (settings.Language != newLanguage)
                        {
                            settings.Language = newLanguage;
                            settings.Save();
                            UpdateLanguageMenuCheckedState(newLanguage);
                            recognizer?.Dispose();
                            InitializeSpeechRecognizer();
                        }
                    };

                    settingsForm.HotkeyChanged += (newHotkey, newModifiers) =>
                    {
                        if (settings.Hotkey != newHotkey || settings.HotkeyModifiers != newModifiers)
                        {
                            if (isHotkeyEnabled)
                            {
                                UnregisterHotKey(this.Handle, HOTKEY_ID);
                            }
                            settings.Hotkey = newHotkey;
                            settings.HotkeyModifiers = newModifiers;
                            settings.Save();
                            if (isHotkeyEnabled)
                            {
                                RegisterHotKey(this.Handle, HOTKEY_ID, settings.HotkeyModifiers, (int)settings.Hotkey);
                            }
                        }
                    };

                    settingsForm.HotkeyCapturingStateChanged += (isCapturing) =>
                    {
                        if (isCapturing && isHotkeyEnabled)
                        {
                            UnregisterHotKey(this.Handle, HOTKEY_ID);
                            isHotkeyEnabled = false;
                        }
                        else if (!isCapturing && !isHotkeyEnabled)
                        {
                            RegisterHotKey(this.Handle, HOTKEY_ID, settings.HotkeyModifiers, (int)settings.Hotkey);
                            isHotkeyEnabled = true;
                        }
                    };

                    settingsForm.AzureConfigChanged += (newRegion, newKey) =>
                    {
                        bool needRestart = false;

                        if (settings.AzureRegion != newRegion)
                        {
                            settings.AzureRegion = newRegion;
                            region = newRegion;
                            needRestart = true;
                        }

                        if (settings.AzureSubscriptionKey != newKey)
                        {
                            settings.AzureSubscriptionKey = newKey;
                            subscriptionKey = newKey;
                            needRestart = true;
                        }

                        if (needRestart)
                        {
                            settings.Save();
                            recognizer?.Dispose();
                            InitializeSpeechRecognizer();
                        }
                    };

                    settingsForm.PunctuationChanged += (includePunctuation) =>
                    {
                        if (settings.IncludePunctuation != includePunctuation)
                        {
                            settings.IncludePunctuation = includePunctuation;
                            settings.Save();
                            recognizer?.Dispose();
                            InitializeSpeechRecognizer();
                        }
                    };

                    settingsForm.StartupChanged += (runAtStartup) =>
                    {
                        if (settings.RunAtStartup != runAtStartup)
                        {
                            settings.RunAtStartup = runAtStartup;
                            settings.Save();
                            UpdateStartupRegistry(runAtStartup);
                        }
                    };

                    settingsForm.Show();
                }
                return;
            }

            if (recognizer == null)
            {
                MessageBox.Show("Error: Speech recognition not initialized", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            try
            {
                if (isRecording) return;
                
                Debug.WriteLine("Starting voice input...");
                isRecording = true;
                await recognizer.StartContinuousRecognitionAsync();
                Debug.WriteLine("Continuous recognition started");
                
                CenterTooltip();
                detectingTooltip?.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting recognition: {ex.Message}");
                MessageBox.Show($"Error starting recognition: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                isRecording = false;
            }
        }

        private async void StopAndTranscribe()
        {
            if (!isRecording || recognizer == null)
            {
                return;
            }
            
            try
            {
                await recognizer.StopContinuousRecognitionAsync();
                detectingTooltip?.Hide();
                isRecording = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping recognition: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                isRecording = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID && isHotkeyEnabled)
            {
                if (!isRecording)
                {
                    StartVoiceInput();
                }
                else
                {
                    StopAndTranscribe();
                }
            }
            base.WndProc(ref m);
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }

            // No need for cleanup here as it's handled in the Exit menu item
            // This prevents double-disposal issues
        }

        public void LoadSettings(AppSettings settings)
        {
            this.settings = settings;
            UpdateLanguageMenuCheckedState(settings.Language);

            // Check if app is in startup
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (key != null)
                    {
                        string? regValue = key.GetValue("VoiceTyper") as string;
                        settings.RunAtStartup = (regValue != null && regValue == Application.ExecutablePath);
                        settings.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking startup registry: {ex.Message}");
            }
        }

        private void ToggleStartup()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    true))
                {
                    if (key != null)
                    {
                        string appPath = Application.ExecutablePath;
                        string? currentValue = key.GetValue("VoiceTyper") as string;

                        if (currentValue == null)
                        {
                            // Add to startup
                            key.SetValue("VoiceTyper", appPath);
                            MessageBox.Show("VoiceTyper will now run at startup.", "Startup Enabled", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            // Remove from startup
                            key.DeleteValue("VoiceTyper", false);
                            MessageBox.Show("VoiceTyper will no longer run at startup.", "Startup Disabled", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error managing startup settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}