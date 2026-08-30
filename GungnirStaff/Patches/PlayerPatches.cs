using HarmonyLib;

namespace GungnirStaff.Patches
{
    [HarmonyPatch(typeof(Player))]
    internal static class PlayerPatches
    {
        /// <summary>
        ///     Once the local player exists, the placeholder prefab can be created and any
        ///     Gungnir already in the inventory can have its selection re-applied.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Player.OnSpawned))]
        private static void OnSpawned_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            GungnirStaffPlugin.Log.LogInfo($"Local player '{__instance.GetPlayerName()}' spawned.");
            Alive.Announce("spawned", GungnirStaffPlugin.Instance?.Harmony);

            StaffRegistry.Invalidate();
            GungnirItem.Create();

            var gungnir = GungnirItem.CarriedBy(__instance);
            if (gungnir != null)
            {
                StaffSwitcher.Reapply(__instance, gungnir);
            }
        }

        /// <summary>
        ///     Stops Ctrl+3 from selecting a staff *and* triggering vanilla hotbar slot 3
        ///     in the same keystroke.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Player.UseHotbarItem))]
        private static bool UseHotbarItem_Prefix()
        {
            if (!ModConfig.Enabled.Value)
            {
                return true;
            }

            if (GungnirItem.Active(Player.m_localPlayer) == null)
            {
                return true;
            }

            // Suppress vanilla only when one of our shortcuts actually fired.
            return !Inputs.SlotShortcutActive();
        }
    }
}
