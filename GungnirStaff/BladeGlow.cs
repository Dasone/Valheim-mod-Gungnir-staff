using System.Linq;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     The glow on the spear head.
    ///
    ///     The clone path inherited this from the donor spear's own particle system. A
    ///     standalone item has no donor, so it is built here instead - which also means
    ///     it is pinned to the blade mesh directly rather than positioned by a hand-tuned
    ///     offset from the hand, so it cannot drift when the grip or stance changes.
    /// </summary>
    internal static class BladeGlow
    {
        private const string ObjectName = "GungnirBladeGlow";

        private static ParticleSystem _system;
        private static ParticleSystem _smoke;
        private static Transform _builtFor;
        private static int _rebuilds;
        private static Material _material;
        private static Color _applied = Color.clear;

        /// <summary>Creates, tints and shows or hides the glow for the current stance.</summary>
        internal static void Apply(GameObject weapon, Transform visual, bool staffStance)
        {
            bool show;
            switch (ModConfig.WeaponEffect.Value)
            {
                case WeaponEffectMode.Always:
                    show = true;
                    break;
                case WeaponEffectMode.Never:
                    show = false;
                    break;
                default:
                    show = !staffStance;
                    break;
            }

            if (ModConfig.BladeGlowStyle.Value == GungnirStaff.BladeGlowStyle.None)
            {
                show = false;
            }

            var blade = visual != null ? FindBlade(visual) : null;
            if (!show || blade == null)
            {
                Clear();
                return;
            }

            // Rebuild only when it is genuinely gone or the blade changed.
            //
            // The previous test asked whether the effect was still a child of the blade,
            // which went false for a frame whenever Unity's deferred Destroy had run but
            // the reference had not been cleared - so it tore the effect down and rebuilt
            // it continuously. That churn was the flicker: one renderer appearing and
            // disappearing every frame.
            var alive = _mounted != null || _system != null;
            if (!alive || !ReferenceEquals(_builtFor, blade.transform))
            {
                Clear();
                Build(blade, weapon, visual);
                _builtFor = blade.transform;

                _rebuilds++;
                if (_rebuilds == 20)
                {
                    GungnirStaffPlugin.Log.LogWarning(
                        "Blade glow has rebuilt 20 times - something is still tearing it "
                        + "down each frame.");
                }
            }

            // A mounted vanilla effect brings its own colours; only our own particles
            // need tinting.
            if (_system == null)
            {
                return;
            }

            var colour = ModConfig.ParseColour(ModConfig.BladeGlowColour.Value,
                new Color(0.5f, 0.78f, 1f));

            if (_applied != colour)
            {
                _applied = colour;
                Tint(colour);
            }
        }

        internal static void Clear()
        {
            if (_system != null)
            {
                Object.Destroy(_system.gameObject);
            }

            if (_smoke != null)
            {
                Object.Destroy(_smoke.gameObject);
            }

            _smoke = null;

            if (_mounted != null)
            {
                Object.Destroy(_mounted);
            }

            _mounted = null;
            _system = null;
            _builtFor = null;
            _material = null;
            _applied = Color.clear;
        }

        private static void Build(Renderer blade, GameObject weapon, Transform visual)
        {
            try
            {
                // Prefer one of the game's own lightning effects: it already looks like
                // Valheim, is lit and timed like Valheim, and needs no tuning from me.
                if (TryMountVanillaEffect(blade))
                {
                    return;
                }

                _material = BorrowParticleMaterial(weapon);

                var go = new GameObject(ObjectName);
                go.transform.SetParent(visual, false);

                // The blade mesh's origin is not at the blade - it is at the model origin,
                // same as the crystal - so anchor on the mesh's own bounds centre, then
                // slide up toward the point.
                go.transform.localPosition = EmitterAnchor(blade, visual);
                // The model root's +Y already runs to the point, and a cone emits along
                // its own +Z, so this one turn aims the spray up the blade.
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                _system = go.AddComponent<ParticleSystem>();
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.material = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (ModConfig.BladeGlowStyle.Value == GungnirStaff.BladeGlowStyle.Lightning)
                {
                    ConfigureLightning(_system, renderer, visual);
                    BuildOrbit(blade, visual);
                }
                else
                {
                    Configure(_system);
                }

                ModConfig.Trace($"Blade glow created ({ModConfig.BladeGlowStyle.Value}).");
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError($"Could not build the blade glow: {ex}");
                Clear();
            }
        }

        private static void Configure(ParticleSystem ps)
        {
            ps.Stop();

            var scale = ModConfig.BladeGlowScale.Value;

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f * scale, 0.07f * scale);
            main.maxParticles = 40;
            main.gravityModifier = -0.01f;
            // Local, so the glow rides the blade through a thrust instead of smearing.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 18f * ModConfig.BladeGlowRate.Value;

            // A stretched sphere hugs the blade's shape rather than balling up at its centre.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f * scale;
            shape.scale = new Vector3(0.5f, 2.4f, 0.5f);
            shape.radiusThickness = 0.7f;

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
                    new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.4f, 1f),
                                       new Keyframe(1f, 0f)));

            ps.Play();
        }


        /// <summary>
        ///     Crackling streaks along the blade.
        ///
        ///     Reads as lightning rather than glow through three things: stretched
        ///     billboards so each particle is a streak and not a dot, a very short
        ///     lifetime so they snap in and out instead of drifting, and bursty emission
        ///     so it crackles unevenly rather than humming at a constant rate.
        /// </summary>
        private static void ConfigureLightning(
            ParticleSystem ps, ParticleSystemRenderer renderer, Transform visual)
        {
            ps.Stop();

            var scale = ModConfig.BladeGlowScale.Value;

            // Stretched along its own velocity: this is what turns a dot into an arc.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3.5f;

            var main = ps.main;
            main.loop = true;
            // Longer-lived and faster than before, so a spark carries well past the
            // point rather than dying at the blade.
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f * scale, 0.07f * scale);
            main.maxParticles = 45;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            // Low steady rate plus irregular bursts: a constant stream reads as a jet,
            // while bursts read as arcing.
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 4.5f * ModConfig.BladeGlowRate.Value;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 2, 4, 0, 0.26f),
                new ParticleSystem.Burst(0.13f, 1, 3, 0, 0.42f),
            });

            // A narrow cone firing along the blade rather than a sphere spraying
            // everywhere: the arcs now travel out past the point instead of hovering
            // around the middle of the mesh.
            //

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 34f;
            shape.radius = 0.05f * scale;
            shape.radiusThickness = 1f;

            // Hard on, hard off - lightning does not fade politely.
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
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            // A little rotation stops every arc looking like the same sprite.
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            ps.Play();
        }


        /// <summary>
        ///     Motes orbiting the blade, mirroring the crystal's shoal.
        ///
        ///     Replaces the smoke, which was never going to work: the game's smoke sprites
        ///     are big soft quads authored to be seen at distance in a plume, and at
        ///     weapon scale they just read as squares with holes. Orbiting points sit at
        ///     this size honestly, and echo the crystal so the weapon looks like one
        ///     object rather than two effects bolted together.
        /// </summary>
        private static void BuildOrbit(Renderer blade, Transform visual)
        {
            var scale = ModConfig.BladeGlowScale.Value;

            var go = new GameObject(ObjectName + "Orbit");
            go.transform.SetParent(visual, false);
            go.transform.localPosition = EmitterAnchor(blade, visual);

            _smoke = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _smoke.Stop();

            var main = _smoke.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f * scale, 0.026f * scale);
            main.maxParticles = 40;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = _smoke.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f * ModConfig.BladeGlowRate.Value;

            var shape = _smoke.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.10f * scale;
            shape.radiusThickness = 0.4f;

            // Orbital velocity on all three axes is what makes them circle the blade
            // rather than drift; randomising each keeps it from looking like a flat ring.
            var vel = _smoke.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalX = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
            vel.radial = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

            var col = _smoke.colorOverLifetime;
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
                    new GradientAlphaKey(0.8f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = _smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.35f, 1f),
                                       new Keyframe(1f, 0.2f)));

            _smoke.Play();
        }

        /// <summary>
        ///     Where the emitters sit: the blade's centre, slid up toward the point.
        /// </summary>
        private static Vector3 EmitterAnchor(Renderer blade, Transform visual)
        {
            // Anchored on the model root, which is unscaled, so this is plain metres -
            // unlike the blade mesh, whose transform carries a 100x scale from the import
            // and turned every offset into a hundred times what it said.
            var centre = visual.InverseTransformPoint(blade.bounds.center);
            var towardsTip = Vector3.up;

            if (!_loggedAnchor)
            {
                _loggedAnchor = true;
                GungnirStaffPlugin.Log.LogWarning(
                    $"EMITTER visualScale={visual.lossyScale.ToString("F2")} "
                    + $"bladeScale={blade.transform.lossyScale.ToString("F2")} "
                    + $"localCentre={centre.ToString("F2")}");
            }

            // Negative: down the shaft from the blade's middle, toward the socket.
            return centre + towardsTip * ModConfig.BladeGlowOffset.Value;
        }

        /// <summary>
        ///     A real smoke material from the game.
        ///
        ///     Our own borrowed material is a hard-edged additive sprite, which as smoke
        ///     just reads as rotating squares. Valheim's smoke systems use a soft, faded
        ///     texture authored for exactly this - reusing it is the difference between a
        ///     cloud and a pile of quads.
        /// </summary>
        private static Material BorrowSmokeMaterial()
        {
            if (_smokeMaterial != null || _smokeSearched)
            {
                return _smokeMaterial;
            }

            _smokeSearched = true;

            foreach (var name in new[]
                     {
                         "SmokeParticleSystem", "RisingSmoke", "SmokeBlob", "SmokeBall",
                         "vfx_Smoke",
                     })
            {
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                if (prefab == null)
                {
                    prefab = Jotunn.Managers.PrefabManager.Instance.GetPrefab(name);
                }

                var mat = prefab == null
                    ? null
                    : prefab.GetComponentsInChildren<ParticleSystemRenderer>(true)
                        .Select(r => r.sharedMaterial)
                        .FirstOrDefault(m => m != null && m.shader != null);

                if (mat != null)
                {
                    _smokeMaterial = new Material(mat);
                    ModConfig.Trace($"Smoke using material from '{name}'.");
                    return _smokeMaterial;
                }
            }

            ModConfig.Trace("No vanilla smoke material found; falling back to the spark sprite.");
            return null;
        }

        private static bool _loggedAnchor;

        private static Material _smokeMaterial;
        private static bool _smokeSearched;


        /// <summary>
        ///     Cancels the inherited scale on an emitter.
        ///
        ///     The blade's transform carries a 100x scale from the model import, and a
        ///     particle system inherits that: sizes, speeds and offsets all came out a
        ///     hundred times larger than the numbers written here. That is why the sparks
        ///     were enormous and why nudging the emitter "8 cm" up the blade actually
        ///     moved it 8 metres and made the effect vanish. Neutralising it means every
        ///     value below is plain metres.
        /// </summary>
        private static void NeutraliseScale(Transform emitter)
        {
            var parentScale = emitter.parent != null ? emitter.parent.lossyScale : Vector3.one;
            emitter.localScale = new Vector3(
                Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
                Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
                Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);
        }

        private static void Tint(Color colour)
        {
            var hot = colour * ModConfig.BladeGlowBrightness.Value;
            hot.a = 1f;

            var main = _system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(hot, colour);

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
        ///     Mounts a vanilla effect prefab on the blade.
        ///
        ///     Hand-built particles will never quite match the game's look, and the effect
        ///     this replaced was vanilla's own. Instantiating the real thing keeps it
        ///     consistent and means the mod is not maintaining an imitation.
        /// </summary>
        private static bool TryMountVanillaEffect(Renderer blade)
        {
            var wanted = ModConfig.BladeGlowPrefab.Value;
            if (string.IsNullOrEmpty(wanted))
            {
                return false;
            }

            var prefab = FindEffectPrefab(wanted);
            if (prefab == null)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    $"Effect prefab '{wanted}' not found; using the mod's own particles. "
                    + "Try one of the names listed in the setting's description.");
                return false;
            }

            var mounted = Object.Instantiate(prefab, blade.transform);
            mounted.name = ObjectName;
            mounted.transform.localPosition =
                blade.transform.InverseTransformPoint(blade.bounds.center);
            mounted.transform.localRotation = Quaternion.identity;
            mounted.transform.localScale = Vector3.one * ModConfig.BladeGlowScale.Value;

            // A world effect can carry components that expect to be a standalone spawn -
            // networked, self-destructing, audible. Strip those so it behaves as decoration
            // welded to a weapon.
            foreach (var znet in mounted.GetComponentsInChildren<ZNetView>(true))
            {
                Object.Destroy(znet);
            }

            foreach (var timed in mounted.GetComponentsInChildren<TimedDestruction>(true))
            {
                Object.Destroy(timed);
            }

            foreach (var audio in mounted.GetComponentsInChildren<AudioSource>(true))
            {
                audio.enabled = false;
            }

            _mounted = mounted;
            ModConfig.Trace($"Mounted vanilla effect '{prefab.name}' on the blade.");
            return true;
        }

        private static GameObject FindEffectPrefab(string name)
        {
            var scene = ZNetScene.instance;
            if (scene != null)
            {
                var found = scene.GetPrefab(name);
                if (found != null)
                {
                    return found;
                }
            }

            return Jotunn.Managers.PrefabManager.Instance.GetPrefab(name);
        }

        private static GameObject _mounted;

        private static Renderer FindBlade(Transform visual)
        {
            return visual.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => r.gameObject.name.StartsWith(
                    "Gungnir_Blade", System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     A particle material the game already ships. Authoring one against an editor
        ///     shader would render magenta, same as the model's materials.
        /// </summary>
        private static Material BorrowParticleMaterial(GameObject weapon)
        {
            var donor = weapon == null
                ? null
                : weapon.GetComponentsInChildren<ParticleSystemRenderer>(true)
                    .Select(r => r.sharedMaterial)
                    .FirstOrDefault(m => m != null && m.shader != null);

            if (donor == null && ObjectDB.instance != null)
            {
                // Standalone: nothing on our own weapon to borrow from, so take one off
                // any vanilla item that has particles.
                donor = ObjectDB.instance.m_items
                    .Where(p => p != null)
                    .SelectMany(p => p.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    .Select(r => r.sharedMaterial)
                    .FirstOrDefault(m => m != null && m.shader != null);
            }

            if (donor != null)
            {
                return new Material(donor);
            }

            var shader = Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) : null;
        }
    }
}
