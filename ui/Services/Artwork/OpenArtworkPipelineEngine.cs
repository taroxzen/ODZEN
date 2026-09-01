// ============================================================================
// ODZEN — Cybernetic Gaming Platform
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

namespace Odzen.Avalonia.Services
{
    public class OpenArtworkPipelineEngine
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "artwork", "logos");

        private static readonly ConcurrentDictionary<string, bool> _pendingDownloads = new();

        static OpenArtworkPipelineEngine()
        {
            try
            {
                if (!Directory.Exists(StorageDir))
                    Directory.CreateDirectory(StorageDir);

                _httpClient.MaxResponseContentBufferSize = 10 * 1024 * 1024;
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json,image/png,image/webp,image/*,*/*;q=0.8");
            }
            catch { }
        }

        public static string GetLogoPath(string gameId)
        {
            return Path.Combine(StorageDir, $"{Sanitize(gameId)}.png");
        }

        public static bool HasLogo(string gameId)
        {
            return File.Exists(GetLogoPath(gameId));
        }

        public static Bitmap? LoadLogo(string gameId)
        {
            string path = GetLogoPath(gameId);
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
            if (HasLogo(gameId)) return;
            if (!_pendingDownloads.TryAdd(gameId, true)) return;

            Task.Run(async () =>
            {
                try
                {
                    bool success = await ResolveAndDownloadLogoAsync(gameId, gameName, platform, storeId);
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

        private static readonly Dictionary<string, string> CuratedLogos = new(StringComparer.OrdinalIgnoreCase)
        {
            ["valorant"] = "https://cdn2.steamgriddb.com/logo/7c3ad1efdb58bc59e87515ee3c02ca4a.png",
            ["ea sports fc 26"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["fc 26"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["fortnite"] = "https://cdn2.steamgriddb.com/logo/5a4a5840caec0e026117b18e7e1136b6.png",
            ["the finals"] = "https://cdn2.steamgriddb.com/logo/4908990ca385cf5ec7ca6c1b3f71c4c8.png",
            ["dirt 5"] = "https://cdn2.steamgriddb.com/logo/d8f07096e2be6b5f4be8cce1a7c50a1d.png",
            ["donut county"] = "https://cdn2.steamgriddb.com/logo/5e7ce9633e3878b30d31e94ba32e3a13.png",
            ["aliens: fireteam elite"] = "https://cdn2.steamgriddb.com/logo/6171b3e1bbd4e8c1ba1ef53e2003c200.png",
            ["minecraft bedrock edition"] = "https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png",
            ["minecraft java edition"] = "https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png",
            ["minecraft"] = "https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png",
            ["project zomboid"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/108600/logo.png",
            ["counter-strike 2"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/logo.png",
            ["marvel rivals"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/2767030/logo.png",
            ["tom clancy's rainbow six siege"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/359550/logo.png",
            ["3d aim trainer"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1155850/logo.png",
            ["gta san andreas"] = "https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png",
            ["gta san andreas - 500mb"] = "https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png",
            ["rinamt2"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png",
            ["rinamt2_testserver"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png"
        };

        public static async Task<bool> ResolveAndDownloadLogoAsync(string gameId, string gameName, string platform, string? storeId)
        {
            string targetPath = GetLogoPath(gameId);
            if (File.Exists(targetPath)) return true;

            // 0. PURE RUST CORE ARTWORK ENGINE (odzen-core.exe artwork)
            string? coreExe = GameScannerService.FindScannerExe();
            if (!string.IsNullOrEmpty(coreExe) && File.Exists(coreExe))
            {
                try
                {
                    string safeName = (gameName ?? "").Replace("\"", "\\\"");
                    string safeId = (gameId ?? "").Replace("\"", "\\\"");
                    string safeStoreId = (storeId ?? "").Replace("\"", "\\\"");
                    string args = $"artwork --id \"{safeId}\" --name \"{safeName}\" --platform \"{platform}\" " +
                                  (!string.IsNullOrWhiteSpace(storeId) ? $"--store-id \"{safeStoreId}\" " : "") + "--json";

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = coreExe,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync();
                        if (File.Exists(targetPath)) return true;
                    }
                }
                catch { }
            }

            string cleanName = CleanGameTitle(gameName);

            // 1. CURATED DATABASE (Instant 4K Direct Hit)
            if (CuratedLogos.TryGetValue(cleanName, out var curatedUrl) || CuratedLogos.TryGetValue(gameName, out curatedUrl))
            {
                bool downloaded = await DownloadProcessAndSaveImageAsync(curatedUrl, targetPath);
                if (downloaded) return true;
            }

            // 2. STEAM STORE OPEN API (Direct AppID or Open Search)
            string? steamLogoUrl = await TryFindSteamStoreLogoUrlAsync(cleanName, storeId, platform);
            if (!string.IsNullOrEmpty(steamLogoUrl))
            {
                bool downloaded = await DownloadProcessAndSaveImageAsync(steamLogoUrl, targetPath);
                if (downloaded) return true;
            }

            // 3. STEAMGRIDDB LOGO ENGINE FALLBACK
            bool sgdbSuccess = await SteamGridDBLogoEngine.DownloadTransparentLogoAsync(gameId, cleanName, platform, storeId);
            if (sgdbSuccess)
            {
                string sgdbPath = SteamGridDBLogoEngine.GetLogoPath(gameId);
                if (File.Exists(sgdbPath))
                {
                    try { File.Copy(sgdbPath, targetPath, true); return true; } catch { }
                }
            }

            // 4. DUCKDUCKGO INSTANT GAMES API (Secondary Search - Zero API Key)
            string? ddgLogoUrl = await TryFindDuckDuckGoLogoUrlAsync(cleanName);
            if (!string.IsNullOrEmpty(ddgLogoUrl))
            {
                bool downloaded = await DownloadProcessAndSaveImageAsync(ddgLogoUrl, targetPath);
                if (downloaded) return true;
            }

            return false;
        }

        private static async Task<string?> TryFindSteamStoreLogoUrlAsync(string gameName, string? storeId, string platform)
        {
            try
            {
                // If we already have Steam AppID
                if (platform.Equals("steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(storeId) && int.TryParse(storeId, out _))
                {
                    return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{storeId}/logo.png";
                }

                // Query Steam Store Open Search API
                string searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&l=english&cc=US";
                var response = await _httpClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                    {
                        var firstItem = items[0];
                        if (firstItem.TryGetProperty("id", out var idElem))
                        {
                            long appId = idElem.GetInt64();
                            return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/logo.png";
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static async Task<string?> TryFindDuckDuckGoLogoUrlAsync(string gameName)
        {
            try
            {
                string searchUrl = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(gameName)}+game&format=json";
                var response = await _httpClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    // Check main Image
                    if (doc.RootElement.TryGetProperty("Image", out var imgElem))
                    {
                        string img = imgElem.GetString() ?? "";
                        if (!string.IsNullOrEmpty(img))
                        {
                            if (!img.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                img = "https://duckduckgo.com" + img;
                            return img;
                        }
                    }

                    // Check RelatedTopics Icon
                    if (doc.RootElement.TryGetProperty("RelatedTopics", out var topics) && topics.GetArrayLength() > 0)
                    {
                        foreach (var topic in topics.EnumerateArray())
                        {
                            if (topic.TryGetProperty("Icon", out var icon) && icon.TryGetProperty("URL", out var urlElem))
                            {
                                string url = urlElem.GetString() ?? "";
                                if (!string.IsNullOrEmpty(url))
                                {
                                    if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                        url = "https://duckduckgo.com" + url;
                                    return url;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static async Task<bool> DownloadProcessAndSaveImageAsync(string imageUrl, string targetPath)
        {
            try
            {
                var response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes.Length > 512)
                    {
                        using var ms = new MemoryStream(bytes);
                        using var codec = SKCodec.Create(ms);
                        if (codec != null)
                        {
                            using var origBmp = SKBitmap.Decode(codec);
                            if (origBmp != null && origBmp.Width > 16 && origBmp.Height > 16)
                            {
                                using var cropped = AutoCropTransparentPixels(origBmp);
                                using var centered = FitAndCenterToCanvas(cropped, 512, 280);

                                using var image = SKImage.FromBitmap(centered);
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

            return false;
        }

        public static SKBitmap AutoCropTransparentPixels(SKBitmap bmp)
        {
            int minX = bmp.Width, minY = bmp.Height, maxX = 0, maxY = 0;
            bool hasVisiblePixels = false;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var pixel = bmp.GetPixel(x, y);
                    if (pixel.Alpha > 15)
                    {
                        hasVisiblePixels = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!hasVisiblePixels || minX > maxX || minY > maxY)
            {
                return bmp.Copy();
            }

            int cropW = Math.Max(1, maxX - minX + 1);
            int cropH = Math.Max(1, maxY - minY + 1);

            var cropped = new SKBitmap(cropW, cropH, bmp.ColorType, bmp.AlphaType);
            using (var canvas = new SKCanvas(cropped))
            {
                var srcRect = new SKRect(minX, minY, maxX + 1, maxY + 1);
                var destRect = new SKRect(0, 0, cropW, cropH);
                canvas.DrawBitmap(bmp, srcRect, destRect);
            }
            return cropped;
        }

        public static SKBitmap FitAndCenterToCanvas(SKBitmap cropped, int canvasW, int canvasH)
        {
            var canvasBmp = new SKBitmap(canvasW, canvasH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(canvasBmp);
            canvas.Clear(SKColors.Transparent);

            float padX = 24;
            float padY = 16;
            float maxAvailW = canvasW - (padX * 2);
            float maxAvailH = canvasH - (padY * 2);

            float scale = Math.Min(maxAvailW / cropped.Width, maxAvailH / cropped.Height);
            float drawW = cropped.Width * scale;
            float drawH = cropped.Height * scale;

            float posX = (canvasW - drawW) / 2.0f;
            float posY = (canvasH - drawH) / 2.0f;

            var destRect = new SKRect(posX, posY, posX + drawW, posY + drawH);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
            canvas.DrawBitmap(cropped, destRect, paint);

            return canvasBmp;
        }

        private static string CleanGameTitle(string title)
        {
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
