using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;

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
            "gungnir give | staffs | status | holster - Gungnir Staff development commands";

        // Flagged as a cheat: vanilla then refuses the whole command unless devcommands
        // is on, so a normal player cannot conjure a Gungnir from the console.
        public override bool IsCheat => true;

        public override List<string> CommandOptionList()
        {
            return new List<string> { "give", "staffs", "status", "holster", "find" };
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
                    Give(player);
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

        private static void Give(Player player)
        {
            if (!SpawningAllowed())
            {
                Print("Spawning Gungnir needs a dev session: launch with -console and "
                      + "enable devcommands. Craft it at the Galdr Table instead.");
                return;
            }

            if (!GungnirItem.Create())
            {
                Print("Could not create the Gungnir prefab - see the BepInEx log.");
                return;
            }

            var added = player.m_inventory.AddItem(
                GungnirItem.PrefabName, 1, 1, 0, 0L, string.Empty, false);

            Print(added != null
                ? "Added a Gungnir to your inventory (level 1)."
                : "No room in your inventory.");
        }

        private static void ListStaffs()
        {
            var staffs = StaffRegistry.AllStaffPrefabs();
            Print($"Detected {staffs.Count} staff(s):");
            foreach (var s in staffs)
            {
                var shared = s.GetComponent<ItemDrop>().m_itemData.m_shared;
                Print($"  {s.name}  ({shared.m_skillType})");
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
            Print($"Level {gungnir.m_quality}/{GungnirRecipe.MaxQuality} "
                  + $"-> {GungnirRecipe.SlotsForQuality(gungnir.m_quality)} rack slots");
            Print($"Selected slot: {(container.SelectedSlot < 0 ? "none" : (container.SelectedSlot + 1).ToString())}");
            Print($"Bar contents ({container.Inventory.m_width} slots):");

            for (var i = 0; i < container.Inventory.m_width; i++)
            {
                var item = container.ItemAt(i);
                Print($"  {i + 1}: {(item == null ? "-" : item.m_shared.m_name + " (lvl " + item.m_quality + ")")}");
            }
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
