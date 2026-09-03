// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Odzen.Avalonia.ViewModels;
using Odzen.Avalonia.Views;

namespace Odzen.Avalonia
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
                desktop.Exit += (s, e) => Services.SingleInstanceService.ReleaseMutex();
            }

            Services.MemoryOptimizerService.Initialize();
            base.OnFrameworkInitializationCompleted();
        }

        public static void BringToForeground()
        {
            try
            {
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    var window = desktop.MainWindow;

                    if (!window.IsVisible)
                    {
                        window.Show();
                    }

                    if (window.WindowState == global::Avalonia.Controls.WindowState.Minimized)
                    {
                        window.WindowState = global::Avalonia.Controls.WindowState.Normal;
                    }

                    window.Activate();

                    if (OperatingSystem.IsWindows())
                    {
                        var handle = window.TryGetPlatformHandle()?.Handle;
                        if (handle.HasValue && handle.Value != IntPtr.Zero)
                        {
                            Services.SingleInstanceService.BringWindowToFront(handle.Value);
                        }
                    }
                }
            }
            catch
            {
                // Sessizce yut
            }
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
                        openHeader = "ODZEN öffnen";
                        exitHeader = "Beenden";
                        toolTip = "ODZEN - Spielebibliothek";
                        break;
                    case Services.AppLanguage.Bulgarian:
                        openHeader = "Отвори ODZEN";
                        exitHeader = "Изход";
                        toolTip = "ODZEN - Библиотека с игри";
                        break;
                    case Services.AppLanguage.Spanish:
                        openHeader = "Abrir ODZEN";
                        exitHeader = "Salir";
                        toolTip = "ODZEN - Biblioteca de juegos";
                        break;
                    case Services.AppLanguage.Dutch:
                        openHeader = "Open ODZEN";
                        exitHeader = "Afsluiten";
                        toolTip = "ODZEN - Gamebibliotheek";
                        break;
                    case Services.AppLanguage.French:
                        openHeader = "Ouvrir ODZEN";
                        exitHeader = "Quitter";
                        toolTip = "ODZEN - Bibliothèque de jeux";
                        break;
                    case Services.AppLanguage.Russian:
                        openHeader = "Открыть ODZEN";
                        exitHeader = "Выход";
                        toolTip = "ODZEN - Игровая библиотека";
                        break;
                    case Services.AppLanguage.English:
                        openHeader = "Open ODZEN";
                        exitHeader = "Exit";
                        toolTip = "ODZEN - Game Library";
                        break;
                    default:
                        openHeader = "ODZEN'i Aç";
                        exitHeader = "Çıkış";
                        toolTip = "ODZEN - Oyun Kütüphanesi";
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
                Services.SingleInstanceService.ReleaseMutex();
                desktop.Shutdown();
            }
        }
    }
}
