using System.Linq;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Builds the Gungnir item prefab from nothing - no vanilla weapon cloned.
    ///
    ///     The clone path exists because cloning a spear hands you working attacks,
    ///     animations, colliders and a hundred-odd SharedData fields for free. Everything
    ///     it gave us is authored explicitly here instead, from values measured off a real
    ///     spear rather than guessed at.
    ///
    ///     Worth knowing: the attack ANIMATIONS are not a vanilla-weapon dependency.
    ///     "spear_poke" is a state in the player's own animator, so naming it costs
    ///     nothing and leaves the item standalone.
    /// </summary>
    internal static class GungnirStandalone
    {
        // Measured from SpearSplitner_Lightning rather than invented, so the weapon
        // handles like a real spear of its tier.
        private const int ToolTier = 5;
        private const float BlockPower = 57f;
        private const float DeflectionForce = 20f;
        private const float AttackForce = 20f;
        private const string AttackAnimation = "spear_poke";
        private const float AttackStamina = 16f;
        private const float AttackRange = 1.9f;
        private const float EquipDuration = 0.2f;

        /// <summary>
        ///     Creates and registers the item. Returns null if anything essential is
        ///     missing, so the caller can fall back to the clone rather than register a
        ///     half-built prefab.
        /// </summary>
        internal static GameObject Build(ObjectDB db)
        {
            var model = GungnirVisual.Prefab;
            if (model == null)
            {
                GungnirStaffPlugin.Log.LogError(
                    "Standalone build needs the model from the AssetBundle; it did not load.");
                return null;
            }

            // Parked under an INACTIVE container, and left active in its own right.
            //
            // This distinction is the whole difference between a prefab and a dead scene
            // object. Awake is gated on activeInHierarchy, so a child of an inactive
            // parent is dormant exactly like a real prefab asset - no component wakes up
            // while we build it. But Object.Instantiate copies activeSelf, and vanilla's
            // ItemDrop.DropItem instantiates the prefab and never calls SetActive: an
            // object that was switched off with its OWN flag therefore lands in the world
            // switched off. No Awake, so no ZNetView, so no ZDO - and ItemDrop.Save()
            // returns early on a null nview, so nothing is written and nothing is
            // replicated. The dropped staff is invisible, unpickupable and gone at the
            // next world load. Being inactive by HIERARCHY instead means the clone comes
            // out active, wakes up during Instantiate, and behaves like any vanilla drop.
            var root = new GameObject(GungnirItem.PrefabName);
            root.transform.SetParent(PrefabContainer().transform, false);
            root.layer = LayerMask.NameToLayer("item");

            try
            {
                // ---- the dropped-on-the-ground body ------------------------------
                var nview = root.AddComponent<ZNetView>();
                nview.m_persistent = true;
                nview.m_distant = false;
                nview.m_type = ZDO.ObjectType.Default;
                nview.m_syncInitialScale = false;

                // Measured off a real spear prefab rather than assumed: it also carries a
                // ZSyncTransform (so a dropped item's position replicates) and a Floating
                // (so it bobs instead of sinking out of reach). Both are easy to miss and
                // only show up as bugs much later.
                root.AddComponent<ZSyncTransform>();

                var body = root.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.None;

                root.AddComponent<Floating>();

                // Vanilla puts the collider on its own child called "collider", not on the
                // root. Sized to the shaft: the wings would give a box so wide the item
                // will not settle on the ground.
                var colliderObject = new GameObject("collider");
                colliderObject.transform.SetParent(root.transform, false);
                colliderObject.layer = root.layer;
                var collider = colliderObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.2f, 0.2f, 2.3f);

                // ---- the held visual --------------------------------------------
                // Named "attach" because that is what VisEquipment looks for; the whole
                // rest of the mod finds our mesh through it too.
                var attach = new GameObject("attach");
                attach.transform.SetParent(root.transform, false);

                var visual = Object.Instantiate(model, attach.transform, false);
                visual.name = GungnirVisual.PrefabName;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                // ---- the item itself ---------------------------------------------
                var drop = root.AddComponent<ItemDrop>();
                drop.m_autoPickup = true;
                drop.m_itemData = new ItemDrop.ItemData
                {
                    m_stack = 1,
                    m_quality = 1,
                    m_durability = 100f,
                    m_dropPrefab = root,
                    m_shared = BuildSharedData(),
                };

                BorrowEffects(drop.m_itemData.m_shared, db);

                return root;
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError($"Standalone prefab build failed: {ex}");
                Object.Destroy(root);
                return null;
            }
        }

        /// <summary>
        ///     The inactive parent the prefab hangs off, standing in for the asset folder
        ///     a real prefab would live in.
        ///
        ///     It must be inactive, so nothing under it ever wakes up, and it must be a
        ///     root object, or DontDestroyOnLoad refuses it and the prefab is destroyed
        ///     on the next scene change. Jotunn keeps an identical container for its own
        ///     custom prefabs, but its property is internal, so we keep our own rather
        ///     than reach into another mod's private state.
        /// </summary>
        private static GameObject PrefabContainer()
        {
            if (_container == null)
            {
                _container = new GameObject("GungnirStaff_Prefabs");
                _container.SetActive(false);
                Object.DontDestroyOnLoad(_container);
            }

            return _container;
        }

        private static GameObject _container;

        /// <summary>
        ///     Every stat, authored rather than inherited. This is the bulk of what
        ///     cloning used to provide.
        /// </summary>
        private static ItemDrop.ItemData.SharedData BuildSharedData()
        {
            var shared = new ItemDrop.ItemData.SharedData
            {
                m_name = GungnirItem.NameToken,
                m_description = GungnirItem.DescToken,
                m_itemType = ItemDrop.ItemData.ItemType.OneHandedWeapon,
                m_skillType = Skills.SkillType.Spears,
                m_animationState = ItemDrop.ItemData.AnimationState.OneHanded,
                m_toolTier = ToolTier,

                m_maxStackSize = 1,
                m_maxQuality = GungnirRecipe.MaxQuality,
                m_weight = 1.5f,
                m_value = 0,
                m_teleportable = true,
                m_questItem = false,
                m_dlc = string.Empty,

                // Odin's spear does not wear out.
                m_useDurability = false,
                m_maxDurability = 1000f,

                m_blockPower = BlockPower,
                m_deflectionForce = DeflectionForce,
                m_attackForce = AttackForce,
                m_equipDuration = EquipDuration,

                m_attack = BuildAttack(1f),
                m_secondaryAttack = BuildAttack(
                    GungnirItem.SecondaryPierce / GungnirItem.PrimaryPierce),
            };

            shared.m_damages.m_pierce = GungnirItem.PrimaryPierce;

            var icon = GungnirVisual.Icon;
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }

            return shared;
        }

        /// <summary>
        ///     A melee thrust. Horizontal rather than Projectile on both attacks, so
        ///     Gungnir - and the whole staff rack inside it - can never be thrown away.
        /// </summary>
        private static Attack BuildAttack(float damageMultiplier)
        {
            return new Attack
            {
                m_attackType = Attack.AttackType.Horizontal,
                m_attackAnimation = AttackAnimation,
                m_attackStamina = AttackStamina,
                m_attackEitr = 0f,
                m_attackRange = AttackRange,
                m_damageMultiplier = damageMultiplier,
                m_consumeItem = false,
                m_requiresReload = false,
                m_attackChainLevels = 1,
                m_speedFactor = 0.2f,
                m_speedFactorRotation = 0.2f,
            };
        }

        /// <summary>
        ///     Gives the standalone item the impact spark, the hit sound, the swing
        ///     whoosh and the equip clink that a real spear has.
        ///
        ///     Every one of those is an <see cref="EffectList"/> hanging off SharedData or
        ///     off the Attack, and an authored-from-scratch item starts with all of them
        ///     empty. Nothing warns about it: the weapon still swings, still connects,
        ///     still prints damage numbers - it simply lands in total silence with no
        ///     spark, because <c>Attack.DoMeleeAttack</c> asks the weapon what to play on
        ///     a hit and the weapon answers "nothing".
        ///
        ///     Borrowed rather than authored, for the same reason the shader is: these are
        ///     asset references, not behaviour. Pointing at the game's own spear sounds is
        ///     what makes a hit feel native, and none of the donor's stats, attacks or
        ///     logic come along with them - only the lists of what to spawn.
        /// </summary>
        internal static void BorrowEffects(ItemDrop.ItemData.SharedData shared, ObjectDB db)
        {
            if (shared == null)
            {
                return;
            }

            var donorPrefab = GungnirItem.PickBaseWeapon(db) ?? FindReferenceSpear(db);
            var donor = donorPrefab != null
                ? donorPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared
                : null;

            if (donor == null)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "No vanilla spear found to read hit effects from; Gungnir will hit "
                    + "silently and without impact sparks.");
                return;
            }

            // The lists are shared by reference, never copied into and never written to.
            // A copy would gain nothing - EffectList is only ever read by Create() - and
            // sharing means a game update that retunes the spear's impact retunes ours.
            var copied = 0;
            copied += Take(donor.m_hitEffect, e => shared.m_hitEffect = e);
            copied += Take(donor.m_hitTerrainEffect, e => shared.m_hitTerrainEffect = e);
            copied += Take(donor.m_blockEffect, e => shared.m_blockEffect = e);
            copied += Take(donor.m_startEffect, e => shared.m_startEffect = e);
            copied += Take(donor.m_holdStartEffect, e => shared.m_holdStartEffect = e);
            copied += Take(donor.m_equipEffect, e => shared.m_equipEffect = e);
            copied += Take(donor.m_unequipEffect, e => shared.m_unequipEffect = e);
            copied += Take(donor.m_triggerEffect, e => shared.m_triggerEffect = e);
            copied += Take(donor.m_trailStartEffect, e => shared.m_trailStartEffect = e);

            // The Attack carries a second set, and DoMeleeAttack plays BOTH on every hit -
            // the SharedData list alone leaves half the impact missing.
            copied += BorrowAttackEffects(donor.m_attack, shared.m_attack);
            copied += BorrowAttackEffects(donor.m_attack, shared.m_secondaryAttack);

            ModConfig.Trace(
                $"Standalone Gungnir borrowed {copied} effect list(s) from '{donorPrefab.name}'.");
        }

        /// <summary>The Attack's own effect lists, which play alongside SharedData's.</summary>
        private static int BorrowAttackEffects(Attack donor, Attack target)
        {
            if (donor == null || target == null)
            {
                return 0;
            }

            var copied = 0;
            copied += Take(donor.m_hitEffect, e => target.m_hitEffect = e);
            copied += Take(donor.m_hitTerrainEffect, e => target.m_hitTerrainEffect = e);
            copied += Take(donor.m_startEffect, e => target.m_startEffect = e);
            copied += Take(donor.m_triggerEffect, e => target.m_triggerEffect = e);
            copied += Take(donor.m_trailStartEffect, e => target.m_trailStartEffect = e);
            copied += Take(donor.m_burstEffect, e => target.m_burstEffect = e);
            return copied;
        }

        /// <summary>
        ///     Assigns an effect list only when the donor actually has one. An empty list
        ///     is not worth taking, and taking it would hide the warning that says so.
        /// </summary>
        private static int Take(EffectList source, System.Action<EffectList> assign)
        {
            if (source == null || source.m_effectPrefabs == null
                || source.m_effectPrefabs.Length == 0)
            {
                return 0;
            }

            assign(source);
            return 1;
        }

        /// <summary>
        ///     A shader that exists in the running game.
        ///
        ///     With no donor spear to borrow from, this takes one off any vanilla item's
        ///     material. Reading a shader is not the same as being built on a vanilla
        ///     weapon - nothing of that item's behaviour comes along.
        /// </summary>
        internal static Shader FindGameShader(ObjectDB db)
        {
            if (db == null || db.m_items == null)
            {
                return null;
            }

            // Deliberately a WEAPON's shader, not "whatever ObjectDB lists first".
            //
            // The old version grabbed the first material it found anywhere, which could
            // easily be a transparent or unlit one - and an alpha-blended shader on a
            // solid blade does not write depth, so the blade z-fights with itself and
            // flickers. The clone path always landed on Custom/Creature because it
            // borrowed from the spear it was cloned from; matching that is the fix.
            var preferred = FindReferenceSpear(db);
            var shader = ShaderOf(preferred);
            if (shader != null)
            {
                ModConfig.Trace($"Standalone using shader '{shader.name}' from '{preferred.name}'.");
                return shader;
            }

            foreach (var prefab in db.m_items)
            {
                if (prefab == null || prefab.name == GungnirItem.PrefabName)
                {
                    continue;
                }

                shader = ShaderOf(prefab);
                if (shader != null)
                {
                    ModConfig.Trace($"Standalone using shader '{shader.name}' from '{prefab.name}'.");
                    return shader;
                }
            }

            return null;
        }

        private static Shader ShaderOf(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            var mat = prefab.GetComponentsInChildren<Renderer>(true)
                .Where(r => !(r is ParticleSystemRenderer))
                .SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m != null && m.shader != null);

            return mat != null ? mat.shader : null;
        }

        /// <summary>
        ///     A vanilla spear to read the sheathed orientation from - referenced, never
        ///     cloned. Only its transform is measured; none of its data is copied.
        /// </summary>
        internal static GameObject FindReferenceSpear(ObjectDB db)
        {
            if (db == null || db.m_items == null)
            {
                return null;
            }

            return db.m_items.FirstOrDefault(p =>
                p != null
                && p.name != GungnirItem.PrefabName
                && p.name.StartsWith("Spear", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
