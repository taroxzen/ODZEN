// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
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
using Onyx.Avalonia.Models;

namespace Onyx.Avalonia.Services
{
    public class GameScannerService
    {
        private static string LocalDataPath
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ONYX");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                return Path.Combine(folder, "library.json");
            }
        }

        private static string? FindScannerExe()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = {
                Path.Combine(baseDir, "onyx-game-scanner.exe"),
                Path.Combine(baseDir, "..", "onyx-game-scanner.exe"),
                Path.Combine(baseDir, "..", "..", "onyx-game-scanner.exe"),
                Path.Combine(baseDir, "..", "..", "..", "onyx-game-scanner.exe"),
                Path.Combine(baseDir, "..", "..", "target", "release", "onyx-game-scanner.exe"),
                Path.Combine(baseDir, "..", "..", "onyx-game-scanner", "target", "release", "onyx-game-scanner.exe"),
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

                                    scanned.Add(item);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (scanned.Count == 0)
            {
                scanned.AddRange(GetSampleGames());
            }

            LoadOfflineData(scanned);

            return scanned;
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

                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = game.Launch.Path,
                                WorkingDirectory = workDir,
                                UseShellExecute = false
                            };
                            if (game.Launch.Args != null && game.Launch.Args.Count > 0)
                            {
                                foreach (var a in game.Launch.Args) psi.ArgumentList.Add(a);
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
                            if (game.Launch.Args != null && game.Launch.Args.Count > 0)
                            {
                                psiFallback.Arguments = string.Join(" ", game.Launch.Args.Select(a => $"\"{a.Replace("\"", "\\\"")}\""));
                            }
                            Process.Start(psiFallback);
                            return (true, $"🚀 {game.Name} başlatılıyor...");
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(game.Executable))
                {
                    if (!File.Exists(game.Executable))
                    {
                        return (false, $"⚠️ Oyun çalıştırılabilir dosyası bulunamadı: {game.Executable}");
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = game.Executable,
                        WorkingDirectory = Path.GetDirectoryName(game.Executable) ?? "",
                        UseShellExecute = true
                    });
                    return (true, $"🚀 {game.Name} başlatılıyor...");
                }

                if (game.Platform.Equals("steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(game.StoreId))
                {
                    Process.Start(new ProcessStartInfo { FileName = $"steam://rungameid/{game.StoreId}", UseShellExecute = true });
                    return (true, $"🚀 Steam üzerinden başlatılıyor: {game.Name}");
                }

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
            try
            {
                var favs = new List<string>();
                foreach (var g in allGames)
                {
                    if (g.IsFavorite) favs.Add(g.Id);
                }

                var data = new
                {
                    all_games = allGames,
                    favorites = favs,
                    custom_games = customGames,
                    recent_games = recentGameIds ?? new List<string>(),
                    updated_at = DateTime.Now.ToString("O")
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                string tempPath = LocalDataPath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(LocalDataPath))
                {
                    File.Delete(LocalDataPath);
                }
                File.Move(tempPath, LocalDataPath);
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
                    if (doc.RootElement.TryGetProperty("favorites", out var favsElem))
                    {
                        var favSet = new HashSet<string>();
                        foreach (var f in favsElem.EnumerateArray())
                        {
                            favSet.Add(f.GetString() ?? "");
                        }
                        foreach (var g in games)
                        {
                            if (favSet.Contains(g.Id)) g.IsFavorite = true;
                        }
                    }
                }
            }
            catch { }
        }

        private List<GameItem> GetSampleGames()
        {
            return new List<GameItem>
            {
                new() { Id = "cyberpunk", Name = "Cyberpunk 2077", Platform = "steam", PlatformName = "Steam", StoreId = "1091500", SizeBytes = 75161927680, IsFavorite = true },
                new() { Id = "witcher3", Name = "The Witcher 3: Wild Hunt", Platform = "steam", PlatformName = "Steam", StoreId = "292030", SizeBytes = 53687091200, IsFavorite = true },
                new() { Id = "cs2", Name = "Counter-Strike 2", Platform = "steam", PlatformName = "Steam", StoreId = "730", SizeBytes = 34359738368 }
            };
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

                    case "riot":
                        return LaunchExe(FindRiotExe(), "Riot Games Client", "--launch-product=riot_client --launch-patchline=live");

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

        private static string? FindSteamExe()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                string? path = key?.GetValue("SteamExe") as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            } catch { }
            return @"C:\Program Files (x86)\Steam\steam.exe";
        }

        private static string? FindEpicExe()
        {
            string p1 = @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe";
            string p2 = @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe";
            if (File.Exists(p1)) return p1;
            if (File.Exists(p2)) return p2;
            return null;
        }

        private static string? FindRiotExe()
        {
            string p1 = @"C:\Riot Games\Riot Client\RiotClientServices.exe";
            if (File.Exists(p1)) return p1;
            return null;
        }

        private static string? FindMinecraftExe()
        {
            string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Minecraft Launcher\MinecraftLauncher.exe");
            if (File.Exists(p1)) return p1;
            return null;
        }

        private static string? FindRockstarExe()
        {
            string p1 = @"C:\Program Files\Rockstar Games\Launcher\Launcher.exe";
            if (File.Exists(p1)) return p1;
            return null;
        }

        private static string? FindUbisoftExe()
        {
            string p1 = @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe";
            string p2 = @"C:\Program Files\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe";
            if (File.Exists(p1)) return p1;
            if (File.Exists(p2)) return p2;
            return null;
        }

        private static string? FindEaExe()
        {
            string p1 = @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe";
            string p2 = @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe";
            if (File.Exists(p1)) return p1;
            if (File.Exists(p2)) return p2;
            return null;
        }

        private static string? FindBattleNetExe()
        {
            string p1 = @"C:\Program Files (x86)\Battle.net\Battle.net.exe";
            string p2 = @"C:\Program Files\Battle.net\Battle.net.exe";
            if (File.Exists(p1)) return p1;
            if (File.Exists(p2)) return p2;
            return null;
        }

        private static string? FindGogExe()
        {
            string p1 = @"C:\Program Files (x86)\GOG Galaxy\GalaxyClient.exe";
            if (File.Exists(p1)) return p1;
            return null;
        }
    }
}
