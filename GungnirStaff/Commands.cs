using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Development console commands. Gungnir is craftable at the Galdr Table, so
    ///     "give" is a testing shortcut rather than the way in. Open the console with F5
    ///     (needs <c>-console</c> in the r2modman launch parameters).
    /// </summary>
    internal sealed class GungnirCommand : ConsoleCommand
    {
        public override string Name => "gungnir";

        public override string Help =>
            "gungnir status | empty | recover | staffs | holster | find | give [level]";

        // NOT flagged as a cheat. It used to be, which meant devcommands had to be on
        // before any of it would run - including 'empty' and 'recover', the two a player
        // reaches for when their staffs are at risk. Spawning is the only part that needs
        // guarding, and Give() checks for a dev session on its own.
        public override bool IsCheat => false;

        public override List<string> CommandOptionList()
        {
            return new List<string>
            {
                "status", "empty", "recover", "staffs", "holster", "find", "give",
            };
        }

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                Print("You need to be in a world first.");
                return;
            }

            var sub = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (sub)
            {
                case "give":
                    Give(player, args.Length > 1 ? args[1] : null);
                    break;
                case "staffs":
                    ListStaffs();
                    break;
                case "holster":
                    Holster(player);
                    break;
                case "find":
                    Find(args.Length > 1 ? args[1] : string.Empty);
                    break;
                case "empty":
                    Empty(player);
                    break;
                case "recover":
                    Recover(player);
                    break;
                default:
                    Status(player);
                    break;
            }
        }

        /// <summary>
        ///     Spawning is allowed only in a genuine development session: the game must
        ///     have been launched with -console AND have devcommands enabled. IsCheat
        ///     already covers the second; checking the launch argument as well means an
        ///     ordinary playthrough cannot reach it even if cheats get toggled on.
        /// </summary>
        private static bool SpawningAllowed()
        {
            var launchedWithConsole = System.Environment.GetCommandLineArgs()
                .Any(a => string.Equals(a, "-console", System.StringComparison.OrdinalIgnoreCase));

            return launchedWithConsole && Terminal.m_cheat;
        }

        /// <summary>
        ///     Spawns a Gungnir, optionally at a given upgrade level.
        /// </summary>
        /// <param name="levelArg">
        ///     Requested level as typed, or null for the default. Rejected rather than
        ///     clamped when it is out of range: silently handing over a level 3 staff
        ///     because 9 was asked for would make a testing command lie about what it
        ///     produced.
        /// </param>
        private static void Give(Player player, string levelArg)
        {
            if (!SpawningAllowed())
            {
                Print("Spawning Gungnir needs a dev session: launch with -console and "
                      + "enable devcommands. Craft it at the Galdr Table instead.");
                return;
            }

            var level = 1;
            if (!string.IsNullOrEmpty(levelArg)
                && (!int.TryParse(levelArg, out level)
                    || level < 1
                    || level > GungnirRecipe.MaxQuality))
            {
                Print($"Usage: gungnir give [1-{GungnirRecipe.MaxQuality}]  "
                      + $"(got '{levelArg}')");
                return;
            }

            if (!GungnirItem.Create())
            {
                Print("Could not create the Gungnir prefab - see the BepInEx log.");
                return;
            }

            // Quality IS the upgrade level - the same field the Galdr Table increments -
            // so a spawned level 3 is indistinguishable from an upgraded one.
            var added = player.m_inventory.AddItem(
                GungnirItem.PrefabName, 1, level, 0, 0L, string.Empty, false);

            if (added == null)
            {
                Print("No room in your inventory.");
                return;
            }

            // Recorded explicitly rather than left to the fallback in OwnQuality. The
            // rack width is derived from Gungnir's own level, and that fallback only
            // reads m_quality while no staff is projected - true here, but the moment
            // one is selected the stored value is what counts.
            var container = StaffContainer.For(added);
            if (container != null)
            {
                container.OwnQuality = level;
            }

            Print($"Added a Gungnir to your inventory (level {level}, "
                  + $"{GungnirRecipe.SlotsForQuality(level)} rack slots).");
        }

        private static void ListStaffs()
        {
            var staffs = StaffRegistry.AllStaffPrefabs();
            Print($"Detected {staffs.Count} staff(s):");
            foreach (var s in staffs)
            {
                var shared = s.GetComponent<ItemDrop>().m_itemData.m_shared;
                Print($"  {s.name}  ({shared.m_skillType}, {shared.m_itemType}, "
                      + $"held as {shared.m_animationState})");
            }
        }

        /// <summary>Searches ObjectDB item prefab names - for confirming exact spellings.</summary>
        private static void Find(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Print("Usage: gungnir find <part of a prefab name>");
                return;
            }

            var hits = ObjectDB.instance.m_items
                .Where(p => p != null && p.name.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(p => p.name)
                .OrderBy(n => n)
                .ToList();

            Print($"{hits.Count} item prefab(s) matching '{text}':");
            foreach (var n in hits)
            {
                Print("  " + n);
            }
        }

        private static void Holster(Player player)
        {
            var gungnir = GungnirItem.Active(player);
            if (gungnir == null)
            {
                Print("No Gungnir active.");
                return;
            }

            StaffSwitcher.Holster(player, gungnir);
            Print("Holstered.");
        }

        /// <summary>
        ///     Unloads the rack into the player's own inventory.
        ///
        ///     The staffs live inside the Gungnir's custom data, which is what lets them
        ///     ride along through relogs, chests and drops - but it also means they share
        ///     the item's fate. Losing the Gungnir loses them, and uninstalling the mod
        ///     takes the whole item with it. This is the way to get them out first.
        /// </summary>
        private static void Empty(Player player)
        {
            var gungnir = GungnirItem.Active(player);
            if (gungnir == null)
            {
                Print($"No Gungnir active (Activation = {ModConfig.Activation.Value}).");
                return;
            }

            // Holster first, or the staff currently projected onto Gungnir would be moved
            // out from under the item that is impersonating it.
            StaffSwitcher.Holster(player, gungnir);

            var container = StaffContainer.For(gungnir);
            var moved = 0;
            var stuck = 0;

            for (var i = 0; i < container.Inventory.m_width; i++)
            {
                var staff = container.ItemAt(i);
                if (staff == null)
                {
                    continue;
                }

                if (!player.m_inventory.HaveEmptySlot())
                {
                    stuck++;
                    continue;
                }

                container.Inventory.RemoveItem(staff);
                if (player.m_inventory.AddItem(staff))
                {
                    moved++;
                }
                else
                {
                    // Put it back rather than drop it on the floor: the rack is the safer
                    // of the two places for it to sit.
                    container.Inventory.AddItem(staff);
                    stuck++;
                }
            }

            container.SaveToItem();
            Print($"Moved {moved} staff(s) out of the rack into your inventory.");
            if (stuck > 0)
            {
                Print($"{stuck} could not be moved - no room. Free some slots and run it again.");
            }
        }

        /// <summary>
        ///     Sweeps up Gungnirs that were dropped while the prefab was broken.
        ///
        ///     A drop built from an inactive prefab never woke up, so it has no ZNetView
        ///     and no ZDO: it is invisible, cannot be picked up, and will not be in the
        ///     save. It IS still a loaded GameObject though, holding the real item data
        ///     and the staffs inside it - so as long as the world has not been unloaded
        ///     since, it can be handed straight back.
        /// </summary>
        private static void Recover(Player player)
        {
            var found = 0;
            var restored = 0;

            foreach (var drop in Resources.FindObjectsOfTypeAll<ItemDrop>())
            {
                if (drop == null || drop.m_itemData == null)
                {
                    continue;
                }

                var go = drop.gameObject;

                // A working drop is active and needs no help. A prefab template is parked
                // under a deactivated container, so it has a parent; a real drop is
                // instantiated at the scene root. And an unloaded asset has no valid
                // scene, which is what separates ObjectDB's prefabs from world objects.
                if (go.activeInHierarchy
                    || go.transform.parent != null
                    || !go.scene.IsValid()
                    || ReferenceEquals(go, GungnirItem.Prefab)
                    || !GungnirItem.IsGungnir(drop.m_itemData))
                {
                    continue;
                }

                found++;

                if (!player.m_inventory.HaveEmptySlot())
                {
                    continue;
                }

                var item = drop.m_itemData;
                GungnirItem.RepairDropPrefab(item);
                if (player.m_inventory.AddItem(item))
                {
                    restored++;
                    Object.Destroy(go);
                }
            }

            if (found == 0)
            {
                Print("No stranded Gungnirs in this world. Anything dropped before a "
                      + "relog is gone for good - it was never written to the save.");
                return;
            }

            Print($"Found {found} stranded Gungnir(s); returned {restored} to your inventory.");
            if (restored < found)
            {
                Print("The rest need free inventory slots. Make room and run it again.");
            }
        }

        private static void Status(Player player)
        {
            Print(Alive.StatusLine(GungnirStaffPlugin.Instance?.Harmony));

            var gungnir = GungnirItem.Active(player);
            if (gungnir == null)
            {
                Print("No Gungnir active "
                      + $"(Activation = {ModConfig.Activation.Value}). Try 'gungnir give'.");
                return;
            }

            var container = StaffContainer.For(gungnir);

            // Gungnir's OWN level. m_quality is whatever staff is projected right now, so
            // reading it here would report the staff's level as the weapon's.
            var level = container.OwnQuality;
            Print($"Level {level}/{GungnirRecipe.MaxQuality} "
                  + $"-> {GungnirRecipe.SlotsForQuality(level)} rack slots");
            PrintDamage(gungnir);
            Print($"Selected slot: {(container.SelectedSlot < 0 ? "none" : (container.SelectedSlot + 1).ToString())}");
            Print($"Bar contents ({container.Inventory.m_width} slots):");

            for (var i = 0; i < container.Inventory.m_width; i++)
            {
                var item = container.ItemAt(i);
                Print($"  {i + 1}: {(item == null ? "-" : item.m_shared.m_name + " (lvl " + item.m_quality + ")")}");
            }
        }

        /// <summary>
        ///     What the weapon actually deals, read off the live item rather than off the
        ///     constants. This is the line to check when a swing feels like it did
        ///     nothing: base damage is what lands on EVERY hit, and the storm touch is
        ///     added on top of it only when it rolls.
        /// </summary>
        private static void PrintDamage(ItemDrop.ItemData gungnir)
        {
            var damage = gungnir.GetDamage();
            var parts = new List<string>();

            void Add(string label, float value)
            {
                if (value > 0f)
                {
                    parts.Add($"{label} {value:0.#}");
                }
            }

            Add("generic", damage.m_damage);
            Add("pierce", damage.m_pierce);
            Add("slash", damage.m_slash);
            Add("blunt", damage.m_blunt);
            Add("lightning", damage.m_lightning);
            Add("fire", damage.m_fire);
            Add("frost", damage.m_frost);
            Add("poison", damage.m_poison);
            Add("spirit", damage.m_spirit);

            Print($"Base damage every hit: {damage.GetTotalDamage():0.#} "
                  + $"({(parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "none")})");

            var secondary = gungnir.m_shared?.m_secondaryAttack;
            if (secondary != null)
            {
                Print($"  secondary x{secondary.m_damageMultiplier:0.##} "
                      + $"-> {damage.GetTotalDamage() * secondary.m_damageMultiplier:0.#}");
            }

            Print($"Storm touch: {ModConfig.LightningChance.Value:P0} chance to ADD "
                  + $"{ModConfig.LightningDamage.Value:0.#} lightning on top, with its "
                  + "effect and sound. A failed roll changes nothing about the hit.");

            // A weapon with no hit effect connects silently and shows no spark, which
            // reads in game as "the attack did nothing" even while damage lands.
            var shared = gungnir.m_shared;
            var onHit = Count(shared?.m_hitEffect) + Count(shared?.m_attack?.m_hitEffect);
            Print($"Hit feedback: {onHit} effect(s) on impact, "
                  + $"{Count(shared?.m_startEffect) + Count(shared?.m_attack?.m_startEffect)} on swing"
                  + (onHit == 0 ? "  <- SILENT: no impact sound or spark" : string.Empty));
        }

        private static int Count(EffectList list)
        {
            return list?.m_effectPrefabs?.Length ?? 0;
        }

        private static void Print(string text)
        {
            Alive.ToGameConsole(text);
            GungnirStaffPlugin.Log.LogInfo(text);
        }

        /// <summary>Registers the command. Safe to call again after a hot reload.</summary>
        internal static void Register()
        {
            if (CommandManager.Instance.CustomCommands.Any(c => c.Name == "gungnir"))
            {
                return;
            }

            CommandManager.Instance.AddConsoleCommand(new GungnirCommand());
        }
    }
}
