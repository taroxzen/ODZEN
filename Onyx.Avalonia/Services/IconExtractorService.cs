// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;

namespace Onyx.Avalonia.Services
{
    public static class IconExtractorService
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
        private static readonly string _iconCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ONYX", "icons");

        static IconExtractorService()
        {
            try
            {
                if (!Directory.Exists(_iconCacheDir))
                {
                    Directory.CreateDirectory(_iconCacheDir);
                }
            }
            catch { }
        }

        public static Bitmap? GetIconForExecutable(string? exePath, string gameId)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return null;

            if (_cache.TryGetValue(gameId, out var cached))
            {
                return cached;
            }

            string cachedPng = Path.Combine(_iconCacheDir, $"{SanitizeFileName(gameId)}.png");
            if (File.Exists(cachedPng))
            {
                try
                {
                    using var stream = File.OpenRead(cachedPng);
                    var bmp = new Bitmap(stream);
                    _cache[gameId] = bmp;
                    return bmp;
                }
                catch { }
            }

            if (File.Exists(exePath))
            {
                IntPtr hIcon = IntPtr.Zero;
                try
                {
                    hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                    if (hIcon != IntPtr.Zero)
                    {
                        using var sysIcon = System.Drawing.Icon.FromHandle(hIcon);
                        using var sysBmp = sysIcon.ToBitmap();
                        using var ms = new MemoryStream();
                        
                        sysBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        
                        try
                        {
                            sysBmp.Save(cachedPng, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch { }

                        ms.Position = 0;
                        var avBmp = new Bitmap(ms);
                        _cache[gameId] = avBmp;
                        return avBmp;
                    }
                }
                catch { }
                finally
                {
                    if (hIcon != IntPtr.Zero)
                    {
                        try { DestroyIcon(hIcon); } catch { }
                    }
                }
            }

            return null;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
