// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Onyx.Avalonia.ViewModels;
using Onyx.Avalonia.Views;

namespace Onyx.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                desktop.MainWindow.Deactivated += (s, e) => Services.MemoryOptimizerService.TrimMemory();
            }

            Services.MemoryOptimizerService.Initialize();
            base.OnFrameworkInitializationCompleted();
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                if (desktop.MainWindow.IsVisible)
                {
                    desktop.MainWindow.Hide();
                }
                else
                {
                    desktop.MainWindow.Show();
                    desktop.MainWindow.Activate();
                }
            }
        }

        private void OnOpenLauncherClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                desktop.MainWindow.Show();
                desktop.MainWindow.Activate();
            }
        }

        public static void UpdateTrayLanguage(Services.AppLanguage lang)
        {
            try
            {
                if (Current == null) return;
                var icons = global::Avalonia.Controls.TrayIcon.GetIcons(Current);
                if (icons == null || icons.Count == 0) return;

                var tray = icons[0];
                if (tray == null) return;

                string openHeader;
                string exitHeader;
                string toolTip;

                switch (lang)
                {
                    case Services.AppLanguage.German:
                        openHeader = "ONYX Launcher öffnen";
                        exitHeader = "Beenden";
                        toolTip = "ONYX Launcher - Spielebibliothek";
                        break;
                    case Services.AppLanguage.Bulgarian:
                        openHeader = "Отвори ONYX Launcher";
                        exitHeader = "Изход";
                        toolTip = "ONYX Launcher - Библиотека с игри";
                        break;
                    case Services.AppLanguage.Spanish:
                        openHeader = "Abrir ONYX Launcher";
                        exitHeader = "Salir";
                        toolTip = "ONYX Launcher - Biblioteca de juegos";
                        break;
                    case Services.AppLanguage.Dutch:
                        openHeader = "Open ONYX Launcher";
                        exitHeader = "Afsluiten";
                        toolTip = "ONYX Launcher - Gamebibliotheek";
                        break;
                    case Services.AppLanguage.French:
                        openHeader = "Ouvrir ONYX Launcher";
                        exitHeader = "Quitter";
                        toolTip = "ONYX Launcher - Bibliothèque de jeux";
                        break;
                    case Services.AppLanguage.Russian:
                        openHeader = "Открыть ONYX Launcher";
                        exitHeader = "Выход";
                        toolTip = "ONYX Launcher - Игровая библиотека";
                        break;
                    case Services.AppLanguage.English:
                        openHeader = "Open ONYX Launcher";
                        exitHeader = "Exit";
                        toolTip = "ONYX Launcher - Game Library";
                        break;
                    default:
                        openHeader = "ONYX Launcher'ı Aç";
                        exitHeader = "Çıkış";
                        toolTip = "ONYX Launcher - Oyun Kütüphanesi";
                        break;
                }

                tray.ToolTipText = toolTip;
                if (tray.Menu != null && tray.Menu.Items.Count >= 3)
                {
                    if (tray.Menu.Items[0] is global::Avalonia.Controls.NativeMenuItem openItem)
                        openItem.Header = openHeader;
                    if (tray.Menu.Items[2] is global::Avalonia.Controls.NativeMenuItem exitItem)
                        exitItem.Header = exitHeader;
                }
            }
            catch { }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }
}
