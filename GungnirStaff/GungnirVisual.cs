using System.Linq;

using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     Swaps the custom Gungnir model onto the item.
    ///
    ///     Only the *visual* comes from the AssetBundle. The item itself stays a runtime
    ///     clone of a vanilla spear, so all of Valheim's own behaviour - colliders,
    ///     attacks, animations, throwing - keeps working untouched, and the bundle needs
    ///     no Valheim components in it. That also means the bundle can be rebuilt without
    ///     touching any of the mod's logic.
    /// </summary>
    internal static class GungnirVisual
    {
        internal const string BundleName = "gungnir";
        internal const string PrefabName = "GungnirVisual";
        internal const string IconName = "GungnirIcon";

        private static AssetBundle _bundle;
        private static GameObject _visualPrefab;
        private static bool _failed;

        /// <summary>The loaded visual prefab, or null if the bundle could not be read.</summary>
        internal static GameObject Prefab
        {
            get
            {
                if (_visualPrefab != null || _failed)
                {
                    return _visualPrefab;
                }

                try
                {
                    _bundle = _bundle ?? LoadBundle();
                    if (_bundle == null)
                    {
                        _failed = true;
                        return null;
                    }

                    _visualPrefab = _bundle.LoadAsset<GameObject>(PrefabName);
                    if (_visualPrefab == null)
                    {
                        GungnirStaffPlugin.Log.LogError(
                            $"'{PrefabName}' not found in the bundle. Contents: "
                            + string.Join(", ", _bundle.GetAllAssetNames()));
                        _failed = true;
                    }
                }
                catch (System.Exception ex)
                {
                    GungnirStaffPlugin.Log.LogError($"Could not load the Gungnir bundle: {ex}");
                    _failed = true;
                }

                return _visualPrefab;
            }
        }

        /// <summary>
        ///     Reads the embedded bundle into memory and loads it.
        ///
        ///     Deliberately not via a stream: Unity reads an AssetBundle lazily, so a
        ///     resource stream that gets disposed once loading "finishes" leaves every
        ///     later LoadAsset failing with "stream.CanRead must return true".
        ///     LoadFromMemory takes its own copy and sidesteps that entirely.
        /// </summary>
        private static AssetBundle LoadBundle()
        {
            // A hot reload gives us fresh statics but Unity still holds the bundle from
            // the previous load, and loading the same files twice throws. Reuse a healthy
            // one; evict a broken one.
            //
            // "Broken" is a real state, not a theoretical one: a bundle opened from a
            // stream that was later disposed stays loaded but every LoadAsset on it
            // throws "stream.CanRead must return true" forever. Probing it is the only
            // way to tell the two apart.
            foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles().ToList())
            {
                if (loaded == null)
                {
                    continue;
                }

                bool mine;
                try
                {
                    mine = loaded.GetAllAssetNames()
                        .Any(n => n.IndexOf(PrefabName, System.StringComparison.OrdinalIgnoreCase) >= 0);
                }
                catch (System.Exception ex)
                {
                    // A bundle that cannot even list its contents is already unusable to
                    // whoever owns it, and while it sits there it holds the internal CAB
                    // id that our reload needs. Evicting it costs nothing and is the only
                    // way to free that id short of restarting the game.
                    GungnirStaffPlugin.Log.LogWarning(
                        $"Evicting an unreadable AssetBundle that is blocking the reload: {ex.Message}");
                    loaded.Unload(false);
                    continue;
                }

                if (!mine)
                {
                    continue;
                }

                try
                {
                    if (loaded.LoadAsset<GameObject>(PrefabName) != null)
                    {
                        ModConfig.Trace("Reusing the already-loaded Gungnir bundle.");
                        return loaded;
                    }
                }
                catch (System.Exception ex)
                {
                    GungnirStaffPlugin.Log.LogWarning(
                        $"Evicting an unreadable Gungnir bundle before reloading: {ex.Message}");
                }

                // false: keep objects already instantiated from it, so hiding the donor
                // renderers and any live visual survive the swap.
                loaded.Unload(false);
            }

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(BundleName, System.StringComparison.OrdinalIgnoreCase));

            if (name == null)
            {
                GungnirStaffPlugin.Log.LogError(
                    $"No embedded resource ending in '{BundleName}'. Embedded: "
                    + string.Join(", ", asm.GetManifestResourceNames()));
                return null;
            }

            using (var stream = asm.GetManifestResourceStream(name))
            {
                if (stream == null)
                {
                    GungnirStaffPlugin.Log.LogError($"Embedded resource '{name}' could not be opened.");
                    return null;
                }

                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0)
                    {
                        break;
                    }

                    read += n;
                }

                ModConfig.Trace($"Read {read} bytes of bundle from '{name}'.");
                return AssetBundle.LoadFromMemory(bytes);
            }
        }

        /// <summary>
        ///     Replaces the held model on the Gungnir prefab with ours.
        ///     Safe to call repeatedly; a second call is a no-op.
        /// </summary>
        internal static bool Apply(GameObject itemPrefab)
        {
            if (itemPrefab == null || Prefab == null)
            {
                return false;
            }

            var attach = FindAttach(itemPrefab);
            if (attach == null)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "No 'attach' child on the Gungnir prefab; leaving the placeholder model. "
                    + "Hierarchy: " + Describe(itemPrefab.transform, 0));
                return false;
            }

            var already = attach.Find(PrefabName);
            if (already != null)
            {
                return true; // already swapped
            }

            // Hide the donor spear's meshes rather than destroying them: the vanilla
            // attach can carry colliders and effects that the animations rely on.
            var hidden = 0;
            foreach (var r in attach.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                {
                    continue;
                }

                r.enabled = false;
                hidden++;
            }

            var visual = Object.Instantiate(Prefab, attach, false);
            visual.name = PrefabName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            AdoptVanillaShaders(visual, itemPrefab);

            ModConfig.Trace(
                $"Custom Gungnir model attached ({hidden} donor renderer(s) hidden).");
            return true;
        }

        /// <summary>
        ///     Re-points our materials at a shader that actually exists in the game.
        ///
        ///     The bundle was authored in the editor against Unity's Standard shader,
        ///     which Valheim does not ship - left alone, every part renders magenta.
        ///     Borrowing the shader off the donor spear's own material guarantees a
        ///     shader the game has, already set up for its lighting.
        /// </summary>
        private static void AdoptVanillaShaders(GameObject visual, GameObject donor)
        {
            var donorMat = donor.GetComponentsInChildren<Renderer>(true)
                .Where(r => !(r is ParticleSystemRenderer))
                .SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m != null && m.shader != null);

            if (donorMat == null)
            {
                GungnirStaffPlugin.Log.LogWarning(
                    "No donor material to borrow a shader from; the model may render untextured.");
                return;
            }

            var shader = donorMat.shader;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.materials)
                {
                    if (m == null || m.shader == shader)
                    {
                        continue;
                    }

                    // Carry the authored look across by hand: swapping the shader resets
                    // any property the new shader does not share.
                    var colour = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    var emission = m.HasProperty("_EmissionColor")
                        ? m.GetColor("_EmissionColor")
                        : Color.black;

                    m.shader = shader;

                    if (m.HasProperty("_Color"))
                    {
                        m.SetColor("_Color", colour);
                    }

                    if (emission.maxColorComponent > 0f && m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", emission);
                    }
                }
            }

            ModConfig.Trace($"Adopted vanilla shader '{shader.name}' for the Gungnir model.");
        }

        /// <summary>
        ///     Points the right end of the spear forwards for the current stance.
        ///
        ///     Two stances, opposite ends leading: blade forward when Gungnir is being
        ///     used as a spear, crystal forward when a staff is selected and the player
        ///     holds it like a caster. Applied to the instance in the hand rather than to
        ///     the prefab, because the stance changes while the item stays equipped.
        /// </summary>
        internal static void Orient(Player player, ItemDrop.ItemData gungnir)
        {
            var vis = player?.m_visEquipment;
            if (vis == null)
            {
                return;
            }

            // Done first: when the weapon is sheathed there is no held instance at all,
            // so anything after the early-return below would never run for it.
            OrientBack(vis);

            var held = PickHeldInstance(vis);
            if (held == null)
            {
                return;
            }

            var visual = FindVisual(held.transform);
            if (visual == null)
            {
                return;
            }

            var container = StaffContainer.For(gungnir);
            var staffStance = (container?.SelectedSlot ?? -1) >= 0;

            var offset = ParseVector(ModConfig.ModelOffset.Value);
            var scale = Vector3.one * ModConfig.ModelScale.Value;


            // The ATTACH is left exactly as vanilla built it. Valheim casts from the
            // attach's forward, so turning it end-for-end fired spells out of the spear
            // head instead of the crystal. Leaving it alone means the spell always leaves
            // whichever end is leading - the crystal in caster stance.
            var attachRotation = BaseAttachRotation();
            if (held.transform.localRotation != attachRotation)
            {
                held.transform.localRotation = attachRotation;
            }

            // The MODEL carries both the mesh's axis correction and the stance flip, so
            // it ends up in the same place it did when the attach was doing the turning.
            var modelRotation = Quaternion.Euler(ParseVector(
                staffStance ? ModConfig.RotationStaff.Value : ModConfig.RotationSpear.Value));
            if (visual.localRotation != modelRotation)
            {
                visual.localRotation = modelRotation;
            }

            DiagnoseFlicker(player, visual);

            // Which end actually leads, measured rather than reasoned about: project the
            // blade's offset from the grip onto the player's facing. Positive means the
            // blade is in front.
            if (staffStance != _lastStanceLogged)
            {
                _lastStanceLogged = staffStance;
                var blade = visual.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(r => r.gameObject.name.StartsWith(
                        "Gungnir_Blade", System.StringComparison.OrdinalIgnoreCase));
                var lead = blade == null
                    ? 0f
                    : Vector3.Dot((blade.bounds.center - held.transform.position).normalized,
                        player.transform.forward);

                var hand = player.m_visEquipment.m_rightItemInstance != null ? "right" : "left";
                ModConfig.Trace(
                    $"Stance={(staffStance ? "STAFF" : "SPEAR")} hand={hand} "
                    + $"visualLocalEuler={visual.localEulerAngles} "
                    + $"bladeLeads={lead:F2} (positive = blade in front)");
            }

            ApplyGrip(visual);

            // The DECORATIVE VFX ride with the model, not with the cast direction, so the
            // glow stays welded to the blade in both stances.
            OrientEffects(held.transform, visual, staffStance);

            // Standalone has no donor particle system to place, so it grows its own on
            // the blade instead.
            if (ModConfig.StandalonePrefab.Value)
            {
                BladeGlow.Apply(held, visual, staffStance);
            }

            if (visual.localPosition != offset)
            {
                visual.localPosition = offset;
            }

            if (visual.localScale != scale)
            {
                visual.localScale = scale;
            }
        }

        /// <summary>
        ///     Vanilla's untouched hand rotation, read from the PREFAB rather than from
        ///     the instance in hand.
        ///
        ///     Reading it off the live instance looked equivalent and was not: a hot
        ///     reload resets our statics while the item stays equipped, so we would
        ///     re-capture a transform we had already rotated and fold our own stance angle
        ///     into the "base" - compounding a little further with every reload until the
        ///     spear was pointing backwards. The prefab is never modified, so it stays
        ///     pristine.
        /// </summary>

        /// <summary>
        ///     The long axis of the donor spear's hidden mesh, in the attach's own space.
        ///
        ///     Our model runs along its local +Y, so rotating that onto this axis lays the
        ///     shaft exactly where a vanilla spear's shaft lies. Uses the mesh's own bounds
        ///     rather than world bounds, which would be axis-aligned and useless here.
        /// </summary>
        private static bool TryDonorLongAxis(Transform attach, Transform visual, out Vector3 axis)
        {
            axis = Vector3.up;

            // Clone path: the hidden donor mesh is right here inside our own attach.
            var donor = attach.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(mf => mf.sharedMesh != null
                                      && !mf.transform.IsChildOf(visual));

            // Standalone path: there is no donor inside us, so read the angle off a
            // vanilla spear prefab in ObjectDB instead. Its attach is positioned by the
            // same bone, so the axis measured in ITS attach space transfers directly to
            // ours. Referenced, never cloned - only the transform is measured.
            if (donor == null)
            {
                var reference = GungnirStandalone.FindReferenceSpear(ObjectDB.instance);
                var referenceAttach = reference != null ? FindAttach(reference) : null;
                if (referenceAttach == null)
                {
                    return false;
                }

                var referenceMesh = referenceAttach.GetComponentsInChildren<MeshFilter>(true)
                    .FirstOrDefault(mf => mf.sharedMesh != null);
                if (referenceMesh == null)
                {
                    return false;
                }

                if (!LongAxisOf(referenceMesh, referenceAttach, out axis))
                {
                    return false;
                }

                if (!_loggedBackAxis)
                {
                    _loggedBackAxis = true;
                    ModConfig.Trace($"Back axis taken from reference spear '{reference.name}'.");
                }

                return true;
            }

            return LongAxisOf(donor, attach, out axis);
        }

        /// <summary>
        ///     The mesh's longest local axis, expressed in the given attach's space.
        ///     Uses mesh bounds, not renderer bounds: the latter are axis-aligned in world
        ///     space and would give a meaningless direction.
        /// </summary>
        private static bool LongAxisOf(MeshFilter mesh, Transform attach, out Vector3 axis)
        {
            axis = Vector3.up;

            var size = mesh.sharedMesh.bounds.size;
            Vector3 local;
            if (size.x >= size.y && size.x >= size.z)
            {
                local = Vector3.right;
            }
            else if (size.y >= size.z)
            {
                local = Vector3.up;
            }
            else
            {
                local = Vector3.forward;
            }

            // A shaft has to be clearly longer than it is thick, or "longest axis" is noise.
            var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var others = size.x + size.y + size.z - longest;
            if (longest < others * 0.8f)
            {
                return false;
            }

            var world = mesh.transform.TransformDirection(local);
            axis = attach.InverseTransformDirection(world).normalized;
            return axis.sqrMagnitude > 0.001f;
        }


        /// <summary>
        ///     The hand instance that actually holds our mesh.
        ///
        ///     Selecting the right hand first looked equivalent and was not: projecting a
        ///     staff makes the item two-handed-left, so Valheim moves it to the other
        ///     hand - but the right-hand instance can still be alive that frame. Taking it
        ///     blindly oriented an empty leftover while the real model kept the melee
        ///     angle, which is exactly the "staff stance looks like melee" symptom.
        /// </summary>
        private static GameObject PickHeldInstance(VisEquipment vis)
        {
            var right = vis.m_rightItemInstance;
            if (right != null && FindVisual(right.transform) != null)
            {
                return right;
            }

            var left = vis.m_leftItemInstance;
            if (left != null && FindVisual(left.transform) != null)
            {
                return left;
            }

            return right ?? left;
        }

        private static Transform FindVisual(Transform root)
        {
            return root.Find(PrefabName)
                   ?? root.GetComponentsInChildren<Transform>(true)
                       .FirstOrDefault(t => t.name == PrefabName);
        }

        /// <summary>
        ///     Orients the sheathed copy slung on the player's back.
        ///
        ///     Vanilla already places and angles the sheathed weapon, so its transform is
        ///     left alone - only our mesh needs correcting inside it. That correction is
        ///     its own setting because the back bone's frame matches neither hand, the
        ///     same reason the two stances need separate angles.
        /// </summary>
        private static void OrientBack(VisEquipment vis)
        {
            var back = vis.m_rightBackItemInstance ?? vis.m_leftBackItemInstance;
            if (back == null)
            {
                if (!_loggedBack)
                {
                    _loggedBack = true;
                    ModConfig.Trace("Sheathed: no back instance on VisEquipment.");
                }

                return;
            }

            var visual = FindVisual(back.transform);
            if (visual == null)
            {
                if (!_loggedBack)
                {
                    _loggedBack = true;
                    GungnirStaffPlugin.Log.LogWarning(
                        $"Sheathed model '{back.name}' has no {PrefabName} child - the back "
                        + "copy is built from a different attach than the hand. Children: "
                        + string.Join(", ", back.GetComponentsInChildren<Transform>(true)
                            .Select(t => t.name).Take(12)));
                }

                return;
            }

            if (!_loggedBack)
            {
                _loggedBack = true;
                ModConfig.Trace($"Sheathed model found under '{back.name}'.");
            }

            var offset = Quaternion.Euler(ParseVector(ModConfig.RotationBack.Value));

            // Take the angle from the game instead of guessing it: the donor spear's mesh
            // is still inside this same attach, hidden but correctly angled for the back.
            // Aligning our shaft to its long axis makes the sheathed Gungnir sit exactly
            // as a vanilla spear does, at any character size or animation.
            var flip = offset;
            Vector3 donorAxis;
            if (ModConfig.AlignBackToVanilla.Value
                && TryDonorLongAxis(back.transform, visual, out donorAxis))
            {
                flip = Quaternion.FromToRotation(Vector3.up, donorAxis) * offset;
            }

            if (visual.localRotation != flip)
            {
                visual.localRotation = flip;
            }

            // Slide along the shaft's own direction rather than any world axis, so the
            // model rides up or down the back no matter what angle it ended up at.
            var alongShaft = flip * Vector3.up;
            var slide = alongShaft * ModConfig.BackSlide.Value;
            if (visual.localPosition != slide)
            {
                visual.localPosition = slide;
            }

            var scale = Vector3.one * ModConfig.ModelScale.Value;
            if (visual.localScale != scale)
            {
                visual.localScale = scale;
            }

            // Deliberately NOT ApplyGrip. The grip setting positions the shaft in the
            // hand; applying it here slid the sheathed model up so it hung off the blade.
            // Vanilla's own placement on the back is already right, so the mesh keeps the
            // prefab's own offset and only the flip is applied.

            // The effect is deliberately left at vanilla's own placement here, NOT turned
            // with the mesh. Turning it swung it round behind the player: vanilla already
            // positions this effect for a sheathed spear, and now that our shaft is
            // aligned to that same spear's axis, its anchor lands on our blade too.
            // Restored explicitly rather than just skipped, so a reload cannot leave it
            // wherever an earlier build put it.
            var prefabAttach = GungnirItem.Prefab != null ? FindAttach(GungnirItem.Prefab) : null;
            foreach (Transform child in back.transform)
            {
                if (child == visual || child.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    continue;
                }

                var pristine = prefabAttach != null ? prefabAttach.Find(child.name) : null;
                if (pristine == null)
                {
                    continue;
                }

                // Same slide as the mesh, so the glow stays on the blade.
                var wanted = pristine.localPosition + slide;
                if (child.localPosition != wanted)
                {
                    child.localPosition = wanted;
                }

                if (child.localRotation != pristine.localRotation)
                {
                    child.localRotation = pristine.localRotation;
                }
            }
        }

        /// <summary>The grip baked into the prefab, which GripHeight is measured against.</summary>
        internal const float PrefabGripHeight = 0.95f;

        /// <summary>
        ///     Slides the mesh inside its holder so the hand closes at the configured
        ///     point on the shaft. Gripping lower down swings the head higher, which also
        ///     lifts the blade clear of the ground in spear stance.
        /// </summary>
        private static void ApplyGrip(Transform visual)
        {
            var model = visual.Find("Model");
            if (model == null)
            {
                return;
            }

            var wanted = new Vector3(0f, -ModConfig.GripHeight.Value, 0f);
            if (model.localPosition != wanted)
            {
                model.localPosition = wanted;
            }
        }


        /// <summary>
        ///     The inventory icon from the bundle, or null if it is missing.
        ///     Cached: this is asked for on every prefab refresh.
        /// </summary>
        internal static Sprite Icon
        {
            get
            {
                if (_icon != null || _iconMissing)
                {
                    return _icon;
                }

                if (Prefab == null || _bundle == null)
                {
                    return null;
                }

                _icon = _bundle.LoadAsset<Sprite>(IconName);
                if (_icon == null)
                {
                    _iconMissing = true;
                    GungnirStaffPlugin.Log.LogWarning(
                        $"'{IconName}' not found in the bundle; the item keeps the donor icon.");
                }

                return _icon;
            }
        }

        private static Sprite _icon;
        private static bool _iconMissing;


        /// <summary>
        ///     Forces the bundle to load now rather than on first use.
        ///
        ///     AssetBundle.LoadFromMemory is synchronous and decompresses ~140 KB, and the
        ///     prefab was only touched when the item was first created - so the cost landed
        ///     as a multi-second freeze mid-game, on whatever frame the player first
        ///     obtained a Gungnir. Paying it during load puts the hitch where hitches are
        ///     expected.
        /// </summary>
        internal static void Preload()
        {
            if (_visualPrefab != null || _failed)
            {
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var loaded = Prefab != null;
            watch.Stop();

            if (loaded)
            {
                ModConfig.Trace($"Bundle preloaded in {watch.ElapsedMilliseconds} ms.");
            }
        }


        private static int _lastVisualCount = -1;
        private static int _lastEnabledRenderers = -1;

        /// <summary>
        ///     Reports how many copies of our mesh exist on the player and how many of
        ///     their renderers are on.
        ///
        ///     Flicker is either two copies fighting over the same depth, or renderers
        ///     being toggled every frame. These two numbers separate those cases, and
        ///     only log when they change, so this is cheap and quiet.
        /// </summary>
        private static void DiagnoseFlicker(Player player, Transform visual)
        {
            if (!ModConfig.VerboseLogging.Value)
            {
                return;
            }

            var copies = 0;
            var enabled = 0;
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != PrefabName)
                {
                    continue;
                }

                copies++;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled)
                    {
                        enabled++;
                    }
                }
            }

            if (copies == _lastVisualCount && enabled == _lastEnabledRenderers)
            {
                return;
            }

            _lastVisualCount = copies;
            _lastEnabledRenderers = enabled;
            GungnirStaffPlugin.Log.LogWarning(
                $"FLICKER copies of the model on the player={copies} "
                + $"enabledRenderers={enabled} "
                + $"(right={(player.m_visEquipment.m_rightItemInstance != null)} "
                + $"left={(player.m_visEquipment.m_leftItemInstance != null)} "
                + $"back={(player.m_visEquipment.m_rightBackItemInstance != null)})");
        }

        private static bool _loggedBackAxis;

        private static bool _loggedBack;

        private static bool _lastStanceLogged;

        private static Quaternion BaseAttachRotation()
        {
            var prefab = GungnirItem.Prefab;
            var attach = prefab != null ? FindAttach(prefab) : null;
            return attach != null ? attach.localRotation : Quaternion.identity;
        }

        /// <summary>
        ///     Applies the weapon-glow settings.
        ///
        ///     At the defaults this leaves the effect exactly where vanilla put it. Two
        ///     earlier attempts to place it automatically both failed, and for different
        ///     reasons worth remembering: rotating it swung it around the grip in an arc
        ///     instead of flipping it in place, and pinning it to the blade fought the
        ///     stance flip. So the mod no longer decides - it only offers the offsets.
        ///
        ///     Everything is measured from the PREFAB's pristine transform, never from
        ///     the live one, so repeated calls and hot reloads cannot accumulate drift.
        ///
        ///     Only objects carrying a ParticleSystem are touched: the attach also holds
        ///     the transform Valheim casts from, and moving that sends spells out of the
        ///     wrong end.
        /// </summary>
        private static void OrientEffects(
            Transform attachInstance, Transform visual, bool staffStance)
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
            var extraRotation = Quaternion.Euler(ParseVector(ModConfig.WeaponEffectRotation.Value));
            var extraDistance = ModConfig.WeaponEffectDistance.Value;
            var prefabAttach = GungnirItem.Prefab != null ? FindAttach(GungnirItem.Prefab) : null;

            foreach (Transform child in attachInstance)
            {
                if (child == visual || child.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    continue;
                }

                if (child.gameObject.activeSelf != show)
                {
                    child.gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                var pristine = prefabAttach != null ? prefabAttach.Find(child.name) : null;
                var basePosition = pristine != null ? pristine.localPosition : Vector3.zero;
                var baseRotation = pristine != null ? pristine.localRotation : Quaternion.identity;

                // Push along whatever direction the effect already sits in. If vanilla
                // parks it right on the origin there is no such direction, so fall back to
                // the model's own long axis and the slider still does something useful.
                var axis = basePosition.sqrMagnitude > 1e-6f
                    ? basePosition.normalized
                    : attachInstance.InverseTransformDirection(
                        visual.TransformDirection(Vector3.up)).normalized;

                // Moving the grip moves the blade relative to the hand, so a distance
                // tuned by eye would slide off it. Compensate by exactly that much, in
                // whichever direction along this axis the blade actually lies, so the
                // tuned value keeps meaning "on the blade" at any grip.
                var towardsBlade = attachInstance.InverseTransformDirection(
                    visual.TransformDirection(Vector3.up)).normalized;
                var sign = Vector3.Dot(axis, towardsBlade) >= 0f ? 1f : -1f;
                var gripShift = PrefabGripHeight - ModConfig.GripHeight.Value;

                var position = basePosition + axis * (extraDistance + sign * gripShift);
                var rotation = baseRotation * extraRotation;

                if (child.localPosition != position)
                {
                    child.localPosition = position;
                }

                if (child.localRotation != rotation)
                {
                    child.localRotation = rotation;
                }
            }
        }

        /// <summary>Parses "x,y,z"; falls back to zero so a typo cannot break the model.</summary>
        internal static Vector3 ParseVector(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return Vector3.zero;
            }

            var parts = raw.Split(',');
            if (parts.Length != 3)
            {
                return Vector3.zero;
            }

            float x = 0f, y = 0f, z = 0f;
            var ok = float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out x)
                     && float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out y)
                     && float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out z);

            return ok ? new Vector3(x, y, z) : Vector3.zero;
        }

        private static Transform FindAttach(GameObject itemPrefab)
        {
            var direct = itemPrefab.transform.Find("attach");
            if (direct != null)
            {
                return direct;
            }

            return itemPrefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.StartsWith("attach", System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Compact hierarchy dump, for when the attach point is not where we expect.</summary>
        internal static string Describe(Transform t, int depth)
        {
            if (depth > 3)
            {
                return string.Empty;
            }

            var line = new string(' ', depth * 2) + t.name;
            foreach (Transform c in t)
            {
                line += "\n" + Describe(c, depth + 1);
            }

            return line;
        }
    }
}
