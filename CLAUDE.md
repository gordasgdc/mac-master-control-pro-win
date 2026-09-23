# Mac Master Control Pro (Windows) — note de arhitectura

Oglinda C#/.NET 8 WPF a `mac-master-control-pro` (Mac). ID produs
oficial: `mac-master-control-pro`. Vezi `~/Developer/MacMasterControlPro/CLAUDE.md`
(Mac) pentru rationamentul complet — aici doar ce difera.

## [PARTEA 1: REGULI GLOBALE ECOSISTEM GDC] — mutată în `~/Developer/CLAUDE.md`

> Din 2026-09-18, regulile globale stau într-un singur fișier,
> `~/Developer/CLAUDE.md`, citit automat de Claude Code în orice proiect din
> `~/Developer/`. Nu se mai copiază aici. Ce era specific acestui repo în fosta
> Partea 1 (statusuri, excepții) e la finalul fișierului.

## [PARTEA 2: SPECIFICATII TEHNICE PROIECT]

## Structura repo-ului

- `src/MacMasterControlPro.Core/Services/` — `LicenseCore.cs`/`MachineID.cs`
  (copiate din GDCVaultWin, namespace ajustat, aceeasi cheie publica Ed25519),
  `LicenseManager.cs` (productID `mac-master-control-pro`, trial NELIMITAT
  fara numarare de zile — diferit de GDCVault), `TextScaleStore.cs`,
  `UpdateChecker.cs` (GitHub Releases API, repo `mac-master-control-pro-win`),
  `Shell.cs`/`PrivilegedRunner.cs` (PowerShell + UAC `runas`, echivalent
  Shell.swift/PrivilegedRunner.swift Mac), `NetworkService.cs` (netsh/
  Set-DnsClientServerAddress), `UserProfileStore.cs`.
- `src/MacMasterControlPro.Client/` — WPF (`Wpf.Ui` 3.0.5,
  `CommunityToolkit.Mvvm`), `Pages/` (Dashboard/Network/Cloud/Cleanup/
  Tweaks/Dependencies/Settings — paritate completa cu Mac, cu exceptia
  Rosetta Inspector, concept Mac-only fara echivalent Windows).
- `installer.iss` — Inno Setup, `LicenseFile=installer\license.txt`
  (Regula 19 — GDCVaultWin NU are asta, gap real gasit in trecere).
- `uninstall.ps1` — dezinstalare completa (Regula Clean Uninstall).

## Stare curenta (2026-08-30) — v1.1.0, paritate module cu Mac

Toate modulele Mac portate, cu adaptarile de platforma:
- **Cloud Manager Universal** (`CloudManagerService.cs`) — acelasi set de
  10 provideri Rclone; monteaza pe litera de disc libera (WinFSP), nu
  folder Desktop ca pe Mac.
- **Curatare & RAM** (`CleanupService.cs`) — cai Windows (DaVinci CacheClip,
  Adobe Media Cache, %Temp%), `EmptyWorkingSet` (P/Invoke psapi.dll) +
  `ipconfig /flushdns` in loc de purge/dscacheutil.
- **Tweak-uri Sistem** (`TweaksService.cs`) — Explorer avansat (Registry),
  block thumbs.db pe retea, `FileAttributes.NotContentIndexed` ca
  echivalent Spotlight Shield (atribut standard Windows Search).
- **Dependency Auto-Installer** (`DependencyChecker.cs`) — winget in loc
  de Homebrew, verifica/instaleaza Rclone + WinFSP.
- **Rosetta Inspector**: NU portat — concept Mac-only (Apple Silicon vs
  Intel), fara echivalent real pe Windows.
- Localizare RO/EN/ES completa in UI: NU inca facuta — doar ghidul PDF
  alege limba dupa `CultureInfo.CurrentUICulture`. Ramane pentru o
  versiune viitoare.

Build verificat local (`dotnet build`, XAML->BAML inclus, gratie
`EnableWindowsTargeting`) — 0 erori, 0 warning-uri. **Verificare finala
tot prin CI Windows real** (`build-windows.yml`) inainte de orice release.

## Release v1.2.0 (2026-08-30) — primul release oficial

Publicat direct din artefactul CI real (`windows-latest`, XAML->BAML
compilat efectiv + `dotnet publish` self-contained win-x64 + Inno Setup
`ISCC.exe`), nu doar build local de pe Mac. Assets:
`MacMasterControlProSetup-1.2.0.exe` (Regula 17) +
`MacMasterControlProSetup.exe` (stabil, tinta Self-Updater-ului si a
site-ului) — ambele verificate `releases/latest/download/...` HTTP 200.

**Nesemnat cu certificat Authenticode platit** — Windows SmartScreen
poate arata "Unrecognized app / Windows protected your PC" la prima
rulare (normal pentru distributie indie, aceeasi nota ca restul
ecosistemului Windows GDC — vezi installer.iss).

**WARNING (Regula 20)**: pasul efectiv de instalare (wizard-ul Inno,
clickurile userului, promptul SmartScreen) NU a putut fi verificat
automat — doar descarcarea reala (HTTP 200, executabil PE32 valid,
57MB) e confirmata. Instalarea + auto-update-ul efectiv TREBUIE
confirmate manual, o data, de Cristi.

## Etapa 2026-08-31 — Port complet al 7 module noi de pe Mac (v1.10.0)

Portate 1:1 toate cele 7 functionalitati noi construite pe Mac in aceeasi
sesiune (Nivel 1: Mod Randare, Pornire Sistem, Sanatate Discuri; Nivel 2:
Auditor Media Pool + Notificare Randare; Nivel 3: Sincronizare LUT/Fusion,
Layout Ferestre) - vezi `~/Developer/MacMasterControlPro/CLAUDE.md` pentru
rationamentul original al fiecareia.

**Adaptari reale de platforma (nu doar redenumiri 1:1):**
- **Mod Randare**: `sc config WSearch`/`sc stop fhsvc` in loc de
  `mdutil`/`tmutil` - Windows Search + File History sunt echivalentele
  Windows ale Spotlight/Time Machine.
- **Pornire Sistem**: Registry `Run` (HKCU+HKLM) + folder Startup, nu
  LaunchAgents/.plist - dezactivare HKLM cere UAC (`PrivilegedRunner`),
  HKCU nu.
- **Sanatate Discuri**: SMART prin WMI (`root\WMI`,
  `MSStorageDriver_FailurePredictStatus`) - nu toate discurile externe USB
  expun asta prin punte, degradeaza elegant (camp `null`, nu eroare).
- **DaVinci Resolve (Audit + Notificare)**: ACEEASI punte Python catre
  Scripting API-ul oficial ca pe Mac (`import DaVinciResolveScript`), doar
  caile difera: `RESOLVE_SCRIPT_API` = `%PROGRAMDATA%\Blackmagic Design\
  DaVinci Resolve\Support\Developer\Scripting\`, `RESOLVE_SCRIPT_LIB` =
  `fusionscript.dll` (nu `.so`). `python.exe` gasit prin `where`, nu cai
  hardcodate - Windows nu are un python de sistem implicit ca Mac.
- **Layout Ferestre**: Win32 API (`EnumWindows`/`GetWindowRect`/
  `MoveWindow`) - SPRE DEOSEBIRE de Mac, NU necesita nicio permisiune
  speciala (Accessibility) - un proces poate repozitiona ferestrele altui
  proces al ACELUIASI utilizator fara UAC.

**Notificare pe email (cerinta separata, aparuta in aceeasi sesiune)**:
Cristi a intrebat explicit "nu inteleg cum functioneaza daca nu vad sa pun
nr de telefon" dupa ce a vazut doar notificarea nativa pe ecran - clarificat
ca WhatsApp NU poate trimite automat fara interactiune (doar deschide o
conversatie pre-completata, la fel ca la activarea licentei), email fiind
SINGURA varianta cu adevarat automata catre telefon. Adaugat
`EmailNotifierService.cs` (`System.Net.Mail.SmtpClient`, marcat obsolet in
.NET dar functional pentru acest caz simplu sincron - fara dependinta noua
tip MailKit), stocare LOCALA in clar (JSON in AppData) - recomanda explicit
in UI o "parola de aplicatie" Gmail/Outlook, nu parola reala a contului.

**Bug real gasit la implementare (nu doar port, fix propriu acestei
platforme)**: activarea `UseWindowsForms=true` (necesara STRICT pentru
`NotifyIcon`, balloon tip-ul de notificare) a stricat compilarea INTREGULUI
proiect existent - SDK-ul .NET adauga automat, prin `ImplicitUsings`,
using-uri globale pentru `System.Windows.Forms`/`System.Drawing`, care intra
in conflict de nume cu WPF (`UserControl`, `Application`, `TextBox`,
`Brushes`, `Color` exista identic in ambele namespace-uri) - a rupt
COMPILAREA a peste 10 fisiere existente, nu doar codul nou. Fix: 
`<Using Remove="System.Windows.Forms" />` + `<Using Remove="System.Drawing" />`
in `.csproj`, fara sa se atinga niciun fisier existent (codul foloseste
`System.Windows.Forms.NotifyIcon` complet calificat oricum). **Regula
practica noua**: orice proiect WPF existent care activeaza `UseWindowsForms`
pentru prima data trebuie sa verifice explicit acest conflict, nu doar sa
presupuna ca merge.

**Verificat**: `dotnet build` (Core + Client, C# + XAML->BAML, de pe Mac
via `EnableWindowsTargeting`) - 0 erori, 0 avertismente. **NU verificat
REAL pe Windows** (WARNING standard, Regula 20) - UAC-ul efectiv de la Mod
Randare/Pornire Sistem, notificarea nativa (NotifyIcon), scripting-ul
Resolve cu un `python.exe` real instalat, toate necesita confirmare
manuala, o data, de Cristi, inainte de a declara portul complet dovedit.

## v1.18.0 (2026-09-04) — Modul nou: Analiză Disc (port complet, de la zero)

Portul cerut explicit de Cristi al refactorizării complete de pe Mac
(`MacMasterControlPro/CLAUDE.md`, v2.30.0) — modulul nu exista DELOC pe
Windows înainte de asta (confirmat prin `grep` — doar `DiskHealthService`/
`DiskHealthPage`, sănătate SMART, complet altceva), TODO-ul rămas deschis
explicit în v1.17.4 de mai jos.

**4 fișiere noi în Core** (`DiskNode.cs`, `DiskCacheStore.cs`,
`DiskScanEngine.cs`, `DiskAnalyzerService.cs`) + `Pages/DiskAnalyzerPage.
xaml(.cs)` (nou) — port 1:1 al arhitecturii Mac, cu adaptările reale de
platformă:

- **Enumerare nativă**: `DirectoryInfo.EnumerateFileSystemInfos()`
  (cheamă intern `FindFirstFile`/`FindNextFile`, Win32 nivel jos) în loc
  de `fts(3)` — o singură trecere pe director dă nume+mărime+dată direct,
  fără `stat()` separat per fișier. Citirea directă a MFT (menționată ca
  opțiune în cerința inițială) respinsă pentru ACELAȘI motiv ca pe Mac
  (`getattrlistbulk` respins acolo în favoarea `fts`): complexitate/risc
  disproporționate față de câștig — specific NTFS, cere Administrator, nu
  merge pe FAT32/exFAT/ReFS/unități de rețea.
- **Paralelizare**: `Parallel.For` peste subfolderele de prim nivel —
  echivalentul .NET al `DispatchQueue.concurrentPerform`.
- **Cache persistent**: JSON (`System.Text.Json`, deja folosit în acest
  Core — `BigFileScanFolders`) în loc de `PropertyListEncoder` (Mac) —
  .NET nu are un echivalent binar la fel de simplu fără dependință nouă.
  Nume de fișier stabil prin SHA256 al căii rădăcinii, hash-uită ÎN
  MINUSCULE ÎNTÂI — Windows e case-insensitive la căi, `C:\Date` și
  `c:\date` trebuie să rezolve la ACELAȘI cache.
- **Scanare incrementală**: mtime de folder (`Directory.GetLastWriteTimeUtc`)
  — exact strategia explicit menționată ca portabilă în jurnalul Mac.

**Capcană reală de platformă, verificată ÎNAINTE de a scrie codul (Regula
30 — nu presupunem)**: spre deosebire de `stat()` pe Mac (eșuează cu cod
de eroare pe o cale lipsă), `Directory.GetLastWriteTimeUtc` pe .NET NU
aruncă pe o cale inexistentă — întoarce tăcut sentinela `1601-01-01`. Orice
verificare „a dispărut folderul?" foloseşte explicit `Directory.Exists`,
niciodată o excepție presupusă de la `GetLastWriteTimeUtc`.

**A doua adaptare reală, deliberată**: `DiskNode.Children` folosește
`Dictionary<string, DiskNode>(StringComparer.OrdinalIgnoreCase)` — spre
deosebire de Mac (dicționar Swift implicit case-sensitiv), sistemul de
fișiere Windows compară numele case-INsensitiv; fără asta, o rescanare
incrementală ar putea crea un nod duplicat dacă un folder e listat cu altă
capitalizare între două scanări.

**UI** (`DiskAnalyzerPage`): breadcrumb + bară proporțională (implementată
ca `Grid` cu coloane `Star`, proporționale cu mărimea fiecărei intrări —
WPF distribuie automat lățimea, fără nicio matematică manuală de pixeli,
spre deosebire de `GeometryReader`/`frame(width:)` pe Mac) + listă cu
buton Explorer/Șterge, TerminalLogView, banner cu data ultimei analize +
„Re-scanează doar modificările"/„Resetare Cache & Scanare Completă".
Butonul de lângă un folder deschide o fereastră Explorer NAVIGATĂ DIRECT
în el (`Process.Start("explorer.exe", "\"<cale>\"")`, fără `/select,`) —
echivalentul Windows al `NSWorkspace.open` de pe Mac, cerința explicită a
lui Cristi de a putea vedea efectiv ce e într-un folder, nu doar
evidențiat în părinte.

Ștergerea rămâne în spatele `RequireLicense()` — spre deosebire de Mac
(unde `delete(_:)` din `DiskAnalyzerViewModel` nu verifică nicio licență
deloc), pentru consistență cu TOATE celelalte acțiuni distructive din
clientul Windows (Duplicate, Fișiere mari, Dezinstalator, Tweaks etc. —
verificat prin `grep`, toate 8 pagini cu ștergere/acțiune de sistem
folosesc acest gate). Semnalat explicit aici ca o DIVERGENȚĂ deliberată
față de Mac, nu o eroare de portare — decizia de produs stabilită deja pe
Windows (orice ștergere din urma unei scanări cere licență) a avut
prioritate față de portarea strictă 1:1 a unei omisiuni Mac neverificate.

**Adăugat în navigare**: `ListBoxItem` nou în `MainWindow.xaml` +
etichetă/tooltip/cuvinte-cheie de căutare/switch de pagină în
`MainWindow.xaml.cs`, cheie nouă `sidebar.diskAnalyzer` (RO/EN/ES) în
`Localization.cs`.

**Verificat**: `dotnet build` (Core + Client, C# + XAML→BAML compilat
efectiv, de pe Mac via `EnableWindowsTargeting`) — 0 erori, 0
avertismente. **NU verificat REAL pe Windows** (WARNING standard, Regula
20) — scanarea efectivă pe un disc mare, comportamentul `Parallel.For` sub
încărcare reală, și fereastra Explorer deschisă efectiv necesită
confirmare manuală, o dată, de Cristi, înainte ca portul să fie declarat
complet dovedit.

## v1.17.4 (2026-09-03) — Paritate: fix deadlock Process/Pipe de pe Mac

Port declanșat de auditul de pe Mac (Shell.swift): `Process`/`Pipe` din
.NET are EXACT același risc de deadlock ca `Process`/`NSTask` pe Mac,
documentat chiar de Microsoft (remarks-urile clasei `Process`) — dacă
`RedirectStandardOutput` ȘI `RedirectStandardError` sunt ambele `true`
dar doar unul e citit sincron (`ReadToEnd()`), iar procesul copil scrie
destul pe fluxul necitit, bufferul acelui pipe se umple, copilul se
blochează la scriere, și părintele așteaptă la nesfârșit un proces care
nu mai poate ieși.

Găsite 3 apariții reale, prin `grep` explicit pe tot repo-ul (Regula 30
— nu ne oprim la primul loc găsit):
- `Shell.cs` (`Run`) — `RedirectStandardError = true` dar stderr
  niciodată citit. Cea mai frecvent apelată funcție din tot Core-ul.
- `CloudManagerService.cs` (creare remote rclone) — `ReadToEnd()` pe
  stdout, apoi pe stderr, SECVENȚIAL, FĂRĂ niciun timeout — cel mai grav
  dintre cele 3, un `rclone` cu mult output pe stderr putea bloca
  aplicația definitiv.
- `ResolveMediaAuditService.cs` (`RunPython`) — stderr redirectat dar
  necitit; are timeout (nu blochează definitiv), dar un script cu mult
  output pe stderr consuma tot timeout-ul degeaba și returna eșec
  silențios, deși scriptul ar fi mers normal.

**Fix, identic la toate 3**: citire ASINCRONĂ a ambelor fluxuri
(`OutputDataReceived`/`ErrorDataReceived` + `BeginOutputReadLine`/
`BeginErrorReadLine`), niciodată `ReadToEnd()` sincron după `WaitForExit()`
— părintele golește mereu ambele buffere pe măsură ce sosesc date.

**TODO paritate Windows**: modulul „Analiză Disc" (Mac v2.26.0) — vezi
`MacMasterControlPro/CLAUDE.md` — nu e portat aici, rămâne de implementat
separat (drill-down pe niveluri + `du`/`find`-echivalent Windows, probabil
`Get-ChildItem` recursiv per nivel prin PowerShell).

**Verificat**: `dotnet build` — 0 erori, 0 avertismente. **NU verificat
REAL pe Windows** (WARNING standard, Regula 20) — comportamentul cu output
mare (ex. o scanare rclone reală de sute de fișiere) necesită confirmare
manuală, o dată, de Cristi.

## Rebuild local (verificare sintaxa, NU echivalent build Windows real)

```bash
cd ~/Developer/MacMasterControlProWin && dotnet build
```

## Etapa 2026-09-11 — v1.18.1 publicat cu semnare Windows activa

Secretele CI (`WIN_SELFSIGN_PFX_BASE64`/`WIN_SELFSIGN_PFX_PASSWORD`,
certificat COMUN ecosistemului) erau deja incarcate de Cristi. Acest release
e primul in care semnarea Regulii 34 chiar a rulat pe un build real.

Verificat direct, nu presupus: pasul de semnare marcat OK in lista de pasi a
job-ului, plus directorul de securitate din header-ul PE al installer-ului
descarcat = 7496 bytes de semnatura Authenticode. Link stabil
`releases/latest/download/...` verificat HTTP 200.

Release creat manual din artefactul CI (repo fara automatizare de release).

### Completări specifice acestui repo, mutate din fosta Partea 1 (2026-09-18)

Păstrate verbatim. Regula generală la care se referă fiecare e în
`~/Developer/CLAUDE.md`.

**Regula 34:**

  installer (asset de release sau folder `dist/`) — colaboratorii îl
  importă o SINGURĂ dată în Trusted Root, apoi orice build viitor semnat
  cu ACELAȘI certificat (persistent via secret CI, NU regenerat la
  fiecare build — un cert nou la fiecare release ar rupe încrederea deja
  acordată) e automat de încredere pe mașinile lor.
- **Aplicare**: la fiecare build de release/actualizare Windows, pe orice
  aplicație din `~/Developer/` care produce un `.exe`/installer Windows —
  aplicată incremental, la următoarea atingere reală a fiecărui repo
  (Regula 11), nu retroactiv peste tot dintr-o sesiune dedicată.
- **Implementare de referință**: CGConvertor (`build-windows.spec` +
  `.github/workflows/build-windows.yml`, 2026-09-06) — vezi
  `codesigning/README-windows.md` din acel repo pentru pașii exacți pe
  care Cristi trebuie să-i ruleze o singură dată (generare cert + upload
  secret CI). **Al doilea repo aliniat**: `mac-master-control-pro-win`
  (`.github/workflows/build-windows.yml`, 2026-09-06) — certificatul e
  COMUN (același `.pfx`, secrete CI identice ca nume dar încărcate
  separat per repo), vezi `codesigning/README-windows.md` din acest repo
  pentru pașii exacți rămași lui Cristi aici.

### Handoff — fișierul de stare (Regula 50, `~/Developer/CLAUDE.md`)

- Fișierul de stare al acestui proiect: `PROJECT_STATE.md` (rădăcina repo-ului). La orice sesiune nouă se citește
  ÎNTÂI el, apoi doar fragmentele strict necesare; se actualizează la milestone-uri și obligatoriu la final.
  Dacă lipsește, se creează la prima sesiune care atinge proiectul. Repo PUBLIC: fișierul e intern, listat în `.gitignore` (doar local, Regula 29).
- Restructurarea/ștergerea lui și orice modificare a acestui `CLAUDE.md`: doar cu diff-ul arătat și acordul lui Cristi.
