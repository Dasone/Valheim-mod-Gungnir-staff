using HarmonyLib;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Serves Gungnir's exact per-level upgrade costs.
    ///
    ///     Vanilla computes a requirement as <c>m_amount</c> at level 1 and
    ///     <c>(level - 1) * m_amountPerLevel</c> above it. That curve cannot produce the
    ///     thunder stone progression 1, 2, 3, 4 at any setting, so the exact numbers are
    ///     substituted here. Keyed on the requirement instance, so this touches only
    ///     Gungnir's recipe and every other recipe in the game is left alone.
    /// </summary>
    [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
    internal static class RequirementAmountPatch
    {
        private static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result)
        {
            int[] table;
            if (!GungnirRecipe.ExactCosts.TryGetValue(__instance, out table) || table.Length == 0)
            {
                return;
            }

            var index = UnityEngine.Mathf.Clamp(qualityLevel, 1, table.Length) - 1;
            __result = table[index];
        }
    }
}
