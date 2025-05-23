using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

namespace VoiceTyper;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Check for existing instance
        bool createdNew;
        using (Mutex mutex = new Mutex(true, "VoiceTyperApplicationMutex", out createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show("VoiceTyper is already running.\nYou can find it in the system tray.", 
                    "VoiceTyper", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // Initialize application configuration
            AppSettings settings = AppSettings.Load();

            // Set up icon paths
            string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "icon.png");
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "icon.ico");

            try
            {
                // Check if PNG exists
                if (!File.Exists(pngPath))
                {
                    Console.WriteLine("Error: icon.png not found in the image directory.");
                    return;
                }

                // Convert PNG to ICO if needed
                if (!File.Exists(icoPath) || File.GetLastWriteTime(pngPath) > File.GetLastWriteTime(icoPath))
                {
                    Console.WriteLine("Converting PNG to ICO...");
                    if (Path.GetDirectoryName(icoPath) is string directory)
                    {
                        Directory.CreateDirectory(directory);
                    }
                    IconConverter.ConvertToIco(pngPath, icoPath);
                    Console.WriteLine("ICO file created successfully.");
                }

                // Load the icon
                using (var icon = new Icon(icoPath))
                {
                    var mainForm = new MainForm { Icon = icon };
                    mainForm.LoadSettings(settings);
                    Application.Run(mainForm);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading/creating icon: {ex.Message}");
                // Fallback: try to load PNG directly if ICO creation fails
                try
                {
                    using (var bitmap = new Bitmap(pngPath))
                    {
                        var mainForm = new MainForm { Icon = Icon.FromHandle(bitmap.GetHicon()) };
                        mainForm.LoadSettings(settings);
                        Application.Run(mainForm);
                    }
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Error loading fallback icon: {fallbackEx.Message}");
                    var mainForm = new MainForm();
                    mainForm.LoadSettings(settings);
                    Application.Run(mainForm);
                }
            }
        }
    }    
}