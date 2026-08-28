> 🇬🇧 **English** | 🇹🇷 [Türkçe'ye Geç](Security-&-Privacy)

# 🛡️ Security & Privacy

Security and user privacy are foundational design principles of ONYX Launcher.

---

## 🔒 Security Hardening in ONYX Launcher

* **PowerShell Script Injection Defense:** Toast notification payloads are encoded using UTF-16 Base64 (-EncodedCommand).
* **Native Protocol Execution:** Web links and protocols launch via undll32 url.dll,FileProtocolHandler.
* **Path Traversal Protection:** All cache paths are sanitized using alphanumeric regex and SHA-256.
* **Zero Telemetry:** No tracking scripts or analytics servers. All data stays local.