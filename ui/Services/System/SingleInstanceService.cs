// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Odzen.Avalonia.Services
{
    /// <summary>
    /// Aynı Windows oturumu içerisinde yalnızca tek bir ODZEN launcher örneğinin
    /// çalışmasını sağlar. İkinci bir başlatma durumunda çalışan ilk örneği
    /// ön plana odaklayıp yeni örneği anında sonlandırır.
    /// </summary>
    public static class SingleInstanceService
    {
        private static Mutex? _mutex;
        private static bool _hasHandle;
        private static CancellationTokenSource? _cts;

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int ASFW_ANY = -1;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private static string GetSessionIdentifier()
        {
            try
            {
                string rawUser = Environment.UserName.Trim().ToLowerInvariant();
                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rawUser));
                return Convert.ToHexString(hash)[..12];
            }
            catch
            {
                return "Default";
            }
        }

        private static string MutexName => $@"Local\ODZEN_SingleInstance_{GetSessionIdentifier()}";
        private static string PipeName => $@"ODZEN_Pipe_{GetSessionIdentifier()}";

        /// <summary>
        /// Mevcut oturum için tekil örnek kilidini almaya çalışır.
        /// </summary>
        /// <returns>İlk örnek ise true, zaten çalışan bir örnek varsa false döner.</returns>
        public static bool TryAcquireMutex()
        {
            try
            {
                _mutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    _hasHandle = false;
                    return false;
                }
                _hasHandle = true;
                return true;
            }
            catch (AbandonedMutexException)
            {
                // Önceki süreç beklenmedik şekilde çöktüyse kilidi devral
                _hasHandle = true;
                return true;
            }
            catch
            {
                // Herhangi bir güvenlik veya işletim sistemi hatasında güvenli çalışmaya izin ver
                return true;
            }
        }

        /// <summary>
        /// Zaten çalışmakta olan ilk örneğe pencereyi ekrana getirmesi için IPC sinyali gönderir.
        /// </summary>
        public static void NotifyExistingInstance(string[]? args = null)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Windows'un arka plan pencere odaklama kısıtını kaldır
                    AllowSetForegroundWindow(ASFW_ANY);
                }

                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1500); // 1.5 saniye bekleme zaman aşımı
                using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                string payload = args != null && args.Length > 0 ? string.Join(" ", args) : "SHOW";
                writer.WriteLine(payload);
            }
            catch
            {
                // İlk örnek yanıt vermese bile ikinci örneğin kapanmasını sağla
            }
        }

        /// <summary>
        /// İlk örnek için arka planda IPC Named Pipe dinleyicisini başlatır.
        /// </summary>
        public static void StartIpcServer(Action onActivateRequested)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                        using var reader = new StreamReader(server, Encoding.UTF8);
                        var message = await reader.ReadLineAsync(token).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            onActivateRequested?.Invoke();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Bağlantı kopmalarını yut ve bir sonraki isteği beklemeye devam et
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                }
            }, token);
        }

        /// <summary>
        /// Windows HWND tanıtıcısı üzerinden pencereyi simge durumundan kurtarır ve en ön plana odaklar.
        /// </summary>
        public static void BringWindowToFront(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !OperatingSystem.IsWindows()) return;

            try
            {
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else
                {
                    ShowWindow(hWnd, SW_SHOW);
                }

                SetForegroundWindow(hWnd);
            }
            catch
            {
                // Sessizce yut
            }
        }

        /// <summary>
        /// Uygulama kapanışında kaynakları ve mutex kilidini temizler.
        /// </summary>
        public static void ReleaseMutex()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                if (_hasHandle && _mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                    _mutex = null;
                    _hasHandle = false;
                }
            }
            catch
            {
                // Sessizce yut
            }
        }
    }
}
