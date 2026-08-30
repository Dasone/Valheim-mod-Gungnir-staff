using HarmonyLib;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Registers the Gungnir prefab as soon as ObjectDB exists, which is the only
    ///     time early enough to matter.
    ///
    ///     Registering from <c>Player.OnSpawned</c> was too late, and the cost was the
    ///     item itself. The player's inventory is deserialised BEFORE that runs, and
    ///     <c>Inventory.Load</c> resolves every saved item through
    ///     <c>Inventory.AddItem(name, ...)</c>, whose first act is
    ///     <c>ObjectDB.instance.GetItemPrefab(name)</c>. A null there is not an error it
    ///     recovers from - it logs "Failed to find item prefab GungnirStaff" and returns
    ///     false, and the item is simply never added. The Gungnir vanished on every
    ///     restart, and because the whole staff rack lives in that item's
    ///     <c>m_customData</c>, every stored staff went with it. The next save then wrote
    ///     the inventory back without any of it.
    ///
    ///     ObjectDB is built twice - <c>Awake</c> in the start scene, and
    ///     <c>CopyOtherDB</c> when the game scene takes it over - so both are covered.
    ///     Registering is idempotent, so doing it twice costs nothing and leaves no gap.
    /// </summary>
    internal static class ObjectDbRegistration
    {
        /// <summary>
        ///     Never lets an exception escape into ObjectDB's own Awake. A throw there
        ///     would take the item database down with it, which is a far worse failure
        ///     than the one this exists to prevent.
        /// </summary>
        internal static void Ensure(string source)
        {
            try
            {
                var db = ObjectDB.instance;
                if (db == null || db.m_items == null || db.m_items.Count == 0)
                {
                    return;
                }

                // The standalone prefab is built around the model, so the bundle has to
                // be in hand before the item can be. Preload is guarded and reuses an
                // already-loaded bundle, so calling it here is cheap.
                GungnirVisual.Preload();

                if (GungnirItem.Create())
                {
                    ModConfig.Trace($"Gungnir registered from ObjectDB.{source}.");
                }
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError(
                    $"Registering Gungnir during ObjectDB.{source} failed. Any Gungnir in a "
                    + $"loaded inventory will be dropped on load: {ex}");
            }
        }
    }

    /// <summary>The start scene's database.</summary>
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    internal static class ObjectDbAwakePatch
    {
        private static void Postfix()
        {
            ObjectDbRegistration.Ensure("Awake");
        }
    }

    /// <summary>The game scene taking the database over from the start scene.</summary>
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    internal static class ObjectDbCopyPatch
    {
        private static void Postfix()
        {
            ObjectDbRegistration.Ensure("CopyOtherDB");
        }
    }
}
