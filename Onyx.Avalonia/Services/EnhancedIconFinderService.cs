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
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;

namespace Onyx.Avalonia.Services
{
    public static class EnhancedIconFinderService
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
        private static readonly string _iconCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ONYX", "icons");

        private static readonly string[] CommonSteamPaths = {
            @"C:\Program Files (x86)\Steam\steam\games",
            @"C:\Program Files\Steam\steam\games",
            @"D:\SteamLibrary\steamapps\common",
            @"E:\SteamLibrary\steamapps\common",
            @"D:\Steam\steamapps\common",
            @"E:\Steam\steamapps\common"
        };

        private static readonly string[] CommonXboxPaths = {
            @"E:\XboxGames",
            @"D:\XboxGames",
            @"C:\XboxGames",
            @"C:\Program Files\WindowsApps"
        };

        static EnhancedIconFinderService()
        {
            try
            {
                if (!Directory.Exists(_iconCacheDir))
                    Directory.CreateDirectory(_iconCacheDir);
            }
            catch { }
        }

        public static Bitmap? FindGameIcon(string? exePath, string? installPath, string? storeId, string platform, string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;

            if (_cache.TryGetValue(gameId, out var cached))
            {
                return cached;
            }

            // 1. Check local PNG cache
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

            // 2. Direct Executable extraction
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                var bmp = ExtractIconFromExe(exePath, cachedPng);
                if (bmp != null)
                {
                    _cache[gameId] = bmp;
                    return bmp;
                }
            }

            // 3. Search inside InstallPath (.exe, .ico, .png in root and subdirectories)
            if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
            {
                try
                {
                    var icoFiles = Directory.GetFiles(installPath, "*.ico", SearchOption.AllDirectories);
                    foreach (var ico in icoFiles)
                    {
                        var bmp = LoadBitmapAndCache(ico, cachedPng);
                        if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                    }

                    // UWP / Xbox Logo PNGs
                    var pngFiles = Directory.GetFiles(installPath, "*Logo*.png", SearchOption.AllDirectories);
                    foreach (var png in pngFiles)
                    {
                        var bmp = LoadBitmapAndCache(png, cachedPng);
                        if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                    }

                    var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories);
                    foreach (var exe in exeFiles)
                    {
                        string fname = Path.GetFileName(exe).ToLowerInvariant();
                        if (fname.Contains("unins") || fname.Contains("crash") || fname.Contains("helper") || fname.Contains("setup") || fname.Contains("installer") || fname.Contains("redist"))
                            continue;

                        var bmp = ExtractIconFromExe(exe, cachedPng);
                        if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                    }
                }
                catch { }
            }

            // 4. Platform specific lookups (Minecraft, Metin2, Steam, Xbox)
            string lowId = gameId.ToLowerInvariant();

            // Minecraft
            if (platform.Equals("minecraft", StringComparison.OrdinalIgnoreCase) || lowId.Contains("minecraft"))
            {
                string assetIco = GetAssetPath("minecraft.ico");
                if (File.Exists(assetIco))
                {
                    var bmp = LoadBitmapAndCache(assetIco, cachedPng);
                    if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                }
            }

            // Metin2 / RinaMT2
            if (platform.Equals("metin2", StringComparison.OrdinalIgnoreCase) || lowId.Contains("metin2") || lowId.Contains("rinamt2"))
            {
                // Check if there is a patcher or metin2 in D:\Oyunlar
                string[] metin2Dirs = { @"D:\Oyunlar\RinaMT2_TestServer", @"D:\Oyunlar\RinaMT2", @"C:\Program Files (x86)\Metin2" };
                foreach (var mDir in metin2Dirs)
                {
                    if (Directory.Exists(mDir))
                    {
                        foreach (var exe in Directory.GetFiles(mDir, "*.exe", SearchOption.TopDirectoryOnly))
                        {
                            var bmp = ExtractIconFromExe(exe, cachedPng);
                            if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                        }
                    }
                }

                string assetIco = GetAssetPath("metin2.ico");
                if (File.Exists(assetIco))
                {
                    var bmp = LoadBitmapAndCache(assetIco, cachedPng);
                    if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                }
            }

            // Steam AppId (CS2=730, etc.)
            if (platform.Equals("steam", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(storeId))
            {
                string sId = storeId ?? "";
                if (string.IsNullOrEmpty(sId) && lowId.Contains("cs2")) sId = "730";

                foreach (var steamDir in CommonSteamPaths)
                {
                    string steamIco = Path.Combine(steamDir, $"{sId}.ico");
                    if (File.Exists(steamIco))
                    {
                        var bmp = LoadBitmapAndCache(steamIco, cachedPng);
                        if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                    }
                }
            }

            // Xbox / GamePass Deep Drive Search
            if (platform.Equals("xbox", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var xDir in CommonXboxPaths)
                {
                    if (!Directory.Exists(xDir)) continue;
                    try
                    {
                        var matches = Directory.GetDirectories(xDir, $"*{SanitizeFileName(gameId)}*", SearchOption.TopDirectoryOnly);
                        foreach (var match in matches)
                        {
                            foreach (var exe in Directory.GetFiles(match, "*.exe", SearchOption.AllDirectories))
                            {
                                var bmp = ExtractIconFromExe(exe, cachedPng);
                                if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                            }
                        }
                    }
                    catch { }
                }

                string assetIco = GetAssetPath("xbox.ico");
                if (File.Exists(assetIco))
                {
                    var bmp = LoadBitmapAndCache(assetIco, cachedPng);
                    if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                }
            }

            // 5. Desktop Shortcuts Search (.lnk)
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

                foreach (var dPath in new[] { desktop, commonDesktop })
                {
                    if (!Directory.Exists(dPath)) continue;
                    var lnks = Directory.GetFiles(dPath, "*.lnk");
                    foreach (var lnk in lnks)
                    {
                        string lName = Path.GetFileNameWithoutExtension(lnk).ToLowerInvariant();
                        if (lowId.Contains(lName) || lName.Contains(lowId))
                        {
                            var bmp = ExtractIconFromExe(lnk, cachedPng);
                            if (bmp != null) { _cache[gameId] = bmp; return bmp; }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static Bitmap? ExtractIconFromExe(string fileOrLnk, string cacheTarget)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                hIcon = ExtractIcon(IntPtr.Zero, fileOrLnk, 0);
                if (hIcon != IntPtr.Zero)
                {
                    using var sysIcon = System.Drawing.Icon.FromHandle(hIcon);
                    using var sysBmp = sysIcon.ToBitmap();
                    using var ms = new MemoryStream();

                    sysBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    try { sysBmp.Save(cacheTarget, System.Drawing.Imaging.ImageFormat.Png); } catch { }

                    ms.Position = 0;
                    return new Bitmap(ms);
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
            return null;
        }

        private static Bitmap? LoadBitmapAndCache(string srcIcoOrPng, string cacheTarget)
        {
            try
            {
                if (srcIcoOrPng.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var sysIcon = new System.Drawing.Icon(srcIcoOrPng, 128, 128);
                    using var sysBmp = sysIcon.ToBitmap();
                    using var ms = new MemoryStream();
                    sysBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    try { sysBmp.Save(cacheTarget, System.Drawing.Imaging.ImageFormat.Png); } catch { }
                    ms.Position = 0;
                    return new Bitmap(ms);
                }
                else
                {
                    using var stream = File.OpenRead(srcIcoOrPng);
                    return new Bitmap(stream);
                }
            }
            catch { }
            return null;
        }

        private static string GetAssetPath(string fileName)
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
            if (File.Exists(local)) return local;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";
            string clean = Regex.Replace(name, @"[^a-zA-Z0-9_\-]", "_").Trim('_');
            if (string.IsNullOrEmpty(clean))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(name))).ToLowerInvariant();
            }
            return clean;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
