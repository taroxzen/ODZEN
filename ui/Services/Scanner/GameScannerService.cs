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
            string[] candidates = {
                Path.Combine(baseDir, "core", "odzen-core.exe"),
                Path.Combine(baseDir, "core", "bin", "odzen-core.exe"),
                Path.Combine(baseDir, "engine", "odzen-core.exe"),
                Path.Combine(baseDir, "odzen-core.exe"),
                Path.Combine(baseDir, "engine", "scanner", "odzen-game-scanner.exe"),
                Path.Combine(baseDir, "tools", "scanner", "odzen-game-scanner.exe"),
                Path.Combine(baseDir, "odzen-game-scanner.exe"),
                Path.Combine(baseDir, "..", "core", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "core", "bin", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "engine", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "..", "core", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "..", "odzen-game-scanner", "target", "release", "odzen-core.exe"),
                Path.Combine(baseDir, "..", "..", "..", "odzen-game-scanner", "target", "release", "odzen-core.exe"),
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

                                    scanned.Add(item);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            LoadOfflineData(scanned);
            SaveOfflineData(scanned, LoadCustomGames(), LoadRecentGameIds());

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
