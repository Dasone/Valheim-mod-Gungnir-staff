using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Drives the crystal in the butt of the spear: tints the crystal material and
    ///     casts a matching light.
    ///
    ///     Works against whatever model is equipped. Once the custom Gungnir mesh is in,
    ///     it finds the renderer named "Gungnir_Crystal" and tints that; until then it
    ///     still lights the placeholder, so the colour coding is usable today.
    /// </summary>
    internal static class CrystalGlow
    {
        internal const string CrystalObjectName = "Gungnir_Crystal";
        private const string GlowObjectName = "GungnirCrystalGlow";

        private static Light _light;
        private static Material _tinted;
        private static Color _applied = Color.clear;

        /// <summary>
        ///     Applies the colour for the current selection. Cheap to call every frame:
        ///     it does nothing unless the colour or the held object actually changed.
        /// </summary>
        internal static void Apply(Player player, ItemDrop.ItemData gungnir)
        {
            var attach = FindHeldObject(player);
            if (attach == null)
            {
                Clear();
                return;
            }

            var container = StaffContainer.For(gungnir);
            var slot = container?.SelectedSlot ?? -1;
            var wanted = CrystalColors.For(slot >= 0 ? container.ItemAt(slot) : null);

            EnsureLight(attach);

            if (_light != null)
            {
                _light.color = wanted;
                _light.intensity = ModConfig.Enabled.Value ? CrystalColors.GlowIntensity.Value : 0f;
                _light.range = CrystalColors.GlowRange.Value;
            }

            if (_applied != wanted)
            {
                TintCrystal(attach, wanted);
                _applied = wanted;
            }

            // Motes live on the crystal itself, so they follow it through the stance flip.
            var crystal = FindCrystal(attach);
            CrystalParticles.Apply(crystal, attach, player, wanted, slot >= 0);
        }

        /// <summary>Drops the glow when Gungnir is put away.</summary>
        internal static void Clear()
        {
            if (_light != null)
            {
                Object.Destroy(_light.gameObject);
            }

            CrystalParticles.Clear();
            _light = null;
            _tinted = null;
            _applied = Color.clear;
        }

        /// <summary>
        ///     The instantiated model in the player's hand. Valheim spawns the item's
        ///     attach prefab under the right-hand bone, so this is where any crystal
        ///     renderer lives.
        /// </summary>
        private static GameObject FindHeldObject(Player player)
        {
            var vis = player?.m_visEquipment;
            if (vis == null)
            {
                return null;
            }

            // Whichever hand actually carries our mesh. A projected staff is
            // two-handed-left, so the item changes hands and the old instance can linger.
            var right = vis.m_rightItemInstance;
            if (right != null && FindCrystal(right) != null)
            {
                return right;
            }

            var left = vis.m_leftItemInstance;
            if (left != null && FindCrystal(left) != null)
            {
                return left;
            }

            return right ?? left;
        }

        private static void EnsureLight(GameObject attach)
        {
            if (_light != null && _light.transform.IsChildOf(attach.transform))
            {
                return;
            }

            if (_light != null)
            {
                Object.Destroy(_light.gameObject);
            }

            var go = new GameObject(GlowObjectName);
            go.transform.SetParent(attach.transform, false);

            // Sits at the crystal if the model has one, otherwise at the butt of
            // whatever is being held.
            var crystal = FindCrystal(attach);
            go.transform.position = crystal != null
                ? crystal.bounds.center
                : attach.transform.position;

            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForceVertex;
        }

        private static Renderer FindCrystal(GameObject attach)
        {
            foreach (var r in attach.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.StartsWith(CrystalObjectName, System.StringComparison.Ordinal))
                {
                    return r;
                }
            }

            return null;
        }

        private static void TintCrystal(GameObject attach, Color colour)
        {
            var crystal = FindCrystal(attach);
            if (crystal == null)
            {
                return;
            }

            // A material instance, not the shared asset: tinting the shared material
            // would recolour every Gungnir - and anything else using it - at once.
            if (_tinted == null || crystal.material != _tinted)
            {
                _tinted = crystal.material;
            }

            if (_tinted.HasProperty("_Color"))
            {
                _tinted.SetColor("_Color", colour);
            }

            if (_tinted.HasProperty("_EmissionColor"))
            {
                _tinted.EnableKeyword("_EMISSION");
                _tinted.SetColor("_EmissionColor", colour * CrystalColors.GlowIntensity.Value);
            }
        }
    }
}
