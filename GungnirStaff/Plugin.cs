using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace GungnirStaff
{
    /// <summary>
    ///     BepInEx entry point. Everything the mod does is wired up from here.
    /// </summary>
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // BOTH executables. valheim.exe alone was a multiplayer bug, not a filter: the
    // release build declares EveryoneMustHaveMod, which means the SERVER has to have it
    // too - and a dedicated server runs valheim_server.exe, where this attribute stopped
    // the plugin from loading at all. Jotunn would then find the mod missing on the
    // server's side of the handshake and refuse the connection, so every player who
    // installed it would be locked out of dedicated servers.
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
#if DEBUG
    // Debug builds stay off the network handshake entirely, so you can join your
    // own server (and anyone else's) while the mod is half-finished.
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
#else
    // Release builds - what you actually ship - require every client to have the
    // mod at a matching minor version, which is correct once custom items exist.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
#endif
    internal sealed class GungnirStaffPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "dev.samspel.gungnirstaff";
        public const string ModName = "Gungnir Staff";
        public const string ModVersion = "1.0.1";

        internal static GungnirStaffPlugin Instance;

        /// <summary>Shared log source, so patches and managers can log without a plugin reference.</summary>
        internal static ManualLogSource Log;

        private Harmony _harmony;

        /// <summary>Tracks equip changes so a selection can be re-applied on pick-up.</summary>
        private ItemDrop.ItemData _lastActive;

        /// <summary>Exposed so patches can report the live patch count.</summary>
        internal Harmony Harmony => _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModConfig.Bind(Config);
            CrystalColors.Bind(Config);
            ModConfig.OnLayoutChanged += GungnirBar.OnLayoutSettingChanged;

            // Patch everything in this assembly annotated with [HarmonyPatch].
            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // ScriptEngine's LoadOnStart ignores [BepInDependency], so on a hot-reload
            // launch there is no hard guarantee Jotunn has Awoken first. It does in this
            // profile, but degrade loudly rather than take the whole plugin down if the
            // load order ever shifts.
            try
            {
                Localizations.Register();
                GungnirCommand.Register();
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Jotunn was not ready during Awake; localisation and the "
                             + $"'gungnir' console command are unavailable this session: {ex.Message}");
            }

            // On a hot reload the world is already up, so the prefab and any carried
            // Gungnir can be brought back immediately instead of waiting for a spawn.
            // Decompress the bundle up front: doing it lazily froze the game for seconds
            // the first time a Gungnir appeared.
            LightningStrike.Reset();

            // A dedicated server has no renderer and never draws the model, so there is
            // nothing for the bundle to be decompressed into. It still needs the plugin
            // loaded - that is what satisfies EveryoneMustHaveMod - just not the art.
            if (!Jotunn.Managers.GUIManager.IsHeadless())
            {
                GungnirVisual.Preload();
            }

            if (Alive.InGame)
            {
                StaffRegistry.Invalidate();
                GungnirItem.Create();
                GungnirItem.RepairInventory(Player.m_localPlayer);
            }

            // One quiet line in the BepInEx log, and nothing on the player's screen.
            // The full announce - console and HUD as well - is reserved for when the
            // status key or the 'gungnir' command is used deliberately.
            Log.LogInfo(Alive.StatusLine(_harmony));
        }

        private void Update()
        {
            if (Inputs.StatusPressed())
            {
                Alive.Announce("alive", _harmony);
            }

            if (!ModConfig.Enabled.Value)
            {
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null)
            {
                _lastActive = null;
                return;
            }

            var gungnir = GungnirItem.Active(player);

            // Driven from here rather than from an InventoryGui postfix so the rack keeps
            // updating while the inventory is closed.
            try
            {
                GungnirBar.Refresh(player);
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Staff bar update failed: {ex}");
            }

            // Picking up / equipping a Gungnir re-applies whatever it had selected.
            if (!ReferenceEquals(gungnir, _lastActive))
            {
                _lastActive = gungnir;
                if (gungnir != null)
                {
                    StaffSwitcher.Reapply(player, gungnir);
                }
            }

            // Orientation runs BEFORE the active check and falls back to the carried
            // item. Sheathing with R clears m_rightItem, so "active" goes false while the
            // model is still on the player's back - gating this on it meant the sheathed
            // copy was never oriented at all.
            try
            {
                GungnirVisual.Orient(player, gungnir ?? GungnirItem.CarriedBy(player));
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Model orientation failed: {ex}");
            }

            if (gungnir == null)
            {
                CrystalGlow.Clear();
                return;
            }

            CrystalGlow.Apply(player, gungnir);

            // Cheap float copy; keeps the stored staff's wear current so nothing is lost
            // if the player logs out without switching away first.
            StaffSwitcher.SyncActiveDurability(gungnir);

            if (Inputs.HolsterPressed())
            {
                StaffSwitcher.Holster(player, gungnir);
                return;
            }

            var slot = Inputs.PressedSlot();
            if (slot >= 0)
            {
                StaffSwitcher.Select(player, gungnir, slot);
            }
        }

        /// <summary>
        ///     Required for ScriptEngine hot-reloading: without unpatching, every reload
        ///     would stack another copy of every patch on top of the old ones, and the
        ///     cloned UI would pile up one bar per reload.
        /// </summary>
        private void OnDestroy()
        {
            ModConfig.OnLayoutChanged -= GungnirBar.OnLayoutSettingChanged;
            GungnirBar.Teardown();
            CrystalGlow.Clear();
            BladeGlow.Clear();
            _harmony?.UnpatchSelf();
            _harmony = null;
            ModConfig.Trace($"{ModName} unloaded.");
        }
    }
}
