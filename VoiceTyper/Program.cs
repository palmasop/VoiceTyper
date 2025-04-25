using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace VoiceTyper;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        Icon? appIcon = null;
        try
        {
            string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "icon.png");
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image", "icon.ico");

            // Convert PNG to ICO if needed
            if (File.Exists(pngPath) && (!File.Exists(icoPath) || File.GetLastWriteTime(pngPath) > File.GetLastWriteTime(icoPath)))
            {
                using (var originalBitmap = new Bitmap(pngPath))
                {
                    // Create icon with multiple sizes for better quality
                    int[] sizes = new[] { 16, 32, 48, 64, 128 };
                    using (var stream = new FileStream(icoPath, FileMode.Create))
                    {
                        // Write ICO header
                        using (var writer = new BinaryWriter(stream))
                        {
                            // Write ICO header
                            writer.Write((short)0);      // Reserved
                            writer.Write((short)1);      // Type: 1 = ICO
                            writer.Write((short)sizes.Length);  // Number of images

                            // Calculate offset to image data
                            int offset = 6 + 16 * sizes.Length;

                            // Store bitmaps temporarily
                            var bitmaps = new List<Bitmap>();
                            var datas = new List<byte[]>();

                            // Create each size and write directory entries
                            foreach (int size in sizes)
                            {
                                var bitmap = new Bitmap(size, size);
                                using (var g = Graphics.FromImage(bitmap))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.DrawImage(originalBitmap, 0, 0, size, size);
                                }
                                bitmaps.Add(bitmap);

                                using (var ms = new MemoryStream())
                                {
                                    bitmap.Save(ms, ImageFormat.Png);
                                    var data = ms.ToArray();
                                    datas.Add(data);

                                    // Write directory entry
                                    writer.Write((byte)size);  // Width
                                    writer.Write((byte)size);  // Height
                                    writer.Write((byte)0);     // Color palette
                                    writer.Write((byte)0);     // Reserved
                                    writer.Write((short)1);    // Color planes
                                    writer.Write((short)32);   // Bits per pixel
                                    writer.Write((int)data.Length); // Size of image data
                                    writer.Write((int)offset); // Offset to image data

                                    offset += data.Length;
                                }
                            }

                            // Write image data
                            foreach (var data in datas)
                            {
                                writer.Write(data);
                            }

                            // Clean up bitmaps
                            foreach (var bitmap in bitmaps)
                            {
                                bitmap.Dispose();
                            }
                        }
                    }
                }
            }

            // Load the ICO file
            if (File.Exists(icoPath))
            {
                appIcon = new Icon(icoPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load/create application icon: {ex.Message}");
        }

        var mainForm = new MainForm();
        if (appIcon != null)
        {
            mainForm.Icon = appIcon;
        }
        
        Application.Run(mainForm);
    }    
}