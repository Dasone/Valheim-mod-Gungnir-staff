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
        private const float EquipDuration = 1f;

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

            var root = new GameObject(GungnirItem.PrefabName);
            root.SetActive(false);
            Object.DontDestroyOnLoad(root);
            root.layer = LayerMask.NameToLayer("item");

            try
            {
                // ---- the dropped-on-the-ground body ------------------------------
                var nview = root.AddComponent<ZNetView>();
                nview.m_persistent = true;
                nview.m_distant = false;
                nview.m_type = ZDO.ObjectType.Default;
                nview.m_syncInitialScale = false;

                var body = root.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Sized to the shaft rather than the whole model: the wings would give a
                // huge box that stops the item resting on the ground properly.
                var collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.12f, 0.12f, 1.6f);
                collider.center = Vector3.zero;

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
                drop.m_autoPickup = false;
                drop.m_itemData = new ItemDrop.ItemData
                {
                    m_stack = 1,
                    m_quality = 1,
                    m_durability = 100f,
                    m_dropPrefab = root,
                    m_shared = BuildSharedData(),
                };

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
                m_weight = 4f,
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

            foreach (var prefab in db.m_items)
            {
                if (prefab == null || prefab.name == GungnirItem.PrefabName)
                {
                    continue;
                }

                var mat = prefab.GetComponentsInChildren<Renderer>(true)
                    .Where(r => !(r is ParticleSystemRenderer))
                    .SelectMany(r => r.sharedMaterials)
                    .FirstOrDefault(m => m != null && m.shader != null);

                if (mat != null)
                {
                    return mat.shader;
                }
            }

            return null;
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
