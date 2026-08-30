using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     The Gungnir staff prefab itself.
    ///
    ///     Cloned at runtime from a vanilla spear, which is what gives it working
    ///     attacks, animations and colliders for free; its own model, name, flavour text,
    ///     damage and recipe are applied on top. Cloning also means it can be created in
    ///     the middle of a running game, which is what keeps the whole mod hot-reloadable.
    /// </summary>
    internal static class GungnirItem
    {
        internal const string PrefabName = "GungnirStaff";
        internal const string NameToken = "$gungnir_item_name";
        internal const string DescToken = "$gungnir_item_desc";

        /// <summary>Pierce damage of the primary attack when no staff is selected.</summary>
        internal const float PrimaryPierce = 95f;

        /// <summary>Pierce damage the secondary attack should land on.</summary>
        internal const float SecondaryPierce = 113f;

        /// <summary>
        ///     What Gungnir is built from when it is not impersonating a staff: a
        ///     Carapace Spear. Tried in order, so a rename between game versions falls
        ///     back rather than breaking.
        /// </summary>
        private static readonly string[] BaseWeaponCandidates =
        {
            "SpearSplitner_Lightning", "SpearSplitner", "SpearCarapace", "SpearElderbark",
            "SpearBronze",
        };

        internal static GameObject Prefab { get; private set; }

        /// <summary>
        ///     Gungnir's own SharedData, kept aside so we can restore it after the item
        ///     has been temporarily turned into one of the stored staffs.
        /// </summary>
        internal static ItemDrop.ItemData.SharedData BaseShared { get; private set; }

        /// <summary>
        ///     Identity test. Deliberately keyed on <c>m_dropPrefab</c>, not on
        ///     <c>m_shared</c>: the shared data gets swapped out while a staff is active,
        ///     but the drop prefab never changes - and it is also what
        ///     <c>Inventory.Save</c> persists, so this stays true across a save/load.
        /// </summary>
        internal static bool IsGungnir(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.m_dropPrefab != null)
            {
                // Reference compare first: GetIcon() is patched on top of this and runs
                // for every visible slot every frame, so the string compare is a fallback.
                return ReferenceEquals(item.m_dropPrefab, Prefab)
                       || item.m_dropPrefab.name == PrefabName;
            }

            // Fallbacks for an item whose drop prefab has not been filled in yet: the
            // name token is authored by us and unique, and custom data catches anything
            // that has already stored a rack.
            return (item.m_shared != null && item.m_shared.m_name == NameToken)
                   || (item.m_customData != null
                       && item.m_customData.ContainsKey(StaffContainer.InventoryKey));
        }

        /// <summary>
        ///     Creates and registers the placeholder prefab. Safe to call repeatedly -
        ///     a hot reload re-enters this and reuses whatever is already in ObjectDB.
        /// </summary>
        internal static bool Create()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                ModConfig.Trace("ObjectDB not ready; deferring Gungnir prefab creation.");
                return false;
            }

            // Already present from an earlier load (or an earlier hot-reload cycle).
            var existing = db.GetItemPrefab(PrefabName);
            if (existing != null)
            {
                Prefab = existing;
                if (existing.name != PrefabName)
                {
                    existing.name = PrefabName;
                }

                var existingDrop = existing.GetComponent<ItemDrop>();
                BaseShared = existingDrop?.m_itemData?.m_shared;

                // Set unconditionally: it may already point at an orphan copy from an
                // earlier registration, which is just as broken as being null.
                if (existingDrop?.m_itemData != null)
                {
                    existingDrop.m_itemData.m_dropPrefab = existing;
                }

                // Re-applied on every load so stat edits land on a hot reload instead of
                // needing a restart. Existing Gungnirs share this SharedData instance, so
                // the ones already in inventories update too.
                ApplyIdentity(BaseShared, db);
                if (ModConfig.StandalonePrefab.Value)
                {
                    ApplyGameShader(existing, db);
                }

                // Re-registered on EVERY load, not just when the prefab is first built.
                // ObjectDB and our prefab survive a world change, but ZNetScene does not -
                // it is rebuilt with an empty m_namedPrefabs. A dropped Gungnir asks that
                // table for its own prefab hash, so without this the item would drop fine
                // in the first world of a session and vanish in every one after it.
                RegisterWithZNetScene(existing);

                GungnirVisual.Apply(existing);
                GungnirRecipe.Register();
                ModConfig.Trace("Gungnir prefab already registered; refreshed its stats.");
                return true;
            }

            if (ModConfig.StandalonePrefab.Value)
            {
                if (BuildStandalone(db))
                {
                    return true;
                }

                GungnirStaffPlugin.Log.LogWarning(
                    "Standalone build did not complete; falling back to the cloned spear so "
                    + "the mod stays usable.");
            }

            var basePrefab = PickBaseWeapon(db);
            if (basePrefab == null)
            {
                GungnirStaffPlugin.Log.LogError(
                    "Could not find a spear to clone the Gungnir placeholder from.");
                return false;
            }

            try
            {
                // Unity deep-copies serialized plain classes on Instantiate, so the clone
                // gets its own SharedData and editing it cannot corrupt the base spear.
                var custom = new CustomItem(PrefabName, basePrefab.name);
                var shared = custom.ItemDrop.m_itemData.m_shared;
                ApplyIdentity(shared, db);

                ItemManager.Instance.AddItem(custom);

                // AddItem alone only takes effect at the next ObjectDB rebuild. This is
                // the call that makes it exist in the game that is already running.
                ItemManager.Instance.RegisterItemInObjectDB(custom.ItemPrefab);

                RegisterWithZNetScene(custom.ItemPrefab);

                Prefab = custom.ItemPrefab;
                BaseShared = shared;
                GungnirVisual.Apply(custom.ItemPrefab);
                GungnirRecipe.Register();
                GungnirStaffPlugin.Log.LogInfo(
                    $"Created the Gungnir prefab (cloned from {basePrefab.name}).");
                return true;
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError($"Failed to create the Gungnir prefab: {ex}");
                return false;
            }
        }


        /// <summary>
        ///     Registers the from-scratch item. Kept separate from the clone path so a
        ///     failure here can fall back instead of leaving the player with no weapon.
        /// </summary>
        private static bool BuildStandalone(ObjectDB db)
        {
            var prefab = GungnirStandalone.Build(db);
            if (prefab == null)
            {
                return false;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            Prefab = prefab;
            BaseShared = drop.m_itemData.m_shared;

            ApplyGameShader(prefab, db);

            ItemManager.Instance.RegisterItemInObjectDB(prefab);
            RegisterWithZNetScene(prefab);

            // Re-read the prefab from ObjectDB rather than trusting the object we handed
            // it. Registration can store a copy, and Unity deep-copies serialized data on
            // Instantiate - so the object players actually get items from may not be the
            // one we built, leaving our cached Prefab/BaseShared pointing at an orphan.
            // That mismatch is what made m_dropPrefab come out null on real items.
            var registered = db.GetItemPrefab(PrefabName);

            // Registration can store an Instantiate()d copy, and Unity appends "(Clone)"
            // to the name. VisEquipment resolves the held model by that name every frame,
            // so a "(Clone)" suffix means the lookup fails, the instance is destroyed and
            // rebuilt continuously - two copies of the mesh alternating with one, which is
            // the flicker. It also explains the null m_dropPrefab and the mismatched
            // SharedData: they were on an object under a name nothing could resolve.
            if (registered != null && registered.name != PrefabName)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    $"Registered prefab was named '{registered.name}'; renaming to "
                    + $"'{PrefabName}' so the game can resolve it.");
                registered.name = PrefabName;
            }

            var registeredDrop = registered != null ? registered.GetComponent<ItemDrop>() : null;
            if (registeredDrop?.m_itemData != null)
            {
                Prefab = registered;
                BaseShared = registeredDrop.m_itemData.m_shared;

                // Vanilla fills this in ItemDrop.Awake, which never runs on an inactive
                // prefab. Every item cloned from here inherits it, so setting it once here
                // is what stops it being null on every Gungnir in the game.
                registeredDrop.m_itemData.m_dropPrefab = registered;
            }

            GungnirRecipe.Register();

            GungnirStaffPlugin.Log.LogInfo(
                "Created the standalone Gungnir prefab - no vanilla weapon behind it.");
            return true;
        }


        /// <summary>
        ///     Re-points the standalone model's materials at a shader the game ships.
        ///
        ///     Called on every load, not just when the prefab is first built: a hot reload
        ///     takes the "already registered" path, so a shader fix applied only at build
        ///     time would need a restart to take effect.
        /// </summary>
        private static void ApplyGameShader(GameObject prefab, ObjectDB db)
        {
            var shader = GungnirStandalone.FindGameShader(db);
            if (shader == null)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "No vanilla shader found; the standalone model may render untextured.");
                return;
            }

            var changed = 0;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.materials)
                {
                    if (m == null || m.shader == shader)
                    {
                        continue;
                    }

                    var colour = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    m.shader = shader;
                    if (m.HasProperty("_Color"))
                    {
                        m.SetColor("_Color", colour);
                    }

                    changed++;
                }
            }

            if (changed > 0)
            {
                ModConfig.Trace(
                    $"Standalone shader set to '{shader.name}' on {changed} material(s).");
            }
        }

        /// <summary>
        ///     Makes a SharedData into Gungnir: a Carapace Spear underneath, with its own
        ///     name, flavour text and damage.
        ///
        ///     Split out from creation so it can also run against an already-registered
        ///     prefab. Every Gungnir shares the prefab's SharedData instance, so
        ///     re-applying here updates items already sitting in inventories.
        /// </summary>
        private static void ApplyIdentity(ItemDrop.ItemData.SharedData target, ObjectDB db)
        {
            if (target == null)
            {
                return;
            }

            // Never re-impose the donor on a standalone item: its stats are authored,
            // and copying a spear over them would quietly undo that on every reload.
            if (ModConfig.StandalonePrefab.Value)
            {
                ApplyGungnirIdentity(target);

                // Re-applied on every load, not just at build time: a Gungnir registered
                // by an older build has empty effect lists and would stay silent until
                // the player started a fresh world. This repairs it on a hot reload.
                GungnirStandalone.BorrowEffects(target, db);

                return;
            }

            var source = PickBaseWeapon(db)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (source != null && !ReferenceEquals(source, target))
            {
                // Rewrites a Gungnir that was cloned from something else (an earlier
                // build used a staff) into a spear without needing a game restart.
                CopyFields(source, target);
            }

            // Attack is a class, not a struct: without cloning, the edits below would
            // also rewrite the real donor spear for the entire game.
            target.m_attack = CloneAttack(target.m_attack);

            // Secondary is a harder thrust, not a throw. Vanilla spears throw on the
            // secondary attack, which would fling Gungnir - and the whole staff rack
            // inside it - across the ground. Building it from the primary keeps a real
            // melee secondary while removing the projectile path entirely.
            target.m_secondaryAttack = CloneAttack(target.m_attack);
            target.m_secondaryAttack.m_attackType = Attack.AttackType.Horizontal;

            // Belt and braces: neither attack may ever consume the item.
            target.m_attack.m_consumeItem = false;
            target.m_secondaryAttack.m_consumeItem = false;

            // Its own icon, so it no longer wears the donor spear's face in the
            // inventory. Applied here rather than at creation so a hot reload picks up a
            // re-exported icon without a restart.
            var icon = GungnirVisual.Icon;
            if (icon != null)
            {
                target.m_icons = new[] { icon };
            }

            target.m_name = NameToken;
            target.m_description = DescToken;
            target.m_maxStackSize = 1;
            target.m_maxQuality = GungnirRecipe.MaxQuality;
            target.m_useDurability = false;
            target.m_dlc = string.Empty;

            target.m_damages.m_pierce = PrimaryPierce;
            if (target.m_secondaryAttack != null)
            {
                // Valheim derives secondary damage from the base damage times this
                // multiplier, so the target number is expressed as a ratio.
                target.m_secondaryAttack.m_damageMultiplier = SecondaryPierce / PrimaryPierce;
            }

            ApplyGungnirIdentity(target);

            // Upgrade damage is left flat for now: the levels buy rack slots, not
            // numbers, until per-level damage is specified.
            target.m_damagesPerLevel = default(HitData.DamageTypes);
        }


        /// <summary>
        ///     The parts that are Gungnir's own, applied on top of whatever the base is.
        ///     Shared by the clone and standalone paths.
        /// </summary>
        private static void ApplyGungnirIdentity(ItemDrop.ItemData.SharedData target)
        {
            var icon = GungnirVisual.Icon;
            if (icon != null)
            {
                target.m_icons = new[] { icon };
            }

            target.m_name = NameToken;
            target.m_description = DescToken;
            target.m_maxStackSize = 1;
            target.m_maxQuality = GungnirRecipe.MaxQuality;
            target.m_useDurability = false;
            target.m_dlc = string.Empty;
            target.m_damages.m_pierce = PrimaryPierce;

            if (target.m_secondaryAttack != null)
            {
                target.m_secondaryAttack.m_damageMultiplier = SecondaryPierce / PrimaryPierce;
            }
        }


        /// <summary>
        ///     Restores m_dropPrefab if it is missing.
        ///
        ///     Humanoid.SetupVisEquipment reads m_dropPrefab.name with no null check, so a
        ///     missing one is an instant NullReferenceException the moment the item is
        ///     equipped or sheathed. Vanilla fills it in ItemDrop.Awake, which never runs
        ///     on an inactive runtime prefab - so items built from one can reach the player
        ///     without it. Cheap to check and it keeps the invariant the game assumes.
        /// </summary>
        internal static void RepairDropPrefab(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab != null || Prefab == null)
            {
                return;
            }

            if (item.m_customData != null
                && item.m_customData.ContainsKey(StaffContainer.InventoryKey))
            {
                item.m_dropPrefab = Prefab;
                ModConfig.Trace("Restored a missing m_dropPrefab on a Gungnir.");
            }
        }

        /// <summary>Field-by-field copy. Used for plain serialized classes only.</summary>
        internal static void CopyFields<T>(T from, T to) where T : class
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;

            foreach (var field in typeof(T).GetFields(flags))
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    continue;
                }

                field.SetValue(to, field.GetValue(from));
            }
        }

        private static Attack CloneAttack(Attack source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new Attack();
            CopyFields(source, clone);
            return clone;
        }

        /// <summary>
        ///     The spear Gungnir is built from. Falls back to any spear in ObjectDB if
        ///     none of the known names resolve.
        /// </summary>
        internal static GameObject PickBaseWeapon(ObjectDB db)
        {
            // Configured choice first, so the model can be A/B'd from F1 without a rebuild.
            var configured = ModConfig.BaseWeapon?.Value;
            if (!string.IsNullOrEmpty(configured))
            {
                var chosen = db.GetItemPrefab(configured);
                if (chosen != null)
                {
                    return chosen;
                }

                GungnirStaffPlugin.Log.LogWarning(
                    $"BaseWeaponPrefab '{configured}' is not in ObjectDB; falling back. "
                    + "Use 'gungnir find spear' to see the exact names.");
            }

            foreach (var name in BaseWeaponCandidates)
            {
                var prefab = db.GetItemPrefab(name);
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return db.m_items.FirstOrDefault(p =>
                p != null
                && p.name != PrefabName
                && p.name.StartsWith("Spear", System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Needed so a dropped Gungnir can exist as a world object. Harmless when the
        ///     scene has not spawned yet.
        /// </summary>
        private static void RegisterWithZNetScene(GameObject prefab)
        {
            var scene = ZNetScene.instance;
            if (scene == null || prefab == null)
            {
                return;
            }

            var hash = prefab.name.GetStableHashCode();
            if (scene.m_namedPrefabs.ContainsKey(hash))
            {
                return;
            }

            scene.m_prefabs.Add(prefab);
            scene.m_namedPrefabs[hash] = prefab;
        }

        /// <summary>
        ///     Repairs every Gungnir already carried.
        ///
        ///     Items handed out before the registration fix still have a null
        ///     m_dropPrefab, and that field is what Inventory.Save writes to identify an
        ///     item - so without this they would quietly vanish on relog rather than
        ///     merely look wrong.
        /// </summary>
        internal static void RepairInventory(Player player)
        {
            var inv = player?.m_inventory;
            if (inv == null || Prefab == null)
            {
                return;
            }

            var repaired = 0;
            foreach (var item in inv.GetAllItems())
            {
                if (item.m_dropPrefab != null)
                {
                    continue;
                }

                var isOurs = (item.m_shared != null && item.m_shared.m_name == NameToken)
                             || (item.m_customData != null
                                 && item.m_customData.ContainsKey(StaffContainer.InventoryKey));
                if (!isOurs)
                {
                    continue;
                }

                item.m_dropPrefab = Prefab;
                repaired++;
            }

            if (repaired > 0)
            {
                GungnirStaffPlugin.Log.LogInfo(
                    $"Repaired {repaired} Gungnir(s) that had no drop prefab - they would "
                    + "not have survived a relog.");
            }
        }

        /// <summary>Finds the Gungnir the player is holding, or null.</summary>
        internal static ItemDrop.ItemData EquippedOn(Player player)
        {
            if (player == null)
            {
                return null;
            }

            if (IsGungnir(player.m_rightItem))
            {
                return player.m_rightItem;
            }

            return IsGungnir(player.m_leftItem) ? player.m_leftItem : null;
        }

        /// <summary>Finds the Gungnir anywhere in the player's inventory, or null.</summary>
        internal static ItemDrop.ItemData CarriedBy(Player player)
        {
            var inv = player?.m_inventory;
            if (inv == null)
            {
                return null;
            }

            foreach (var item in inv.GetAllItems())
            {
                if (IsGungnir(item))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        ///     The Gungnir whose features are currently available, honouring the
        ///     Equipped/Carried setting. Null when the mod should stay out of the way.
        /// </summary>
        internal static ItemDrop.ItemData Active(Player player)
        {
            if (player == null || !ModConfig.Enabled.Value)
            {
                return null;
            }

            return ModConfig.Activation.Value == ActivationMode.Carried
                ? CarriedBy(player)
                : EquippedOn(player);
        }
    }
}
