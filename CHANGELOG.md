# Changelog — Master Control Studio Pro (Windows)

## v1.3.0 (2026-08-30)
**Selecție granulară** (checkbox-uri per element), port 1:1 din Mac v2.2.0:
- **Curățare & RAM**: fiecare cache (DaVinci/Adobe/%Temp%) are bifă proprie, Selectează/Deselectează tot, contor live „X GB din Y GB", ștergere doar pe elementele bifate.
- **Tweak-uri Sistem**: Explorer avansat + blocare thumbs.db convertite în checklist cu „Aplică tweak-urile selectate".
- Butoanele de acțiune dezactivate fără nicio bifă.

## v1.2.0 (2026-08-30)
- **Rebranding**: "Mac Master Control Pro" → "Master Control Studio Pro" în tot codul (afișare, căi AppData, installer). ProductID și nume repo rămân neschimbate.
- **Localizare RO/EN/ES** completă în UI (Sidebar, Dashboard, Settings, TrialGateWindow) — `Localization.cs`, port 1:1 al Mac.
- **Buton „Activează Licența"** persistent în Sidebar Footer (badge Pro/Trial apăsabil), nu doar la teasing.
- **Contact WhatsApp** în fereastra de activare (`WhatsAppLink.cs`), mesaj pre-completat cu Machine ID.

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
