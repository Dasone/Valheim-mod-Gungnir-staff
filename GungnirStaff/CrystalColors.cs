using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     The colour Gungnir's crystal burns with, per selected staff.
    ///
    ///     Entries are bound lazily, one per staff actually found in ObjectDB, so the
    ///     F1 menu lists exactly the staffs this installation has - including any added
    ///     by other mods - rather than a hard-coded set that would go stale.
    ///
    ///     Stored as hex strings because BepInEx has no built-in converter for
    ///     UnityEngine.Color; hex also round-trips cleanly through the config file and
    ///     is easy to paste from a colour picker.
    /// </summary>
    internal static class CrystalColors
    {
        private const string Section = "Crystal colours";

        /// <summary>Shipped defaults, keyed by prefab name.</summary>
        /// <summary>
        ///     Shipped defaults, keyed by prefab name.
        ///
        ///     Deliberately spread around the colour wheel rather than picked per staff in
        ///     isolation: the point of the crystal is telling staffs apart at a glance, so
        ///     two staffs sharing a hue defeats it. The earlier set had two reds, two
        ///     greens and two cyans. These are eight distinct hues at full saturation.
        /// </summary>
        private static readonly Dictionary<string, string> Defaults =
            new Dictionary<string, string>
            {
                { "StaffShield", "#FF0A2E" },       // Protection - vivid red
                { "StaffRedTroll", "#FF2ECC" },     // Trollstav - magenta
                { "StaffSkeleton", "#B44BFF" },     // Dead Raiser - necrotic violet
                { "StaffLightning", "#6A5BFF" },    // Lightning - blue-violet
                { "StaffIceShards", "#00D4FF" },    // Frost - cyan
                { "StaffGreenRoots", "#35FF00" },   // The Wild - green
                { "StaffClusterbomb", "#FFE100" },  // Clusterbomb - yellow
                { "StaffFireball", "#FF6600" },     // Embers - orange
            };

        private static readonly Dictionary<string, ConfigEntry<string>> Bound =
            new Dictionary<string, ConfigEntry<string>>();

        private static ConfigFile _config;

        internal static ConfigEntry<string> Idle;
        internal static ConfigEntry<string> Fallback;
        internal static ConfigEntry<float> GlowIntensity;
        internal static ConfigEntry<float> GlowRange;

        internal static void Bind(ConfigFile config)
        {
            _config = config;
            Bound.Clear();

            Idle = config.Bind(
                Section, "NoStaffSelected", "#FFC24A",
                "Crystal colour when no staff is selected - Gungnir's own idle glow.");

            Fallback = config.Bind(
                Section, "UnknownStaff", "#FFFFFF",
                "Crystal colour for a staff with no entry of its own, e.g. one added by "
                + "another mod. Its own entry appears here once the game has seen it.");

            GlowIntensity = config.Bind(
                Section, "GlowIntensity", 1.6f,
                new ConfigDescription(
                    "Brightness of the light the crystal casts. 0 turns the light off and "
                    + "leaves only the material tint.",
                    new AcceptableValueRange<float>(0f, 8f)));

            GlowRange = config.Bind(
                Section, "GlowRange", 3.5f,
                new ConfigDescription(
                    "How far the crystal's light reaches, in metres.",
                    new AcceptableValueRange<float>(0f, 20f)));
        }

        /// <summary>
        ///     The configured colour for a staff, binding a fresh entry the first time
        ///     that staff is seen so it shows up in F1 from then on.
        /// </summary>
        internal static Color For(ItemDrop.ItemData staff)
        {
            if (staff == null)
            {
                return Parse(Idle.Value, new Color(1f, 0.76f, 0.29f));
            }

            var key = PrefabNameOf(staff);
            if (string.IsNullOrEmpty(key))
            {
                return Parse(Fallback.Value, new Color(0.79f, 0.64f, 0.15f));
            }

            if (!Bound.TryGetValue(key, out var entry))
            {
                Defaults.TryGetValue(key, out var shipped);
                entry = _config.Bind(
                    Section, key, shipped ?? Fallback.Value,
                    $"Crystal colour while {key} is selected. Hex, e.g. #FF6A18.");
                Bound[key] = entry;
            }

            return Parse(entry.Value, Parse(Fallback.Value, Color.white));
        }

        private static string PrefabNameOf(ItemDrop.ItemData item)
        {
            if (item.m_dropPrefab != null)
            {
                return item.m_dropPrefab.name;
            }

            // m_shared.m_name is a $token, not a prefab name, but it is stable and
            // unique enough to key a colour on when the drop prefab is missing.
            return item.m_shared?.m_name;
        }

        private static Color Parse(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex)
                && ColorUtility.TryParseHtmlString(hex.Trim(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
