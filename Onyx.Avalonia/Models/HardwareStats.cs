// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using CommunityToolkit.Mvvm.ComponentModel;

namespace Onyx.Avalonia.Models
{
    public partial class HardwareStats : ObservableObject
    {
        [ObservableProperty]
        private int _cpuUsagePercent = 12;

        [ObservableProperty]
        private int _gpuUsagePercent = 8;

        [ObservableProperty]
        private double _ramUsageGb = 160;

        [ObservableProperty]
        private int _ramPercent = 15;

        [ObservableProperty]
        private int _fps = 144;
    }
}
