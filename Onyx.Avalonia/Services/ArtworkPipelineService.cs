// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Onyx.Avalonia.Services
{
    public static class ArtworkPipelineService
    {
        public static bool IsOnlineDownloadEnabled { get; set; } = true;

        private static readonly ConcurrentDictionary<string, Bitmap?> _runtimeCache = new();
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ONYX", "open_artwork");

        private static readonly string[] SteamLibraryCachePaths = {
            @"C:\Program Files (x86)\Steam\appcache\librarycache",
            @"C:\Program Files\Steam\appcache\librarycache",
            @"D:\Steam\appcache\librarycache",
            @"E:\Steam\appcache\librarycache",
            @"D:\SteamLibrary\appcache\librarycache",
            @"E:\SteamLibrary\appcache\librarycache"
        };

        private static readonly string[] SteamGamesIconPaths = {
            @"C:\Program Files (x86)\Steam\steam\games",
            @"C:\Program Files\Steam\steam\games",
            @"D:\SteamLibrary\steam\games",
            @"E:\SteamLibrary\steam\games"
        };

        private static readonly string[] XboxBasePaths = {
            @"E:\XboxGames",
            @"D:\XboxGames",
            @"C:\XboxGames",
            @"C:\Program Files\WindowsApps"
        };

        static ArtworkPipelineService()
        {
            try
            {
                if (!Directory.Exists(StorageDir))
                    Directory.CreateDirectory(StorageDir);
            }
            catch { }
        }

        public static void ClearCache()
        {
            _runtimeCache.Clear();
        }

        public static Bitmap? ResolveLocalSystemArtwork(string? exePath, string? installPath, string? storeId, string platform, string gameId, string gameName)
        {
            string targetLocalPng = Path.Combine(StorageDir, $"{Sanitize(gameId)}_local.png");
            if (File.Exists(targetLocalPng))
            {
                try
                {
                    using var stream = File.OpenRead(targetLocalPng);
                    return new Bitmap(stream);
                }
                catch { }
            }

            string? localSource = FindLocalPCHighResFile(exePath, installPath, storeId, platform, gameId, gameName);
            if (!string.IsNullOrEmpty(localSource) && File.Exists(localSource))
            {
                var processedBmp = ProcessAndCenterImage(localSource, targetLocalPng);
                if (processedBmp != null) return processedBmp;
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var jumboBmp = ExtractAndCenterJumboIcon(exePath, targetLocalPng);
                if (jumboBmp != null) return jumboBmp;
            }

            string? builtIn = MatchBuiltInArtworkBank(platform, gameId, gameName);
            if (!string.IsNullOrEmpty(builtIn) && File.Exists(builtIn))
            {
                return ProcessAndCenterImage(builtIn, targetLocalPng);
            }

            return null;
        }

        public static Bitmap? ResolveArtwork(string? exePath, string? installPath, string? storeId, string platform, string gameId, string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;

            if (_runtimeCache.TryGetValue(gameId, out var cached))
            {
                return cached;
            }

            string targetPng = Path.Combine(StorageDir, $"{Sanitize(gameId)}.png");

            // 1. OPEN ARTWORK PIPELINE (Steam Store Open Search + DuckDuckGo Instant API)
            if (IsOnlineDownloadEnabled)
            {
                if (OpenArtworkPipelineEngine.HasLogo(gameId))
                {
                    var logoBmp = OpenArtworkPipelineEngine.LoadLogo(gameId);
                    if (logoBmp != null)
                    {
                        _runtimeCache[gameId] = logoBmp;
                        return logoBmp;
                    }
                }
                else
                {
                    OpenArtworkPipelineEngine.QueueDownload(gameId, gameName, platform, storeId, () =>
                    {
                        _runtimeCache.TryRemove(gameId, out _);
                    });
                }
            }

            // 2. CHECK PERSISTENT CACHE
            if (File.Exists(targetPng))
            {
                try
                {
                    using var stream = File.OpenRead(targetPng);
                    var bmp = new Bitmap(stream);
                    _runtimeCache[gameId] = bmp;
                    return bmp;
                }
                catch { }
            }

            // 3. LOCAL SYSTEM FALLBACK (Steam Library Cache, Xbox Manifest, Local Executable)
            string targetLocalPng = Path.Combine(StorageDir, $"{Sanitize(gameId)}_local.png");
            if (File.Exists(targetLocalPng))
            {
                try
                {
                    using var stream = File.OpenRead(targetLocalPng);
                    var bmp = new Bitmap(stream);
                    _runtimeCache[gameId] = bmp;
                    return bmp;
                }
                catch { }
            }

            string? localSource = FindLocalPCHighResFile(exePath, installPath, storeId, platform, gameId, gameName);
            if (!string.IsNullOrEmpty(localSource) && File.Exists(localSource))
            {
                var processedBmp = ProcessAndCenterImage(localSource, targetLocalPng);
                if (processedBmp != null)
                {
                    _runtimeCache[gameId] = processedBmp;
                    return processedBmp;
                }
            }

            // 4. JUMBO EXE ICON (256x256 from exe)
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                string exeName = Path.GetFileName(exePath).ToLowerInvariant();
                if (!exeName.Contains("dxwebsetup") && !exeName.Contains("unins") && !exeName.Contains("setup"))
                {
                    var jumboBmp = ExtractAndCenterJumboIcon(exePath, targetLocalPng);
                    if (jumboBmp != null)
                    {
                        _runtimeCache[gameId] = jumboBmp;
                        return jumboBmp;
                    }
                }
            }

            // 5. PLATFORM BUILT-IN FALLBACK
            string? builtInAsset = MatchBuiltInArtworkBank(platform, gameId, gameName);
            if (!string.IsNullOrEmpty(builtInAsset) && File.Exists(builtInAsset))
            {
                var processedBmp = ProcessAndCenterImage(builtInAsset, targetLocalPng);
                if (processedBmp != null)
                {
                    _runtimeCache[gameId] = processedBmp;
                    return processedBmp;
                }
            }

            return null;
        }

        private static string? FindLocalPCHighResFile(string? exePath, string? installPath, string? storeId, string platform, string gameId, string gameName)
        {
            string normPlat = platform.ToLowerInvariant();
            string normName = gameName.ToLowerInvariant();

            // Steam
            if (normPlat == "steam" || !string.IsNullOrWhiteSpace(storeId))
            {
                string sId = storeId ?? "";
                if (string.IsNullOrEmpty(sId) && (normName.Contains("counter-strike") || normName.Contains("cs2"))) sId = "730";
                if (normName.Contains("marvel rivals")) sId = "2767030";
                if (normName.Contains("rainbow six") || normName.Contains("siege")) sId = "359550";
                if (normName.Contains("project zomboid")) sId = "108600";

                if (!string.IsNullOrEmpty(sId))
                {
                    foreach (var scPath in SteamLibraryCachePaths)
                    {
                        if (!Directory.Exists(scPath)) continue;
                        string logoPng = Path.Combine(scPath, $"{sId}_logo.png");
                        if (File.Exists(logoPng)) return logoPng;

                        string headerJpg = Path.Combine(scPath, $"{sId}_header.jpg");
                        if (File.Exists(headerJpg)) return headerJpg;
                    }

                    foreach (var sPath in SteamGamesIconPaths)
                    {
                        if (!Directory.Exists(sPath)) continue;
                        string ico = Path.Combine(sPath, $"{sId}.ico");
                        if (File.Exists(ico)) return ico;
                    }
                }
            }

            // Xbox
            if (normPlat == "xbox")
            {
                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                {
                    try
                    {
                        var pngs = Directory.GetFiles(installPath, "*Logo*.png", SearchOption.AllDirectories)
                            .OrderByDescending(f => new FileInfo(f).Length)
                            .ToList();
                        if (pngs.Count > 0) return pngs[0];
                    }
                    catch { }
                }

                foreach (var xDir in XboxBasePaths)
                {
                    if (!Directory.Exists(xDir)) continue;
                    try
                    {
                        var dirs = Directory.GetDirectories(xDir, $"*{Sanitize(gameName)}*", SearchOption.TopDirectoryOnly);
                        foreach (var d in dirs)
                        {
                            var pngs = Directory.GetFiles(d, "*Logo*.png", SearchOption.AllDirectories)
                                .OrderByDescending(f => new FileInfo(f).Length)
                                .ToList();
                            if (pngs.Count > 0) return pngs[0];
                        }
                    }
                    catch { }
                }
            }

            // Minecraft
            if (normPlat == "minecraft" || normName.Contains("minecraft"))
            {
                return @"d:\ONYX OYUN KÜTÜPHANESİ\Onyx.Avalonia\Assets\minecraft.ico";
            }

            // Metin2
            if (normPlat == "metin2" || normName.Contains("metin2") || normName.Contains("rinamt2"))
            {
                string[] possibleDirs = { installPath ?? "", @"D:\Oyunlar\RinaMT2_TestServer", @"D:\Oyunlar\RinaMT2" };
                foreach (var pDir in possibleDirs)
                {
                    if (string.IsNullOrEmpty(pDir) || !Directory.Exists(pDir)) continue;
                    try
                    {
                        var icos = Directory.GetFiles(pDir, "*.ico", SearchOption.AllDirectories);
                        if (icos.Length > 0) return icos[0];

                        var pngs = Directory.GetFiles(pDir, "*logo*.png", SearchOption.AllDirectories);
                        if (pngs.Length > 0) return pngs[0];
                    }
                    catch { }
                }
                return @"d:\ONYX OYUN KÜTÜPHANESİ\Onyx.Avalonia\Assets\metin2.ico";
            }

            // Local folder
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                try
                {
                    var icos = Directory.GetFiles(installPath, "*.ico", SearchOption.TopDirectoryOnly);
                    if (icos.Length > 0) return icos[0];

                    var pngs = Directory.GetFiles(installPath, "*logo*.png", SearchOption.TopDirectoryOnly);
                    if (pngs.Length > 0) return pngs[0];
                }
                catch { }
            }

            return null;
        }

        private static string? MatchBuiltInArtworkBank(string platform, string gameId, string gameName)
        {
            string low = $"{gameId} {gameName}".ToLowerInvariant();
            string assetsDir = @"d:\ONYX OYUN KÜTÜPHANESİ\Onyx.Avalonia\Assets";

            if (low.Contains("minecraft")) return Path.Combine(assetsDir, "minecraft.ico");
            if (low.Contains("metin2") || low.Contains("rinamt2")) return Path.Combine(assetsDir, "metin2.ico");
            if (low.Contains("valorant") || low.Contains("riot")) return Path.Combine(assetsDir, "riot.ico");
            if (low.Contains("fortnite") || low.Contains("scope")) return Path.Combine(assetsDir, "epic.ico");
            if (low.Contains("fc 26") || low.Contains("fifa") || low.Contains("ea sports")) return Path.Combine(assetsDir, "ea.ico");
            if (low.Contains("rainbow six") || low.Contains("ubisoft")) return Path.Combine(assetsDir, "ubisoft.ico");
            if (low.Contains("gta") || low.Contains("rockstar")) return Path.Combine(assetsDir, "rockstar.ico");

            string platIco = Path.Combine(assetsDir, $"{platform.ToLowerInvariant()}.ico");
            if (File.Exists(platIco)) return platIco;

            return Path.Combine(assetsDir, "onyx_logo.ico");
        }

        private static Bitmap? ProcessAndCenterImage(string sourceFile, string targetPng)
        {
            try
            {
                SKBitmap? origBmp = null;
                if (sourceFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var sysIcon = new System.Drawing.Icon(sourceFile, 256, 256);
                    using var sysBmp = sysIcon.ToBitmap();
                    using var ms = new MemoryStream();
                    sysBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    using var codec = SKCodec.Create(ms);
                    origBmp = SKBitmap.Decode(codec);
                }
                else
                {
                    using var skData = SKData.Create(sourceFile);
                    using var codec = SKCodec.Create(skData);
                    origBmp = SKBitmap.Decode(codec);
                }

                if (origBmp != null)
                {
                    using (origBmp)
                    {
                        using var cropped = OpenArtworkPipelineEngine.AutoCropTransparentPixels(origBmp);
                        using var centered = OpenArtworkPipelineEngine.FitAndCenterToCanvas(cropped, 512, 280);

                        using var image = SKImage.FromBitmap(centered);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                        using var fs = new FileStream(targetPng, FileMode.Create);
                        data.SaveTo(fs);

                        data.AsStream().Position = 0;
                        return new Bitmap(data.AsStream());
                    }
                }
            }
            catch { }
            return null;
        }

        private static Bitmap? ExtractAndCenterJumboIcon(string exePath, string targetPng)
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
                    ms.Position = 0;
                    
                    using var codec = SKCodec.Create(ms);
                    using var origBmp = SKBitmap.Decode(codec);
                    if (origBmp != null)
                    {
                        using var cropped = OpenArtworkPipelineEngine.AutoCropTransparentPixels(origBmp);
                        using var centered = OpenArtworkPipelineEngine.FitAndCenterToCanvas(cropped, 512, 280);

                        using var image = SKImage.FromBitmap(centered);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                        using var fs = new FileStream(targetPng, FileMode.Create);
                        data.SaveTo(fs);

                        data.AsStream().Position = 0;
                        return new Bitmap(data.AsStream());
                    }
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

        private static string Sanitize(string name)
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
