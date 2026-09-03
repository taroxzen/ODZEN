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
using System.Linq;
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
            ["league of legends"] = "https://cdn2.steamgriddb.com/logo/9ebc82cba727df5eb38d2a6a617a268b.png",
            ["lol"] = "https://cdn2.steamgriddb.com/logo/9ebc82cba727df5eb38d2a6a617a268b.png",
            ["ea sports fc 26"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["fc 26"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["ea sports fc 25"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["fc 25"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["ea sports fc 24"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["fc 24"] = "https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png",
            ["fortnite"] = "https://cdn2.steamgriddb.com/logo/5a4a5840caec0e026117b18e7e1136b6.png",
            ["the finals"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/2073850/logo.png",
            ["dirt 5"] = "https://cdn2.steamgriddb.com/logo/d8f07096e2be6b5f4be8cce1a7c50a1d.png",
            ["donut county"] = "https://cdn2.steamgriddb.com/logo/5e7ce9633e3878b30d31e94ba32e3a13.png",
            ["aliens: fireteam elite"] = "https://cdn2.steamgriddb.com/logo/6171b3e1bbd4e8c1ba1ef53e2003c200.png",
            ["minecraft bedrock edition"] = "https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png",
            ["minecraft java edition"] = "https://upload.wikimedia.org/wikipedia/commons/c/cb/Minecraft_Logo-en.svg",
            ["minecraft"] = "https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png",
            ["genshin impact"] = "https://upload.wikimedia.org/wikipedia/en/5/5d/Genshin_Impact_logo.svg",
            ["project zomboid"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/108600/logo.png",
            ["counter-strike 2"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/logo.png",
            ["cs2"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/logo.png",
            ["marvel rivals"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/2767030/logo.png",
            ["tom clancy's rainbow six siege"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/359550/logo.png",
            ["rainbow six siege"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/359550/logo.png",
            ["3d aim trainer"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1155850/logo.png",
            ["gta san andreas"] = "https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png",
            ["grand theft auto: san andreas"] = "https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png",
            ["grand theft auto san andreas"] = "https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png",
            ["gta vice city"] = "https://upload.wikimedia.org/wikipedia/commons/e/ea/Grand_Theft_Auto_Vice_City_logo.svg",
            ["grand theft auto: vice city"] = "https://upload.wikimedia.org/wikipedia/commons/e/ea/Grand_Theft_Auto_Vice_City_logo.svg",
            ["grand theft auto vice city"] = "https://upload.wikimedia.org/wikipedia/commons/e/ea/Grand_Theft_Auto_Vice_City_logo.svg",
            ["need for speed: underground 2"] = "https://upload.wikimedia.org/wikipedia/commons/4/48/NFSU2.svg",
            ["need for speed underground 2"] = "https://upload.wikimedia.org/wikipedia/commons/4/48/NFSU2.svg",
            ["nfs underground 2"] = "https://upload.wikimedia.org/wikipedia/commons/4/48/NFSU2.svg",
            ["diablo ii"] = "https://upload.wikimedia.org/wikipedia/commons/0/0e/Diablo_II_logo.png",
            ["diablo 2"] = "https://upload.wikimedia.org/wikipedia/commons/0/0e/Diablo_II_logo.png",
            ["max payne"] = "https://upload.wikimedia.org/wikipedia/commons/1/1a/Max_Payne_Logo.svg",
            ["rinamt2"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png",
            ["rinamt2_testserver"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png",
            ["metin2"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png",
            ["astra2"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png",
            ["rohan2"] = "https://assets.metin2.dev/logo/metin2_logo_hd.png",
            ["goals"] = "https://upload.wikimedia.org/wikipedia/commons/f/f0/GOALS_Logo.png"
        };

        public static bool EnableSteamSource { get; set; } = true;
        public static bool EnableWikimediaSource { get; set; } = true;
        public static bool EnableSteamGridDbSource { get; set; } = true;

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

            // 2. STEAM DIRECT STORE ID (Eğer platform Steam ve storeId biliniyorsa)
            if (EnableSteamSource && platform.Equals("steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(storeId) && int.TryParse(storeId, out _))
            {
                string directUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{storeId}/logo.png";
                if (await DownloadProcessAndSaveImageAsync(directUrl, targetPath)) return true;
            }

            // 3. WIKIMEDIA COMMONS OPEN MEDIA API (Klasik, Retro ve Steam Dışı Oyunlar İçin Şeffaf Vektör/PNG)
            if (EnableWikimediaSource)
            {
                string? wikiLogoUrl = await TryFindWikimediaCommonsLogoUrlAsync(cleanName);
                if (!string.IsNullOrEmpty(wikiLogoUrl))
                {
                    bool downloaded = await DownloadProcessAndSaveImageAsync(wikiLogoUrl, targetPath);
                    if (downloaded) return true;
                }
            }

            // 4. STEAM STORE SEARCH API (SIKI EŞLEŞME GÜVENLİĞİ / STRICT SIMILARITY GUARD İLE)
            if (EnableSteamSource)
            {
                string? steamLogoUrl = await TryFindSteamStoreLogoUrlAsync(cleanName, storeId, platform);
                if (!string.IsNullOrEmpty(steamLogoUrl))
                {
                    bool downloaded = await DownloadProcessAndSaveImageAsync(steamLogoUrl, targetPath);
                    if (downloaded) return true;
                }
            }

            // 5. ROMA RAKAMI / ALTERNATİF BAŞLIKLARLA WIKIMEDIA & STEAM
            var alternates = GenerateAlternateTitles(cleanName);
            foreach (var alt in alternates)
            {
                if (EnableWikimediaSource)
                {
                    string? altWiki = await TryFindWikimediaCommonsLogoUrlAsync(alt);
                    if (!string.IsNullOrEmpty(altWiki) && await DownloadProcessAndSaveImageAsync(altWiki, targetPath))
                        return true;
                }

                if (EnableSteamSource)
                {
                    string? altSteam = await TryFindSteamStoreLogoUrlAsync(alt, null, platform);
                    if (!string.IsNullOrEmpty(altSteam) && await DownloadProcessAndSaveImageAsync(altSteam, targetPath))
                        return true;
                }
            }

            // 6. STEAMGRIDDB LOGO ENGINE FALLBACK
            if (EnableSteamGridDbSource)
            {
                bool sgdbSuccess = await SteamGridDBLogoEngine.DownloadTransparentLogoAsync(gameId, cleanName, platform, storeId);
                if (sgdbSuccess)
                {
                    string sgdbPath = SteamGridDBLogoEngine.GetLogoPath(gameId);
                    if (File.Exists(sgdbPath))
                    {
                        try { File.Copy(sgdbPath, targetPath, true); return true; } catch { }
                    }
                }
            }

            // 7. DUCKDUCKGO INSTANT GAMES API (Son Fallback)
            string? ddgLogoUrl = await TryFindDuckDuckGoLogoUrlAsync(cleanName);
            if (!string.IsNullOrEmpty(ddgLogoUrl))
            {
                bool downloaded = await DownloadProcessAndSaveImageAsync(ddgLogoUrl, targetPath);
                if (downloaded) return true;
            }

            return false;
        }

        private static async Task<string?> TryFindWikimediaCommonsLogoUrlAsync(string gameName)
        {
            string primaryQuery = (gameName.Length <= 6 || !gameName.Contains(' '))
                ? $"{gameName} video game logo"
                : $"{gameName} logo";

            string? url = await QueryWikimediaCommonsApiAsync(primaryQuery, gameName);
            if (!string.IsNullOrEmpty(url)) return url;

            if (!primaryQuery.Contains("video game"))
            {
                url = await QueryWikimediaCommonsApiAsync($"{gameName} video game logo", gameName);
            }
            return url;
        }

        private static async Task<string?> QueryWikimediaCommonsApiAsync(string query, string gameName)
        {
            try
            {
                string searchUrl = $"https://commons.wikimedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&srnamespace=6&format=json";
                var response = await _httpClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("query", out var queryElem) &&
                        queryElem.TryGetProperty("search", out var searchItems) &&
                        searchItems.GetArrayLength() > 0)
                    {
                        foreach (var item in searchItems.EnumerateArray().Take(5))
                        {
                            string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                            string titleLower = title.ToLowerInvariant();
                            if (!titleLower.EndsWith(".svg") && !titleLower.EndsWith(".png"))
                                continue;

                            if (!IsPlausibleWikiLogo(title, gameName))
                                continue;

                            string infoUrl = $"https://commons.wikimedia.org/w/api.php?action=query&titles={Uri.EscapeDataString(title)}&prop=imageinfo&iiprop=url&iiurlwidth=600&format=json";
                            var infoResp = await _httpClient.GetAsync(infoUrl);
                            if (infoResp.IsSuccessStatusCode)
                            {
                                string infoJson = await infoResp.Content.ReadAsStringAsync();
                                using var infoDoc = JsonDocument.Parse(infoJson);
                                if (infoDoc.RootElement.TryGetProperty("query", out var q) &&
                                    q.TryGetProperty("pages", out var pages))
                                {
                                    foreach (var page in pages.EnumerateObject())
                                    {
                                        if (page.Value.TryGetProperty("imageinfo", out var ii) && ii.GetArrayLength() > 0)
                                        {
                                            var firstInfo = ii[0];
                                            string? imgUrl = firstInfo.TryGetProperty("thumburl", out var thumb) ? thumb.GetString() : null;
                                            if (string.IsNullOrEmpty(imgUrl) && firstInfo.TryGetProperty("url", out var u))
                                                imgUrl = u.GetString();

                                            if (!string.IsNullOrEmpty(imgUrl))
                                                return imgUrl;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static bool IsPlausibleWikiLogo(string fileTitle, string gameName)
        {
            string lowerTitle = fileTitle.ToLowerInvariant();
            string lowerGame = gameName.ToLowerInvariant();

            string[] badWords = {
                "development goals", "sustainable", "agenda 2030", "organization",
                "council", "university", "political", "coat of arms", "flag of",
                "election", "government", "ministry", "treaty", "convention", "association",
                "railway", "train", "s-bahn", "suburban", "metro", "linea", "line ", "traffic", "road sign",
                "season", "season 2", "season 1", "s01", "s02", "s03", "series", "episode",
                "album", "soundtrack", "tour", "concert", "station", "bus", "transport"
            };
            foreach (var bad in badWords)
            {
                if (lowerTitle.Contains(bad) && !lowerGame.Contains(bad))
                    return false;
            }

            // 3 karakter veya daha kısa oyunlar için (örn: "S2", "CS", "BF")
            if (lowerGame.Trim().Length <= 3)
            {
                bool isExplicitGame = lowerTitle.Contains("video game") 
                    || lowerTitle.Contains("game logo") 
                    || lowerTitle.Contains("computerspiel") 
                    || lowerTitle.Contains("jeu vidéo");
                if (!isExplicitGame) return false;
            }

            var firstToken = lowerGame.Split(new[] { ' ', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstToken))
            {
                if (!Regex.IsMatch(lowerTitle, $@"\b{Regex.Escape(firstToken)}\b", RegexOptions.IgnoreCase))
                    return false;
            }

            return true;
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

                // Query Steam Store Open Search API with Strict Similarity Guard
                string searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&l=english&cc=US";
                var response = await _httpClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            string itemName = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            if (item.TryGetProperty("id", out var idElem))
                            {
                                long appId = idElem.GetInt64();
                                if (IsAcceptableGameMatch(gameName, itemName))
                                {
                                    return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/logo.png";
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static bool IsAcceptableGameMatch(string query, string result)
        {
            string q = query.ToLowerInvariant();
            string r = result.ToLowerInvariant();

            if (string.Equals(q, r, StringComparison.OrdinalIgnoreCase)) return true;

            var qTokens = Regex.Split(q, @"[^a-zA-Z0-9]").Where(s => !string.IsNullOrWhiteSpace(s) && !IsNoiseWord(s)).ToList();
            var rTokens = Regex.Split(r, @"[^a-zA-Z0-9]").Where(s => !string.IsNullOrWhiteSpace(s) && !IsNoiseWord(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (qTokens.Count == 0 || rTokens.Count == 0) return false;

            if (qTokens.Count == 1)
            {
                if (!rTokens.Contains(qTokens[0])) return false;
                if (rTokens.Count > 3) return false;
            }

            // Kritik devam oyunu tanımlayıcıları (2, 3, underground, vice city vb.)
            foreach (var token in qTokens)
            {
                if (IsCriticalIdentifier(token) && !rTokens.Contains(token))
                    return false;
            }

            int matches = qTokens.Count(t => rTokens.Contains(t));
            return (double)matches / qTokens.Count >= 0.70;
        }

        private static bool IsCriticalIdentifier(string token) => token switch
        {
            "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or
            "ii" or "iii" or "iv" or "v" or "vi" or "vii" or "viii" or "ix" or
            "underground" or "unbound" or "heat" or "payback" or "rivals" or
            "vice" or "city" or "san" or "andreas" or "liberty" or
            "eternal" or "2016" or "infinite" or "remake" or "reborn" => true,
            _ => false
        };

        private static bool IsNoiseWord(string word) => word switch
        {
            "the" or "a" or "an" or "of" or "and" or "in" or "on" or "at" or "to" or "for" or "game" or "edition" => true,
            _ => false
        };

        private static List<string> GenerateAlternateTitles(string title)
        {
            var alts = new List<string>();
            string lower = title.ToLowerInvariant();

            if (lower.Contains(" 2")) alts.Add(title.Replace(" 2", " II"));
            else if (lower.Contains(" ii")) alts.Add(title.Replace(" ii", " 2").Replace(" II", " 2"));

            if (lower.Contains(" 3")) alts.Add(title.Replace(" 3", " III"));
            else if (lower.Contains(" iii")) alts.Add(title.Replace(" iii", " 3").Replace(" III", " 3"));

            if (lower.Contains(" 4")) alts.Add(title.Replace(" 4", " IV"));
            else if (lower.Contains(" iv")) alts.Add(title.Replace(" iv", " 4").Replace(" IV", " 4"));

            if (lower.Contains("grand theft auto")) alts.Add(title.Replace("Grand Theft Auto", "GTA").Replace("grand theft auto", "GTA"));
            else if (lower.StartsWith("gta ")) alts.Add("Grand Theft Auto " + title.Substring(4));

            if (lower.Contains("need for speed")) alts.Add(title.Replace("Need for Speed", "NFS").Replace("need for speed", "NFS"));
            else if (lower.StartsWith("nfs ")) alts.Add("Need for Speed " + title.Substring(4));

            return alts;
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
                                // 1. ŞEFFAFLIK KONTROLÜ (Düz kapak fotoğraflarını ve ekran görüntülerini reddet)
                                int totalPx = origBmp.Width * origBmp.Height;
                                int transPx = 0;
                                for (int y = 0; y < origBmp.Height; y++)
                                {
                                    for (int x = 0; x < origBmp.Width; x++)
                                    {
                                        if (origBmp.GetPixel(x, y).Alpha < 30) transPx++;
                                    }
                                }
                                if ((double)transPx / totalPx < 0.05) return false;

                                // 2. EN-BOY ORANI KONTROLÜ (Dikey afişleri reddet)
                                if (origBmp.Height > origBmp.Width * 1.35) return false;

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
            // Koyu Tema Kontrast Adaptörü (Siyah/karanlık şeffaf logoları aydınlat)
            long totalLum = 0;
            int visCount = 0;
            for (int y = 0; y < cropped.Height; y++)
            {
                for (int x = 0; x < cropped.Width; x++)
                {
                    var p = cropped.GetPixel(x, y);
                    if (p.Alpha > 40)
                    {
                        totalLum += (long)(0.299 * p.Red + 0.587 * p.Green + 0.114 * p.Blue);
                        visCount++;
                    }
                }
            }
            double avgLum = visCount > 0 ? (double)totalLum / visCount : 255;
            if (avgLum < 65)
            {
                for (int y = 0; y < cropped.Height; y++)
                {
                    for (int x = 0; x < cropped.Width; x++)
                    {
                        var p = cropped.GetPixel(x, y);
                        if (p.Alpha > 20)
                        {
                            cropped.SetPixel(x, y, new SKColor((byte)(255 - p.Red), (byte)(255 - p.Green), (byte)(255 - p.Blue), p.Alpha));
                        }
                    }
                }
            }

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

        public static async Task<bool> SaveCustomLogoFromUrlAsync(string gameId, string imageUrl)
        {
            string targetPath = GetLogoPath(gameId);
            return await DownloadProcessAndSaveImageAsync(imageUrl, targetPath);
        }

        public static async Task<List<LogoCandidate>> SearchLogoCandidatesAsync(string query, string? publisher = null)
        {
            var candidates = new List<LogoCandidate>();
            if (string.IsNullOrWhiteSpace(query)) return candidates;
            string clean = CleanGameTitle(query);

            // 1. Curated
            if (CuratedLogos.TryGetValue(clean, out var curUrl) || CuratedLogos.TryGetValue(query, out curUrl))
            {
                candidates.Add(new LogoCandidate { Title = $"{query} (Resmi HD)", ThumbnailUrl = curUrl, Source = "Önerilen", DownloadUrl = curUrl });
            }

            // 2. Steam Store Search
            try
            {
                string steamUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(clean)}&l=english&cc=US";
                var resp = await _httpClient.GetAsync(steamUrl);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("items", out var items))
                    {
                        foreach (var item in items.EnumerateArray().Take(4))
                        {
                            string name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            if (item.TryGetProperty("id", out var idElem))
                            {
                                long appId = idElem.GetInt64();
                                string logoUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/logo.png";
                                string thumbUrl = item.TryGetProperty("tiny_image", out var ti) ? ti.GetString() ?? logoUrl : logoUrl;
                                candidates.Add(new LogoCandidate { Title = name, ThumbnailUrl = thumbUrl, Source = "Steam", DownloadUrl = logoUrl });
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Wikimedia Commons Search
            try
            {
                string wikiUrl = $"https://commons.wikimedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(clean + " logo")}&srnamespace=6&format=json";
                var resp = await _httpClient.GetAsync(wikiUrl);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("query", out var qElem) &&
                        qElem.TryGetProperty("search", out var searchItems))
                    {
                        foreach (var item in searchItems.EnumerateArray().Take(4))
                        {
                            string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                            string lower = title.ToLowerInvariant();
                            if (!lower.EndsWith(".svg") && !lower.EndsWith(".png")) continue;
                            if (!IsPlausibleWikiLogo(title, clean)) continue;

                            string infoUrl = $"https://commons.wikimedia.org/w/api.php?action=query&titles={Uri.EscapeDataString(title)}&prop=imageinfo&iiprop=url&iiurlwidth=400&format=json";
                            var infoResp = await _httpClient.GetAsync(infoUrl);
                            if (infoResp.IsSuccessStatusCode)
                            {
                                string infoJson = await infoResp.Content.ReadAsStringAsync();
                                using var infoDoc = JsonDocument.Parse(infoJson);
                                if (infoDoc.RootElement.TryGetProperty("query", out var iq) &&
                                    iq.TryGetProperty("pages", out var pages))
                                {
                                    foreach (var page in pages.EnumerateObject())
                                    {
                                        if (page.Value.TryGetProperty("imageinfo", out var ii) && ii.GetArrayLength() > 0)
                                        {
                                            var fi = ii[0];
                                            string? thumb = fi.TryGetProperty("thumburl", out var thu) ? thu.GetString() : null;
                                            string? full = fi.TryGetProperty("url", out var fu) ? fu.GetString() : thumb;
                                            if (!string.IsNullOrEmpty(full))
                                            {
                                                string cleanTitle = title.Replace("File:", "").Replace(".png", "").Replace(".svg", "");
                                                candidates.Add(new LogoCandidate { Title = cleanTitle, ThumbnailUrl = thumb ?? full, Source = "Wikimedia", DownloadUrl = full });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Paralel olarak küçük resim bitmap'lerini belleğe yükle
            await Task.WhenAll(candidates.Select(async c =>
            {
                try
                {
                    string loadUrl = !string.IsNullOrEmpty(c.ThumbnailUrl) ? c.ThumbnailUrl : c.DownloadUrl;
                    var bytes = await _httpClient.GetByteArrayAsync(loadUrl);
                    if (bytes != null && bytes.Length > 0)
                    {
                        using var ms = new MemoryStream(bytes);
                        c.ThumbnailBitmap = new global::Avalonia.Media.Imaging.Bitmap(ms);
                    }
                }
                catch { }
            }));

            return candidates;
        }

        private static string CleanGameTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";

            string clean = title;
            if (clean.Contains("- 500MB", StringComparison.OrdinalIgnoreCase))
                return "Grand Theft Auto: San Andreas";

            if (clean.StartsWith("RinaMT2", StringComparison.OrdinalIgnoreCase))
                return "Metin2";

            // Parantez ve köşeli parantezleri temizle: (500MB), (v1.0), [FitGirl Repack] vb.
            clean = Regex.Replace(clean, @"\s*\((.*?)\)", " ");
            clean = Regex.Replace(clean, @"\s*\[(.*?)\]", " ");

            // Repack ve grup etiketleri
            string[] noisePatterns = {
                @"\bFitGirl(?:\s*Repack)?\b", @"\bDODI(?:\s*Repack)?\b", @"\bElAmigos\b",
                @"\bCPY\b", @"\bCODEX\b", @"\bRazor1911\b", @"\bSKIDROW\b", @"\bPLAZA\b", @"\bPROPHET\b",
                @"\bPortable\b", @"\bRepack\b", @"\bSetup\b", @"\bInstaller\b", @"\bSteam-Rip\b",
                @"\bDigital\s+Deluxe(?:\s+Edition)?\b", @"\bCollector's\s+Edition\b", @"\bGold\s+Edition\b",
                @"\bUltimate\s+Edition\b", @"\bGame\s+of\s+the\s+Year(?:\s+Edition)?\b", @"\bGOTY(?:\s+Edition)?\b",
                @"\bRemastered\b", @"\bDirector's\s+Cut\b", @"\bSteam\s+Edition\b",
                @"\bBedrock\s+Edition\b", @"\bJava\s+Edition\b", @"\bStandard\s+Edition\b",
                @"\bDefinitive\s+Edition\b", @"\bComplete\s+Edition\b", @"\bAnniversary\s+Edition\b",
                @"\bEnchanted\s+Edition\b", @"\bEnhanced\s+Edition\b", @"\bDay\s+One\s+Edition\b",
                @"\(TM\)", @"\(R\)", @"®", @"™"
            };

            foreach (var pattern in noisePatterns)
            {
                clean = Regex.Replace(clean, pattern, " ", RegexOptions.IgnoreCase);
            }

            // Tire sonrası sürüm veya boyut etiketleri (- 500MB, - v1.0 vb.)
            clean = Regex.Replace(clean, @"\s*-\s*(?:\d+\s*(?:MB|GB|KB)|v\d+.*|build\s*\d+.*)$", "", RegexOptions.IgnoreCase);

            // Çift boşlukları sadeleştir
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            return clean;
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

    public class LogoCandidate
    {
        public string Title { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Source { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public global::Avalonia.Media.Imaging.Bitmap? ThumbnailBitmap { get; set; }
        public bool HasBitmap => ThumbnailBitmap != null;
    }
}
