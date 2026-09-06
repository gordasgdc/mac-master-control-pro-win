# codesigning/ — semnare Windows (Self-Signed, testare internă)

Acest document acoperă semnarea Windows pentru **Master Control Studio
Pro** — adăugată 2026-09-06 (CLAUDE.md, Regula 34), port direct al
implementării finale (după corecțiile reale) din `CGConvertor`.

## Certificat COMUN pentru tot ecosistemul GDC

Certificatul self-signed e **COMUN tuturor aplicațiilor GDC** (decizie
explicită a lui Cristi) — dacă a fost deja generat pentru un alt repo
(ex. CGConvertor), **NU se regenerează aici**. Secretele CI se numesc
IDENTIC în toate repo-urile (`WIN_SELFSIGN_PFX_BASE64` /
`WIN_SELFSIGN_PFX_PASSWORD`), dar GitHub cere încărcarea lor separat,
per repo — Cristi le încarcă manual, o dată per repo, cu ACEEAȘI
valoare.

## De ce Self-Signed, și ce NU rezolvă

Un certificat self-signed **nu elimină avertismentul SmartScreen/"Unknown
publisher"** pentru publicul larg — doar un certificat real de la o CA
publică (cu reputație acumulată) sau un certificat EV fac asta. Self-signed
e util STRICT pentru:
- testare internă (buildurile pe care le rulează Cristi însuși),
- distribuire către un cerc restrâns de colaboratori care importă manual
  certificatul public (`.cer`) în Trusted Root o singură dată.

La lansarea comercială publică, planul e Azure Trusted Signing sau un
certificat EV (HSM cloud) — vezi CLAUDE.md Regula 34 pentru context complet.

## Setup unic per repo (făcut DIRECT de Cristi pe Windows real)

Certificatul (privat, cu cheie) nu trece niciodată prin conversația cu
Claude — la fel ca orice altă parolă/cheie din ecosistem.

1. Dacă certificatul COMUN GDC NU există încă, pe Windows real (Parallels
   e suficient), deschide PowerShell **ca Administrator** și rulează:
   ```powershell
   .\codesigning\generate-self-signed-cert.ps1
   ```
   Scriptul cere o parolă nouă (pentru `.pfx`) și produce două fișiere:
   - `gdc-selfsign.pfx` — **PRIVAT**, nu se distribuie, nu se comite în
     git.
   - `gdc-selfsign.cer` — **PUBLIC**, se distribuie colaboratorilor
     (o singură dată pentru tot ecosistemul, nu per repo).

   Dacă certificatul COMUN există deja (generat pentru alt repo GDC),
   sari peste acest pas — folosește direct `.pfx`-ul existent la pasul 2.

2. Încarcă `.pfx`-ul ca secrete GitHub Actions **pentru acest repo**
   (comenzile exacte sunt afișate la finalul scriptului dacă tocmai l-ai
   rulat; altfel rulează-le direct, necesită `gh` CLI autentificat pe
   acea mașină):
   ```powershell
   gh secret set WIN_SELFSIGN_PFX_BASE64 --repo gordasgdc/mac-master-control-pro-win --body $b64
   gh secret set WIN_SELFSIGN_PFX_PASSWORD --repo gordasgdc/mac-master-control-pro-win
   ```

3. Șterge `.pfx`-ul local imediat după (`Remove-Item gdc-selfsign.pfx -Force`)
   — rămâne doar în secretele CI, criptate.

4. Distribuie `gdc-selfsign.cer` colaboratorilor (dacă nu a fost deja
   distribuit pentru alt repo GDC). Pe fiecare mașină a lor, o singură
   dată: dublu-click → **Install Certificate** → **Local Machine** →
   "Place all certificates in the following store" → **Trusted Root
   Certification Authorities**.

Odată făcuți pașii 1-4, **fiecare build viitor din CI** (push pe `main`)
semnează automat `.exe`-ul și installer-ul cu ACELAȘI certificat —
colaboratorii nu mai trebuie să reimporte nimic la versiunile următoare,
și niciun alt repo GDC nou nu mai trebuie să regenereze certificatul, doar
să repete pasul 2 cu propriile sale secrete.

## Ce face CI-ul automat (`.github/workflows/build-windows.yml`)

- Dacă secretele NU sunt setate: build-ul continuă **nesemnat**, exact ca
  până acum — nicio eroare, nicio schimbare de comportament.
- Dacă secretele SUNT setate: după ce `MacMasterControlPro.exe`
  (`dotnet publish`, self-contained win-x64) și installer-ul final (Inno
  Setup, `MacMasterControlProSetup.exe`) există, ambele sunt semnate cu
  `signtool.exe` (localizat dinamic din Windows Kits, cu timestamp), apoi
  verificate cu `Get-AuthenticodeSignature` — confirmă DOAR că semnătura
  a fost atașată corect, fără să ceară lanț de încredere complet (asta ar
  eșua mereu pe un runner CI proaspăt, care nu are certificatul în
  Trusted Root — normal pentru self-signed, nu un bug). Un eșec real de
  semnare (fișier fără nicio semnătură) tot oprește build-ul (CI roșu).

## Regenerarea certificatului (dacă expiră sau e compromis)

Rulează din nou `generate-self-signed-cert.ps1`, reîncarcă secretele
(pasul 2 de mai sus îi suprascrie pe cei vechi) — dar **toți
colaboratorii trebuie să reimporte noul `.cer`**, și **toate repo-urile
GDC trebuie să-și actualizeze secretele CI cu noul `.pfx`** (certificatul
e comun, o regenerare afectează tot ecosistemul). Evită regenerarea
inutilă — de asta scriptul folosește o valabilitate de 5 ani.
