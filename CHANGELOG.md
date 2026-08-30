# Changelog — Master Control Studio Pro (Windows)

## v1.4.1 (2026-08-30) — FIX CRITIC: aplicația nu deschidea nicio fereastră

**Bug real, raportat de Cristi după primul test pe Windows real**: aplicația
pornea (apărea în Task Manager, era detectată corect de GDC Plugin Manager),
dar NU se vedea nicio fereastră — `App.xaml` nu avea `StartupUri` și nicăieri
în cod nu se apela `new MainWindow().Show()`. `dotnet build` nu prinde
niciodată asta (XAML/BAML compilează identic cu sau fără StartupUri) — doar
rularea reală pe Windows a arătat problema.

- Fix: `App.xaml.cs` creează și afișează explicit `MainWindow` în `OnStartup`.
- **Log de diagnostic nou** (`DiagnosticLog.cs`, port 1:1 din GDCPluginManagerWin) — scrie la `%TEMP%\mmcpro-crash.log`, cu handler global pentru excepții nehandled (`AppDomain.UnhandledException`/`DispatcherUnhandledException`) care arată și un `MessageBox` vizibil în loc să eșueze silențios.

## v1.4.0 (2026-08-30)
**Standard Global de Multi-Selecție**, port 1:1 din Mac v2.3.0:
- **Spotlight Shield (Tweak-uri Sistem)**: `ProtectFromIndexing(un singur folder)` a devenit un manager cu listare automată a discurilor (`DriveInfo.GetDrives()`, exclus discul de sistem) + foldere adăugate multiplu (`OpenFolderDialog.Multiselect`), fiecare cu bifă „protejat" (`FileAttributes.NotContentIndexed`), Selectează/Deselectează tot, contor „Protejate X din Y".
- **Rețea**: plăcile de rețea au bifă individuală (nu doar un `ComboBox` cu un singur adaptor); Tuning TCP/DNS se aplică pe toate plăcile bifate simultan.
- **Cloud Manager**: conturile au bifă proprie, Selectează/Deselectează tot, „Montează selecția"/„Demontează selecția".

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
