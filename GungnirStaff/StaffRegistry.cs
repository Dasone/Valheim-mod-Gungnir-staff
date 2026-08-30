using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Decides what counts as "a staff".
    ///     Detected from ObjectDB at runtime by skill type rather than from a hard-coded
    ///     name list, so the vanilla staffs are found automatically however many there
    ///     are - and so are staffs added by other mods, with no changes here.
    /// </summary>
    internal static class StaffRegistry
    {
        private static List<GameObject> _cache;

        /// <summary>True if this item is a magic staff (and not Gungnir itself).</summary>
        internal static bool IsStaff(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null || GungnirItem.IsGungnir(item))
            {
                return false;
            }

            return IsStaffShared(item.m_shared);
        }

        private static bool IsStaffShared(ItemDrop.ItemData.SharedData shared)
        {
            // Every vanilla staff is driven by one of the two magic skills. Checking the
            // skill rather than the prefab name is what makes this future-proof.
            if (shared.m_skillType != Skills.SkillType.ElementalMagic
                && shared.m_skillType != Skills.SkillType.BloodMagic)
            {
                return false;
            }

            return shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
                   || shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                   || shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon;
        }

        /// <summary>All staff prefabs currently known to ObjectDB. Cached after the first scan.</summary>
        internal static List<GameObject> AllStaffPrefabs()
        {
            if (_cache != null)
            {
                return _cache;
            }

            var db = ObjectDB.instance;
            if (db == null || db.m_items == null)
            {
                return new List<GameObject>();
            }

            var found = new List<GameObject>();
            foreach (var prefab in db.m_items)
            {
                if (prefab == null || prefab.name == GungnirItem.PrefabName)
                {
                    continue;
                }

                var drop = prefab.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared == null || !IsStaffShared(drop.m_itemData.m_shared))
                {
                    continue;
                }

                found.Add(prefab);
            }

            _cache = found;
            ModConfig.Trace($"Detected {found.Count} staff(s): {string.Join(", ", found.Select(p => p.name))}");
            return _cache;
        }

        /// <summary>Drops the cache. Called on hot reload and when ObjectDB changes.</summary>
        internal static void Invalidate()
        {
            _cache = null;

            // The common stance is measured from this list, so it has to be re-measured
            // whenever the list can change.
            StaffStance.Reset();
        }
    }
}
