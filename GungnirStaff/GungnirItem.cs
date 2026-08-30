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

            // Fallback for items built by hand before m_dropPrefab is assigned.
            return item.m_customData != null && item.m_customData.ContainsKey(StaffContainer.InventoryKey);
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
                BaseShared = existing.GetComponent<ItemDrop>()?.m_itemData?.m_shared;

                // Re-applied on every load so stat edits land on a hot reload instead of
                // needing a restart. Existing Gungnirs share this SharedData instance, so
                // the ones already in inventories update too.
                ApplyIdentity(BaseShared, db);
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
                GungnirStaffPlugin.Log.LogMessage(
                    $"Created placeholder Gungnir prefab (cloned from {basePrefab.name}).");
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

            // Its materials came from the editor's Standard shader, which the game does
            // not ship. With no donor to borrow from, take one off any vanilla item.
            var shader = GungnirStandalone.FindGameShader(db);
            if (shader != null)
            {
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
                    }
                }
            }
            else
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "No vanilla shader found; the standalone model may render untextured.");
            }

            ItemManager.Instance.RegisterItemInObjectDB(prefab);
            RegisterWithZNetScene(prefab);
            GungnirRecipe.Register();

            GungnirStaffPlugin.Log.LogMessage(
                "Created the standalone Gungnir prefab - no vanilla weapon behind it.");
            return true;
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

            DumpDonorBlueprint(target);
            Blueprint.DumpVanillaItem(PickBaseWeapon(db)?.name ?? "SpearBronze");

            // Upgrade damage is left flat for now: the levels buy rack slots, not
            // numbers, until per-level damage is specified.
            target.m_damagesPerLevel = default(HitData.DamageTypes);
        }


        /// <summary>
        ///     Logs the values the donor spear supplies, so an equivalent prefab can be
        ///     authored from scratch without guessing at a hundred-odd SharedData fields.
        ///     Trace-level; only interesting while building the standalone item.
        /// </summary>
        private static void DumpDonorBlueprint(ItemDrop.ItemData.SharedData shared)
        {
            if (shared == null || !ModConfig.VerboseLogging.Value)
            {
                return;
            }

            var a = shared.m_attack;
            var s2 = shared.m_secondaryAttack;
            GungnirStaffPlugin.Log.LogInfo(
                "BLUEPRINT itemType=" + shared.m_itemType
                + " skill=" + shared.m_skillType
                + " anim=" + shared.m_animationState
                + " toolTier=" + shared.m_toolTier
                + " blockPower=" + shared.m_blockPower
                + " deflection=" + shared.m_deflectionForce
                + " attackForce=" + shared.m_attackForce
                + " eitrRegen=" + shared.m_eitrRegenModifier
                + " staminaMod=" + shared.m_attackStaminaModifier
                + " dmg(blunt/slash/pierce)=" + shared.m_damages.m_blunt + "/"
                + shared.m_damages.m_slash + "/" + shared.m_damages.m_pierce
                + " | primary: type=" + (a != null ? a.m_attackType.ToString() : "-")
                + " anim=" + (a != null ? a.m_attackAnimation : "-")
                + " stamina=" + (a != null ? a.m_attackStamina.ToString() : "-")
                + " eitr=" + (a != null ? a.m_attackEitr.ToString() : "-")
                + " range=" + (a != null ? a.m_attackRange.ToString() : "-")
                + " | secondary: type=" + (s2 != null ? s2.m_attackType.ToString() : "-")
                + " anim=" + (s2 != null ? s2.m_attackAnimation : "-")
                + " mult=" + (s2 != null ? s2.m_damageMultiplier.ToString() : "-"));
        }

        /// <summary>Field-by-field copy. Used for plain serialized classes only.</summary>
        private static void CopyFields<T>(T from, T to) where T : class
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
        private static GameObject PickBaseWeapon(ObjectDB db)
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
