using BepInEx;
using Silverpine.ModdingTools;
using UnityEngine;

namespace Silverpine.ModUpdateChecker;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Silverpine.ModdingTools.Plugin.PluginGuid, "1.11.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Saelac.Silverpine.ModUpdateChecker";
    public const string PluginName = "Mod Update Checker";
    public const string PluginVersion = "1.0.0";
    private void Awake()
    {
        var root = new GameObject("SilverpineModUpdateChecker");
        DontDestroyOnLoad(root);
        root.AddComponent<UpdateService>();
        ModdingToolsMenu.RegisterSession(PluginGuid + ".main", "Check for Updates", (_, session) => UpdateWindow.Open(session), order: 950);
        InventoryModTools.RegisterSession(PluginGuid + ".inventory", "Check for Updates", (_, session) => UpdateWindow.Open(session), order: 950);
        Logger.LogInfo("Check for Updates registered in Modding Tools and the inventory Mods tab.");
        // Registrations and the persistent runner outlive BepInEx's bootstrap scene host.
    }
}
