using BepInEx;
using Silverpine.ModdingTools;

namespace Silverpine.ModUpdateChecker;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Silverpine.ModdingTools.Plugin.PluginGuid, "1.11.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Saelac.Silverpine.ModUpdateChecker";
    public const string PluginName = "Mod Update Checker";
    public const string PluginVersion = "1.0.1";
    private void Awake()
    {
        ModdingToolsMenu.RegisterSession(PluginGuid + ".main", "Check for Updates", (_, session) => UpdateWindow.Open(session), order: 950);
        InventoryModTools.RegisterSession(PluginGuid + ".inventory", "Check for Updates", (_, session) => UpdateWindow.Open(session), order: 950);
        Logger.LogInfo("Check for Updates registered in Modding Tools and the inventory Mods tab.");
        // Register only managed callbacks here. The runner is created on demand after bootstrap.
    }
}
