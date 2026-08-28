# 🛡️ Security & Privacy

Security and user privacy are foundational design principles of ONYX Launcher.

---

## 🔒 Security Hardening in ONYX Launcher

### 1. PowerShell Script Injection Defense
* In [WindowsNotificationService.cs](https://github.com/taroxzen/ONYX-Launcher/blob/main/Onyx.Avalonia/Services/WindowsNotificationService.cs), toast notification parameters are encoded as UTF-16 Base64 strings passed via -EncodedCommand.
* This completely eliminates quotation breaking, backtick interpolation, and shell injection vulnerabilities.

### 2. Native Protocol Execution
* Web links and platform protocols are opened via undll32 url.dll,FileProtocolHandler instead of cmd.exe /C start.
* Prevents Windows Command Prompt meta-character chaining (&, |, ^).

### 3. Path Traversal & Cache Sanitation
* All cache files are filtered using strict regex ([a-zA-Z0-9_\-]) and SHA-256 hashing.
* Sequences like ../ or absolute drive root injections are strictly prevented.

### 4. Network Bounds (DoS Protection)
* HTTP download engines enforce a hard limit of MaxResponseContentBufferSize = 10 MB.
* Mitigates memory exhaustion from malformed CDN responses.

---

## 🕵️ Zero-Telemetry Privacy Guarantee

* **No tracking scripts:** ONYX Launcher does not send your hardware profile, library list, or personal data to any private analytics server.
* **Local Storage:** All preferences and library state are stored exclusively on your machine in %LOCALAPPDATA%\ONYX\library.json.