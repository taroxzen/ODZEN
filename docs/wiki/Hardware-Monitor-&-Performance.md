# 📊 Hardware Monitor & Performance

The ONYX Launcher sidebar features a real-time hardware telemetry dashboard displaying key metrics about your system performance without requiring third-party tools like MSI Afterburner or HWMonitor.

---

## 📈 Monitored System Metrics

* **CPU Utilization (%):** Calculated via Win32 GetSystemTimes kernel idle / user time deltas.
* **RAM Usage (% & GB):** Total system physical memory vs. available memory.
* **GPU Utilization (%):** Direct3D / NVAPI / DXGI adapter query telemetry.
* **VRAM Allocation (GB):** Dedicated video memory usage.

---

## ⚡ Zero-Impact Performance Engineering

1. **Low Polling Frequency:** Telemetry updates every 1,500 ms when the launcher is focused, and stops completely when in the tray.
2. **Minimal CPU Overhead:** Telemetry uses less than 0.05% CPU on a modern 4-core processor.
3. **RAM Trimming on Idle:** Minimizing to tray invokes SetProcessWorkingSetSize(-1, -1), reducing memory usage to under 20 MB.