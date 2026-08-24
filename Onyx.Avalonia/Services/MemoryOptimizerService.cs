// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Onyx.Avalonia.Services
{
    public static class MemoryOptimizerService
    {
        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private static Timer? _trimTimer;

        public static void Initialize()
        {
            // Her 25 saniyede bir arka planda RAM optimizasyonu
            _trimTimer = new Timer(_ => TrimMemory(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(25));
        }

        public static void TrimMemory()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, false, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, false, true);

                if (OperatingSystem.IsWindows())
                {
                    using var currentProcess = Process.GetCurrentProcess();
                    EmptyWorkingSet(currentProcess.Handle);
                }
            }
            catch
            {
                // Sessizce yut
            }
        }
    }
}
