using HarmonyLib;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Adds Gungnir's own stats to its tooltip.
    ///
    ///     The lightning strike and the staff rack are mod mechanics, so vanilla's
    ///     tooltip has nothing to say about them - the numbers only existed in the config
    ///     file. Appending them here means the item explains itself in game, and the
    ///     values shown are read live so they always match what is configured.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int) })]
    internal static class TooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, int qualityLevel, ref string __result)
        {
            if (!ModConfig.Enabled.Value || !GungnirItem.IsGungnir(item))
            {
                return;
            }

            var slots = ModConfig.SlotCount.Value > 0
                ? ModConfig.SlotCount.Value
                : GungnirRecipe.SlotsForQuality(qualityLevel);

            var used = 0;
            var container = StaffContainer.For(item);
            if (container != null)
            {
                for (var i = 0; i < container.Inventory.m_width; i++)
                {
                    if (container.ItemAt(i) != null)
                    {
                        used++;
                    }
                }
            }

            var text = "\n";
            text += "\n<color=orange>Staff rack</color>: <color=yellow>"
                    + used + " / " + slots + "</color> staffs";

            // Only promised when the next level genuinely adds slots - the rack is
            // capped, so the curve can top out before the item runs out of levels.
            if (GungnirRecipe.MoreSlotsAvailable(qualityLevel))
            {
                text += " <color=#A8A8A8>(+" + GungnirRecipe.SlotsPerLevel
                        + " per upgrade)</color>";
            }

            var chance = ModConfig.LightningChance.Value;
            if (chance > 0f && ModConfig.LightningDamage.Value > 0f)
            {
                // Spelled out as "on top of" on purpose: the strike is a bonus on a hit
                // that already landed for full weapon damage, not a chance for the hit
                // to do anything at all.
                text += "\n<color=orange>Storm-touched</color>: <color=yellow>"
                        + chance.ToString("P0") + "</color> chance of <color=yellow>"
                        + ModConfig.LightningDamage.Value.ToString("0")
                        + "</color> extra lightning damage on top of every hit's damage";
            }

            __result += text;
        }
    }
}
