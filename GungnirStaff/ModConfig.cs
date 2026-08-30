using BepInEx.Configuration;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>How the mod decides the Gungnir staff's features are available.</summary>
    internal enum ActivationMode
    {
        /// <summary>Only while Gungnir is actually in the player's hand.</summary>
        Equipped,

        /// <summary>Any time a Gungnir is anywhere in the player's inventory.</summary>
        Carried,
    }

    /// <summary>When the blade's glow effect is shown.</summary>
    internal enum WeaponEffectMode
    {
        /// <summary>Only while Gungnir is a spear - off the moment a staff is selected.</summary>
        OnlyInMeleeStance,

        /// <summary>Shown in both stances.</summary>
        Always,

        /// <summary>Never shown.</summary>
        Never,
    }

    /// <summary>When the crystal's particles are shown.</summary>
    internal enum CrystalParticleMode
    {
        /// <summary>Only while a staff is selected - off when swinging it as a spear.</summary>
        OnlyInCastingStance,

        /// <summary>Shown in both stances.</summary>
        Always,

        /// <summary>Never shown.</summary>
        Never,
    }

    /// <summary>When the staff rack is drawn on screen.</summary>
    internal enum BarVisibility
    {
        /// <summary>On screen the whole time Gungnir is active, like a second hotbar.</summary>
        WhenGungnirActive,

        /// <summary>Only while the inventory screen is open.</summary>
        InventoryOnly,

        /// <summary>On screen permanently, even with no Gungnir at all.</summary>
        Always,
    }

    /// <summary>
    ///     Every user-facing setting. Values land in
    ///     BepInEx/config/com.samuelhaggren.gungnirstaff.cfg and are editable in-game
    ///     via Configuration Manager (F1) - which is what makes the bar position,
    ///     scale and every keybind customisable at runtime.
    /// </summary>
    internal static class ModConfig
    {
        internal const int MaxSlots = 8;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> VerboseLogging;

        internal static ConfigEntry<int> SlotCount;
        internal static ConfigEntry<ActivationMode> Activation;
        internal static ConfigEntry<BarVisibility> Visibility;
        internal static ConfigEntry<string> BaseWeapon;
        internal static ConfigEntry<bool> StandalonePrefab;
        internal static ConfigEntry<string> RotationSpear;
        internal static ConfigEntry<string> RotationStaff;
        internal static ConfigEntry<string> RotationBack;
        internal static ConfigEntry<bool> AlignBackToVanilla;
        internal static ConfigEntry<float> BackSlide;
        internal static ConfigEntry<string> ModelOffset;
        internal static ConfigEntry<float> ModelScale;
        internal static ConfigEntry<float> GripHeight;
        internal static ConfigEntry<CrystalParticleMode> CrystalParticles;
        internal static ConfigEntry<float> CrystalParticleRate;
        internal static ConfigEntry<float> CrystalParticleScale;
        internal static ConfigEntry<float> CrystalParticleBrightness;

        internal static ConfigEntry<WeaponEffectMode> WeaponEffect;
        internal static ConfigEntry<string> WeaponEffectRotation;
        internal static ConfigEntry<float> WeaponEffectDistance;

        internal static ConfigEntry<KeyboardShortcut> StatusKey;
        internal static ConfigEntry<KeyboardShortcut> HolsterKey;
        internal static ConfigEntry<KeyboardShortcut>[] SlotKeys;

        internal static ConfigEntry<float> BarOffsetX;
        internal static ConfigEntry<float> BarOffsetY;
        internal static ConfigEntry<float> BarScale;

        /// <summary>Raised when a setting changes that needs the staff bar rebuilt.</summary>
        internal static event System.Action OnLayoutChanged;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "General", "Enabled", true,
                "Master switch. Turn off to disable the mod's behaviour without uninstalling it. "
                + "The status key keeps working either way.");

            VerboseLogging = config.Bind(
                "General", "VerboseLogging", false,
                "Log extra detail to the BepInEx console. Useful while developing.");

            SlotCount = config.Bind(
                "Staff bar", "SlotCount", 0,
                new ConfigDescription(
                    "0 follows the staff's own upgrade level - two slots per level, so 2/4/6/8. "
                    + "Set 1-8 to override with a fixed number regardless of level. Shrinking it "
                    + "while staffs sit in the removed slots will strand them, so empty the bar "
                    + "first.",
                    new AcceptableValueRange<int>(0, MaxSlots)));

            Activation = config.Bind(
                "Staff bar", "Activation", ActivationMode.Equipped,
                "Equipped: the staff bar and slot keys only work while holding Gungnir. "
                + "Carried: they work whenever a Gungnir is anywhere in your inventory.");

            // Off by default while the standalone item is still being proven. The clone
            // path is what everything has been tuned against, so flipping this is an
            // opt-in experiment rather than a silent swap of the whole weapon.
            StandalonePrefab = config.Bind(
                "Appearance", "StandalonePrefab", false,
                "Build Gungnir as its own item with no vanilla weapon behind it, instead of "
                + "cloning a spear. Every stat is then authored by the mod. TAKES EFFECT ON "
                + "THE NEXT GAME START - the prefab is registered once at load.");

            BaseWeapon = config.Bind(
                "Appearance", "BaseWeaponPrefab", "SpearSplitner_Lightning",
                "Which vanilla weapon Gungnir is built from - its model, animations and "
                + "handling. Only the look and feel come from here; Gungnir's own damage and "
                + "flavour text are applied on top. Vanilla candidates worth trying: "
                + "SpearSplitner_Lightning, SpearSplitner, SpearSplitner_Blood, "
                + "SpearSplitner_Nature, SpearCarapace, SpearElderbark, SpearWolfFang. "
                + "TAKES EFFECT ON THE NEXT GAME START - the held model comes from the "
                + "prefab, which is registered once at load.");

            // Two INDEPENDENT rotations, not a base plus a flip.
            //
            // Spears are OneHandedWeapon and staffs are TwoHandedWeaponLeft, so selecting
            // a staff moves Gungnir from the right hand to the left - and the two bones
            // have different orientations. A shared base with a 180 flip therefore cannot
            // satisfy both stances at once: correcting one always broke the other. Each
            // hand gets its own absolute angle instead, so tuning one cannot disturb the
            // other.
            RotationSpear = config.Bind(
                "Appearance", "RotationSpear", "90,0,0",
                "Model rotation with NO staff selected - right hand, blade leading. "
                + "Degrees (x,y,z). Independent of RotationStaff.");

            RotationStaff = config.Bind(
                "Appearance", "RotationStaff", "90,0,0",
                "Model rotation WITH a staff selected - left hand, crystal leading. "
                + "Degrees (x,y,z). Independent of RotationSpear.");

            // Where along the shaft the hand closes, measured from the crystal end. The
            // model runs 0 (crystal) to 2.30 (blade tip). The prefab bakes in 0.95;
            // 1.18 is that mirrored by the same amount, which is what read correctly in
            // game - the hand frame reverses the direction from what the numbers suggest.
            GripHeight = config.Bind(
                "Appearance", "GripHeight", 1.18f,
                new ConfigDescription(
                    "Where the hand holds the shaft, in metres from the crystal end. The "
                    + "shaft runs 0.20-1.74 and the blade tip is at 2.30. Lower values grip "
                    + "further down, raising the head; the blade effect follows automatically.",
                    new AcceptableValueRange<float>(0.1f, 2f)));

            // A third bone, a third frame. Sheathing puts the weapon on the back, which
            // is neither hand, so it needs its own angle for exactly the same reason the
            // two stances do - and it is NOT the hand value: the hands need 90 about X,
            // the back needs none, or the spear lies horizontally past the player's face
            // instead of running up the back.
            RotationBack = config.Bind(
                "Appearance", "RotationBack", "0,0,0",
                "Rotation applied on top of the back alignment, in degrees (x,y,z). With "
                + "AlignBackToVanilla on, 0,0,0 lays it along the vanilla spear's axis and "
                + "180,0,0 swaps which end points up. The blade glow is turned by the same "
                + "amount, about the same pivot, so it stays on the blade.");

            AlignBackToVanilla = config.Bind(
                "Appearance", "AlignBackToVanilla", true,
                "Lay the sheathed model along the same axis as the vanilla spear it was "
                + "cloned from, whose hidden mesh is still inside the same attach and is "
                + "already angled correctly for the back. Measured from the game rather "
                + "than guessed. Turn off to use RotationBack alone.");

            BackSlide = config.Bind(
                "Appearance", "BackSlide", -0.35f,
                new ConfigDescription(
                    "Slides the sheathed model along its own shaft, in metres. Negative "
                    + "carries it lower down the back; positive rides it higher. The blade "
                    + "glow slides with it.",
                    new AcceptableValueRange<float>(-2f, 2f)));

            ModelOffset = config.Bind(
                "Appearance", "ModelOffset", "0,0,0",
                "Positional nudge of the model in the hand, in metres. Format: x,y,z.");

            ModelScale = config.Bind(
                "Appearance", "ModelScale", 1f,
                new ConfigDescription(
                    "Size of the Gungnir model in hand.",
                    new AcceptableValueRange<float>(0.25f, 3f)));

            // Key renamed from Enabled: the old value was a bool and would not parse
            // into the new three-way mode.
            CrystalParticles = config.Bind(
                "Crystal particles", "Mode", CrystalParticleMode.OnlyInCastingStance,
                "When to show the motes orbiting the crystal and the sparks shooting out of "
                + "it, tinted to match the selected staff. OnlyInCastingStance: just while a "
                + "staff is selected. Always: both stances. Never: off.");

            CrystalParticleRate = config.Bind(
                "Crystal particles", "Rate", 1f,
                new ConfigDescription(
                    "How many particles are emitted, as a multiplier. Lower it if the effect "
                    + "is too busy, or to save a little performance.",
                    new AcceptableValueRange<float>(0f, 4f)));

            CrystalParticleScale = config.Bind(
                "Crystal particles", "Scale", 1f,
                new ConfigDescription(
                    "Size of the motes and how far from the crystal they orbit.",
                    new AcceptableValueRange<float>(0.25f, 4f)));

            CrystalParticleBrightness = config.Bind(
                "Crystal particles", "Brightness", 2.2f,
                new ConfigDescription(
                    "Pushes the particle colour past white so the motes read as glowing. "
                    + "1 is the flat crystal colour.",
                    new AcceptableValueRange<float>(1f, 8f)));

            // Defaults found by eye in game. Automatic placement was tried twice and
            // failed both times - rotating the effect swung it around the grip instead of
            // flipping it, and pinning it to the blade fought the stance swap - so these
            // are plain dials with values that were measured rather than derived.
            WeaponEffect = config.Bind(
                "Weapon effect", "Mode", WeaponEffectMode.OnlyInMeleeStance,
                "When to show the glow on the blade. OnlyInMeleeStance: just while Gungnir "
                + "is a spear, off once a staff is selected. Always: both stances. "
                + "Never: off.");

            WeaponEffectRotation = config.Bind(
                "Weapon effect", "Rotation", "180,0,0",
                "Rotation of the glow about its own centre, in degrees (x,y,z).");

            WeaponEffectDistance = config.Bind(
                "Weapon effect", "Distance", -1.75f,
                new ConfigDescription(
                    "Moves the glow along its axis from the weapon's origin point, in metres. "
                    + "Negative pulls it back past the grip towards the blade.",
                    new AcceptableValueRange<float>(-3f, 3f)));

            Visibility = config.Bind(
                "Staff bar", "Visibility", BarVisibility.WhenGungnirActive,
                "WhenGungnirActive: on screen the whole time you have Gungnir, like a second "
                + "hotbar. InventoryOnly: only while the inventory is open. "
                + "Always: on screen permanently, even with no Gungnir.");

            // Renamed from OffsetX/OffsetY when the bar moved onto the HUD: the numbers
            // now mean something different, so a fresh key avoids inheriting old values.
            BarOffsetX = config.Bind(
                "Staff bar", "PositionX", 0f,
                new ConfigDescription(
                    "Horizontal position of the staff bar in pixels, from the bottom-centre "
                    + "of the screen.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));

            BarOffsetY = config.Bind(
                "Staff bar", "PositionY", 190f,
                new ConfigDescription(
                    "Vertical position of the staff bar in pixels, up from the bottom of the "
                    + "screen. The vanilla hotbar sits at roughly 60-120.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));

            BarScale = config.Bind(
                "Staff bar", "Scale", 1f,
                new ConfigDescription(
                    "Size multiplier for the staff bar.",
                    new AcceptableValueRange<float>(0.25f, 3f)));

            StatusKey = config.Bind(
                "Keys", "StatusKey", new KeyboardShortcut(KeyCode.F9),
                "Prints a 'still alive' line with the running version and build timestamp to the "
                + "BepInEx console, the in-game console and the screen.");

            // Alt, not Ctrl: Ctrl is Valheim's crouch, so Ctrl+3 also made the character
            // sneak. Alt is free - vanilla's inventory only reads Shift (split) and Ctrl
            // (quick-move) as click modifiers. Keys renamed from Slot#Key/HolsterKey so
            // existing configs pick up the new default instead of keeping Ctrl.
            HolsterKey = config.Bind(
                "Keys", "HolsterStaff", new KeyboardShortcut(KeyCode.Alpha0, KeyCode.LeftAlt),
                "Puts the active staff away and turns Gungnir back into its plain self.");

            SlotKeys = new ConfigEntry<KeyboardShortcut>[MaxSlots];
            for (var i = 0; i < MaxSlots; i++)
            {
                var digit = KeyCode.Alpha1 + i;
                SlotKeys[i] = config.Bind(
                    "Keys", "SelectSlot" + (i + 1),
                    new KeyboardShortcut(digit, KeyCode.LeftAlt),
                    "Selects staff slot " + (i + 1) + ". While a slot shortcut is triggered the "
                    + "vanilla hotbar is suppressed, so the number key does not do both things.");
            }

            Visibility.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            SlotCount.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            BarOffsetX.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            BarOffsetY.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            BarScale.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
        }

        /// <summary>Logs only when <see cref="VerboseLogging"/> is on.</summary>
        internal static void Trace(string message)
        {
            if (VerboseLogging != null && VerboseLogging.Value)
            {
                GungnirStaffPlugin.Log.LogInfo(message);
            }
        }
    }
}
