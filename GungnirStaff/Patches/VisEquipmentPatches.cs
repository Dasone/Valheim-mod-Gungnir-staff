using HarmonyLib;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Fills in m_dropPrefab the moment an item is created.
    ///
    ///     Setting it on the prefab should be enough - every item is cloned from there -
    ///     but on this runtime-built prefab it demonstrably is not arriving, and the
    ///     field is what Inventory.Save uses to identify an item. Catching it at creation
    ///     covers every route in: the console, a craft, a chest, a pickup.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
        new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long),
                typeof(string), typeof(bool) })]
    internal static class InventoryAddItemPatch
    {
        private static void Postfix(string name, ItemDrop.ItemData __result)
        {
            if (__result == null || __result.m_dropPrefab != null
                || name != GungnirItem.PrefabName)
            {
                return;
            }

            var prefab = ObjectDB.instance != null
                ? ObjectDB.instance.GetItemPrefab(GungnirItem.PrefabName)
                : null;

            if (prefab != null)
            {
                __result.m_dropPrefab = prefab;
                ModConfig.Trace("Filled in m_dropPrefab on a newly created Gungnir.");
            }
        }
    }

    /// <summary>
    ///     Guarantees every equipped Gungnir has the m_dropPrefab the game assumes.
    ///
    ///     Humanoid.SetupVisEquipment reads m_dropPrefab.name with no null check, so a
    ///     missing one is an immediate NullReferenceException on equip and on sheathe.
    ///     Vanilla populates it in ItemDrop.Awake, which never runs on an inactive
    ///     runtime prefab - so a standalone-built item can reach the player without it.
    ///     Repairing here fixes it at the exact call that would otherwise throw.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
    internal static class SetupVisEquipmentPatch
    {
        private static bool _logged;

        private static void Prefix(Humanoid __instance)
        {
            Repair(__instance.m_rightItem);
            Repair(__instance.m_leftItem);
            Repair(__instance.m_hiddenRightItem);
            Repair(__instance.m_hiddenLeftItem);
        }

        private static void Repair(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab != null)
            {
                return;
            }

            // Identified by the NAME TOKEN, not by SharedData reference. The item can
            // legitimately carry a different SharedData instance than the prefab, so a
            // reference check silently never matches - which is exactly what happened.
            if (item.m_shared == null || item.m_shared.m_name != GungnirItem.NameToken)
            {
                return;
            }

            var prefab = GungnirItem.Prefab
                         ?? (ObjectDB.instance != null
                             ? ObjectDB.instance.GetItemPrefab(GungnirItem.PrefabName)
                             : null);
            if (prefab == null)
            {
                return;
            }

            item.m_dropPrefab = prefab;

            if (!_logged)
            {
                _logged = true;
                GungnirStaffPlugin.Log.LogWarning(
                    "A Gungnir reached the player without m_dropPrefab - restored it. "
                    + "The standalone prefab is inactive, so ItemDrop.Awake never fills it in.");
            }
        }
    }
}
