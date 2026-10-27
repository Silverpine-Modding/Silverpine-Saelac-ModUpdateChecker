using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Silverpine.ModdingTools;
using UnityEngine;
using UnityEngine.Networking;

namespace Silverpine.ModUpdateChecker;

// Results belong to the process, not to a Unity scene/MonoBehaviour wrapper.
internal sealed class UpdateService
{
    internal const string CatalogUrl = "https://raw.githubusercontent.com/Silverpine-Modding/.github/main/versions.json";
    internal static UpdateService Instance { get; } = new();
    internal Catalog? Catalog;
    internal List<UpdateRow> Rows = new();
    internal IReadOnlyList<string> InventoryWarnings = Array.Empty<string>();
    internal bool Busy;
    internal string Error = "";
    internal DateTimeOffset? CheckedAt;
    private float nextAllowed;
    private int generation;
    private UnityWebRequest? activeRequest;
    internal bool CanCheck => !Busy && Time.realtimeSinceStartup >= nextAllowed;
    internal void Check()
    {
        if (!CanCheck) return;
        Busy = true; Error = ""; nextAllowed = Time.realtimeSinceStartup + 30f;
        int current = ++generation;
        try { UpdateRunner.Run(Fetch(current), () => Interrupt(current)); }
        catch (Exception ex)
        {
            Interrupt(current);
            Error = "Could not start the update check: " + ex.Message;
        }
    }
    private void Interrupt(int current)
    {
        if (current != generation || !Busy) return;
        generation++;
        try { activeRequest?.Abort(); }
        finally
        {
            activeRequest?.Dispose();
            activeRequest = null;
            Busy = false;
            nextAllowed = Time.realtimeSinceStartup;
            Error = "The update check was interrupted by scene cleanup. Choose Check now to retry.";
        }
    }
    internal void RefreshInventory()
    {
        if (Catalog == null) return;
        var inventory = ModInventory.Capture();
        InventoryWarnings = inventory.Warnings;
        Rows = Comparison.Compare(Catalog, inventory.Mods.Select(m => new LocalMod
        { Id = m.Id, Name = m.Name, Version = m.Version.ToString(), Kind = m.Kind }).ToArray());
    }
    private IEnumerator Fetch(int current)
    {
        UnityWebRequest? request = null;
        try
        {
            UnityWebRequestAsyncOperation? operation = null;
            try
            {
                request = UnityWebRequest.Get(CatalogUrl);
                activeRequest = request;
                request.timeout = 25;
                request.redirectLimit = 0;
                request.SetRequestHeader("Cache-Control", "no-cache");
                operation = request.SendWebRequest();
            }
            catch (Exception ex) { Error = "Could not start the update request: " + ex.Message; }
            if (operation == null || request == null) yield break;
            while (!operation.isDone)
            {
                if (request.downloadedBytes > 2 * 1024 * 1024) { request.Abort(); break; }
                yield return null;
            }
            if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
            {
                Error = "Could not check GitHub: " + (request.error ?? "HTTP " + request.responseCode) + ". Try again when connected.";
                yield break;
            }
            if (request.downloadedBytes > 2 * 1024 * 1024)
            {
                Error = "The version catalog exceeded the 2 MiB limit.";
                yield break;
            }
            try
            {
                var catalog = Catalog.Parse(request.downloadHandler.text);
                Catalog = catalog; CheckedAt = DateTimeOffset.Now;
                RefreshInventory();
            }
            catch (Exception ex) { Error = "Could not read the version catalog: " + ex.Message; }
        }
        finally
        {
            request?.Dispose();
            // A stopped old coroutine must never reset a replacement request's state.
            if (current == generation) { activeRequest = null; Busy = false; }
        }
    }
}
