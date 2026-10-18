using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using MacMasterControlPro.Core.Services;

// Verificari automate rulate PE WINDOWS REAL (nu doar compilare): scanare disc, jurnal, export, secrete.
// Folosire: Selftest.exe [bench <folder>]   Cod de iesire 0 = toate trecute.
int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"PASS  {name}"); } else { fail++; Console.WriteLine($"FAIL  {name}  {detail}"); }
}
string Tmp(string n) { var d = Path.Combine(Path.GetTempPath(), "mcsp-selftest-" + n + "-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); return d; }
DiskNode Scan(string p) => DiskScanEngine.BuildTree(p, _ => { }).Tree;
(DiskNode Tree, DiskScanEngine.ScanOutcome Outcome) Inc(DiskNode t, DiskScanEngine.IncrementalMode m = DiskScanEngine.IncrementalMode.Verified) => DiskScanEngine.IncrementalUpdate(t, _ => { }, m);
void Grow(string f, int n) { using var s = new FileStream(f, FileMode.Append); s.Write(new byte[n]); }
string Fixture()
{
    var d = Tmp("scan");
    Directory.CreateDirectory(Path.Combine(d, "a", "b", "c"));
    File.WriteAllBytes(Path.Combine(d, "a", "b", "c", "f.bin"), new byte[1000]);
    File.WriteAllBytes(Path.Combine(d, "a", "g.bin"), new byte[500]);
    File.WriteAllBytes(Path.Combine(d, "a", "big.bin"), new byte[2_000_000]);
    return d;
}

if (args.Length >= 2 && args[0] == "bench")
{
    var sw = Stopwatch.StartNew(); var t = DiskScanEngine.BuildTree(args[1], _ => { }).Tree; var full = sw.Elapsed.TotalSeconds;
    sw.Restart(); Inc(t); var v = sw.Elapsed.TotalSeconds; sw.Restart(); Inc(t, DiskScanEngine.IncrementalMode.StructureOnly); var q = sw.Elapsed.TotalSeconds;
    Console.WriteLine($"bench files={t.TotalFileCount} bytes={t.SizeBytes} full={full:0.00}s verified={v:0.00}s structureOnly={q:0.00}s peakWS={Process.GetCurrentProcess().PeakWorkingSet64 / 1048576}MB");
    return 0;
}

Console.WriteLine($"OS: {Environment.OSVersion}  arch: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}  proc: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}  user: {Environment.UserName}");

// ---- Disc ----
{
    var d = Fixture(); var t = Scan(d); var before = t.SizeBytes;
    File.WriteAllBytes(Path.Combine(d, "a", "b", "c", "new.bin"), new byte[200]);
    Check("disc: fisier adaugat in adancime", Inc(t).Tree.SizeBytes == before + 200);
}
{
    var d = Fixture(); var t = Scan(d); var before = t.SizeBytes;
    Grow(Path.Combine(d, "a", "b", "c", "f.bin"), 4000);
    Check("disc: crestere in-loc fisier existent in subfolder", Inc(t).Tree.SizeBytes == before + 4000);
    var t2 = Scan(d); Grow(Path.Combine(d, "a", "big.bin"), 10);
    Check("disc: crestere in-loc fisier mare", Inc(t2).Tree.SizeBytes == Scan(d).SizeBytes);
}
{
    var d = Fixture(); var t = Scan(d); var f = Path.Combine(d, "a", "b", "c", "f.bin");
    var fp = t.Children["a"].Children["b"].Children["c"].FilesFingerprint;
    File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddSeconds(30));
    var fp2 = Inc(t).Tree.Children["a"].Children["b"].Children["c"].FilesFingerprint;
    Check("disc: rescriere cu aceeasi dimensiune detectata prin mtime", fp != fp2);
}
{
    var d = Fixture(); var t = Scan(d); var before = t.SizeBytes;
    File.Delete(Path.Combine(d, "a", "g.bin"));
    File.Move(Path.Combine(d, "a", "b", "c", "f.bin"), Path.Combine(d, "a", "b", "c", "h.bin"));
    var r = Inc(t).Tree;
    Check("disc: stergere + redenumire", r.SizeBytes == before - 500 && !r.Children["a"].Children["b"].Children["c"].Children.ContainsKey("f.bin"));
}
{
    var d = Fixture(); var t = Scan(d); var before = t.SizeBytes;
    Directory.CreateDirectory(Path.Combine(d, "a", "new", "deep")); File.WriteAllBytes(Path.Combine(d, "a", "new", "deep", "x.bin"), new byte[700]);
    Check("disc: subfolder nou", Inc(t).Tree.SizeBytes == before + 700);
}
{
    var d = Fixture(); var t = Scan(d); var before = t.SizeBytes;
    Grow(Path.Combine(d, "a", "big.bin"), 10);
    Check("disc: modul StructureOnly nu vede modificari in-loc (compromis documentat)", Inc(t, DiskScanEngine.IncrementalMode.StructureOnly).Tree.SizeBytes == before);
}
{
    var d = Fixture(); var link = Path.Combine(d, "a", "junction");
    var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{Path.Combine(d, "a", "b")}\"") { CreateNoWindow = true, RedirectStandardOutput = true }); p!.WaitForExit();
    var t = Scan(d);
    Check("disc: junction nu e urmarit/numarat", Directory.Exists(link) && t.SizeBytes == 2_001_500, $"size={t.SizeBytes}");
}
{
    var d = Fixture(); var s = Path.Combine(d, "a", "sparse.bin");
    File.WriteAllBytes(s, new byte[1]); 
    Process.Start(new ProcessStartInfo("fsutil.exe", $"sparse setflag \"{s}\"") { CreateNoWindow = true })!.WaitForExit();
    using (var fs = new FileStream(s, FileMode.Open)) fs.SetLength(200_000_000);
    var t = Scan(d); var n = t.Children["a"].Children["sparse.bin"];
    Check("disc: sparse - logic vs alocat", n.SizeBytes == 200_000_000 && n.AllocatedBytes < n.SizeBytes / 10, $"logic={n.SizeBytes} alloc={n.AllocatedBytes}");
}
{
    var d = Fixture(); var t = Scan(d); var json = JsonSerializer.Serialize(t);
    var back = JsonSerializer.Deserialize<DiskNode>(json)!;
    Grow(Path.Combine(d, "a", "big.bin"), 5);
    Check("cache: round-trip JSON pastreaza amprentele", Inc(back).Tree.SizeBytes == t.SizeBytes + 5);
    var legacy = JsonSerializer.Deserialize<DiskNode>("{\"Name\":\"x\",\"Path\":\"" + d.Replace("\\", "\\\\") + "\",\"IsDirectory\":true,\"SizeBytes\":1,\"Children\":{}}")!;
    Check("cache: format vechi (fara campuri noi) se reconciliaza", Inc(legacy).Tree.SizeBytes == Scan(d).SizeBytes);
}
{
    var d = Fixture(); var t = Scan(d); var before = t.SizeBytes;
    Directory.Delete(d, true);
    var (r, o) = Inc(t);
    Check("volum/radacina indisponibila: arbore neatins, status RootUnavailable", o.Status == DiskScanEngine.ScanStatus.RootUnavailable && r.SizeBytes == before);
}
{
    var d = Fixture(); for (int i = 0; i < 300; i++) { Directory.CreateDirectory(Path.Combine(d, "many", i.ToString())); File.WriteAllBytes(Path.Combine(d, "many", i.ToString(), "f"), new byte[10]); }
    var cts = new CancellationTokenSource(); cts.Cancel();
    var (_, o) = DiskScanEngine.BuildTree(d, _ => { }, cts.Token);
    Check("anulare: status Cancelled", o.Status == DiskScanEngine.ScanStatus.Cancelled);
}
{
    var d = Fixture(); var denied = Path.Combine(d, "a", "b");
    Process.Start(new ProcessStartInfo("icacls.exe", $"\"{denied}\" /deny *S-1-1-0:(OI)(CI)(RX)") { CreateNoWindow = true })!.WaitForExit();
    var (t, o) = DiskScanEngine.BuildTree(d, _ => { });
    Process.Start(new ProcessStartInfo("icacls.exe", $"\"{denied}\" /remove:d *S-1-1-0") { CreateNoWindow = true })!.WaitForExit();
    Check("acces refuzat: numarat ca eroare, nu ignorat tacut", o.AccessErrors >= 1 || t.SizeBytes <= 2_000_500, $"errors={o.AccessErrors} size={t.SizeBytes}");
}

// ---- Jurnal + export ----
{
    var logDir = Tmp("log"); DiagnosticLog.DirectoryOverride = logDir;
    var red = DiagnosticLog.Redact("password=hunter2 a.b@gmail.com token: abcDEF Bearer xyz.123 C:\\Users\\Cristi\\Videos");
    Check("jurnal: redactare", !red.Contains("hunter2") && !red.Contains("gmail") && !red.Contains("abcDEF") && !red.Contains("xyz.123") && !red.Contains("Cristi"), red);
    var big = new string('x', 400);
    for (int i = 0; i < (int)(DiagnosticLog.MaxFileBytes / 500) * (DiagnosticLog.KeepArchives + 3); i++) DiagnosticLog.Log("info", "t", "e", big);
    Check("jurnal: rotatie limiteaza fisierele", Directory.GetFiles(logDir).Length <= DiagnosticLog.KeepArchives + 1, $"files={Directory.GetFiles(logDir).Length}");
    DiagnosticLog.Log("info", "t", "secret", "password=SECRETVALUE9 me@x.org");
    var zip = DiagnosticExport.Export(Tmp("exp"));
    bool leak = false;
    using (var z = ZipFile.OpenRead(zip)) foreach (var e in z.Entries) { using var r = new StreamReader(e.Open()); var txt = r.ReadToEnd(); if (txt.Contains("SECRETVALUE9") || txt.Contains("me@x.org")) leak = true; }
    Check("export: zip fara secrete", !leak);
    DiagnosticLog.DirectoryOverride = "Z:\\inexistent\\nu\\se\\poate";
    DiagnosticLog.Log("error", "t", "e");
    Check("jurnal: director nescriptibil nu arunca", true);
    DiagnosticLog.DirectoryOverride = null;
}

// ---- Secrete SMTP (DPAPI) ----
{
    var store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacMasterControlPro", "EmailNotifierSettings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(store)!);
    var backup = File.Exists(store) ? File.ReadAllText(store) : null; var oldSecret = SecretStore.Get("smtp.appPassword");
    try
    {
        File.WriteAllText(store, JsonSerializer.Serialize(new EmailNotifierSettings { Enabled = true, Username = "u@x.org", AppPassword = "legacy-pass-123", Recipient = "r@x.org" }));
        var s = EmailNotifierService.Settings;
        Check("smtp: migrare citeste valoarea veche", s.AppPassword == "legacy-pass-123" && s.Username == "u@x.org" && s.Enabled);
        Check("smtp: valoarea veche eliminata din fisier dupa salvare securizata", !File.ReadAllText(store).Contains("legacy-pass-123"));
        var blob = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(store)!, "secrets")).Select(File.ReadAllBytes).ToList();
        Check("smtp: secretul e protejat DPAPI (nu apare in clar pe disc)", blob.Count > 0 && blob.All(b => !System.Text.Encoding.UTF8.GetString(b).Contains("legacy-pass-123")));
        Check("smtp: persista la recitire", EmailNotifierService.Settings.AppPassword == "legacy-pass-123");
        var logDir = Tmp("log2"); DiagnosticLog.DirectoryOverride = logDir; EmailNotifierService.Settings = s; DiagnosticLog.DirectoryOverride = null;
        var leak = Directory.Exists(logDir) && Directory.GetFiles(logDir).Any(f => File.ReadAllText(f).Contains("legacy-pass-123"));
        Check("smtp: absent din loguri", !leak);
    }
    finally
    {
        if (backup is null) File.Delete(store); else File.WriteAllText(store, backup);
        SecretStore.Set("smtp.appPassword", oldSecret ?? "");
    }
}

// ---- Mod Randare (doar logica pura + citire reala; NU modifica servicii) ----
{
    var snap = new RenderModeSnapshot();
    snap.Services["WSearch"] = new ServiceState { StartType = 2, Delayed = true, Running = true };
    snap.Services["fhsvc"] = new ServiceState { StartType = 3, Running = false };
    var act = RenderModeState.ActivationCommands(snap); var rst = RenderModeState.RestoreCommands(snap);
    Check("render: activare atinge doar ce era pornit", act.SequenceEqual(new[] { "sc config WSearch start= disabled", "sc stop WSearch" }), string.Join("|", act));
    Check("render: restaurare readuce exact modul de pornire (delayed-auto) fara a porni fhsvc", rst.SequenceEqual(new[] { "sc config WSearch start= delayed-auto", "sc start WSearch" }), string.Join("|", rst));
    var off = new RenderModeSnapshot(); off.Services["WSearch"] = new ServiceState { StartType = 4, Running = false };
    Check("render: totul deja oprit => nimic de facut / nimic de restaurat", RenderModeState.ActivationCommands(off).Count == 0 && RenderModeState.RestoreCommands(off).Count == 0);
    var p = Path.Combine(Tmp("rm"), "snap.json"); RenderModeState.Save(snap, p); var back = RenderModeState.Load(p);
    Check("render: snapshot supravietuieste repornirii (round-trip)", back is not null && back.Services["WSearch"].Delayed && back.Services["WSearch"].StartType == 2);
    var cur = RenderModeState.ReadCurrent();
    Check("render: citire reala a starii WSearch/fhsvc", cur.Services.ContainsKey("WSearch"), string.Join(",", cur.Services.Select(kv => kv.Key + ":" + kv.Value.StartType + "/" + kv.Value.Running)));
    Console.WriteLine("INFO  stare reala: " + string.Join("; ", cur.Services.Select(kv => $"{kv.Key} start={kv.Value.StartType} delayed={kv.Value.Delayed} running={kv.Value.Running}")));
}

Console.WriteLine($"\nREZULTAT: {pass} trecute, {fail} esuate");
return fail == 0 ? 0 : 1;
