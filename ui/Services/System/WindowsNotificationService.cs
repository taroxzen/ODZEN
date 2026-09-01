// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Diagnostics;
using System.IO;

namespace Odzen.Avalonia.Services
{
    public static class WindowsNotificationService
    {
        public static void ShowWindowsNotification(string title, string message)
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                // PowerShell üzerinden Windows Yerel Toast / Balon bildirimi gönderir (Enjeksiyon korumalı Base64)
                string safeTitleB64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(title ?? string.Empty));
                string safeMessageB64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(message ?? string.Empty));

                string psScript = $@"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$textNodes = $template.GetElementsByTagName('text')
$t = [System.Text.Encoding]::Unicode.GetString([System.Convert]::FromBase64String('{safeTitleB64}'))
$m = [System.Text.Encoding]::Unicode.GetString([System.Convert]::FromBase64String('{safeMessageB64}'))
$textNodes.Item(0).AppendChild($template.CreateTextNode($t)) > $null
$textNodes.Item(1).AppendChild($template.CreateTextNode($m)) > $null
$toast = [Windows.UI.Notifications.ToastNotification]::new($template)
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('ODZEN')
$notifier.Show($toast)";

                string encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encodedCommand}",
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
                    title = "🎮 ODZEN - Neues Spiel erkannt!";
                    message = $"\"{gameTitle}\" wurde erkannt. Öffne ODZEN, um es zur Bibliothek hinzuzufügen.";
                    break;
                case AppLanguage.Bulgarian:
                    title = "🎮 ODZEN - Открита е нова игра!";
                    message = $"\"{gameTitle}\" е открита. Отворете ODZEN, за да я добавите към библиотеката.";
                    break;
                case AppLanguage.Spanish:
                    title = "🎮 ODZEN - ¡Nuevo juego detectado!";
                    message = $"\"{gameTitle}\" detectado. Abre ODZEN para añadirlo a tu biblioteca.";
                    break;
                case AppLanguage.Dutch:
                    title = "🎮 ODZEN - Nieuwe game gedetecteerd!";
                    message = $"\"{gameTitle}\" gedetecteerd. Open ODZEN om toe te voegen aan je bibliotheek.";
                    break;
                case AppLanguage.French:
                    title = "🎮 ODZEN - Nouveau jeu détecté !";
                    message = $"\"{gameTitle}\" a été détecté. Ouvrez ODZEN pour l'ajouter à votre bibliothèque.";
                    break;
                case AppLanguage.Russian:
                    title = "🎮 ODZEN - Обнаружена новая игра!";
                    message = $"Обнаружена игра \"{gameTitle}\". Откройте ODZEN, чтобы добавить её в библиотеку.";
                    break;
                case AppLanguage.English:
                    title = "🎮 ODZEN - New Game Detected!";
                    message = $"\"{gameTitle}\" detected. Open ODZEN to add it to your library.";
                    break;
                default:
                    title = "🎮 ODZEN - Yeni Oyun Algılandı!";
                    message = $"\"{gameTitle}\" tespit edildi. Kütüphanenize eklemek için ODZEN'i açın.";
                    break;
            }

            ShowWindowsNotification(title, message);
        }
    }
}
