// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using Avalonia;
using Avalonia.Threading;
using Odzen.Avalonia.Services;

namespace Odzen.Avalonia
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Tekil launcher kontrolü: Aynı oturumda ikinci bir örneğin çalışmasını engelle
            if (!SingleInstanceService.TryAcquireMutex())
            {
                // Çalışan ilk örneği ön plana getir ve yeni süreci derhal kapat
                SingleInstanceService.NotifyExistingInstance(args);
                return;
            }

            try
            {
                // İlk örnek: Arka planda IPC dinleyicisini başlat (diğer başlatma çağrılarını yakalamak için)
                SingleInstanceService.StartIpcServer(() =>
                {
                    Dispatcher.UIThread.Post(App.BringToForeground);
                });

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch { }
            finally
            {
                SingleInstanceService.ReleaseMutex();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
