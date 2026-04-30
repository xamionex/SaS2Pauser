using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ProjectMage;
using ProjectMage.character;
using ProjectMage.gamestate;
using ProjectMage.map;
using ProjectMage.particles;
using ProjectMage.player;
using ProjectMage.player.menu;
using ProjectMage.player.menu.levels;

namespace SaS2Pauser;

[HarmonyPatch]
public static class PausePatch
{
    private static readonly MethodInfo GetMainPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetMainPlayer");
    private static readonly FieldInfo LevelListField =
        AccessTools.Field(typeof(PlayerMenu), "level");

    /// Returns true if the game should be paused (any relevant modal menu is active).
    /// Called every frame by the world-update patches.
    private static bool IsPaused()
    {
        if (GameState.state != 1) return false; // only during actual gameplay

        var mainPlayer = GetMainPlayerMethod?.Invoke(null, null) as Player;
        if (mainPlayer?.menu == null) return false;

        if (LevelListField?.GetValue(mainPlayer.menu) is not List<LevelBase> levelList) return false;

        // Must be active
        // Exclude non-gameplay menus
        // Must be a full‑screen / modal menu (uiFlag 9)
        foreach (var level in from level in levelList where level.IsActive() where level is not (LevelMainMenu or LevelPressStart or LevelNotification) where level.screen?.uiFlag != null && level.screen.uiFlag.Contains(9) select level)
        {
            switch (level)
            {
                case LevelGameMenu when Plugin.PauseInMenu.Value:
                case LevelSettings when Plugin.PauseInSettings.Value:
                    return true;
                default:
                    return Plugin.PauseWhenBrowsing.Value; // any other modal menu
            }

        }

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(CharMgr), "Update")] public static bool CharMgrUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(MapMgr), "Update")] public static bool MapMgrUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(ParticleManager), "UpdateParticles")] public static bool ParticleManagerUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(GameSession), "Update")] public static bool GameSessionUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(NetworkMgr), "Update")] public static bool NetworkMgrUpdatePatch() => !IsPaused();
}