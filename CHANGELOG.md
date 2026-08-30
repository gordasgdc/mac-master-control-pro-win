# Changelog — Mac Master Control Pro (Windows)

## v1.1.0 (2026-08-30)
Paritate de module cu Mac:
- **Cloud Manager Universal**: 10 provideri Rclone, montare pe literă de disc (WinFSP).
- **Curățare & RAM**: analiză spațiu recuperabil, curățare cache media, purjare RAM + flush DNS.
- **Tweak-uri Sistem**: Explorer avansat, blocare thumbs.db pe rețea, protecție folder de indexare Windows Search.
- **Dependency Auto-Installer**: verificare/instalare Rclone + WinFSP prin winget.
- Rosetta Inspector NU portat (concept Mac-only, Apple Silicon vs Intel).
- Localizare RO/EN/ES completă în UI: rămâne pentru o versiune viitoare (doar ghidul PDF alege limba automat).

## v1.0.0 (2026-08-30)
Scaffold inițial: `MacMasterControlPro.Core` + `.Client` (WPF), licențiere
Ed25519 reală, temă System/Light/Dark, Mărime Text, Sidebar Footer
(profil/Machine ID/versiune/update), Self-Updater real, modulul Rețea.
