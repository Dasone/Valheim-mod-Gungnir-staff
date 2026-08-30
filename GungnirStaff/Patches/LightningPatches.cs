using HarmonyLib;

namespace GungnirStaff.Patches
{
    /// <summary>
    ///     Hooks the moment damage lands, so Gungnir can roll for a lightning strike.
    ///
    ///     Patched on the victim's Damage call rather than on the attack: this is the one
    ///     place where the hit, its damage numbers and its world position are all
    ///     available at once, which is exactly what the strike needs.
    ///
    ///     A prefix, and the only safe place to be one: vanilla's Damage() does nothing
    ///     but forward the hit to RPC_Damage, so editing it here reaches the owner intact
    ///     and runs exactly once - RPC_Damage never calls back into Damage(). The hit
    ///     still carries the weapon's own damage when we see it; the strike only ever
    ///     adds to it.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class CharacterDamagePatch
    {
        private static void Prefix(HitData hit)
        {
            if (hit == null)
            {
                return;
            }

            // Only the local player's own hits. Every client runs this code, so rolling
            // on all of them would multiply the chance by the number of players nearby.
            var attacker = hit.GetAttacker() as Player;
            if (attacker == null || attacker != Player.m_localPlayer)
            {
                return;
            }

            LightningStrike.TryStrike(hit, attacker);
        }
    }
}
