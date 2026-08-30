using System.Runtime.CompilerServices;

namespace GungnirStaff
{
    /// <summary>
    ///     The one-row inventory that lives *inside* a Gungnir staff.
    ///
    ///     Persisted into the item's own <c>m_customData</c> dictionary, which vanilla
    ///     <c>Inventory.Save</c>/<c>Load</c> round-trips for us. That means the stored
    ///     staffs survive relogs, follow the item if it is dropped or put in a chest,
    ///     and need no save file of our own.
    /// </summary>
    internal sealed class StaffContainer
    {
        internal const string InventoryKey = "gungnir.inv";
        internal const string SelectedKey = "gungnir.sel";
        internal const string OwnQualityKey = "gungnir.q";
        internal const string OwnVariantKey = "gungnir.var";

        /// <summary>
        ///     One container per Gungnir ItemData. Weak, so containers die with the item
        ///     rather than pinning dropped/destroyed items in memory.
        /// </summary>
        private static readonly ConditionalWeakTable<ItemDrop.ItemData, StaffContainer> Cache =
            new ConditionalWeakTable<ItemDrop.ItemData, StaffContainer>();

        private readonly ItemDrop.ItemData _owner;

        private StaffContainer(ItemDrop.ItemData owner)
        {
            _owner = owner;
            Inventory = new Inventory("$gungnir_bar_title", null, WantedWidth(), 1);
            LoadFromItem();
            Inventory.m_onChanged += OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            NormaliseGrid();
            SaveToItem();
        }

        /// <summary>
        ///     Forces every stored staff onto a valid slot of this one-row grid.
        ///
        ///     Items carry the m_gridPos they had in whatever inventory they came from.
        ///     A staff quick-moved out of row 2 of the 8-wide player inventory keeps
        ///     (x, 2), and vanilla's InventoryGrid.GetElement then indexes
        ///     m_elements[2 * width + x] - past the end of a 7x1 grid, throwing every
        ///     frame and leaving the grid half-drawn. Normalising on every change is what
        ///     keeps vanilla's own UI code in bounds.
        /// </summary>
        private void NormaliseGrid()
        {
            var width = Inventory.m_width;
            if (width <= 0)
            {
                return;
            }

            var taken = new bool[width];
            var misplaced = new System.Collections.Generic.List<ItemDrop.ItemData>();

            foreach (var item in Inventory.m_inventory)
            {
                var pos = item.m_gridPos;
                if (pos.y == 0 && pos.x >= 0 && pos.x < width && !taken[pos.x])
                {
                    taken[pos.x] = true;
                }
                else
                {
                    misplaced.Add(item);
                }
            }

            foreach (var item in misplaced)
            {
                var slot = System.Array.IndexOf(taken, false);
                if (slot < 0)
                {
                    // More staffs than slots: clamp rather than leave an index that
                    // crashes the UI. Overlap looks odd; a crash loop is worse.
                    item.m_gridPos = new Vector2i(width - 1, 0);
                    continue;
                }

                taken[slot] = true;
                item.m_gridPos = new Vector2i(slot, 0);
            }
        }

        /// <summary>The backing inventory. One row, <see cref="ModConfig.SlotCount"/> wide.</summary>
        internal Inventory Inventory { get; }

        /// <summary>Index of the staff currently projected onto Gungnir, or -1 for none.</summary>
        internal int SelectedSlot
        {
            get
            {
                if (_owner.m_customData != null
                    && _owner.m_customData.TryGetValue(SelectedKey, out var raw)
                    && int.TryParse(raw, out var slot))
                {
                    return slot;
                }

                return -1;
            }
            set
            {
                EnsureCustomData();
                _owner.m_customData[SelectedKey] = value.ToString();
            }
        }

        /// <summary>
        ///     Gungnir's OWN upgrade level, kept aside while it is impersonating a staff.
        ///
        ///     Projecting a staff overwrites m_quality with that staff's level, because
        ///     every vanilla system reads the level off the item. Gungnir's own level
        ///     would otherwise be gone the moment a staff was selected - and it is not a
        ///     cosmetic number: it decides how many rack slots the staff has, and it is
        ///     what an upgrade at the Galdr Table increments.
        /// </summary>
        internal int OwnQuality
        {
            get
            {
                if (_owner.m_customData != null
                    && _owner.m_customData.TryGetValue(OwnQualityKey, out var raw)
                    && int.TryParse(raw, out var quality)
                    && quality >= 1)
                {
                    return quality;
                }

                // Nothing stored yet - a Gungnir from before this was recorded, or one
                // that has never had a staff selected. Its own level is still on the item
                // in that case, so trust it rather than assuming level 1.
                return System.Math.Max(1, _owner.m_quality);
            }
            set
            {
                EnsureCustomData();
                _owner.m_customData[OwnQualityKey] = System.Math.Max(1, value).ToString();
            }
        }

        /// <summary>Gungnir's own variant, kept aside for the same reason as the level.</summary>
        internal int OwnVariant
        {
            get
            {
                if (_owner.m_customData != null
                    && _owner.m_customData.TryGetValue(OwnVariantKey, out var raw)
                    && int.TryParse(raw, out var variant))
                {
                    return variant;
                }

                return 0;
            }
            set
            {
                EnsureCustomData();
                _owner.m_customData[OwnVariantKey] = value.ToString();
            }
        }

        /// <summary>
        ///     Records the level the item is showing as Gungnir's own.
        ///
        ///     Only ever called while no staff is projected, so what is on the item really
        ///     is Gungnir's own level and not some staff's.
        /// </summary>
        internal void RememberOwnLevel()
        {
            OwnQuality = _owner.m_quality;
            OwnVariant = _owner.m_variant;
        }

        /// <summary>Gets (or lazily builds) the container stored in this Gungnir.</summary>
        internal static StaffContainer For(ItemDrop.ItemData gungnir)
        {
            if (gungnir == null)
            {
                return null;
            }

            if (Cache.TryGetValue(gungnir, out var existing))
            {
                existing.SyncWidth();
                return existing;
            }

            var created = new StaffContainer(gungnir);
            Cache.Add(gungnir, created);
            return created;
        }

        /// <summary>
        ///     Drops the cached container for an item, so the next lookup rebuilds it
        ///     from whatever is in the item's custom data now.
        ///
        ///     Needed after custom data is replaced wholesale - an upgrade at the Galdr
        ///     Table hands back a new item, and anything that had already touched it would
        ///     be holding a container built from the empty data it arrived with.
        /// </summary>
        internal static void Forget(ItemDrop.ItemData item)
        {
            if (item != null)
            {
                Cache.Remove(item);
            }
        }

        /// <summary>The staff in a given slot, or null.</summary>
        internal ItemDrop.ItemData ItemAt(int slot)
        {
            if (slot < 0 || slot >= Inventory.m_width)
            {
                return null;
            }

            return Inventory.GetItemAt(slot, 0);
        }

        /// <summary>Writes the inventory back into the item's custom data.</summary>
        internal void SaveToItem()
        {
            EnsureCustomData();

            var pkg = new ZPackage();
            Inventory.Save(pkg);
            _owner.m_customData[InventoryKey] = pkg.GetBase64();
        }

        private void LoadFromItem()
        {
            if (_owner.m_customData == null
                || !_owner.m_customData.TryGetValue(InventoryKey, out var base64)
                || string.IsNullOrEmpty(base64))
            {
                return;
            }

            try
            {
                Inventory.Load(new ZPackage(base64));

                // Load restores the saved dimensions; force our configured shape back on.
                SyncWidth();
                NormaliseGrid();
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError(
                    $"Could not read the staffs stored in this Gungnir, starting empty: {ex.Message}");
            }
        }

        /// <summary>
        ///     Slots the staff should have: four to start and two more per upgrade,
        ///     unless the config overrides it with a fixed number.
        /// </summary>
        private int WantedWidth()
        {
            var configured = ModConfig.SlotCount.Value;

            // OwnQuality, not m_quality: while a staff is projected the item is carrying
            // that staff's level, and sizing the rack from it would grow or shrink the
            // bar every time a different staff was selected.
            return configured > 0
                ? configured
                : GungnirRecipe.SlotsForQuality(OwnQuality);
        }

        /// <summary>Keeps the row width in step with the staff's level.</summary>
        private void SyncWidth()
        {
            var wanted = WantedWidth();
            if (Inventory.m_width == wanted && Inventory.m_height == 1)
            {
                return;
            }

            Inventory.m_width = wanted;
            Inventory.m_height = 1;
            NormaliseGrid();
        }

        private void EnsureCustomData()
        {
            if (_owner.m_customData == null)
            {
                _owner.m_customData = new System.Collections.Generic.Dictionary<string, string>();
            }
        }
    }
}
