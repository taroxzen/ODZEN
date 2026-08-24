// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Diagnostics;
using System.IO;

namespace Onyx.Avalonia.Services
{
    public class MusicService
    {
        public static void LaunchSpotify() => TryLaunch("spotify:", "https://open.spotify.com");
        public static void LaunchYouTubeMusic() => TryLaunch("https://music.youtube.com", "https://music.youtube.com");
        public static void LaunchAppleMusic() => TryLaunch("applemusic:", "https://music.apple.com");
        public static void LaunchTidal() => TryLaunch("tidal:", "https://listen.tidal.com");
        public static void LaunchDeezer() => TryLaunch("deezer:", "https://www.deezer.com");

        public static void LaunchDiscord()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "discord://-/", UseShellExecute = true });
                return;
            }
            catch { }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string exe = Path.Combine(local, "Discord", "Update.exe");
            if (File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = "--processStart Discord.exe",
                        UseShellExecute = true
                    });
                    return;
                }
                catch { }
            }

            TryLaunch("https://discord.com/app", "https://discord.com/app");
        }

        private static void TryLaunch(string uri, string fallback)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = fallback, UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
