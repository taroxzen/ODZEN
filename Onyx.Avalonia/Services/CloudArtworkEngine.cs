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
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Onyx.Avalonia.Services
{
    public class CloudArtworkEngine
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly string CloudCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ONYX", "cloud_artwork");

        private static readonly ConcurrentDictionary<string, bool> _pendingDownloads = new();

        static CloudArtworkEngine()
        {
            try
            {
                if (!Directory.Exists(CloudCacheDir))
                    Directory.CreateDirectory(CloudCacheDir);

                _httpClient.MaxResponseContentBufferSize = 10 * 1024 * 1024;
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            }
            catch { }
        }

        public static string GetCloudArtworkPath(string gameId)
        {
            return Path.Combine(CloudCacheDir, $"{Sanitize(gameId)}.png");
        }

        public static bool HasCloudArtwork(string gameId)
        {
            return File.Exists(GetCloudArtworkPath(gameId));
        }

        public static Bitmap? LoadCloudArtwork(string gameId)
        {
            string path = GetCloudArtworkPath(gameId);
            if (File.Exists(path))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    return new Bitmap(stream);
                }
                catch { }
            }
            return null;
        }

        public static void QueueDownload(string gameId, string gameName, string platform, string? storeId, Action? onCompleted = null)
        {
            if (HasCloudArtwork(gameId)) return;
            if (!_pendingDownloads.TryAdd(gameId, true)) return;

            Task.Run(async () =>
            {
                try
                {
                    bool success = await DownloadGameArtworkAsync(gameId, gameName, platform, storeId);
                    if (success && onCompleted != null)
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(onCompleted);
                    }
                }
                finally
                {
                    _pendingDownloads.TryRemove(gameId, out _);
                }
            });
        }

        public static async Task<bool> DownloadGameArtworkAsync(string gameId, string gameName, string platform, string? storeId)
        {
            string targetPath = GetCloudArtworkPath(gameId);
            if (File.Exists(targetPath)) return true;

            var urlsToTry = GenerateTwitchAmazonArtworkUrls(gameName, platform, storeId);

            foreach (var url in urlsToTry)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        // Verify valid image (must not be Twitch 404 placeholder 430 bytes)
                        if (bytes.Length > 2048)
                        {
                            using var origStream = new MemoryStream(bytes);
                            using var codec = SKCodec.Create(origStream);
                            if (codec != null)
                            {
                                using var origBmp = SKBitmap.Decode(codec);
                                if (origBmp != null && origBmp.Width > 48 && origBmp.Height > 48)
                                {
                                    // Process into ultra-sharp 512px artwork
                                    int targetSize = Math.Max(256, Math.Min(512, Math.Max(origBmp.Width, origBmp.Height)));
                                    using var scaledBmp = origBmp.Resize(new SKImageInfo(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul), SKFilterQuality.High);

                                    using var image = SKImage.FromBitmap(scaledBmp ?? origBmp);
                                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                                    using var fs = new FileStream(targetPath, FileMode.Create);
                                    data.SaveTo(fs);
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return false;
        }

        private static List<string> GenerateTwitchAmazonArtworkUrls(string gameName, string platform, string? storeId)
        {
            var list = new List<string>();

            // Clean game names for Twitch directory matching
            string clean = CleanGameTitle(gameName);
            string encoded = Uri.EscapeDataString(clean);

            // 1. TWITCH / AMAZON OFFICIAL GAME CDN (High-Res BoxArt & Logos)
            list.Add($"https://static-cdn.jtvnw.net/ttv-boxart/{encoded}-570x760.jpg");
            list.Add($"https://static-cdn.jtvnw.net/ttv-boxart/{encoded}_IGDB-570x760.jpg");
            list.Add($"https://static-cdn.jtvnw.net/ttv-boxart/{encoded}-285x380.jpg");
            list.Add($"https://static-cdn.jtvnw.net/ttv-boxart/{encoded}_IGDB-285x380.jpg");

            // Raw clean variations without special characters
            string alphanumeric = Regex.Replace(clean, @"[^\w\s]", "").Trim();
            if (!string.Equals(alphanumeric, clean, StringComparison.OrdinalIgnoreCase))
            {
                string encAlpha = Uri.EscapeDataString(alphanumeric);
                list.Add($"https://static-cdn.jtvnw.net/ttv-boxart/{encAlpha}-570x760.jpg");
                list.Add($"https://static-cdn.jtvnw.net/ttv-boxart/{encAlpha}-285x380.jpg");
            }

            // 2. Known Twitch Directory Slug mappings
            string low = clean.ToLowerInvariant();
            if (low.Contains("cs2") || low.Contains("counter-strike"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/32399_IGDB-570x760.jpg");

            if (low.Contains("minecraft"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/27471_IGDB-570x760.jpg");

            if (low.Contains("valorant"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/516575-570x760.jpg");

            if (low.Contains("fortnite"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/33214-570x760.jpg");

            if (low.Contains("metin2") || low.Contains("rinamt2"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/16301_IGDB-570x760.jpg");

            if (low.Contains("fc 26") || low.Contains("fifa") || low.Contains("fc 25") || low.Contains("fc 24"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/1745202732_IGDB-570x760.jpg");

            if (low.Contains("rainbow six") || low.Contains("siege"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/460630_IGDB-570x760.jpg");

            if (low.Contains("the finals"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/1628178648_IGDB-570x760.jpg");

            if (low.Contains("project zomboid"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/31339_IGDB-570x760.jpg");

            if (low.Contains("gta") || low.Contains("grand theft auto"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/32982_IGDB-570x760.jpg");

            if (low.Contains("donut county"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/494925_IGDB-570x760.jpg");

            if (low.Contains("marvel rivals"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/2105151590_IGDB-570x760.jpg");

            if (low.Contains("alien") && low.Contains("descent"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/1435272314_IGDB-570x760.jpg");

            if (low.Contains("doom eternal"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/506443_IGDB-570x760.jpg");

            if (low.Contains("payday 3"))
                list.Add("https://static-cdn.jtvnw.net/ttv-boxart/1385135118_IGDB-570x760.jpg");

            return list;
        }

        private static string CleanGameTitle(string title)
        {
            // Remove edition suffixes or size info e.g. "Gta san andreas - 500MB" -> "Grand Theft Auto: San Andreas"
            string clean = title;

            if (clean.Contains("- 500MB", StringComparison.OrdinalIgnoreCase))
                return "Grand Theft Auto: San Andreas";

            if (clean.StartsWith("RinaMT2", StringComparison.OrdinalIgnoreCase))
                return "Metin2";

            clean = Regex.Replace(clean, @"\s*-\s*\d+.*$", "");
            clean = Regex.Replace(clean, @"\s*\((.*?)\)", "");
            clean = Regex.Replace(clean, @"\s*\[(.*?)\]", "");

            return clean.Trim();
        }

        private static string Sanitize(string name)
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
    }
}
