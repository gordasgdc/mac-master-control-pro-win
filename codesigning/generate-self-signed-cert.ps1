# generate-self-signed-cert.ps1
#
# CERTIFICAT COMUN pentru toate aplicatiile GDC (decizie explicita a lui
# Cristi) - daca certificatul a fost deja generat pentru un alt repo GDC
# (ex. CGConvertor), NU rula asta din nou aici - refoloseste ACELASI
# .pfx, incarca-l doar ca secrete CI pentru ACEST repo (pasul 2 mai jos),
# cu ACEEASI parola. Ruleaza scriptul de generare O SINGURA DATA in tot
# ecosistemul, nu per repo.
#
# Rulat DOAR de Cristi, pe Windows real (nu de Claude - un certificat cu
# cheie privata nu trece niciodata prin conversatie, vezi CLAUDE.md,
# Regula 34). Genereaza un certificat Self-Signed de Code Signing STABIL
# (valabil 5 ani) - NU se regenereaza la fiecare build, ca sa nu rupa
# increderea deja acordata de colaboratori (fiecare certificat nou ar
# cere re-import in Trusted Root pe toate masinile lor).
#
# Foloseste:
#   1. Daca certificatul COMUN GDC nu exista inca, ruleaza acest script o
#      data (PowerShell, ca Administrator). Produce doua fisiere in
#      acelasi folder:
#      - gdc-selfsign.pfx  (PRIVAT, cu cheie - NU se distribuie,
#        NU se comite in git, NU se lipeste in chat/conversatie)
#      - gdc-selfsign.cer  (PUBLIC, fara cheie - se distribuie
#        colaboratorilor pentru import manual in Trusted Root)
#   2. Incarca .pfx-ul ca secrete GitHub Actions PENTRU ACEST REPO
#      (comenzile exacte sunt afisate la finalul scriptului) - secretele
#      se numesc IDENTIC in toate repo-urile GDC
#      (WIN_SELFSIGN_PFX_BASE64 / WIN_SELFSIGN_PFX_PASSWORD), dar trebuie
#      incarcate separat, o data per repo (GitHub nu permite secrete
#      partajate intre repo-uri fara un cont Organization).

$ErrorActionPreference = "Stop"

$subject = "CN=GDC Software (Self-Signed, testare interna)"
$pfxPath = Join-Path $PSScriptRoot "gdc-selfsign.pfx"
$cerPath = Join-Path $PSScriptRoot "gdc-selfsign.cer"
$pfxPassword = Read-Host -Prompt "Alege o parola noua pentru fisierul .pfx (o vei pune ca secret CI) - SARI peste acest pas daca certificatul COMUN GDC exista deja" -AsSecureString

Write-Host "==> Generez certificatul self-signed (valabil 5 ani)..."
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5) `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA `
    -KeyLength 2048

Write-Host "==> Exporting .pfx (PRIVAT - nu distribui acest fisier)..."
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword | Out-Null

Write-Host "==> Exporting .cer (PUBLIC - acesta se distribuie colaboratorilor)..."
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

Write-Host ""
Write-Host "==> Gata:"
Write-Host "    $pfxPath  (PRIVAT - foloseste-l DOAR pentru pasii de mai jos, apoi sterge-l local)"
Write-Host "    $cerPath  (PUBLIC - trimite-l colaboratorilor)"
Write-Host ""
Write-Host "==> Urmatorul pas - incarca secretele in GitHub Actions PENTRU ACEST REPO (necesita 'gh' CLI autentificat):"
Write-Host ""
Write-Host '    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("' -NoNewline
Write-Host "$pfxPath" -NoNewline
Write-Host '"))'
Write-Host '    gh secret set WIN_SELFSIGN_PFX_BASE64 --repo gordasgdc/mac-master-control-pro-win --body $b64'
Write-Host '    gh secret set WIN_SELFSIGN_PFX_PASSWORD --repo gordasgdc/mac-master-control-pro-win'
Write-Host "    (al doilea comand cere parola interactiv - foloseste ACEEASI parola aleasa mai sus)"
Write-Host ""
Write-Host "==> Daca certificatul COMUN GDC exista deja (alt repo l-a generat), SARI peste generarea de mai sus -"
Write-Host "    ia direct .pfx-ul existent si repeta DOAR cele 2 comenzi 'gh secret set' de mai sus, pentru acest repo."
Write-Host ""
Write-Host "==> Dupa ce secretele sunt incarcate, sterge fisierul .pfx local:"
Write-Host "    Remove-Item `"$pfxPath`" -Force"
Write-Host ""
Write-Host "==> Distribuie $cerPath colaboratorilor (o singura data, pentru tot ecosistemul GDC). Import manual pe masinile lor:"
Write-Host "    dublu-click pe .cer -> Install Certificate -> Local Machine ->"
Write-Host "    'Place all certificates in the following store' -> Trusted Root Certification Authorities."
