using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Gungnir's crafting recipe and its two upgrades, made at the Galdr Table.
    ///
    ///     A new Gungnir starts with four rack slots and each upgrade adds two, so the
    ///     item's quality is what governs how many staffs it can carry.
    /// </summary>
    internal static class GungnirRecipe
    {
        internal const string Station = "piece_magetable";
        // Three levels, not four, because the rack is what the levels buy and the rack
        // is full at level 3: slots run 4/6/8 against a ModConfig.MaxSlots ceiling of 8,
        // which is itself pinned by the Alt+1..Alt+8 shortcuts. A fourth level would cost
        // real materials and grant nothing, so it does not exist.
        internal const int MaxQuality = 3;

        /// <summary>Needs an upgraded Galdr Table, unlike the basic staffs.</summary>
        internal const int MinStationLevel = 2;

        /// <summary>Rack slots a freshly crafted, un-upgraded Gungnir has.</summary>
        internal const int BaseSlots = 4;

        /// <summary>Extra rack slots each upgrade buys: 4, 6, 8.</summary>
        internal const int SlotsPerLevel = 2;

        /// <summary>
        ///     Cost per level, indexed by quality - 1: craft, then the two upgrades.
        ///     Kept as literal tables because that is how they were specified, and because
        ///     vanilla cannot express all of them: <c>Requirement.GetAmount</c> returns
        ///     <c>m_amount</c> at level 1 and <c>(level - 1) * m_amountPerLevel</c> above
        ///     it. Wood, eitr and the trophy happen to fit that curve; thunder stones
        ///     (3, 2, 4) do not. The patch below serves these exact numbers instead of
        ///     approximating them.
        /// </summary>
        private static readonly Dictionary<string, int[]> Costs = new Dictionary<string, int[]>
        {
            // Mistlands staples, in quantities well above a normal staff.
            { "YggdrasilWood", new[] { 30, 15, 25 } },
            { "Eitr", new[] { 30, 20, 30 } },

            // Thunder stones carry the lightning: they are what the vanilla lightning
            // staff is built from, so they read as the Odin part of the recipe.
            { "Thunderstone", new[] { 3, 2, 4 } },

            // Black cores gate it behind actually delving the Infested Mines rather than
            // gathering on the surface, which is what makes it harder than the staffs.
            { "BlackCore", new[] { 2, 1, 2 } },

            // A Gjall trophy, not the Queen's: a mid-Mistlands gate rather than a
            // "finished the biome" one.
            { "TrophyGjall", new[] { 1, 0, 0 } },
        };

        /// <summary>
        ///     Spelling variants to try per material. Valheim is inconsistent about
        ///     casing - the asset manifest carries both "ThunderStone" and "Thunderstone"
        ///     and only one of them is the item - so the name is resolved against ObjectDB
        ///     rather than assumed.
        /// </summary>
        private static readonly Dictionary<string, string[]> Aliases =
            new Dictionary<string, string[]>
            {
                { "Thunderstone", new[] { "Thunderstone", "ThunderStone", "thunderstone" } },
                { "BlackCore", new[] { "BlackCore", "Blackcore" } },
                { "Eitr", new[] { "Eitr", "RefinedEitr" } },
                { "YggdrasilWood", new[] { "YggdrasilWood", "Yggdrasilwood" } },
                { "TrophyGjall", new[] { "TrophyGjall" } },
            };

        /// <summary>The real prefab name for a material, or null if none of them exist.</summary>
        private static string Resolve(ObjectDB db, string wanted)
        {
            string[] candidates;
            if (!Aliases.TryGetValue(wanted, out candidates))
            {
                candidates = new[] { wanted };
            }

            return candidates.FirstOrDefault(n => db.GetItemPrefab(n) != null);
        }

        /// <summary>
        ///     The live Requirement objects, mapped to their exact per-level costs. Keyed
        ///     by reference, so only this recipe's requirements are affected and every
        ///     other recipe in the game keeps vanilla behaviour.
        /// </summary>
        internal static readonly Dictionary<Piece.Requirement, int[]> ExactCosts =
            new Dictionary<Piece.Requirement, int[]>();

        private static bool _added;

        /// <summary>Registers the recipe. Safe to call repeatedly.</summary>
        internal static void Register()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                return;
            }

            var resolved = Costs.Keys.ToDictionary(k => k, k => Resolve(db, k));
            var missing = resolved.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
            if (missing.Count > 0)
            {
                GungnirStaffPlugin.Log.LogError(
                    "Cannot build the Gungnir recipe, no prefab found for: "
                    + string.Join(", ", missing));
                return;
            }

            if (db.GetItemPrefab(GungnirItem.PrefabName) == null)
            {
                return;
            }

            if (!_added)
            {
                try
                {
                    var config = new RecipeConfig
                    {
                        Name = "Recipe_GungnirStaff",
                        Item = GungnirItem.PrefabName,
                        Amount = 1,
                        CraftingStation = Station,
                        RepairStation = Station,
                        MinStationLevel = MinStationLevel,
                        Requirements = Costs.Select(kv => new RequirementConfig
                        {
                            Item = resolved[kv.Key],
                            // Base and per-level are set so vanilla's own curve is as close
                            // as it can get; the patch corrects what it cannot express.
                            Amount = kv.Value[0],
                            AmountPerLevel = kv.Value.Length > 1 ? kv.Value[1] : 0,
                            Recover = true,
                        }).ToArray(),
                    };

                    ItemManager.Instance.AddRecipe(new CustomRecipe(config));
                    _added = true;
                }
                catch (System.Exception ex)
                {
                    GungnirStaffPlugin.Log.LogError($"Could not add the Gungnir recipe: {ex}");
                    return;
                }
            }

            EnsureInObjectDb(db, resolved);
            BindExactCosts(db);
        }

        /// <summary>
        ///     Builds the recipe straight into ObjectDB when it is not already there.
        ///
        ///     Jotunn's AddRecipe queues the recipe for the next ObjectDB rebuild, which
        ///     mid-session never comes - so on a hot reload the staff would be uncraftable
        ///     until a restart. Same reasoning as RegisterItemInObjectDB for the item.
        ///     Guarded on the recipe not already existing, so the two paths cannot produce
        ///     a duplicate.
        /// </summary>
        private static void EnsureInObjectDb(ObjectDB db, Dictionary<string, string> resolved)
        {
            if (db.m_recipes == null)
            {
                return;
            }

            var itemPrefab = db.GetItemPrefab(GungnirItem.PrefabName);
            var itemDrop = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
            if (itemDrop == null)
            {
                return;
            }

            var stationPrefab = PrefabManager.Instance.GetPrefab(Station);
            var station = stationPrefab != null
                ? stationPrefab.GetComponent<CraftingStation>()
                : null;

            if (station == null)
            {
                GungnirStaffPlugin.Log.LogError(
                    $"Crafting station '{Station}' not found; Gungnir will not be craftable.");
                return;
            }

            // Reuse the existing recipe object if there is one and refresh it in place,
            // rather than returning early. Editing the cost tables and hot reloading has
            // to actually change the recipe, and adding a second one would show the staff
            // twice at the table.
            var recipe = FindRecipe(db);
            var isNew = recipe == null;
            if (isNew)
            {
                recipe = ScriptableObject.CreateInstance<Recipe>();
            }

            recipe.name = "Recipe_GungnirStaff";
            recipe.m_item = itemDrop;
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = station;
            recipe.m_repairStation = station;
            recipe.m_minStationLevel = MinStationLevel;
            recipe.m_resources = Costs.Select(kv =>
            {
                var prefab = db.GetItemPrefab(resolved[kv.Key]);
                return new Piece.Requirement
                {
                    m_resItem = prefab != null ? prefab.GetComponent<ItemDrop>() : null,
                    m_amount = kv.Value[0],
                    m_amountPerLevel = kv.Value.Length > 1 ? kv.Value[1] : 0,
                    m_recover = true,
                };
            }).Where(r => r.m_resItem != null).ToArray();

            if (isNew)
            {
                db.m_recipes.Add(recipe);
            }

            ModConfig.Trace(
                $"Gungnir recipe {(isNew ? "registered at" : "refreshed for")} the Galdr Table "
                + $"({recipe.m_resources.Length} materials, station level {MinStationLevel}, "
                + $"up to level {MaxQuality}).");
        }

        private static Recipe FindRecipe(ObjectDB db)
        {
            return db.m_recipes.FirstOrDefault(
                r => r != null && r.m_item != null
                     && r.m_item.gameObject.name == GungnirItem.PrefabName);
        }

        /// <summary>
        ///     Finds the recipe now living in ObjectDB and maps its requirements to the
        ///     exact cost tables. Re-run on load and hot reload, because the recipe object
        ///     is rebuilt whenever ObjectDB is.
        /// </summary>
        private static void BindExactCosts(ObjectDB db)
        {
            if (db.m_recipes == null)
            {
                return;
            }

            var recipe = FindRecipe(db);
            if (recipe == null)
            {
                ModConfig.Trace("Gungnir recipe not in ObjectDB yet; will bind later.");
                return;
            }

            ExactCosts.Clear();
            foreach (var req in recipe.m_resources)
            {
                if (req?.m_resItem == null)
                {
                    continue;
                }

                var name = req.m_resItem.gameObject.name;
                var key = Costs.Keys.FirstOrDefault(
                    k => string.Equals(Resolve(db, k), name, System.StringComparison.Ordinal));
                if (key != null)
                {
                    ExactCosts[req] = Costs[key];
                }
            }

            ModConfig.Trace($"Gungnir recipe bound with {ExactCosts.Count} exact cost table(s).");
        }

        /// <summary>How many rack slots a Gungnir of this quality has.</summary>
        internal static int SlotsForQuality(int quality)
        {
            var upgrades = Mathf.Max(0, quality - 1);
            return Mathf.Clamp(
                BaseSlots + (upgrades * SlotsPerLevel), BaseSlots, ModConfig.MaxSlots);
        }

        /// <summary>
        ///     True when the next upgrade would actually widen the rack.
        ///
        ///     Not the same question as "is this below max quality": the rack is capped at
        ///     <see cref="ModConfig.MaxSlots"/>, so the curve can reach its ceiling before
        ///     the item runs out of levels. Asked before promising more slots, so the
        ///     tooltip cannot advertise an upgrade that would add none.
        /// </summary>
        internal static bool MoreSlotsAvailable(int quality)
        {
            return quality < MaxQuality
                   && SlotsForQuality(quality + 1) > SlotsForQuality(quality);
        }
    }
}
