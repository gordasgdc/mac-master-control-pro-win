# Mac Master Control Pro (Windows)

Oglinda C#/.NET 8 WPF a [Mac Master Control Pro](https://github.com/gordasgdc/mac-master-control-pro) (Mac) — panou de tuning sistem, Cloud Manager, curățare cache media.

**Stare curentă**: scaffold v1.0.0 — licențiere Ed25519 reală, temă System/Light/Dark, Mărime Text, Sidebar Footer, Self-Updater, modulul Rețea. Restul modulelor (Cloud Manager, Cleanup, Tweaks, Dependency Auto-Installer) urmează în etape viitoare, la paritate cu Mac.

## Build local

```bash
dotnet build
```

Verificare completă (inclusiv XAML→BAML) necesită Windows real — vezi `.github/workflows/build-windows.yml`.

## Installer

Pe Windows, cu [Inno Setup](https://jrsoftware.org/isdl.php) instalat:

```powershell
dotnet publish src\MacMasterControlPro.Client -c Release -r win-x64 --self-contained -o publish
ISCC.exe installer.iss
```
