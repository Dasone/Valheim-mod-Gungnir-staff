using System.Linq;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Motes around the crystal: a slow shoal that orbits it, and occasional sparks
    ///     that shoot outward.
    ///
    ///     Built in code rather than authored into the AssetBundle, for two reasons. The
    ///     colour has to follow whichever staff is selected, which a baked particle system
    ///     cannot do; and building it here means it hot-reloads, instead of costing a
    ///     Blender-Unity-bundle round trip for every tweak.
    /// </summary>
    internal static class CrystalParticles
    {
        private const string OrbitName = "GungnirCrystalOrbit";
        private const string SparkName = "GungnirCrystalSparks";

        private static ParticleSystem _orbit;
        private static ParticleSystem _sparks;
        private static Material _material;
        private static Color _applied = Color.clear;

        /// <summary>
        ///     Creates the systems if needed and keeps them tinted. Cheap per frame: the
        ///     colour work only runs when the colour actually changes.
        /// </summary>
        internal static void Apply(
            Renderer crystal, GameObject weapon, Player player, Color colour, bool staffStance)
        {
            bool show;
            switch (ModConfig.CrystalParticles.Value)
            {
                case CrystalParticleMode.Always:
                    show = true;
                    break;
                case CrystalParticleMode.Never:
                    show = false;
                    break;
                default:
                    show = staffStance;
                    break;
            }

            if (!show || crystal == null)
            {
                Clear();
                return;
            }

            if (_orbit == null || !_orbit.transform.IsChildOf(crystal.transform))
            {
                Clear();
                Build(crystal, weapon, player);
            }

            if (_orbit == null)
            {
                return;
            }

            // Cheap and every frame, because the bone underneath can change scale
            // without the systems being rebuilt - swapping hands is exactly that.
            HoldSize(_orbit.transform);
            HoldSize(_sparks != null ? _sparks.transform : null);

            if (_applied != colour)
            {
                _applied = colour;
                Tint(colour);
            }
        }

        internal static void Clear()
        {
            // DestroyImmediate, not Destroy. Destroy is deferred to the end of the
            // frame, so a rebuild in the same frame - which is what a hand swap causes -
            // put the new systems on screen alongside the old ones that were still
            // waiting to die. Two overlapping emitters read as one effect at twice the
            // density, which is exactly the "doubled" look.
            if (_orbit != null)
            {
                Object.DestroyImmediate(_orbit.gameObject);
            }

            if (_sparks != null)
            {
                Object.DestroyImmediate(_sparks.gameObject);
            }

            _orbit = null;
            _sparks = null;
            _material = null;
            _applied = Color.clear;
        }

        private static void Build(Renderer crystal, GameObject weapon, Player player)
        {
            try
            {
                // Anything left over from a previous life - a hot reload resets these
                // statics without destroying what they pointed at, and a model instance
                // can outlive the reference we had to it. Without this they accumulate,
                // one more emitter each time.
                // BOTH hands, not just the weapon we are building on. A staff is
                // TwoHandedWeaponLeft and a spear is OneHanded, so selecting one moves
                // Gungnir across - and the instance it came from can still be alive,
                // carrying the emitters we built on it last time. Scanning only the
                // current weapon left those running, so every swap added another set
                // and the effect kept growing. That is the "gets bigger the more you
                // swap" part.
                var vis = player != null ? player.m_visEquipment : null;
                PurgeStrays(weapon);
                if (vis != null)
                {
                    PurgeStrays(vis.m_rightItemInstance);
                    PurgeStrays(vis.m_leftItemInstance);
                }

                _material = BorrowParticleMaterial(weapon);
                var scale = ModConfig.CrystalParticleScale.Value;

                // The crystal object's ORIGIN sits at the model origin, not at the
                // crystal itself - the mesh was built by offsetting its vertices, which
                // leaves the origin behind. Anchoring to the mesh's own centre is what
                // puts the motes on the crystal instead of wherever the origin lands.
                var anchor = crystal.transform.InverseTransformPoint(crystal.bounds.center);

                _orbit = MakeSystem(crystal.transform, OrbitName, anchor);
                ConfigureOrbit(_orbit, scale);

                _sparks = MakeSystem(crystal.transform, SparkName, anchor);
                ConfigureSparks(_sparks, scale);

                ModConfig.Trace("Crystal particle systems created.");
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError($"Could not build crystal particles: {ex}");
                Clear();
            }
        }

        private static ParticleSystem MakeSystem(Transform parent, string name, Vector3 anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = anchor;
            go.transform.localRotation = Quaternion.identity;
            HoldSize(go.transform);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>The slow shoal circling the crystal.</summary>
        private static void ConfigureOrbit(ParticleSystem ps, float scale)
        {
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f * scale, 0.024f * scale);
            main.maxParticles = 60;
            main.gravityModifier = 0f;
            // Local space so the shoal travels with the weapon instead of being left
            // behind every time the player moves.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f * ModConfig.CrystalParticleRate.Value;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.115f * scale;
            shape.radiusThickness = 0.45f;

            // Orbital velocity is what makes them circle rather than drift; randomising
            // all three axes keeps the paths from looking like a single flat ring.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalX = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);
            vel.radial = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

            FadeInOut(ps);
            ShrinkOverLife(ps, 1f, 0.25f);
            ps.Play();
        }

        /// <summary>Occasional sparks flung out of the crystal.</summary>
        private static void ConfigureSparks(ParticleSystem ps, float scale)
        {
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f * scale, 0.018f * scale);
            main.maxParticles = 40;
            main.gravityModifier = 0.02f;
            // World space so they stream off behind the weapon as it swings.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 3f * ModConfig.CrystalParticleRate.Value;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 3, 6, 0, 0.85f),
            });

            // A sphere emitter with speed but no thickness throws everything radially
            // outward, which is the "shot from the crystal" read.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.035f * scale;
            shape.radiusThickness = 1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);

            FadeInOut(ps);
            ShrinkOverLife(ps, 1f, 0f);
            ps.Play();
        }

        /// <summary>
        ///     Cancels out whatever scale the weapon is hanging off, so the motes are the
        ///     configured size and nothing else.
        ///
        ///     This is the fix for the effect appearing to change size on its own.
        ///     Selecting a staff moves Gungnir from the right hand to the left, because a
        ///     staff is TwoHandedWeaponLeft and a spear is OneHanded - and those are two
        ///     different bones, which need not be scaled the same. The systems hang off
        ///     the crystal, so they inherited that difference and the motes grew or shrank
        ///     with a stance change that has nothing to do with them. Setting the local
        ///     scale to the inverse of the parent's makes the world scale exactly one, so
        ///     size comes from startSize alone.
        /// </summary>
        private static void HoldSize(Transform t)
        {
            if (t == null)
            {
                return;
            }

            var parent = t.parent;
            var inherited = parent != null ? parent.lossyScale : Vector3.one;

            // A zero on any axis means the bone is degenerate this frame; dividing by it
            // would put a NaN into the transform and the particles would vanish.
            if (Mathf.Approximately(inherited.x, 0f)
                || Mathf.Approximately(inherited.y, 0f)
                || Mathf.Approximately(inherited.z, 0f))
            {
                return;
            }

            var wanted = new Vector3(1f / inherited.x, 1f / inherited.y, 1f / inherited.z);
            if (t.localScale != wanted)
            {
                t.localScale = wanted;
            }
        }

        /// <summary>
        ///     Destroys any of our particle objects hanging off the given root that we
        ///     are no longer tracking. Matched by name, which is why they have
        ///     distinctive ones.
        /// </summary>
        private static void PurgeStrays(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null || ReferenceEquals(ps, _orbit) || ReferenceEquals(ps, _sparks))
                {
                    continue;
                }

                var name = ps.gameObject.name;
                if (name.StartsWith(OrbitName, System.StringComparison.Ordinal)
                    || name.StartsWith(SparkName, System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(ps.gameObject);
                }
            }
        }

        private static void FadeInOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0.85f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ShrinkOverLife(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, to > 0f ? 0.4f : 0.2f),
                new Keyframe(0.3f, from),
                new Keyframe(1f, to));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>
        ///     Colours both systems from the crystal's light. Start colour is pushed
        ///     past white so the motes read as emissive against a dark shaft rather than
        ///     as flat dots.
        /// </summary>
        private static void Tint(Color colour)
        {
            var hot = colour * ModConfig.CrystalParticleBrightness.Value;
            hot.a = 1f;

            foreach (var ps in new[] { _orbit, _sparks })
            {
                if (ps == null)
                {
                    continue;
                }

                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(hot, colour);
            }

            if (_material == null)
            {
                return;
            }

            foreach (var prop in new[] { "_TintColor", "_Color", "_BaseColor", "_EmissionColor" })
            {
                if (_material.HasProperty(prop))
                {
                    _material.SetColor(prop, hot);
                }
            }
        }

        /// <summary>
        ///     Takes a particle material from something already in the game.
        ///
        ///     Same reasoning as the model's shaders: anything authored against a shader
        ///     Valheim does not ship renders magenta. Borrowing a live one guarantees a
        ///     shader that exists and is already set up for the game's rendering.
        /// </summary>
        private static Material BorrowParticleMaterial(GameObject weapon)
        {
            // The weapon's own effect already carries a working particle material, so
            // look there first rather than sweeping the whole scene.
            var donor = weapon == null
                ? null
                : weapon.GetComponentsInChildren<ParticleSystemRenderer>(true)
                    .Select(r => r.sharedMaterial)
                    .FirstOrDefault(m => m != null && m.shader != null);

            if (donor != null)
            {
                return new Material(donor);
            }

            var shader = Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Legacy Shaders/Particles/Additive")
                         ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "No particle shader available; crystal motes may not render.");
                return null;
            }

            return new Material(shader);
        }
    }
}
