using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Timers;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using HarmonyLib;

namespace SaS2Pauser;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
// ReSharper disable once StringLiteralTypo
[BepInDependency("amione.SaS2ModOptions", BepInDependency.DependencyFlags.SoftDependency)]
// ReSharper disable once ClassNeverInstantiated.Global
public class Plugin : BasePlugin
{
    internal static Plugin Instance;
    internal static ConfigEntry<bool> PauseInMenu;
    internal static ConfigEntry<bool> PauseInSettings;
    internal static ConfigEntry<bool> PauseInAllMenus;
    internal static ConfigEntry<bool> PauseInMenuSubmenus;
    internal static ConfigEntry<bool> UnpauseWhenEquipping;

    // Counter of active menus that should keep the game paused.

    private FileSystemWatcher _configWatcher;
    private Timer _debounceTimer;

    public override void Load()
    {
        Instance = this;

        PauseInMenu = Config.Bind("General", "PauseInMenu", true, "Pauses the game when the in-game menu is open.");
        PauseInSettings = Config.Bind("General", "PauseInSettings", true, "Pauses the game when you open the settings screen from the in-game menu.");
        PauseInAllMenus = Config.Bind("General", "PauseInAllMenus", false, "Pauses the game when any menu is open.");
        PauseInMenuSubmenus = Config.Bind("General", "PauseInMenuSubmenus", false, "Pause in all sub-menus (Inventory, Skill Tree, Bestiary, etc.) after entering the game menu.");
        UnpauseWhenEquipping = Config.Bind("General", "UnpauseWhenEquipping", false, "Temporarily unpause the game for the frame when you equip an item, allowing the equip animation to play. If turned off, skip the animation instead");
        
        var modOptionsType = Type.GetType("SaS2ModOptions.SaS2ModOptions, amione.SaS2ModOptions");
        if (modOptionsType != null)
        {
            TryRegisterModOptions();
            Instance.Log.LogInfo("Successfully registered configs with SaS2ModOptions.");
        }
        else
        {
            Instance.Log.LogInfo("Mod Options not installed; config file only.");
        }

        var configDirectory = Path.GetDirectoryName(Config.ConfigFilePath);
        var configFileName  = Path.GetFileName(Config.ConfigFilePath);
        if (!string.IsNullOrEmpty(configDirectory))
        {
            _configWatcher = new FileSystemWatcher(configDirectory, configFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(1000) { AutoReset = false };
            _debounceTimer.Elapsed += (_, _) =>
            {
                Config.Reload();
                Instance.Log.LogInfo("Configuration reloaded.");
            };
            _configWatcher.Changed += (_, _) => { _debounceTimer.Stop(); _debounceTimer.Start(); };
        }
        else
        {
            Instance.Log.LogWarning("Could not determine config directory, live reload disabled.");
        }

        var harmony = new Harmony(PluginInfo.PluginGuid);
        harmony.PatchAll();
        Instance.Log.LogInfo($"{PluginInfo.PluginName} v{PluginInfo.PluginVersion} loaded.");
    }

    // ReSharper disable RedundantAssignment
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TryRegisterModOptions()
    {
        var order = 0;
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseInMenu, "Pauser", "Pause in Menu", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseInSettings, "Pauser", "Pause in Settings", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseInMenuSubmenus, "Pauser", "Pause in Sub-menus", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseInAllMenus, "Pauser", "Pause in all menus", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(UnpauseWhenEquipping, "Pauser", "Unpause while equipping (Off=skip animation)", order += 1);
    }
    // ReSharper restore RedundantAssignment

    public override bool Unload()
    {
        _configWatcher?.Dispose();
        _debounceTimer?.Dispose();
        return base.Unload();
    }
}