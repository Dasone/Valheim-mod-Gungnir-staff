using System.Collections.Generic;
using HarmonyLib;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Carries the staff rack across an upgrade at the Galdr Table.
    ///
    ///     Upgrading is not what it looks like. Vanilla does not raise the level of the
    ///     item you hand it - <c>InventoryGui.DoCrafting</c> unequips it, calls
    ///     <c>Inventory.RemoveItem</c> on it, and then adds a COMPLETELY NEW item built
    ///     from the recipe's prefab at the next level, restoring only the old item's grid
    ///     position and variant.
    ///
    ///     Everything else is left behind, and for Gungnir "everything else" is the whole
    ///     rack: the staffs live in <c>m_customData</c>, which the new item does not have.
    ///     Upgrading therefore ate every stored staff. This copies that data onto the
    ///     replacement, so an upgrade keeps the rack, the selection and the level.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    internal static class CraftingUpgradePatch
    {
        private static Dictionary<string, string> _carried;
        private static Vector2i _slot;
        private static int _expectedQuality;

        private static void Prefix(InventoryGui __instance, Player player)
        {
            _carried = null;

            var upgrading = __instance?.m_craftUpgradeItem;
            if (player == null || !GungnirItem.IsGungnir(upgrading))
            {
                return;
            }

            // Put the staff away first. While one is projected the item is wearing that
            // staff's SharedData and that staff's level, so vanilla would read the
            // STAFF's level, add one to it, and check the staff's max quality - upgrading
            // the wrong thing entirely.
            StaffSwitcher.Holster(player, upgrading);

            var container = StaffContainer.For(upgrading);
            container?.SaveToItem();

            // A copy, not the dictionary itself: the old item is about to be removed, and
            // ItemData.Clone shares this reference rather than duplicating it.
            _carried = upgrading.m_customData == null
                ? null
                : new Dictionary<string, string>(upgrading.m_customData);
            _slot = upgrading.m_gridPos;
            _expectedQuality = upgrading.m_quality + 1;
        }

        private static void Postfix(Player player)
        {
            var carried = _carried;
            _carried = null;

            if (carried == null || player?.m_inventory == null)
            {
                return;
            }

            // The replacement is added back at the old item's grid position, which is the
            // only thing that reliably identifies it - it is a different object with none
            // of the old one's data on it.
            var replacement = player.m_inventory.GetItemAt(_slot.x, _slot.y);
            if (!GungnirItem.IsGungnir(replacement))
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "An upgraded Gungnir could not be found in its old slot; its staff rack "
                    + "has been left on the original item rather than moved.");
                return;
            }

            // Only ever onto a fresh item. If the upgrade did not go through, the item in
            // that slot is the original - already holding this exact data - and writing
            // over it would be pointless at best.
            if (replacement.m_quality != _expectedQuality)
            {
                return;
            }

            var moved = new Dictionary<string, string>(carried);

            // Written before the data lands on the item, not after. The rack width is
            // derived from Gungnir's own level, so the container has to be built already
            // knowing the new one - set it afterwards and the bar comes up at the old
            // width and only corrects itself a frame later.
            moved[StaffContainer.OwnQualityKey] = replacement.m_quality.ToString();
            moved[StaffContainer.OwnVariantKey] = replacement.m_variant.ToString();
            replacement.m_customData = moved;

            // Anything that touched the item on its way into the inventory may already
            // have built a container from the empty data it arrived with.
            StaffContainer.Forget(replacement);

            ModConfig.Trace(
                $"Carried the staff rack onto the upgraded Gungnir (level {replacement.m_quality}).");
        }
    }
}
