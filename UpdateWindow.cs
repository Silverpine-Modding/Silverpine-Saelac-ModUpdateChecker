using System;
using System.Linq;
using Silverpine.ModdingTools;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Silverpine.ModUpdateChecker;

internal sealed class UpdateWindow : ModToolBehaviour
{
    private Vector2 scroll;
    private Vector2 detailScroll;
    private string search = "";
    private bool showAvailable;
    private bool attentionOnly;
    private UpdateRow? selected;
    private GUIStyle? wrap;
    internal static void Open(ModToolSession session)
    {
        GameObject? root = null;
        try
        {
            root = ModUi.CreateOverlayCanvas("SilverpineModUpdateWindow", sortingOrder: 10000);
            var shield = root.AddComponent<Image>();
            shield.color = new Color(0, 0, 0, 0.01f);
            shield.raycastTarget = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            root.AddComponent<UpdateWindow>().AttachSession(session);
            var service = UpdateService.Instance;
            service.RefreshInventory();
            if (service.Catalog == null) service.Check();
        }
        catch
        {
            if (root != null) Destroy(root);
            session.Close();
            throw;
        }
    }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Close(); }
    private void Close() { ReleaseSession(); gameObject.SetActive(false); Destroy(gameObject); }
    private void OnGUI()
    {
        if (FrameworkSession == null || FrameworkSession.IsClosed) return;
        using var scope = ModGui.BeginScaled();
        GUI.enabled = true; GUI.depth = -1100; GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
        var panel = new Rect(160, 70, 1600, 940);
        GUI.color = new Color(0.055f, 0.065f, 0.085f, 1);
        GUI.DrawTexture(panel, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
        GUI.color = Color.white;
        wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
        var service = UpdateService.Instance;
        GUILayout.BeginArea(panel, GUI.skin.window);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Check for Updates — Silverpine mods", GUILayout.Height(30));
        GUI.enabled = service.CanCheck;
        if (GUILayout.Button(service.Busy ? "Checking..." : "Check now", GUILayout.Width(150), GUILayout.Height(30))) service.Check();
        GUI.enabled = true;
        if (GUILayout.Button("Close", GUILayout.Width(100), GUILayout.Height(30))) Close();
        GUILayout.EndHorizontal();
        GUILayout.Label("One catalog request per check. Open release pages to download updates; close Silverpine before replacing DLLs.", wrap);
        if (service.CheckedAt.HasValue)
            GUILayout.Label("Last successful check: " + service.CheckedAt.Value.ToString("g") +
                "   |   Catalog changed: " + service.Catalog!.GeneratedAt.ToLocalTime().ToString("g") +
                "   |   " + service.Rows.Count(r => r.IsInstalled && r.Status == "Update available") + " updates, " +
                service.Rows.Count(r => r.IsInstalled && r.Status == "Obsolete") + " obsolete");
        if (service.Error.Length > 0)
        {
            GUI.contentColor = new Color(1f, 0.7f, 0.45f);
            GUILayout.Label(service.Error + (service.Catalog == null ? "" : " Showing previous results; they may be outdated."), wrap);
            GUI.contentColor = Color.white;
        }
        if (service.Busy) GUILayout.Label("Downloading the latest shared catalog...");
        foreach (var warning in service.InventoryWarnings) GUILayout.Label(warning, wrap);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(60));
        search = GUILayout.TextField(search, GUILayout.Width(500));
        showAvailable = GUILayout.Toggle(showAvailable, "Show mods not installed", GUILayout.Width(250));
        attentionOnly = GUILayout.Toggle(attentionOnly, "Updates / obsolete only", GUILayout.Width(250));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Mod", GUILayout.Width(670));
        GUILayout.Label("Installed", GUILayout.Width(150));
        GUILayout.Label("Latest release", GUILayout.Width(180));
        GUILayout.Label("Status");
        GUILayout.EndHorizontal();
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        foreach (var row in service.Rows.Where(r => (showAvailable || r.IsInstalled) && (!attentionOnly || r.NeedsAttention) &&
            (search.Length == 0 || r.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
             (r.Mod?.Repository.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)))
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            if (GUILayout.Button(row.Name, GUILayout.Width(670), GUILayout.Height(30))) selected = row;
            GUILayout.Label(row.IsInstalled ? row.Installed : "—", GUILayout.Width(150), GUILayout.Height(30));
            GUILayout.Label(row.Latest, GUILayout.Width(180), GUILayout.Height(30));
            GUILayout.Label(row.Status, GUILayout.Width(220), GUILayout.Height(30));
            if (row.Mod != null && GUILayout.Button("Releases", GUILayout.Width(110), GUILayout.Height(30))) OpenLink(ReleaseLink(row.Mod));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        if (selected != null)
        {
            // The comparison is rebuilt after a check; resolve the selection to its fresh row.
            selected = service.Rows.FirstOrDefault(r => r.Mod?.Repository == selected.Mod?.Repository && r.Name == selected.Name) ?? selected;
            GUILayout.Label(selected.Name + " — " + selected.Status, GUI.skin.box);
            detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.Height(220));
            foreach (var detail in selected.Details) GUILayout.Label(detail, wrap);
            if (selected.Mod?.ReplacementRepository != null)
            {
                var replacement = service.Catalog!.Mods.FirstOrDefault(m => m.Repository == selected.Mod.ReplacementRepository);
                if (GUILayout.Button("Open replacement: " + (replacement?.Name ?? selected.Mod.ReplacementRepository), GUILayout.Height(28)))
                    OpenLink(replacement == null ? "https://github.com/" + selected.Mod.ReplacementRepository + "/releases" : ReleaseLink(replacement));
            }
            GUILayout.EndScrollView();
        }
        GUILayout.Label("Loaded plugins and patcher files are checked locally. Disabled/unloaded plugin DLLs are not scanned. Rechecks are limited to once per 30 seconds.", wrap);
        GUILayout.EndArea();
    }
    private static string ReleaseLink(CatalogMod mod) => mod.ReleaseUrl ?? mod.RepositoryUrl + "/releases";
    private static void OpenLink(string url) { if (Catalog.SafeLink(url)) Application.OpenURL(url); }
}
