using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HeadclefUI;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("headclef.Improve", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("headclef.CharacterStats", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("headclef.IncreaseTumbleDamage", BepInDependency.DependencyFlags.SoftDependency)]
public class HeadclefUI : BaseUnityPlugin
{
    private const string PluginGuid = "headclef.UI";
    private const string PluginName = "UI";
    private const string PluginVersion = "1.1.1";

    internal static HeadclefUI Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void Awake()
    {
        Instance = this;
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    private void OnDestroy()
    {
        Harmony?.UnpatchSelf();
    }
}
