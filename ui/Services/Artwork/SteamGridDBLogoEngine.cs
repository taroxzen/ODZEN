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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Odzen.Avalonia.Services
{
    public class SteamGridDBLogoEngine
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "steamgriddb_logos");

        private static readonly ConcurrentDictionary<string, bool> _pendingDownloads = new();

        static SteamGridDBLogoEngine()
        {
            try
            {
                if (!Directory.Exists(StorageDir))
                    Directory.CreateDirectory(StorageDir);

                _httpClient.MaxResponseContentBufferSize = 10 * 1024 * 1024;
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept", "image/png,image/webp,image/*,*/*;q=0.8");
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
                    bool success = await DownloadTransparentLogoAsync(gameId, gameName, platform, storeId);
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

        public static async Task<bool> DownloadTransparentLogoAsync(string gameId, string gameName, string platform, string? storeId)
        {
            string targetPath = GetLogoPath(gameId);

            var urlsToTry = GenerateSteamGridDBUrls(gameName, platform, storeId, gameId);

            foreach (var url in urlsToTry)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        if (bytes.Length > 512)
                        {
                            using var origStream = new MemoryStream(bytes);
                            using var codec = SKCodec.Create(origStream);
                            if (codec != null)
                            {
                                using var origBmp = SKBitmap.Decode(codec);
                                if (origBmp != null && origBmp.Width > 16 && origBmp.Height > 16)
                                {
                                    // 1. AUTO-CROP: Trim all outer transparent empty pixels
                                    using var croppedBmp = AutoCropTransparentPixels(origBmp);

                                    // 2. PERFECT CENTER & FIT into a 512x280 Canvas
                                    using var centeredCanvas = FitAndCenterToCanvas(croppedBmp, 512, 280);

                                    using var image = SKImage.FromBitmap(centeredCanvas);
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

        private static List<string> GenerateSteamGridDBUrls(string gameName, string platform, string? storeId, string gameId)
        {
            var list = new List<string>();
            string clean = CleanGameTitle(gameName);
            string low = $"{gameId} {clean}".ToLowerInvariant();

            // 1. FORTNITE (100% Transparent Official High-Res Vector/PNG Logo)
            if (low.Contains("fortnite"))
            {
                list.Add("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0e/FortniteLogo.svg/512px-FortniteLogo.svg.png");
                list.Add("https://cdn2.unrealengine.com/fn-social-logo-1920x1080-1920x1080-496660183.png");
            }
            // 2. VALORANT (Official Transparent VALORANT V / Typography)
            else if (low.Contains("valorant"))
            {
                list.Add("https://upload.wikimedia.org/wikipedia/commons/thumb/f/fc/Valorant_logo_-_pink_color_version.svg/512px-Valorant_logo_-_pink_color_version.svg.png");
                list.Add("https://upload.wikimedia.org/wikipedia/commons/thumb/8/87/Valorant_Emblem.svg/512px-Valorant_Emblem.svg.png");
                list.Add("https://images.contentstack.io/v3/assets/blt0eb2a2986b796d20/blt01c2555627a69b59/659850cf60946b5a3ebc69f2/VALORANT_Logo_V.png");
            }
            // 3. EA SPORTS FC (Official Triangle Emblem Transparent Logo)
            else if (low.Contains("fc 26") || low.Contains("fc 25") || low.Contains("ea sports") || low.Contains("fifa"))
            {
                list.Add("https://upload.wikimedia.org/wikipedia/commons/thumb/6/6f/EA_Sports_FC_logo.svg/512px-EA_Sports_FC_logo.svg.png");
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/2195250/logo.png");
            }
            // 4. CROSSHAIR X
            else if (low.Contains("crosshair"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/1366800/logo.png");
            }
            // 5. S2: SON SİLAH
            else if (low.Contains("s2") || low.Contains("son silah") || (low.Contains("siege") && low.Contains("son")))
            {
                list.Add("https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Crosshair_red.svg/512px-Crosshair_red.svg.png");
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/359550/logo.png");
            }
            // 6. COUNTER-STRIKE 2
            else if (low.Contains("cs2") || low.Contains("counter-strike"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/730/logo.png");
            }
            // 7. MINECRAFT
            else if (low.Contains("minecraft"))
            {
                list.Add("https://launchercontent.mojang.com/dungeons/gameTitle.png");
                list.Add("https://www.minecraft.net/etc.clientlibs/minecraft/clientlibs/main/resources/img/minecraft-creeper-face.png");
            }
            // 8. METIN2
            else if (low.Contains("metin2") || low.Contains("rinamt2"))
            {
                list.Add("https://gf1.geo.gfsrv.net/cdnff/cb019ef5fe82d54cb6a38cf0dbd21b.png");
            }
            // 9. GTA SAN ANDREAS
            else if (low.Contains("gta") || low.Contains("grand theft auto") || low.Contains("san andreas"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/12120/logo.png");
            }
            // 10. THE FINALS
            else if (low.Contains("the finals"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/2073850/logo.png");
            }
            // 11. MARVEL RIVALS
            else if (low.Contains("marvel rivals"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/2767030/logo.png");
            }
            // 12. RAINBOW SIX SIEGE
            else if (low.Contains("rainbow six") || low.Contains("siege"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/359550/logo.png");
            }
            // 13. DISPATCH
            else if (low.Contains("dispatch"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/2497920/logo.png");
            }
            // 14. DIRT 5
            else if (low.Contains("dirt 5") || low.Contains("dirt5"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/1038250/logo.png");
            }
            // 15. DONUT COUNTY
            else if (low.Contains("donut county"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/702670/logo.png");
            }
            // 16. PROJECT ZOMBOID
            else if (low.Contains("project zomboid"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/108600/logo.png");
            }
            // 17. ALIENS
            else if (low.Contains("alien"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/1150440/logo.png");
            }
            // 18. DOOM ETERNAL
            else if (low.Contains("doom eternal"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/782330/logo.png");
            }
            // 19. PAYDAY 3
            else if (low.Contains("payday 3"))
            {
                list.Add("https://cdn.cloudflare.steamstatic.com/steam/apps/1272080/logo.png");
            }

            // General Steam App fallback
            if (platform.Equals("steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(storeId))
            {
                list.Add($"https://cdn.cloudflare.steamstatic.com/steam/apps/{storeId}/logo.png");
            }

            return list;
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
