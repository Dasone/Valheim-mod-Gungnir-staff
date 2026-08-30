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

    /// <summary>What the blade effect looks like.</summary>
    internal enum BladeGlowStyle
    {
        /// <summary>Fast crackling streaks - Odin's storm.</summary>
        Lightning,

        /// <summary>The softer haze this replaced.</summary>
        SoftGlow,

        /// <summary>No particles at all.</summary>
        None,
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
    ///     BepInEx/config/dev.samspel.gungnirstaff.cfg and are editable in-game
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
        internal static ConfigEntry<bool> NormaliseStaffStance;
        internal static ConfigEntry<CrystalParticleMode> CrystalParticles;
        internal static ConfigEntry<float> CrystalParticleRate;
        internal static ConfigEntry<float> CrystalParticleScale;
        internal static ConfigEntry<float> CrystalParticleBrightness;

        internal static ConfigEntry<WeaponEffectMode> WeaponEffect;
        internal static ConfigEntry<string> WeaponEffectRotation;
        internal static ConfigEntry<float> WeaponEffectDistance;
        internal static ConfigEntry<BladeGlowStyle> BladeGlowStyle;
        internal static ConfigEntry<float> LightningChance;
        internal static ConfigEntry<float> LightningDamage;
        internal static ConfigEntry<string> LightningEffectPrefab;
        internal static ConfigEntry<string> BladeGlowPrefab;
        internal static ConfigEntry<string> BladeGlowColour;
        internal static ConfigEntry<float> BladeGlowRate;
        internal static ConfigEntry<float> BladeGlowScale;
        internal static ConfigEntry<float> BladeGlowOffset;
        internal static ConfigEntry<float> BladeGlowBrightness;

        internal static ConfigEntry<KeyboardShortcut> StatusKey;
        internal static ConfigEntry<KeyboardShortcut> HolsterKey;
        internal static ConfigEntry<KeyboardShortcut>[] SlotKeys;

        internal static ConfigEntry<float> BarOffsetX;
        internal static ConfigEntry<float> BarOffsetY;
        internal static ConfigEntry<float> BarScale;
        internal static ConfigEntry<int> MoveBarMouseButton;
        internal static ConfigEntry<bool> ShowSlotKeys;
        internal static ConfigEntry<float> SlotKeyScale;

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
                    "0 follows the staff's own upgrade level - four slots to start and two "
                    + "more per upgrade, so 4/6/8. "
                    + "Set 1-8 to override with a fixed number regardless of level. Shrinking it "
                    + "while staffs sit in the removed slots will strand them, so empty the bar "
                    + "first.",
                    new AcceptableValueRange<int>(0, MaxSlots)));

            Activation = config.Bind(
                "Staff bar", "Activation", ActivationMode.Carried,
                "Carried (default): the staff bar and slot keys work whenever a Gungnir is "
                + "anywhere in your inventory. Equipped: only while it is actually in hand.");

            // On by default. The clone path was the safe option while the standalone
            // item was unproven, but the standalone one is now what the mod is built
            // and tested around - it authors every stat itself and carries no vanilla
            // weapon's behaviour along with it. The clone remains as a fallback, both
            // here and automatically if a standalone build ever fails.
            StandalonePrefab = config.Bind(
                "Appearance", "StandalonePrefab", true,
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

            // Both are -90 for the standalone prefab, where the clone needed +90: our
            // attach is built at identity while vanilla's carries its own rotation, so the
            // model lands 180 out. Still two INDEPENDENT values, not a base plus a flip.
            //
            // Spears are OneHandedWeapon and staffs are TwoHandedWeaponLeft, so selecting
            // a staff moves Gungnir from the right hand to the left - and the two bones
            // have different orientations. A shared base with a 180 flip therefore cannot
            // satisfy both stances at once: correcting one always broke the other. Each
            // hand gets its own absolute angle instead, so tuning one cannot disturb the
            // other.
            RotationSpear = config.Bind(
                "Appearance", "RotationSpear", "-90,0,0",
                "Model rotation with NO staff selected - right hand, blade leading. "
                + "Degrees (x,y,z). Independent of RotationStaff.");

            RotationStaff = config.Bind(
                "Appearance", "RotationStaff", "-90,0,0",
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

            NormaliseStaffStance = config.Bind(
                "Appearance", "NormaliseStaffStance", true,
                "Hold Gungnir the same way whichever staff is selected. Vanilla gives some "
                + "magic items their own posture - the Dead Raiser is a skull, and is carried "
                + "like one - which looks wrong on a spear. Off uses each staff's own stance.");

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

            // Only used by the standalone item: the cloned one inherits the donor spear's
            // own effect, and the Rotation/Distance dials above place that. A standalone
            // Gungnir has no donor, so the glow is built by the mod and pinned to the
            // blade mesh - no placement dials needed.
            // OFF by default. The blade glow can still come out at the wrong size after
            // some stance changes - every measurable property of the particle systems is
            // identical between a good and a bad one, so the cause is not yet found and
            // shipping it on by default would mean shipping a visible bug. The crystal
            // effects are unaffected and stay on. Set Lightning or SoftGlow to opt back in.
            BladeGlowStyle = config.Bind(
                "Weapon effect", "BladeGlowStyle", GungnirStaff.BladeGlowStyle.None,
                "Lightning: fast crackling streaks along the blade. SoftGlow: the calmer "
                + "haze this replaced. None (default): no particles. Off for now because "
                + "the glow can render at the wrong size after some stance changes.");

            LightningChance = config.Bind(
                "Weapon effect", "LightningChance", 0.25f,
                new ConfigDescription(
                    "Chance per hit of triggering the game's own lightning strike, with its "
                    + "vanilla visual and sound.",
                    new AcceptableValueRange<float>(0f, 1f)));

            LightningDamage = config.Bind(
                "Weapon effect", "LightningDamage", 40f,
                new ConfigDescription(
                    "Lightning damage added when a strike triggers. At the default 25 percent "
                    + "chance this averages 10 per hit, matching the vanilla lightning spear which "
                    + "adds a flat 10 every time - the same damage, delivered as an occasional jolt.",
                    new AcceptableValueRange<float>(0f, 300f)));

            LightningEffectPrefab = config.Bind(
                "Weapon effect", "LightningEffectPrefab", "",
                "Vanilla effect spawned where a strike lands, carrying the game own visual and "
                + "sound. Blank picks the best available automatically.");

            BladeGlowPrefab = config.Bind(
                "Weapon effect", "BladeGlowPrefab", "",
                "Optional: name of a vanilla effect prefab to mount on the spear head. "
                + "Blank (the default) uses the mod's own particles. Be careful - most of "
                + "these are authored as one-shot, world-scale spawns, not weapon "
                + "decorations: fx_lightningstaff_charge fills the screen with a two-second "
                + "burst and then stops.");

            BladeGlowColour = config.Bind(
                "Weapon effect", "BladeGlowColour", "#7FC8FF",
                "Colour of the glow on the spear head (standalone prefab only). Hex.");

            BladeGlowRate = config.Bind(
                "Weapon effect", "BladeGlowRate", 1f,
                new ConfigDescription(
                    "How much the blade glows, as a multiplier. 0 stops emission.",
                    new AcceptableValueRange<float>(0f, 4f)));

            BladeGlowScale = config.Bind(
                "Weapon effect", "BladeGlowScale", 1f,
                new ConfigDescription(
                    "Size of the glow around the blade.",
                    new AcceptableValueRange<float>(0.25f, 4f)));

            BladeGlowOffset = config.Bind(
                "Weapon effect", "BladeGlowOffset", -0.04f,
                new ConfigDescription(
                    "Where the blade effect sits along the shaft, in metres from the middle "
                    + "of the blade. Negative moves it down toward the socket, positive up "
                    + "toward the point.",
                    new AcceptableValueRange<float>(-1f, 1f)));

            BladeGlowBrightness = config.Bind(
                "Weapon effect", "BladeGlowBrightness", 2.2f,
                new ConfigDescription(
                    "Pushes the glow past white so it reads as emissive. 1 is flat colour.",
                    new AcceptableValueRange<float>(1f, 8f)));

            // Unbound by default. This is a diagnostic, and a released mod has no
            // business claiming a key on everyone's keyboard for one - F9 in particular
            // is a key other mods and the player may well want. Bind one here to get it
            // back; an empty shortcut costs a single key comparison per frame.
            // Deliberately NOT wired to OnLayoutChanged: the position is read fresh
            // every frame, so a drag needs no rebuild.
            MoveBarMouseButton = config.Bind(
                "Staff bar", "MoveBarMouseButton", 2,
                new ConfigDescription(
                    "Mouse button that drags the staff bar to a new place while the inventory "
                    + "is open. 2 is the middle button; 0 is left and 1 is right, though both of "
                    + "those are already used by the slots. -1 turns dragging off and leaves "
                    + "PositionX/PositionY as the only way to move it.",
                    new AcceptableValueRange<int>(-1, 6)));

            ShowSlotKeys = config.Bind(
                "Staff bar", "ShowSlotKeys", true,
                "Print each slot's shortcut under it, the way the vanilla hotbar numbers its "
                + "own slots. The text is read from the live binding, so rebinding a slot "
                + "relabels it.");

            SlotKeyScale = config.Bind(
                "Staff bar", "SlotKeyScale", 1f,
                new ConfigDescription(
                    "Size of the shortcut labels, as a multiplier. The base size follows the "
                    + "slot size, so the labels already scale with the bar - this is for "
                    + "nudging them relative to it.",
                    new AcceptableValueRange<float>(0.25f, 3f)));

            StatusKey = config.Bind(
                "Keys", "StatusKey", KeyboardShortcut.Empty,
                "Optional diagnostic key, unbound by default. When bound, prints the running "
                + "version and build timestamp to the BepInEx console, the in-game console "
                + "and the screen. 'gungnir' in the F5 console shows the same thing.");

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

            // Rebuilding the bar is how a label change is applied: the labels are built
            // onto the slot widgets, so the cheapest correct refresh is the same teardown
            // the other layout settings already use.
            ShowSlotKeys.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            SlotKeyScale.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            foreach (var key in SlotKeys)
            {
                key.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            }

            Visibility.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            SlotCount.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            BarOffsetX.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            BarOffsetY.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
            BarScale.SettingChanged += (s, e) => OnLayoutChanged?.Invoke();
        }

        /// <summary>Parses "#RRGGBB"; falls back rather than throwing on a typo.</summary>
        internal static Color ParseColour(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex)
                   && ColorUtility.TryParseHtmlString(hex.Trim(), out var parsed)
                ? parsed
                : fallback;
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
