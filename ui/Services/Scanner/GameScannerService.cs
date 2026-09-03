// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Odzen.Avalonia.Models;

namespace Odzen.Avalonia.Services
{
    public class GameScannerService
    {
        private static string LocalDataPath
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                return Path.Combine(folder, "library.json");
            }
        }

        public static string? FindScannerExe()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "engine");

            string[] candidates = {
                Path.Combine(baseDir, "odzen-core.exe"),
                Path.Combine(baseDir, "core", "odzen-core.exe"),
                Path.Combine(localAppDir, "odzen-core.exe"),
                Path.Combine(baseDir, "..", "core", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "..", "core", "odzen-core.exe"),
            };

            foreach (var c in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(c);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }

            // AUTO-EXTRACT EMBEDDED ENGINE (True All-in-One Portable Mode)
            return ExtractEmbeddedEngine();
        }

        private static string? ExtractEmbeddedEngine()
        {
            try
            {
                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "engine");
                Directory.CreateDirectory(targetDir);
                string targetPath = Path.Combine(targetDir, "odzen-core.exe");

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string[] possibleNames = {
                    "Odzen.Avalonia.Assets.odzen-core.exe",
                    "Odzen.Avalonia.odzen-core.exe",
                    "odzen-core.exe"
                };

                Stream? stream = null;
                foreach (var name in possibleNames)
                {
                    stream = assembly.GetManifestResourceStream(name);
                    if (stream != null) break;
                }

                if (stream == null)
                {
                    var resName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("odzen-core.exe", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(resName))
                    {
                        stream = assembly.GetManifestResourceStream(resName);
                    }
                }

                if (stream != null)
                {
                    using (stream)
                    {
                        if (File.Exists(targetPath) && new FileInfo(targetPath).Length == stream.Length)
                        {
                            return targetPath;
                        }

                        using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        stream.CopyTo(fs);
                    }
                    return targetPath;
                }
            }
            catch { }
            return null;
        }

        public async Task<List<GameItem>> ScanGamesAsync()
        {
            var scanned = new List<GameItem>();
            string? exePath = FindScannerExe();

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "scan --json",
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string json = await proc.StandardOutput.ReadToEndAsync();
                        await proc.WaitForExitAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("games", out var gamesElem))
                            {
                                foreach (var g in gamesElem.EnumerateArray())
                                {
                                    var item = new GameItem
                                    {
                                        Id = g.GetProperty("id").GetString() ?? "",
                                        Name = g.GetProperty("name").GetString() ?? "",
                                        Platform = g.GetProperty("platform").GetString() ?? "local",
                                        PlatformName = GetPrettyPlatformName(g.GetProperty("platform").GetString() ?? "local"),
                                        InstallPath = g.TryGetProperty("install_path", out var ip) ? ip.GetString() : null,
                                        Executable = g.TryGetProperty("executable", out var ex) ? ex.GetString() : null,
                                        StoreId = g.TryGetProperty("store_id", out var sid) ? sid.GetString() : null,
                                        SizeBytes = g.TryGetProperty("size_bytes", out var sb) && sb.ValueKind == JsonValueKind.Number ? sb.GetUInt64() : null
                                    };

                                    if (g.TryGetProperty("launch", out var launchElem))
                                    {
                                        item.Launch = new GameLaunchInfo
                                        {
                                            Type = launchElem.TryGetProperty("type", out var lt) ? lt.GetString() ?? "executable" : "executable",
                                            Uri = launchElem.TryGetProperty("uri", out var lu) ? lu.GetString() : null,
                                            Path = launchElem.TryGetProperty("path", out var lp) ? lp.GetString() : null,
                                            Cwd = launchElem.TryGetProperty("cwd", out var lcwd) ? lcwd.GetString() : null
                                        };

                                        if (launchElem.TryGetProperty("args", out var argsElem) && argsElem.ValueKind == JsonValueKind.Array)
                                        {
                                            item.Launch.Args = new List<string>();
                                            foreach (var arg in argsElem.EnumerateArray())
                                            {
                                                item.Launch.Args.Add(arg.GetString() ?? "");
                                            }
                                        }
                                    }

                                    EnrichGameWithDualVerification(item);
                                    scanned.Add(item);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Dual-Verification: Deduplicate multiple game entries in the same install folder
            scanned = DeduplicateGamesByInstallPath(scanned);

            LoadOfflineData(scanned);
            foreach (var g in scanned)
            {
                EnrichGameWithDualVerification(g);
            }

            SaveOfflineData(scanned, LoadCustomGames(), LoadRecentGameIds());

            return scanned;
        }

        /// <summary>
        /// Level 2 Dual Verification: PE FileVersionInfo & Manufacturer / Publisher Cross-Check
        /// </summary>
        public static void EnrichGameWithDualVerification(GameItem item)
        {
            if (item == null) return;

            // 1. If executable is missing or invalid or points to redist/uninstaller, find the authentic one
            if (string.IsNullOrEmpty(item.Executable) || !File.Exists(item.Executable) || IsRedistOrUninstaller(item.Executable))
            {
                if (!string.IsNullOrEmpty(item.InstallPath) && Directory.Exists(item.InstallPath))
                {
                    string? bestExe = FindBestGameExecutable(item.InstallPath);
                    if (!string.IsNullOrEmpty(bestExe))
                    {
                        item.Executable = bestExe;
                        if (item.Launch != null)
                        {
                            item.Launch.Path = bestExe;
                        }
                    }
                }
            }

            // 2. Extract PE FileVersionInfo (Company, Product, Description)
            if (!string.IsNullOrEmpty(item.Executable) && File.Exists(item.Executable))
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(item.Executable);
                    
                    // Publisher / Manufacturer
                    if (!string.IsNullOrWhiteSpace(vi.CompanyName))
                    {
                        string company = vi.CompanyName.Trim();
                        if (!IsGenericCompany(company))
                        {
                            item.Publisher = company;
                        }
                    }

                    // Refine short titles (like S2 or generic titles)
                    if (item.Name.Equals("S2", StringComparison.OrdinalIgnoreCase) && item.Publisher?.Contains("CJ", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        item.Publisher = "CJGameLab (S2 Son Silah)";
                    }
                }
                catch { }
            }
        }

        private static bool IsGenericCompany(string company)
        {
            string lower = company.ToLowerInvariant();
            return lower.Contains("microsoft") || lower.Contains("directx") || lower.Contains("install") ||
                   lower.Contains("inno setup") || lower.Contains("nullsoft") || lower.Contains("taroxzen");
        }

        private static bool IsRedistOrUninstaller(string exePath)
        {
            string lower = exePath.ToLowerInvariant();
            return lower.Contains(@"\_redist\") || lower.Contains(@"\redist\") || lower.Contains("dxwebsetup") ||
                   lower.Contains("unins000") || lower.Contains("uninstall") || lower.Contains("yamakaldır") ||
                   lower.Contains("yamakaldir") || lower.Contains("quicksfv");
        }

        /// <summary>
        /// Level 1 Dual Verification: Heuristic Game Assets & Engine Binary Selection (Depth 5)
        /// </summary>
        public static string? FindBestGameExecutable(string dir)
        {
            if (!Directory.Exists(dir)) return null;

            string[] skipNames = {
                "unitycrashhandler", "crashpad", "crashreporter", "crashhandler",
                "uninstall", "unins", "redist", "vcredist", "dxsetup", "dxwebsetup",
                "quicksfv", "yamakaldır", "yamakaldir", "dotnet", "easyanticheat",
                "battleye", "cefsharp", "notification_helper", "report", "patcher",
                "setup", "installer", "helper", "config", "autoupdate"
            };

            string[] skipDirParts = {
                "_redist", "\\redist", "/redist", "directx", "support", "prerequisites",
                "installer", "dependencies", "$recycle.bin"
            };

            string dirName = Path.GetFileName(dir.TrimEnd('\\', '/')).ToLowerInvariant();
            string? bestPath = null;
            long bestScore = -1;

            try
            {
                var opt = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 5,
                    IgnoreInaccessible = true
                };

                foreach (var file in Directory.EnumerateFiles(dir, "*.exe", opt))
                {
                    string fileLower = file.ToLowerInvariant();
                    if (skipDirParts.Any(d => fileLower.Contains(d))) continue;

                    string stem = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (skipNames.Any(s => stem.Contains(s))) continue;

                    var fi = new FileInfo(file);
                    if (fi.Length < 50_000) continue; // Skip tiny batch wrappers

                    long score = fi.Length;

                    // Shipping Game Binary Priority
                    if (stem.EndsWith("-win64-shipping") || stem.EndsWith("_shipping") || stem.EndsWith("shipping"))
                    {
                        score += 150_000_000;
                    }

                    // Direct match with directory title
                    if (!string.IsNullOrEmpty(dirName) && stem == dirName)
                    {
                        score += 100_000_000;
                    }

                    // Binaries\Win64 location
                    if (fileLower.Contains(@"binaries\win64") || fileLower.Contains("binaries/win64"))
                    {
                        score += 50_000_000;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPath = file;
                    }
                }
            }
            catch { }

            return bestPath;
        }

        /// <summary>
        /// Deduplicates game items sharing the same installation directory root
        /// </summary>
        public static List<GameItem> DeduplicateGamesByInstallPath(List<GameItem> games)
        {
            if (games == null || games.Count == 0) return new List<GameItem>();

            var result = new List<GameItem>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in games)
            {
                if (string.IsNullOrEmpty(game.InstallPath))
                {
                    result.Add(game);
                    continue;
                }

                string normPath = Path.GetFullPath(game.InstallPath).TrimEnd('\\', '/').ToLowerInvariant();

                if (seenPaths.Contains(normPath))
                {
                    continue;
                }

                seenPaths.Add(normPath);
                result.Add(game);
            }

            return result;
        }

        private static string GetPrettyPlatformName(string platform) => platform.ToLowerInvariant() switch
        {
            "steam" => "Steam",
            "epic" => "Epic Games",
            "ea" => "EA App",
            "minecraft" => "Minecraft",
            "riot" => "Riot Games",
            "battlenet" or "battle_net" => "Battle.net",
            "gog" => "GOG Galaxy",
            "ubisoft" => "Ubisoft Connect",
            "rockstar" => "Rockstar Games",
            "xbox" => "XBOX",
            "amazon" => "Amazon Games",
            "metin2" => "Metin2 Sunucuları",
            _ => "Yerel Oyun"
        };

        public (bool success, string message) LaunchGame(GameItem game)
        {
            try
            {
                // 1. Hızlı Başlatma / Kısayol Komutu Önceliği (Örn: steam://rungameid/730 veya özel protokol)
                if (!string.IsNullOrWhiteSpace(game.QuickLaunchCommand))
                {
                    string qCmd = game.QuickLaunchCommand.Trim();
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = qCmd,
                            UseShellExecute = true
                        });
                        return (true, $"🚀 {game.Name} hızlı komut ile başlatılıyor...");
                    }
                    catch (Exception qEx)
                    {
                        Debug.WriteLine($"Quick launch fallback: {qEx.Message}");
                    }
                }

                // 1.5. Epic Games ise doğrudan resmi Epic Launcher URL protokolü ile başlat (Kısayol bağlantısı)
                if (game.Platform.Equals("epic", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(game.StoreId))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"com.epicgames.launcher://apps/{game.StoreId}?action=launch&silent=true",
                            UseShellExecute = true
                        });
                        return (true, $"🚀 Epic Games üzerinden başlatılıyor: {game.Name}");
                    }
                    catch (Exception epEx)
                    {
                        Debug.WriteLine($"Epic protocol launch fallback: {epEx.Message}");
                    }
                }

                // 2. Yapılandırılmış Başlatma Bilgisi (Rust Scanner veya Özel Tanım)
                if (game.Launch != null)
                {
                    if (game.Launch.Type == "protocol" && !string.IsNullOrWhiteSpace(game.Launch.Uri))
                    {
                        Process.Start(new ProcessStartInfo { FileName = game.Launch.Uri, UseShellExecute = true });
                        return (true, $"🚀 {game.Name} başlatılıyor...");
                    }

                    if (game.Launch.Type == "executable" && !string.IsNullOrWhiteSpace(game.Launch.Path))
                    {
                        if (!File.Exists(game.Launch.Path))
                        {
                            return (false, $"⚠️ Oyun dosyası bulunamadı: {game.Launch.Path}");
                        }

                        string workDir = !string.IsNullOrWhiteSpace(game.Launch.Cwd) && Directory.Exists(game.Launch.Cwd)
                            ? game.Launch.Cwd
                            : Path.GetDirectoryName(game.Launch.Path) ?? "";

                        var allArgs = new List<string>();
                        if (game.Launch.Args != null && game.Launch.Args.Count > 0)
                        {
                            allArgs.AddRange(game.Launch.Args);
                        }
                        if (!string.IsNullOrWhiteSpace(game.LaunchArgs))
                        {
                            allArgs.Add(game.LaunchArgs.Trim());
                        }

                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = game.Launch.Path,
                                WorkingDirectory = workDir,
                                UseShellExecute = false
                            };
                            foreach (var a in allArgs)
                            {
                                if (!string.IsNullOrWhiteSpace(a)) psi.ArgumentList.Add(a);
                            }
                            Process.Start(psi);
                            return (true, $"🚀 {game.Name} başlatılıyor...");
                        }
                        catch
                        {
                            var psiFallback = new ProcessStartInfo
                            {
                                FileName = game.Launch.Path,
                                WorkingDirectory = workDir,
                                UseShellExecute = true
                            };
                            if (allArgs.Count > 0)
                            {
                                psiFallback.Arguments = string.Join(" ", allArgs.Select(a => a.Contains(' ') ? $"\"{a.Replace("\"", "\\\"")}\"" : a));
                            }
                            Process.Start(psiFallback);
                            return (true, $"🚀 {game.Name} başlatılıyor...");
                        }
                    }
                }

                // 3. Doğrudan Çalıştırılabilir Dosya (.exe) + Başlatma Parametreleri
                if (!string.IsNullOrWhiteSpace(game.Executable))
                {
                    if (!File.Exists(game.Executable))
                    {
                        return (false, $"⚠️ Oyun çalıştırılabilir dosyası bulunamadı: {game.Executable}");
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = game.Executable,
                        WorkingDirectory = Path.GetDirectoryName(game.Executable) ?? "",
                        UseShellExecute = true
                    };
                    if (!string.IsNullOrWhiteSpace(game.LaunchArgs))
                    {
                        psi.Arguments = game.LaunchArgs.Trim();
                    }
                    Process.Start(psi);
                    return (true, $"🚀 {game.Name} başlatılıyor...");
                }

                // 4. Steam Geri Çekilme (Fallback)
                if (game.Platform.Equals("steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(game.StoreId))
                {
                    Process.Start(new ProcessStartInfo { FileName = $"steam://rungameid/{game.StoreId}", UseShellExecute = true });
                    return (true, $"🚀 Steam üzerinden başlatılıyor: {game.Name}");
                }

                // 5. Epic Games Geri Çekilme (Fallback)
                if (game.Platform.Equals("epic", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(game.StoreId))
                {
                    Process.Start(new ProcessStartInfo { FileName = $"com.epicgames.launcher://apps/{game.StoreId}?action=launch&silent=true", UseShellExecute = true });
                    return (true, $"🚀 Epic Games üzerinden başlatılıyor: {game.Name}");
                }

                return (false, $"⚠️ {game.Name} için geçerli başlatıcı bilgisi bulunamadı.");
            }
            catch (Win32Exception wEx)
            {
                return (false, $"⚠️ Yönetici izni veya sistem engeli: {wEx.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"⚠️ Başlatma hatası: {ex.Message}");
            }
        }

        public void SaveOfflineData(List<GameItem> allGames, List<GameItem> customGames, List<string>? recentGameIds = null)
        {
            if (allGames == null || allGames.Count == 0) return;
            try
            {
                var favs = new List<string>();
                foreach (var g in allGames)
                {
                    if (g.IsFavorite && !string.IsNullOrEmpty(g.Id)) favs.Add(g.Id);
                }

                var data = new OfflineLibraryData
                {
                    AllGames = allGames,
                    Favorites = favs,
                    CustomGames = customGames ?? new List<GameItem>(),
                    RecentGames = recentGameIds ?? new List<string>(),
                    UpdatedAt = DateTime.Now.ToString("O")
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                string? targetDir = Path.GetDirectoryName(LocalDataPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.WriteAllText(LocalDataPath, json);
            }
            catch { }
        }

        public (List<GameItem> allGames, List<GameItem> customGames, List<string> recentIds) LoadOfflineLibrary()
        {
            var all = new List<GameItem>();
            var custom = new List<GameItem>();
            var recents = new List<string>();

            try
            {
                if (File.Exists(LocalDataPath))
                {
                    string json = File.ReadAllText(LocalDataPath);
                    using var doc = JsonDocument.Parse(json);

                    var favSet = new HashSet<string>();
                    if (doc.RootElement.TryGetProperty("favorites", out var favsElem))
                    {
                        foreach (var f in favsElem.EnumerateArray())
                        {
                            var s = f.GetString();
                            if (!string.IsNullOrEmpty(s)) favSet.Add(s);
                        }
                    }

                    if (doc.RootElement.TryGetProperty("all_games", out var allElem))
                    {
                        foreach (var g in allElem.EnumerateArray())
                        {
                            var item = JsonSerializer.Deserialize<GameItem>(g.GetRawText());
                            if (item != null)
                            {
                                if (favSet.Contains(item.Id)) item.IsFavorite = true;
                                all.Add(item);
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("custom_games", out var customElem))
                    {
                        foreach (var g in customElem.EnumerateArray())
                        {
                            var item = JsonSerializer.Deserialize<GameItem>(g.GetRawText());
                            if (item != null)
                            {
                                if (favSet.Contains(item.Id)) item.IsFavorite = true;
                                custom.Add(item);
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("recent_games", out var recentsElem))
                    {
                        foreach (var r in recentsElem.EnumerateArray())
                        {
                            var s = r.GetString();
                            if (!string.IsNullOrEmpty(s)) recents.Add(s);
                        }
                    }
                }
            }
            catch { }

            return (all, custom, recents);
        }

        public List<string> LoadRecentGameIds()
        {
            try
            {
                if (File.Exists(LocalDataPath))
                {
                    string json = File.ReadAllText(LocalDataPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("recent_games", out var recentsElem))
                    {
                        var list = new List<string>();
                        foreach (var r in recentsElem.EnumerateArray())
                        {
                            list.Add(r.GetString() ?? "");
                        }
                        return list;
                    }
                }
            }
            catch { }
            return new List<string>();
        }

        public List<GameItem> LoadCustomGames()
        {
            var customList = new List<GameItem>();
            try
            {
                if (File.Exists(LocalDataPath))
                {
                    string json = File.ReadAllText(LocalDataPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("custom_games", out var customElem))
                    {
                        foreach (var g in customElem.EnumerateArray())
                        {
                            var item = JsonSerializer.Deserialize<GameItem>(g.GetRawText());
                            if (item != null) customList.Add(item);
                        }
                    }
                }
            }
            catch { }
            return customList;
        }

        private void LoadOfflineData(List<GameItem> games)
        {
            try
            {
                if (File.Exists(LocalDataPath))
                {
                    string json = File.ReadAllText(LocalDataPath);
                    using var doc = JsonDocument.Parse(json);

                    var favSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (doc.RootElement.TryGetProperty("favorites", out var favsElem))
                    {
                        foreach (var f in favsElem.EnumerateArray())
                        {
                            var s = f.GetString();
                            if (!string.IsNullOrEmpty(s)) favSet.Add(s);
                        }
                    }

                    var customMap = new Dictionary<string, (string? logo, string? args, string? qlaunch, bool preferOnline)>(StringComparer.OrdinalIgnoreCase);
                    if (doc.RootElement.TryGetProperty("all_games", out var allElem))
                    {
                        foreach (var g in allElem.EnumerateArray())
                        {
                            if (g.TryGetProperty("id", out var idElem))
                            {
                                string id = idElem.GetString() ?? "";
                                if (!string.IsNullOrEmpty(id))
                                {
                                    string? logo = g.TryGetProperty("custom_logo_path", out var lp) ? lp.GetString() : null;
                                    string? args = g.TryGetProperty("launch_args", out var la) ? la.GetString() : null;
                                    string? qlaunch = g.TryGetProperty("quick_launch_command", out var ql) ? ql.GetString() : null;
                                    bool prefer = !g.TryGetProperty("prefer_online_logo", out var po) || po.GetBoolean();
                                    customMap[id] = (logo, args, qlaunch, prefer);
                                }
                            }
                        }
                    }

                    foreach (var g in games)
                    {
                        if (favSet.Contains(g.Id)) g.IsFavorite = true;
                        if (customMap.TryGetValue(g.Id, out var cust))
                        {
                            if (!string.IsNullOrEmpty(cust.logo)) g.CustomLogoPath = cust.logo;
                            if (!string.IsNullOrEmpty(cust.args)) g.LaunchArgs = cust.args;
                            if (!string.IsNullOrEmpty(cust.qlaunch)) g.QuickLaunchCommand = cust.qlaunch;
                            g.PreferOnlineLogo = cust.preferOnline;
                        }
                    }
                }
            }
            catch { }
        }

        public static List<GameItem> ScanShortcuts()
        {
            return new List<GameItem>();
        }
    }

    public static class PlatformLauncherService
    {
        public static (bool Success, string Message) LaunchPlatform(string platformKey)
        {
            try
            {
                switch (platformKey.ToLowerInvariant())
                {
                    case "steam":
                        return LaunchUriOrExe("steam://open/main", FindSteamExe(), "Steam");

                    case "epic":
                        return LaunchUriOrExe("com.epicgames.launcher://", FindEpicExe(), "Epic Games");

                    case "riot" or "riotclient":
                        return LaunchRiotClient();

                    case "minecraft":
                        return LaunchUriOrExe("minecraft://", FindMinecraftExe(), "Minecraft Launcher");

                    case "rockstar":
                        return LaunchExe(FindRockstarExe(), "Rockstar Games Launcher");

                    case "ubisoft":
                        return LaunchUriOrExe("uplay://", FindUbisoftExe(), "Ubisoft Connect");

                    case "ea":
                        return LaunchUriOrExe("origin2://", FindEaExe(), "EA App");

                    case "battlenet" or "battle_net":
                        return LaunchUriOrExe("battlenet://", FindBattleNetExe(), "Battle.net");

                    case "gog":
                        return LaunchExe(FindGogExe(), "GOG Galaxy");

                    case "xbox":
                        return LaunchUriOrExe("xbox://", null, "Xbox");

                    default:
                        return (false, "Bilinmeyen platform.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Başlatma hatası: {ex.Message}");
            }
        }

        private static (bool, string) LaunchRiotClient()
        {
            string? riotExe = FindRiotExe();
            if (!string.IsNullOrEmpty(riotExe) && File.Exists(riotExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = riotExe,
                        Arguments = "--launch-product=riot_client --launch-patchline=live",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return (true, "🚀 Riot Client resmi istemcisi başlatılıyor...");
                }
                catch
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = riotExe, UseShellExecute = true });
                        return (true, "🚀 Riot Client başlatılıyor...");
                    }
                    catch { }
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = "riotclient:", UseShellExecute = true });
                return (true, "🚀 Riot Client başlatılıyor...");
            }
            catch { }

            return (false, "⚠️ Riot Client sistemde bulunamadı. Lütfen yüklü olduğundan emin olun.");
        }

        private static (bool, string) LaunchUriOrExe(string uri, string? exePath, string name)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
                return (true, $"🚀 {name} resmi istemcisi başlatılıyor...");
            }
            catch
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                    return (true, $"🚀 {name} başlatılıyor...");
                }
                return (false, $"⚠️ {name} sistemde bulunamadı veya yüklü değil.");
            }
        }

        private static (bool, string) LaunchExe(string? exePath, string name, string? args = null)
        {
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var psi = new ProcessStartInfo { FileName = exePath, UseShellExecute = true };
                if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
                Process.Start(psi);
                return (true, $"🚀 {name} resmi istemcisi başlatılıyor...");
            }
            return (false, $"⚠️ {name} sistemde bulunamadı.");
        }

        private static string? FindRiotExe()
        {
            try
            {
                // 1. ProgramData JSON manifest check
                string jsonPath = @"C:\ProgramData\Riot Games\RiotClientInstalls.json";
                if (File.Exists(jsonPath))
                {
                    string content = File.ReadAllText(jsonPath);
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"([A-Za-z]:[\\/][^""\r\n]+?RiotClientServices\.exe)");
                    if (match.Success)
                    {
                        string path = match.Groups[1].Value.Replace('/', '\\');
                        if (File.Exists(path)) return path;
                    }
                }
            } catch { }

            try
            {
                // 2. Scan all available drives
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string root = drive.RootDirectory.FullName;
                    string[] candidates = {
                        Path.Combine(root, @"Riot Games\Riot Client\RiotClientServices.exe"),
                        Path.Combine(root, @"Games\Riot Games\Riot Client\RiotClientServices.exe"),
                        Path.Combine(root, @"Program Files\Riot Games\Riot Client\RiotClientServices.exe"),
                        Path.Combine(root, @"Program Files (x86)\Riot Games\Riot Client\RiotClientServices.exe")
                    };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c)) return c;
                    }
                }
            } catch { }

            try
            {
                // 3. Registry uninstall check
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game riot_client.live");
                string? icon = key?.GetValue("DisplayIcon") as string;
                if (!string.IsNullOrEmpty(icon) && File.Exists(icon)) return icon;
            } catch { }

            return null;
        }

        private static string? FindSteamExe()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                string? path = key?.GetValue("SteamExe") as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            } catch { }

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files (x86)\Steam\steam.exe"),
                    Path.Combine(root, @"Program Files\Steam\steam.exe"),
                    Path.Combine(root, @"Steam\steam.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return @"C:\Program Files (x86)\Steam\steam.exe";
        }

        private static string? FindEpicExe()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher");
                string? path = key?.GetValue("AppDataPath") as string;
                if (!string.IsNullOrEmpty(path))
                {
                    string candidate = Path.Combine(path, @"Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            } catch { }

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"),
                    Path.Combine(root, @"Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"),
                    Path.Combine(root, @"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string? FindMinecraftExe()
        {
            string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Minecraft Launcher\MinecraftLauncher.exe");
            if (File.Exists(p1)) return p1;

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files (x86)\Minecraft Launcher\MinecraftLauncher.exe"),
                    Path.Combine(root, @"Program Files\Minecraft Launcher\MinecraftLauncher.exe"),
                    Path.Combine(root, @"XboxGames\Minecraft Launcher\Content\Minecraft.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string? FindRockstarExe()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files\Rockstar Games\Launcher\Launcher.exe"),
                    Path.Combine(root, @"Program Files (x86)\Rockstar Games\Launcher\Launcher.exe"),
                    Path.Combine(root, @"Rockstar Games\Launcher\Launcher.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string? FindUbisoftExe()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files (x86)\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe"),
                    Path.Combine(root, @"Program Files\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe"),
                    Path.Combine(root, @"Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string? FindEaExe()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe"),
                    Path.Combine(root, @"Program Files\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe"),
                    Path.Combine(root, @"Program Files (x86)\Origin\Origin.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string? FindBattleNetExe()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files (x86)\Battle.net\Battle.net.exe"),
                    Path.Combine(root, @"Program Files\Battle.net\Battle.net.exe"),
                    Path.Combine(root, @"Battle.net\Battle.net.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string? FindGogExe()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                string[] paths = {
                    Path.Combine(root, @"Program Files (x86)\GOG Galaxy\GalaxyClient.exe"),
                    Path.Combine(root, @"Program Files\GOG Galaxy\GalaxyClient.exe"),
                    Path.Combine(root, @"GOG Galaxy\GalaxyClient.exe")
                };
                foreach (var p in paths) if (File.Exists(p)) return p;
            }
            return null;
        }
    }
}
