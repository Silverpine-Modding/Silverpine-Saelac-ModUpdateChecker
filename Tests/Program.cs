using Silverpine.ModUpdateChecker;
using Newtonsoft.Json.Linq;

int count = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); count++; }
void Reject(string json, string message)
{
    bool rejected = false;
    try { Catalog.Parse(json); } catch { rejected = true; }
    Check(rejected, message);
}
string fixture = """
{
  "schemaVersion": 1, "generatedAt": "2026-10-26T00:00:00Z",
  "mods": [
    {
      "repository": "Silverpine-Modding/Framework", "repositoryUrl": "https://github.com/Silverpine-Modding/Framework",
      "name": "Framework", "releaseUrl": "https://github.com/Silverpine-Modding/Framework/releases/tag/v1.10.0",
      "releaseTag": "v1.10.0", "status": "active", "components": [
        {"id":"framework","name":"Framework","version":"1.10.0","kind":"plugin","dependencies":[]},
        {"id":"legacy","name":"Compatibility","version":"1.10.0","kind":"plugin","dependencies":[]}
      ]
    },
    {
      "repository": "Silverpine-Modding/Old", "repositoryUrl": "https://github.com/Silverpine-Modding/Old",
      "name": "Old", "releaseUrl": "https://github.com/Silverpine-Modding/Old/releases/tag/v1.0.0",
      "releaseTag":"v1.0.0", "status":"obsolete","replacementRepository":"Silverpine-Modding/Framework","components":[
        {"id":"assembly:Old","name":"Old","version":"1.0.0.0","kind":"patcher","dependencies":[]}
      ]
    },
    {
      "repository": "Silverpine-Modding/Consumer", "repositoryUrl": "https://github.com/Silverpine-Modding/Consumer",
      "name":"Consumer", "releaseUrl":"https://github.com/Silverpine-Modding/Consumer/releases/tag/v2.0.0",
      "releaseTag":"v2.0.0","status":"active","components":[
        {"id":"consumer","name":"Consumer","version":"2.0.0","kind":"plugin","dependencies":[
          {"id":"framework","minimumVersion":"1.10.0","optional":false},
          {"id":"optional","minimumVersion":"1.0.0","optional":true}
        ]}
      ]
    }
  ]
}
""";
LocalMod Local(string id, string version, string kind = "plugin") => new() { Id=id,Name=id,Version=version,Kind=kind };
var catalog = Catalog.Parse(fixture);
Check(NumericVersion.Compare("1.10.0", "1.9.9") > 0, "numeric comparison handles multi-digit components");
Check(NumericVersion.Compare("1.0.0.0", "1.0") == 0, "missing assembly version components normalize to zero");
Check(NumericVersion.Compare("1.0.0.1", "1.0.0") > 0, "fourth assembly version component participates");
Check(!NumericVersion.TryParse("1.2.3-beta", out _), "release tags are not mistaken for BepInEx numeric versions");
var rows = Comparison.Compare(catalog, new[] { Local("framework","1.9.0"),Local("legacy","1.9.0"),Local("consumer","1.0.0"),Local("assembly:Old","9.0.0","patcher"),Local("unlisted","1.0.0") });
Check(rows.Count(r => r.Name == "Framework") == 1 && rows.Single(r => r.Name == "Framework").Status == "Update available", "legacy and current identities produce one repository row");
Check(rows.Single(r => r.Name == "Consumer").Details.Any(d => d.Contains("update this dependency")), "latest-release dependency upgrades are reported");
Check(!rows.Single(r => r.Name == "Consumer").Details.Any(d => d.Contains("optional")), "optional dependencies are not required");
Check(rows.Single(r => r.Name == "Old").Status == "Obsolete", "obsolete state overrides newer installed version");
Check(rows.Single(r => r.Name == "Old").Details.Any(d => d.Contains("read from disk")), "patcher results explain restart/disk semantics");
Check(rows.Single(r => r.Name == "unlisted").Status == "Not in catalog", "unknown mods never become up-to-date");
Check(Comparison.Compare(catalog,new[] { Local("framework","2.0.0") }).Single(r=>r.Name=="Framework").Status=="Newer local build", "newer local builds are not downgraded");
Check(Comparison.Compare(catalog,new[] { Local("framework","1.10.0.0") }).Single(r=>r.Name=="Framework").Status=="Up to date", "equal normalized versions are current");
Check(Comparison.Compare(catalog,new[] { Local("framework","1.10.0"),Local("framework","1.9.0") }).Single(r=>r.Name=="Framework").Status=="Check installation", "duplicate installed identities are flagged");
Check(Comparison.Compare(catalog,new[] { Local("framework","garbage") }).Single(r=>r.Name=="Framework").Status=="Check installation", "bad installed version is not a false success");
Check(Comparison.Compare(catalog,new[] { Local("consumer","1.0.0") }).Single(r=>r.Name=="Consumer").Details.Any(d=>d.Contains("not loaded")), "missing hard dependency reported");
Check(Comparison.Compare(catalog,Array.Empty<LocalMod>()).All(r=>!r.IsInstalled), "catalog browsing does not imply installation");
Reject(fixture.Replace("\"schemaVersion\": 1","\"schemaVersion\": 99"), "unsupported schema rejected");
Reject(fixture.Replace("\"schemaVersion\": 1","\"schemaVersion\": 1, \"schemaVersion\": 2"), "duplicate JSON fields rejected");
Reject(fixture.Replace("\"id\":\"legacy\"","\"id\":\"framework\""), "ambiguous duplicate GUIDs rejected");
Reject(fixture.Replace("\"version\":\"2.0.0\"","\"version\":\"banana\""), "invalid published versions rejected");
Reject(fixture.Replace("\"dependencies\":[]","\"dependencies\":null"), "null dependencies rejected");
Reject(fixture.Replace("\"status\": \"active\"","\"status\": \"bogus\""), "unknown status rejected");
Reject("<html>maintenance</html>", "HTML error pages rejected");
Reject(new string(' ',2*1024*1024+1), "oversized documents rejected");
foreach (string url in new[] { "file:///C:/Windows", "http://github.com/Silverpine-Modding/a", "https://github.com.evil.example/Silverpine-Modding/a", "https://evil@github.com/Silverpine-Modding/a", "https://github.com:8443/Silverpine-Modding/a", "https://github.com/unrelated/a" })
    Check(!Catalog.SafeLink(url), "unsafe link blocked: " + url);
Check(Catalog.SafeLink("https://github.com/Silverpine-Modding/Mod/releases/tag/v1.0.0"), "organization HTTPS release links accepted");
var service = new UpdateService();
Check(UnityEngine.Networking.UnityWebRequest.Sent.Count == 0, "service creation does not contact GitHub");
UnityEngine.Networking.UnityWebRequest.Next = new();
UnityEngine.Networking.UnityWebRequest.Next.downloadHandler.text = fixture;
service.Check(); service.Check(); service.Finish();
var request = UnityEngine.Networking.UnityWebRequest.Sent.Single();
Check(request.Url == UpdateService.CatalogUrl && request.timeout == 25 && request.redirectLimit == 0 && request.Disposed,
    "one bounded, redirect-free request is disposed after success");
Check(service.Catalog != null && service.Error == "" && !service.Busy, "successful network result populates catalog");
service.Check();
Check(UnityEngine.Networking.UnityWebRequest.Sent.Count == 1, "cooldown prevents repeat requests");
UnityEngine.Time.realtimeSinceStartup = 31;
UnityEngine.Networking.UnityWebRequest.Next = new() { result=UnityEngine.Networking.UnityWebRequest.Result.ConnectionError, error="offline", responseCode=0 };
service.Check(); service.Finish();
Check(service.Catalog != null && service.Error.Contains("offline") && !service.Busy, "offline check retains previous results and reports failure");
UnityEngine.Time.realtimeSinceStartup = 62;
UnityEngine.Networking.UnityWebRequest.Next = new();
UnityEngine.Networking.UnityWebRequest.Next.downloadHandler.text = "<html>error</html>";
service.Check(); service.Finish();
Check(service.Catalog != null && service.Error.Contains("Could not read") && !service.Busy, "malformed network response preserves last successful catalog");
UnityEngine.Time.realtimeSinceStartup = 93;
UnityEngine.Networking.UnityWebRequest.Next = new() { downloadedBytes=3*1024*1024 };
service.Check(); service.Finish();
Check(service.Error.Contains("limit") && !service.Busy, "oversized response is rejected");
UnityEngine.Time.realtimeSinceStartup = 124;
UnityEngine.Networking.UnityWebRequest.Next = new() { ThrowOnSend=true };
service.Check(); service.Finish();
Check(service.Error.Contains("Could not start") && !service.Busy && UnityEngine.Networking.UnityWebRequest.Next.Disposed,
    "request startup exception releases busy state and request");
if (args.Length > 0)
{
    var real = Catalog.Parse(File.ReadAllText(args[0]));
    Check(real.Mods.All(m => !m.Repository.EndsWith("SILCStudio")), "standalone SILC Studio excluded");
    Check(real.Mods.Count >= 55, "current organization mod coverage");
    Check(real.Mods.Count(m=>m.Status=="obsolete")==2, "both obsolete item fixes mapped");
    Check(real.Mods.Single(m=>m.Repository.EndsWith("Modding-Tools")).Components.Count==2, "published framework GUIDs extracted");
    Console.WriteLine("Validated real catalog: " + real.Mods.Count + " mods.");
}
Console.WriteLine(count + " checks passed.");
