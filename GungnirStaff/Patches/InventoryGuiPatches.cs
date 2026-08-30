using HarmonyLib;
using UnityEngine;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Ctrl+click a staff in the player inventory to send it into the rack.
    ///
    ///     Vanilla's Modifier.Move only knows two destinations - the open container, or
    ///     the player. With no container open it falls through to dropping the item on
    ///     the ground, which is what happened when you tried to quick-move staffs into
    ///     the rack. This claims that case when a rack is available.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    internal static class QuickMoveToRackPatch
    {
        private static bool Prefix(
            InventoryGui __instance,
            InventoryGrid grid,
            ItemDrop.ItemData item,
            InventoryGrid.Modifier mod)
        {
            if (ModConfig.VerboseLogging.Value)
            {
                var which = grid == GungnirBar.Grid ? "RACK"
                    : grid == __instance.m_playerGrid ? "PLAYER"
                    : grid == __instance.m_containerGrid ? "CONTAINER"
                    : "other";
                ModConfig.Trace($"OnSelectedItem grid={which} mod={mod} "
                                + $"item={item?.m_shared?.m_name ?? "empty"} "
                                + $"drag={__instance.m_dragItem?.m_shared?.m_name ?? "nothing"}");
            }

            if (!ModConfig.Enabled.Value || mod != InventoryGrid.Modifier.Move || item == null)
            {
                return true;
            }

            // Mid-drag, or a grid that is not the player's: leave vanilla alone.
            if (__instance.m_dragItem != null
                || grid == GungnirBar.Grid
                || grid != __instance.m_playerGrid)
            {
                return true;
            }

            // A chest is open - the player means "put it in the chest".
            if (__instance.m_currentContainer != null || !StaffRegistry.IsStaff(item))
            {
                return true;
            }

            var player = Player.m_localPlayer;
            var gungnir = GungnirItem.Active(player);
            var container = gungnir != null ? StaffContainer.For(gungnir) : null;
            if (container == null)
            {
                return true;
            }

            if (!container.Inventory.CanAddItem(item))
            {
                player.Message(MessageHud.MessageType.Center, "$gungnir_msg_rack_full");
                return false;
            }

            container.Inventory.MoveItemToThis(player.m_inventory, item);
            container.SaveToItem();
            ModConfig.Trace($"Quick-moved {item.m_shared.m_name} into the rack.");
            return false;
        }
    }

    /// <summary>
    ///     Stops a staff aimed at the rack from landing on the ground.
    ///
    ///     This is the method the log caught throwing staffs on the floor: the rack sits
    ///     on its own canvas, and when the inventory's outside-catcher wins the raycast
    ///     the click reads as "dropped outside". Over the rack, place it instead.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnDropOutside))]
    internal static class DropOutsidePatch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            if (!ModConfig.Enabled.Value || __instance.m_dragItem == null)
            {
                return true;
            }

            if (!GungnirBar.PointerIsOverBar())
            {
                return true;
            }

            ModConfig.Trace("Drop-outside landed on the staff rack; placing instead.");
            return !GungnirBar.TryPlaceDraggedItem(__instance);
        }
    }

    /// <summary>Keeps non-staff items out of the staff rack.</summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
    internal static class InventoryGridDropPatch
    {
        private static bool Prefix(
            InventoryGrid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (GungnirBar.Grid == null || __instance != GungnirBar.Grid)
            {
                return true;
            }

            if (StaffRegistry.IsStaff(item))
            {
                ModConfig.Trace($"Accepting {item.m_shared.m_name} into the rack.");
                return true;
            }

            ModConfig.Trace($"Rejecting {item?.m_shared?.m_name ?? "null"} from the rack.");

            // Reject: only magic staffs belong in the rack.
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$gungnir_msg_only_staffs");
            __result = false;
            return false;
        }
    }

    /// <summary>
    ///     Keeps Gungnir looking like Gungnir.
    ///
    ///     Selecting a staff points Gungnir's <c>m_shared</c> at that staff, which is
    ///     what makes every vanilla system treat it as that staff - but it also means
    ///     the icon would change with it. The item's identity should stay visually
    ///     stable in the hotbar and inventory, so the icon is restored here. Behaviour
    ///     is untouched; this is display only.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetIcon))]
    internal static class ItemIconPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref Sprite __result)
        {
            if (!ModConfig.Enabled.Value || !GungnirItem.IsGungnir(__instance))
            {
                return;
            }

            var icons = GungnirItem.BaseShared?.m_icons;
            if (icons != null && icons.Length > 0 && icons[0] != null)
            {
                __result = icons[0];
            }
        }
    }
}
