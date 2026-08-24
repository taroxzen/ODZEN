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
    public static class WindowsNotificationService
    {
        public static void ShowWindowsNotification(string title, string message)
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                // PowerShell üzerinden Windows Yerel Toast / Balon bildirimi gönderir
                string psScript = $@"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$textNodes = $template.GetElementsByTagName('text')
$textNodes.Item(0).AppendChild($template.CreateTextNode('{title.Replace("'", "''")}')) > $null
$textNodes.Item(1).AppendChild($template.CreateTextNode('{message.Replace("'", "''")}')) > $null
$toast = [Windows.UI.Notifications.ToastNotification]::new($template)
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('ONYX.Launcher')
$notifier.Show($toast)";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch
            {
                // Fallback sessiz geçiş
            }
        }

        public static void ShowGameDetectedNotification(AppLanguage lang, string gameTitle)
        {
            string title;
            string message;

            switch (lang)
            {
                case AppLanguage.German:
                    title = "🎮 ONYX - Neues Spiel erkannt!";
                    message = $"\"{gameTitle}\" wurde erkannt. Öffne ONYX, um es zur Bibliothek hinzuzufügen.";
                    break;
                case AppLanguage.Bulgarian:
                    title = "🎮 ONYX - Открита е нова игра!";
                    message = $"\"{gameTitle}\" е открита. Отворете ONYX, за да я добавите към библиотеката.";
                    break;
                case AppLanguage.Spanish:
                    title = "🎮 ONYX - ¡Nuevo juego detectado!";
                    message = $"\"{gameTitle}\" detectado. Abre ONYX para añadirlo a tu biblioteca.";
                    break;
                case AppLanguage.Dutch:
                    title = "🎮 ONYX - Nieuwe game gedetecteerd!";
                    message = $"\"{gameTitle}\" gedetecteerd. Open ONYX om toe te voegen aan je bibliotheek.";
                    break;
                case AppLanguage.French:
                    title = "🎮 ONYX - Nouveau jeu détecté !";
                    message = $"\"{gameTitle}\" a été détecté. Ouvrez ONYX pour l'ajouter à votre bibliothèque.";
                    break;
                case AppLanguage.Russian:
                    title = "🎮 ONYX - Обнаружена новая игра!";
                    message = $"Обнаружена игра \"{gameTitle}\". Откройте ONYX, чтобы добавить её в библиотеку.";
                    break;
                case AppLanguage.English:
                    title = "🎮 ONYX - New Game Detected!";
                    message = $"\"{gameTitle}\" detected. Open ONYX to add it to your library.";
                    break;
                default:
                    title = "🎮 ONYX - Yeni Oyun Algılandı!";
                    message = $"\"{gameTitle}\" tespit edildi. Kütüphanenize eklemek için ONYX'i açın.";
                    break;
            }

            ShowWindowsNotification(title, message);
        }
    }
}
