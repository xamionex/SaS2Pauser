using System;
using System.IO;
using System.Timers;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.NetLauncher.Common;
using HarmonyLib;
using System.Runtime.CompilerServices;

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
    internal static ConfigEntry<bool> PauseWhenBrowsing;

    // Counter of active menus that should keep the game paused.

    private FileSystemWatcher _configWatcher;
    private Timer _debounceTimer;

    public override void Load()
    {
        Instance = this;

        PauseInMenu = Config.Bind("General", "PauseInMenu", true, "Pause the game when the in‑game menu is open.");
        PauseInSettings = Config.Bind("General", "PauseInSettings", true, "When PauseInMenu is also true, keep the game paused when you open the settings screen from the in‑game menu.");
        PauseWhenBrowsing = Config.Bind("General", "PauseWhenBrowsing", true, "When PauseInMenu is also true, keep the game paused when you open any other sub‑menu (equipment, inventory, etc.) from the in‑game menu.");
        
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
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseInMenu,        "Pauser", "Pause in Menu",       order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseInSettings,    "Pauser", "Pause in Settings",   order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(PauseWhenBrowsing,  "Pauser", "Pause when Browsing", order += 1);
    }
    // ReSharper restore RedundantAssignment

    public override bool Unload()
    {
        _configWatcher?.Dispose();
        _debounceTimer?.Dispose();
        return base.Unload();
    }
}