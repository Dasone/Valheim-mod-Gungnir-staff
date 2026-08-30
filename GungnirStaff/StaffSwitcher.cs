namespace GungnirStaff
{
    /// <summary>
    ///     Turns Gungnir into whichever stored staff is selected, and back again.
    ///
    ///     The trick is that vanilla persists an item's identity through
    ///     <c>m_dropPrefab</c> but drives all of its *behaviour* through
    ///     <c>m_shared</c>. Pointing Gungnir's <c>m_shared</c> at the selected staff
    ///     therefore makes every vanilla system - attacks, eitr cost, projectiles,
    ///     skill XP, left/right/middle click, model and icon - behave exactly as if
    ///     that staff were equipped, with no per-staff code on our side, while the item
    ///     still saves and reloads as a Gungnir.
    /// </summary>
    internal static class StaffSwitcher
    {
        /// <summary>Selects a slot. Passing the already-selected slot holsters instead (toggle).</summary>
        internal static void Select(Player player, ItemDrop.ItemData gungnir, int slot)
        {
            var container = StaffContainer.For(gungnir);
            if (container == null)
            {
                return;
            }

            var staff = container.ItemAt(slot);
            if (staff == null)
            {
                ModConfig.Trace($"Slot {slot + 1} is empty.");
                return;
            }

            if (container.SelectedSlot == slot)
            {
                Holster(player, gungnir);
                return;
            }

            SyncActiveDurability(gungnir);
            container.SelectedSlot = slot;
            Project(player, gungnir, staff);

            ModConfig.Trace($"Selected slot {slot + 1}: {staff.m_shared.m_name}");
        }

        /// <summary>Puts the active staff away; Gungnir goes back to being plain Gungnir.</summary>
        internal static void Holster(Player player, ItemDrop.ItemData gungnir)
        {
            var container = StaffContainer.For(gungnir);
            if (container == null || container.SelectedSlot < 0)
            {
                return;
            }

            SyncActiveDurability(gungnir);
            container.SelectedSlot = -1;
            Project(player, gungnir, null);

            ModConfig.Trace("Holstered.");
        }

        /// <summary>
        ///     Re-applies whatever selection the item already carries. Called after a
        ///     load, an equip, or a hot reload, so the state in custom data becomes real
        ///     again without the player having to press anything.
        /// </summary>
        internal static void Reapply(Player player, ItemDrop.ItemData gungnir)
        {
            var container = StaffContainer.For(gungnir);
            if (container == null)
            {
                return;
            }

            var slot = container.SelectedSlot;
            var staff = slot >= 0 ? container.ItemAt(slot) : null;

            if (slot >= 0 && staff == null)
            {
                // The selected staff was taken out of the bar while we were away.
                container.SelectedSlot = -1;
            }

            Project(player, gungnir, staff);
        }

        /// <summary>
        ///     Keeps the stored staff's durability in step with the Gungnir that is
        ///     currently impersonating it, so wear is not lost on switch or logout.
        /// </summary>
        internal static void SyncActiveDurability(ItemDrop.ItemData gungnir)
        {
            var container = StaffContainer.For(gungnir);
            var slot = container?.SelectedSlot ?? -1;
            if (slot < 0)
            {
                return;
            }

            var staff = container.ItemAt(slot);
            if (staff == null)
            {
                return;
            }

            // This runs every frame, and serialising the container is not free - only
            // write when the value actually moved.
            if (staff.m_durability == gungnir.m_durability)
            {
                return;
            }

            staff.m_durability = gungnir.m_durability;
            container.SaveToItem();
        }

        /// <summary>
        ///     Does the actual swap. Passing null restores Gungnir's own identity.
        ///     Unequip/equip around the change is what makes the held model and the
        ///     attack setup refresh; without it the visuals would lag a swap behind.
        /// </summary>
        private static void Project(Player player, ItemDrop.ItemData gungnir, ItemDrop.ItemData staff)
        {
            var wasEquipped = player != null && gungnir.m_equipped;
            if (wasEquipped)
            {
                player.UnequipItem(gungnir, false);
            }

            if (staff?.m_shared != null)
            {
                gungnir.m_shared = staff.m_shared;
                gungnir.m_quality = staff.m_quality;
                gungnir.m_variant = staff.m_variant;
                gungnir.m_durability = staff.m_durability;
                gungnir.m_worldLevel = staff.m_worldLevel;
            }
            else if (GungnirItem.BaseShared != null)
            {
                gungnir.m_shared = GungnirItem.BaseShared;
                gungnir.m_quality = 1;
                gungnir.m_variant = 0;
            }

            if (wasEquipped)
            {
                player.EquipItem(gungnir, false);
            }
        }
    }
}
