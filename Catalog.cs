using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Silverpine.ModUpdateChecker;

internal sealed class Catalog
{
    public int SchemaVersion { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public List<CatalogMod> Mods { get; set; } = new();
    internal static Catalog Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 2 * 1024 * 1024) throw new InvalidDataException("Catalog is empty or too large.");
        var document = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var catalog = document.ToObject<Catalog>(JsonSerializer.Create(new JsonSerializerSettings
        { MaxDepth = 32, TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.DateTimeOffset }))!;
        if (catalog.SchemaVersion != 1) throw new InvalidDataException("Unsupported catalog format. Update Mod Update Checker.");
        if (catalog.GeneratedAt == default || catalog.Mods == null || catalog.Mods.Count == 0 || catalog.Mods.Count > 1000)
            throw new InvalidDataException("Incomplete catalog.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in catalog.Mods)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.Name) || !ValidRepository(mod.Repository) ||
                !repos.Add(mod.Repository) || !SafeLink(mod.RepositoryUrl) || mod.Components == null ||
                !new[] { "active", "obsolete", "archived", "unreleased" }.Contains(mod.Status))
                throw new InvalidDataException("Invalid or duplicate repository in catalog.");
            if (mod.Status != "unreleased" && (!SafeLink(mod.ReleaseUrl) || mod.Components.Count == 0))
                throw new InvalidDataException("Missing release metadata for " + mod.Name);
            if (mod.ReplacementRepository != null && !ValidRepository(mod.ReplacementRepository))
                throw new InvalidDataException("Invalid replacement repository.");
            foreach (var component in mod.Components)
            {
                if (component == null || string.IsNullOrWhiteSpace(component.Id) || !ids.Add(component.Id) ||
                    !NumericVersion.TryParse(component.Version, out _) ||
                    (component.Kind != "plugin" && component.Kind != "patcher") || component.Dependencies == null)
                    throw new InvalidDataException("Invalid or duplicate component in " + mod.Name);
                foreach (var dependency in component.Dependencies)
                    if (dependency == null || string.IsNullOrWhiteSpace(dependency.Id) ||
                        (dependency.MinimumVersion != null && !NumericVersion.TryParse(dependency.MinimumVersion, out _)))
                        throw new InvalidDataException("Invalid dependency in " + mod.Name);
            }
        }
        return catalog;
    }
    internal static bool ValidRepository(string? value) => value != null &&
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^Silverpine-Modding/[A-Za-z0-9_.-]+$");
    internal static bool SafeLink(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith("/Silverpine-Modding/", StringComparison.OrdinalIgnoreCase);
}
internal sealed class CatalogMod
{
    public string Repository { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RepositoryUrl { get; set; } = "";
    public string? ReleaseUrl { get; set; }
    public string? ReleaseTag { get; set; }
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
    public string? ReplacementRepository { get; set; }
    public string? ReplacementMinimumVersion { get; set; }
    public List<CatalogComponent> Components { get; set; } = new();
}
internal sealed class CatalogComponent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Kind { get; set; } = "";
    public List<CatalogDependency> Dependencies { get; set; } = new();
}
internal sealed class CatalogDependency
{
    public string Id { get; set; } = "";
    public string? MinimumVersion { get; set; }
    public bool Optional { get; set; }
}
internal static class NumericVersion
{
    // BepInEx plugin versions and CLR assembly versions are numeric, not release-tag strings.
    internal static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (value == null || !Version.TryParse(value, out var parsed)) return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        return true;
    }
    internal static int Compare(string left, string right)
    {
        if (!TryParse(left, out var a) || !TryParse(right, out var b)) throw new InvalidDataException("Invalid numeric version.");
        return a.CompareTo(b);
    }
}
internal sealed class LocalMod
{
    internal string Id = "", Name = "", Version = "", Kind = "";
}
internal sealed class UpdateRow
{
    internal CatalogMod? Mod;
    internal string Name = "", Installed = "", Latest = "", Status = "";
    internal bool IsInstalled;
    internal readonly List<string> Details = new();
    internal bool NeedsAttention => Status == "Update available" || Status == "Obsolete" || Status == "Check installation";
}
internal static class Comparison
{
    internal static List<UpdateRow> Compare(Catalog catalog, IReadOnlyList<LocalMod> local)
    {
        var byId = local.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(catalog.Mods.SelectMany(m => m.Components).Select(c => c.Id), StringComparer.OrdinalIgnoreCase);
        var rows = new List<UpdateRow>();
        foreach (var mod in catalog.Mods)
        {
            var installed = mod.Components.Where(c => byId.ContainsKey(c.Id)).ToArray();
            var row = new UpdateRow { Mod = mod, Name = mod.Name, IsInstalled = installed.Length > 0,
                Latest = mod.ReleaseTag ?? "No stable release", Status = "Not installed" };
            row.Installed = string.Join(", ", installed.SelectMany(c => byId[c.Id]).Select(i => i.Version).Distinct());
            bool update = false, newer = false, invalid = false;
            foreach (var component in installed)
            {
                var matches = byId[component.Id];
                if (matches.Length > 1) { invalid = true; row.Details.Add("Duplicate installed identity: " + component.Id + ". Check for extra DLL copies."); }
                foreach (var match in matches)
                {
                    if (!NumericVersion.TryParse(match.Version, out _)) { invalid = true; continue; }
                    int comparison = NumericVersion.Compare(match.Version, component.Version);
                    update |= comparison < 0; newer |= comparison > 0;
                    row.Details.Add(match.Kind + ": " + match.Version + " installed; " + component.Version + " published (" + component.Id + ")");
                }
                foreach (var dep in component.Dependencies.Where(d => !d.Optional))
                {
                    if (!byId.TryGetValue(dep.Id, out var dependencies))
                        row.Details.Add("Latest release requires " + dep.Id + (dep.MinimumVersion == null ? "" : " >= " + dep.MinimumVersion) + " (not loaded).");
                    else if (dep.MinimumVersion != null && dependencies.All(d =>
                        !NumericVersion.TryParse(d.Version, out _) || NumericVersion.Compare(d.Version, dep.MinimumVersion) < 0))
                        row.Details.Add("Latest release requires " + dep.Id + " >= " + dep.MinimumVersion + "; update this dependency too.");
                }
            }
            if (row.IsInstalled) row.Status = invalid ? "Check installation" : update ? "Update available" : newer ? "Newer local build" : "Up to date";
            if (mod.Status == "obsolete") row.Status = "Obsolete";
            else if (mod.Status == "archived") row.Details.Add("This repository is archived.");
            if (!string.IsNullOrWhiteSpace(mod.Notes)) row.Details.Add(mod.Notes!);
            if (installed.Any(c => c.Kind == "patcher")) row.Details.Add("Patcher versions are read from disk. Restart after changing patcher files.");
            rows.Add(row);
        }
        foreach (var item in local.Where(i => !known.Contains(i.Id)))
            rows.Add(new UpdateRow { Name = item.Name, Installed = item.Version, Latest = "Unknown", Status = "Not in catalog", IsInstalled = true,
                Details = { item.Id, "No published version mapping. This does not mean the mod is up to date." } });
        return rows.OrderByDescending(r => r.IsInstalled && r.NeedsAttention).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
