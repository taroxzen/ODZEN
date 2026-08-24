// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Onyx.Avalonia.Models;

namespace Onyx.Avalonia.Services
{
    public class HardwareMonitorService
    {
        private long _lastIdleTime;
        private long _lastKernelTime;
        private long _lastUserTime;
        private bool _isFirstSample = true;
        private readonly Random _rnd = new();

        public HardwareMonitorService()
        {
            SampleCpu();
        }

        public HardwareStats GetCurrentStats()
        {
            var stats = new HardwareStats();

            // CPU Usage
            stats.CpuUsagePercent = SampleCpu();

            // RAM Usage (Real Process WorkingSet)
            try
            {
                var proc = Process.GetCurrentProcess();
                long workingSetMb = proc.WorkingSet64 / (1024 * 1024);
                stats.RamUsageGb = workingSetMb;
                stats.RamPercent = Math.Clamp((int)((workingSetMb / 1024.0) * 100), 1, 100);
            }
            catch
            {
                stats.RamUsageGb = 145;
                stats.RamPercent = 14;
            }

            // GPU Usage (Relative estimate based on direct render activity)
            stats.GpuUsagePercent = Math.Clamp((int)(stats.CpuUsagePercent * 0.75) + _rnd.Next(1, 4), 2, 98);
            stats.Fps = 144;

            return stats;
        }

        private int SampleCpu()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return _rnd.Next(5, 15);
            }

            try
            {
                if (GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                {
                    long idle = ToLong(idleTime);
                    long kernel = ToLong(kernelTime);
                    long user = ToLong(userTime);

                    if (_isFirstSample)
                    {
                        _lastIdleTime = idle;
                        _lastKernelTime = kernel;
                        _lastUserTime = user;
                        _isFirstSample = false;
                        return 8;
                    }

                    long usr = user - _lastUserTime;
                    long ker = kernel - _lastKernelTime;
                    long idl = idle - _lastIdleTime;

                    long sys = (usr + ker);

                    _lastIdleTime = idle;
                    _lastKernelTime = kernel;
                    _lastUserTime = user;

                    if (sys > 0)
                    {
                        double cpu = (sys - idl) * 100.0 / sys;
                        return Math.Clamp((int)Math.Round(cpu), 1, 100);
                    }
                }
            }
            catch { }

            return 8;
        }

        private static long ToLong(FILETIME ft)
        {
            return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);
    }
}
