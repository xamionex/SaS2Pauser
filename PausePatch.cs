using System;
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
    private static readonly MethodInfo GetMainPlayerMethod = AccessTools.Method(typeof(PlayerMgr), "GetMainPlayer");
    private static readonly MethodInfo GetPlayerMethod = AccessTools.Method(typeof(PlayerMgr), "GetPlayer", [typeof(int)]);
    private static readonly FieldInfo LevelListField = AccessTools.Field(typeof(PlayerMenu), "level");
    private static readonly FieldInfo PlayersField = AccessTools.Field(typeof(PlayerMgr), "player") ?? AccessTools.Field(typeof(PlayerMgr), "players");
    
    // Time-based unpause for animation mode
    private static DateTime _unpauseExpireTime = DateTime.MinValue;

    public static void RequestUnpause()
    {
        if (Plugin.UnpauseWhenEquipping.Value)
            _unpauseExpireTime = DateTime.UtcNow + TimeSpan.FromSeconds(1);
    }

    // Get all local players (main + coop partners)
    private static IEnumerable<Player> GetAllLocalPlayers()
    {
        if (PlayersField != null)
        {
            var playersObj = PlayersField.GetValue(null);
            switch (playersObj)
            {
                case Player[] playersArr:
                    foreach (var p in playersArr)
                        if (p != null) yield return p;
                    yield break;
                case List<Player> playersList:
                    foreach (var p in playersList.Where(p => p != null)) yield return p;
                    yield break;
            }
        }

        var uniquePlayers = new HashSet<Player>();
        if (GetMainPlayerMethod?.Invoke(null, null) is Player main)
            uniquePlayers.Add(main);
        if (GetPlayerMethod != null)
        {
            for (var i = 0; i < 4; i++)
            {
                try
                {
                    if (GetPlayerMethod.Invoke(null, [i]) is Player p)
                        uniquePlayers.Add(p);
                }
                catch
                {
                    // ignore invalid
                }
            }
        }
        foreach (var player in uniquePlayers)
            yield return player;
    }

    private static bool IsAnySettingsLevelActive(List<LevelBase> levelList)
    {
        foreach (var level in levelList.Where(level => level.IsActive()))
        {
            if (level is LevelSettings or LevelSettingsList or LevelMidChoice)
                return true;
            var typeName = level.GetType().Name;
            if (typeName is "LevelFileBug" or "LevelBugReport" or "LevelReport")
                return true;
        }

        return false;
    }

    public static bool IsPaused()
    {
        // Temporary unpause for equipment animation
        if (Plugin.UnpauseWhenEquipping.Value && DateTime.UtcNow < _unpauseExpireTime)
            return false;
        
        if (GameState.state != 1) return false;

        foreach (var player in GetAllLocalPlayers())
        {
            if (player?.menu == null) continue;
            if (LevelListField?.GetValue(player.menu) is not List<LevelBase> levelList) continue;

            foreach (var level in levelList.Where(l => l.IsActive()))
            {
                if (level is LevelMainMenu or LevelPressStart or LevelNotification)
                    continue;
                if (level.screen?.uiFlag == null || !level.screen.uiFlag.Contains(9))
                    continue;

                if (level is LevelGameMenu && Plugin.PauseInMenu.Value)
                    return true;
                if (Plugin.PauseInMenuSubmenus.Value)
                    return true;
                if (Plugin.PauseInAllMenus.Value)
                    return true;
            }

            if (Plugin.PauseInSettings.Value && IsAnySettingsLevelActive(levelList)) return true;
        }

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(CharMgr), "Update")] public static bool CharMgrUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(MapMgr), "Update")] public static bool MapMgrUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(ParticleManager), "UpdateParticles")] public static bool ParticleManagerUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(GameSession), "Update")] public static bool GameSessionUpdatePatch() => !IsPaused();
    [HarmonyPrefix, HarmonyPatch(typeof(NetworkMgr), "Update")] public static bool NetworkMgrUpdatePatch() => !IsPaused();
}