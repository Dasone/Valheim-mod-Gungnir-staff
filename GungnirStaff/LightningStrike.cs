using System.Linq;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Gungnir's chance-on-hit lightning - the "storm touch".
    ///
    ///     Not a vanilla mechanic. The lightning spear this was measured from carries no
    ///     status effect at all - it simply adds a flat 10 lightning to every hit - so a
    ///     chance-based strike has to be built. The roll, the damage and the effect are
    ///     handled here, using the game's own lightning impact VFX so a trigger looks and
    ///     sounds native rather than hand-made.
    ///
    ///     The contract this file exists to keep:
    ///
    ///       * Every landed hit deals Gungnir's own weapon damage. That number comes from
    ///         the item's SharedData and vanilla applies it - nothing here ever writes to
    ///         <c>hit.m_damage</c> except to ADD, so a failed roll cannot subtract from,
    ///         zero or replace the swing that carried it.
    ///       * A successful roll ADDS lightning on top of that damage, in the same hit,
    ///         so the enemy takes base + lightning rather than one or the other.
    ///       * The visual and the sound belong to the roll, not to the swing. They are
    ///         spawned only on a trigger, which is what makes the proc readable.
    /// </summary>
    internal static class LightningStrike
    {
        /// <summary>
        ///     Vanilla effects to try, best first. These are the lightning staff's own
        ///     impact effects, which carry the visual and the sound together.
        /// </summary>
        private static readonly string[] EffectCandidates =
        {
            "fx_lightningstaffprojectile_hit",
            "vfx_lightningstaff_fire",
            "fx_lightningstaff_charge",
        };

        private static GameObject _effect;
        private static bool _searched;
        private static bool _warned;

        /// <summary>Rolls for a strike and applies it. True when it triggered.</summary>
        internal static bool TryStrike(HitData hit, Player attacker)
        {
            if (hit == null || attacker == null || !ModConfig.Enabled.Value)
            {
                return false;
            }

            // A player's own melee/attack hit, not a damage-over-time tick. Vanilla
            // stamps PlayerHit only when the attacker is a Player; burning, poison,
            // freezing and drowning all arrive here under their own hit types and would
            // otherwise let a single fire proc roll for lightning once a second.
            if (hit.m_hitType != HitData.HitType.PlayerHit)
            {
                return false;
            }

            // The storm touch lives in the weapon, so it has to be the weapon in hand -
            // not merely one in the backpack. GungnirItem.Active() honours the
            // Equipped/Carried setting, which is about who may use the staff rack; using
            // it here would mean a carried Gungnir electrified every bow shot too.
            var weapon = attacker.GetCurrentWeapon();
            if (!GungnirItem.IsGungnir(weapon))
            {
                return false;
            }

            // Melee only. With a staff selected the weapon is casting that staff's spell,
            // and adding a spear's lightning on top would be double-dipping.
            var container = StaffContainer.For(weapon);
            if ((container?.SelectedSlot ?? -1) >= 0)
            {
                return false;
            }

            // The lightning is a bonus ON the hit, so there has to be a hit to bonus.
            // A swing that was fully blocked or resisted away carries nothing, and
            // paying out 40 lightning for it would make the proc a way to bypass armour.
            var baseDamage = hit.GetTotalDamage();
            if (baseDamage <= 0f)
            {
                return false;
            }

            if (Random.value > ModConfig.LightningChance.Value)
            {
                // Deliberately silent: no effect, no sound, and hit.m_damage is left
                // exactly as vanilla built it, so the swing still lands for full damage.
                ModConfig.Trace(
                    $"Gungnir hit for {baseDamage:0.#} base damage; storm touch did not trigger.");
                return false;
            }

            var bonus = ModConfig.LightningDamage.Value;
            hit.m_damage.m_lightning += bonus;
            SpawnEffect(hit.m_point);

            ModConfig.Trace(
                $"Storm touch: {baseDamage:0.#} base damage + {bonus:0.#} lightning "
                + $"= {hit.GetTotalDamage():0.#} before resistances.");
            return true;
        }

        private static void SpawnEffect(Vector3 point)
        {
            var prefab = Effect();
            if (prefab == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    GungnirStaffPlugin.Log.LogWarning(
                        "No lightning impact effect found; strikes still deal damage but show "
                        + "nothing. Set LightningEffectPrefab to a valid effect name.");
                }

                return;
            }

            // A plain Instantiate rather than a networked spawn: this is cosmetic feedback
            // for the attacker, and a network spawn would need ownership and would
            // replicate a decoration to every player.
            var spawned = Object.Instantiate(prefab, point, Quaternion.identity);

            foreach (var znet in spawned.GetComponentsInChildren<ZNetView>(true))
            {
                Object.Destroy(znet);
            }

            // The impact prefabs are picked for their look and their sound, and those are
            // the only two things we want from them. Several vanilla effects also carry an
            // Aoe or a Projectile that deals damage of its own on spawn - which would put
            // an unconfigurable second damage number on top of LightningDamage and make
            // the strike hit far harder than the tooltip claims. Strip them so all of the
            // proc's damage stays the number the player can see and configure.
            foreach (var aoe in spawned.GetComponentsInChildren<Aoe>(true))
            {
                Object.Destroy(aoe);
            }

            foreach (var projectile in spawned.GetComponentsInChildren<Projectile>(true))
            {
                Object.Destroy(projectile);
            }

            // These effects are authored to be spawned and forgotten, but with their
            // ZNetView stripped nothing else will clean them up.
            Object.Destroy(spawned, 5f);
        }

        private static GameObject Effect()
        {
            if (_searched)
            {
                return _effect;
            }

            _searched = true;

            var configured = ModConfig.LightningEffectPrefab.Value;
            var names = string.IsNullOrEmpty(configured)
                ? EffectCandidates
                : new[] { configured }.Concat(EffectCandidates).ToArray();

            foreach (var name in names)
            {
                var found = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                if (found == null)
                {
                    found = Jotunn.Managers.PrefabManager.Instance.GetPrefab(name);
                }

                if (found != null)
                {
                    _effect = found;
                    ModConfig.Trace($"Lightning strikes will use '{name}'.");
                    return _effect;
                }
            }

            return null;
        }

        /// <summary>Forces a fresh lookup, e.g. after a hot reload.</summary>
        internal static void Reset()
        {
            _searched = false;
            _effect = null;
            _warned = false;
        }
    }
}
