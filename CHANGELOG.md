# Changelog — Master Control Studio Pro (Windows)

## v1.8.0 (2026-08-30) — Fazele 2+3: Statistici live + Explorare remote fără montare

- **Faza 2**: fiecare cont montat afișează viteza de transfer live, bytes
  transferați și transferuri active (`rclone rc core/stats`, port RC unic
  per montare — deja stabilit la fix-ul de demontare din v1.7.0).
- **Faza 3**: fereastră nouă „Explorează" — răsfoiește conținutul unui cont
  Cloud (`rclone lsjson`) FĂRĂ să-l montezi, cu navigare pe foldere. Buton
  „Deschide" pe conturile montate — deschide direct în Explorer.
- Cloud Manager complet, paritate cu Mac: locație de montare configurabilă
  (v1.7.0), statistici live, explorare fără montare.

## v1.7.0 (2026-08-30) — Faza 1: Locație de montare configurabilă (disc extern)

Cloud Manager permite acum alegerea unui folder (posibil pe disc extern)
unde se montează conturile, în loc de o literă de disc nouă automată.
Fix real adiacent, găsit în timpul lucrului: demontarea folosea un port RC
fix (5572) comun pentru toate montările — a doua montare simultană ar fi
demontat-o accidental pe prima. Acum fiecare montare are propriul port RC
unic. Progresul de montare/demontare apare linie cu linie în Terminal Live.

## v1.6.1 (2026-08-30) — Donație actualizată la 17€

Decizie Cristi: rămâne un singur nivel de licențiere — suma de referință a
donației Lifetime crește de la 9€ la 17€. Actualizat în `TrialGateWindow`
și mesajul WhatsApp de activare.

## v1.6.0 (2026-08-30) — Panou „terminal live” + butoane roșu/verde la Dependențe + fix buton actualizări invizibil

- **Fix**: `CheckUpdatesButton` nu avea `Foreground` explicit — invizibil sub
  tema Light, exact ca restul textului din sidebar (v1.5.0). Self-Updater-ul
  era deja complet implementat (descărcare reală + instalare + relansare,
  ca la DataMover/GDCVault) — doar butonul era invizibil.
- **`TerminalLogView`** (`Controls/TerminalLogView.xaml`) — panou reutilizabil
  tip terminal, afișează linie cu linie orice comandă externă rulată.
- **Dependențe rescrise**: fiecare pachet (Rclone, WinFSP) are propriul
  buton — roșu (neinstalat, apăsabil) devine verde (instalat) după succes.
  Niciun buton „instalează tot" — instalare pas-cu-pas, ca să nu blocheze
  sistemul.
- **Fix real ștergere cache**: catch-ul original învelea toată bucla de
  ștergere, nu fiecare fișier — un singur fișier blocat oprea silențios
  TOATĂ operația. Acum fiecare fișier are propriul try/catch + raportare în
  panoul terminal.

## v1.5.1 (2026-08-30) — Fix real: „Adaugă cont Cloud” + indicator verde Dependențe

**Fix cauza reală a erorii rclone**: „Adaugă cont Cloud” eșua cu
„the system cannot find the file specified", cu `WorkingDirectory =
C:\Program Files\GDC Plugin Manager` — userul lansase Master Control Studio
Pro din butonul „Deschide" al GDC Plugin Manager. `Process.Start
(UseShellExecute:true)` fără `WorkingDirectory` explicit moștenește
directorul curent al PĂRINTELUI, nu al acestei aplicații. Fix:
`Environment.CurrentDirectory` resetat explicit la propriul folder, la
pornire, indiferent cine a lansat aplicația.

**Fix UX Dependențe**: checkbox-ul dezactivat pentru un pachet deja
instalat nu arăta niciun indicator verde — confuz („de ce nu apare cu
verde?"). Punctul de status (verde/roșu) e acum mereu vizibil; checkbox-ul
apare DOAR pentru componentele neinstalate.

## v1.5.0 (2026-08-30) — Fix meniu invizibil (temă Light) + selecție granulară Dependențe

**Fix real, raportat de Cristi**: sidebar-ul (Panel/Rețea/Cloud/etc.) avea
fundal fix întunecat (`#14161A`), dar textul n-avea `Foreground` explicit —
moștenea tema globală (Light/Dark). Când sistemul era pe Light, textul
devenea aproape negru pe fundalul întunecat = invizibil. Fix: sidebar-ul are
acum text mereu deschis (`#EDEFF2`), independent de tema aleasă pentru
restul ferestrei.

**Dependențe & Cerințe Sistem — rescris complet**:
- Bifă individuală per componentă instalabilă (Rclone, WinFSP) — NU mai
  există buton „instalează tot ce lipsește"; Selectează exact ce vrei,
  nimic nu pornește fără bifă explicită.
- **Fix cauza reală a erorii de instalare**: după un `winget install`
  reușit, verificarea imediată tot raporta „Neinstalat" — procesul GUI e de
  lungă durată, deci PATH-ul moștenit de orice comandă pornită de el rămâne
  cel de la lansarea aplicației; winget actualizează PATH doar în Registry,
  nu și în procesele deja pornite. `Shell.cs` citește acum PATH-ul proaspăt
  (Machine+User) din Registry la fiecare comandă — un CLI nou instalat e
  văzut instant, fără repornirea aplicației.

## v1.4.2 (2026-08-30) — FIX CRITIC (cauza reală): NullReferenceException la pornire

Log-ul de diagnostic din v1.4.1 a prins imediat cauza reală: `ItemDashboard`
avea `IsSelected="True"` în `MainWindow.xaml`, ceea ce declanșa
`OnModuleSelected` SINCRON în timpul `InitializeComponent()` — la acel
moment `PageHost` (declarat mai jos în același XAML) încă nu era asignat,
deci `PageHost.Content = ...` arunca `NullReferenceException` chiar în
constructor, prinsă de handler-ul din v1.4.1 (care măcar a arătat eroarea
în loc să eșueze silențios ca înainte).

- Fix: eliminat `IsSelected="True"` din XAML; selecția vizuală „Dashboard”
  se face acum programatic, după ce `PageHost` e garantat asignat.
- v1.4.1 (log de diagnostic + handler global de excepții) rămâne inclus.

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
