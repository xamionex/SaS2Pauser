using System.Reflection;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.player;
using ProjectMage.player.menu.levels;

namespace SaS2Pauser;

[HarmonyPatch]
public class EquipmentPatch
{
    private static readonly MethodInfo UpdateTextMethod = AccessTools.Method(typeof(LevelLoadout), "UpdateText");
    private static readonly MethodInfo ResetWeaponBuffsAndCooldownsMethod = AccessTools.Method(typeof(PlayerEquipment), "ResetWeaponBuffsAndCooldowns");
    private static readonly FieldInfo LoadoutTypeField = AccessTools.Field(typeof(LevelInventoryPicker), "loadoutType");
    private static readonly FieldInfo LoadoutSelXField = AccessTools.Field(typeof(LevelLoadout), "selX");
    private static readonly FieldInfo LoadoutSelYField = AccessTools.Field(typeof(LevelLoadout), "selY");
    
    // Handles equip when you've already selected the item and are choosing a slot.
    [HarmonyPatch(typeof(LevelLoadout), "Update")]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool LevelLoadout_Update_Prefix(LevelLoadout __instance, Character character, float frameTime)
    {
        if (!PausePatch.IsPaused())
            return true; // Run original

        var player = __instance.player;
        if (player == null || !__instance.CanInput())
            return true; // Let original handle (it will likely do nothing)

        // Detect equip action: equipMode > -1 and Accept key or mouse click
        if (__instance.equipMode <= -1 || (!player.keys.keyAccept && !__instance.queueClick)) return true;
        var selComp = __instance.GetSelComponent();
        if (selComp == null) return true;
        if (Plugin.UnpauseWhenEquipping.Value)
        {
            // Let the game run for a moment to play the animation
            PausePatch.RequestUnpause();
            return true; // Run original method (will now be unpaused)
        }
                
        var equipSlot = selComp.GetIntData(12);
        var itemIdx = ((LevelInventoryOptions)player.menu.GetLevelByScreen(73)).itemIdx;

        // Perform equip (data only)
        player.equipment.EquipItem(equipSlot, itemIdx);
        player.equipment.UpdateAbilityItems();

        // Refresh loadout UI
        if (UpdateTextMethod != null)
            UpdateTextMethod.Invoke(__instance, null);
        __instance.SetPlayerValues(2);

        // Reset equip mode and play sound
        __instance.equipMode = -1;
        __instance.PlayAccept();

        // Consume input to avoid double action
        player.keys.keyAccept = false;
        __instance.queueClick = false;

        // Skip original to prevent animation
        return false;

        // For all other inputs (navigation, cancel, etc.), run original
    }

    // Handles equip when you select an item from the inventory picker.
    [HarmonyPatch(typeof(LevelInventoryPicker), "Update")]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool LevelInventoryPicker_Update_Prefix(LevelInventoryPicker __instance, Character character, float frameTime)
    {
        if (!PausePatch.IsPaused())
            return true; // Run original

        var player = __instance.player;
        if (player == null || !__instance.CanInput())
            return true;

        // Look for Accept key press
        if (!player.keys.keyAccept) return true;
        // Get selected item index
        if (__instance.selComponentIdx.Count <= 0) return true;
        var uiComponent = __instance.screen.uiComponent[__instance.selComponentIdx[0]];
        var num = __instance.selX + __instance.selY * uiComponent.GetIntData(2);
        if (num <= -1 || num >= __instance.invItem.Count) return true;
        if (Plugin.UnpauseWhenEquipping.Value)
        {
            // Let the game run for a moment to play the animation
            PausePatch.RequestUnpause();
            return true; // Run original method (now unpaused)
        }
                    
        var invItemIdx = __instance.invItem[num];
        var loadoutType = (int)LoadoutTypeField.GetValue(__instance);

        // Remove from any other slots that would conflict (mirroring original logic)
        __instance.CheckRemove(loadoutType, 4, 5, invItemIdx);
        __instance.CheckRemove(loadoutType, 7, 8, invItemIdx);
        __instance.CheckRemove(loadoutType, 10, 19, invItemIdx);

        // Perform equip
        player.equipment.EquipItem(loadoutType, invItemIdx);
        player.equipment.UpdateAbilityItems();

        // Refresh weapon buffs if needed
        if (loadoutType is 4 or 5 && ResetWeaponBuffsAndCooldownsMethod != null)
            ResetWeaponBuffsAndCooldownsMethod.Invoke(player.equipment, [character]);

        // Fix tool selection if the equipped tool slot became empty
        if (player.equipment.equippedItem[player.equipment.selTool + 9] < 0)
        {
            for (var i = 0; i < 10; i++)
            {
                player.equipment.selTool = (player.equipment.selTool + 1) % 10;
                if (player.equipment.equippedItem[player.equipment.selTool + 9] > -1)
                    break;
            }
        }

        // Refresh the loadout screen (LevelLoadout)
        var loadout = (LevelLoadout)player.menu.GetLevelByScreen(9);
        if (loadout != null)
        {
            // Save current cursor position before reactivating
            var oldSelX = (int)LoadoutSelXField.GetValue(loadout);
            var oldSelY = (int)LoadoutSelYField.GetValue(loadout);

            // Refresh UI, deactivate picker, reactivate loadout (this would reset cursor)
            __instance.Deactivate();
            loadout.Activate();

            // Restore saved cursor position
            LoadoutSelXField.SetValue(loadout, oldSelX);
            LoadoutSelYField.SetValue(loadout, oldSelY);

            // Optionally refresh text and stats (won't affect cursor)
            if (UpdateTextMethod != null)
                UpdateTextMethod.Invoke(loadout, null);
            loadout.SetPlayerValues(2);
        }
        else
        {
            // Fallback: just close picker
            __instance.Deactivate();
        }

        __instance.PlayAccept();

        // Consume input
        player.keys.keyAccept = false;
        __instance.queueClick = false;

        // Skip original to prevent animation
        return false;
    }
}