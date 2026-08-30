# Gungnir Staff

A Valheim mod that adds **Gungnir**, Odin's spear — a Mistlands-tier weapon that
carries other magic staffs inside itself and *becomes* whichever one you select.

Built on **BepInEx 5.4.23.3** + **Jötunn 2.29.2**, targeting `net462` (the framework
Jötunn and Valheim's Unity Mono runtime use).

---

## What the mod does

Gungnir is craftable at an upgraded **Galdr Table** and behaves as a spear in the
hand — full vanilla attacks, throwing, animations and colliders, because the item is
a runtime clone of a vanilla spear with its own model, damage and flavour text
applied on top.

Its own trick is the **staff rack**: a one-row inventory that lives inside the item.
Drop a magic staff into the rack, select the slot, and Gungnir turns into that staff
— its attack, eitr cost, projectile, skill XP, icon and held model all become the
staff's. Holster, and it is a spear again. Each upgrade level adds two rack slots
(2 → 4 → 6 → 8), so the item's quality is what governs how many staffs it carries.

The crystal in the butt of the spear is colour-coded per staff, with a matching light
and a shoal of motes around it, so you can see which staff is loaded at a glance.

---

## Layout

```
GungnirStaff.sln
Directory.Build.props          # all machine paths live here
Environment.props.example      # copy -> Environment.props to override paths
GungnirStaff/
  GungnirStaff.csproj          # references, embedded bundle, deploy targets
  Plugin.cs                    # BepInEx entry point, Harmony patch/unpatch
  ModConfig.cs                 # every user-facing setting
  Inputs.cs                    # rebindable KeyboardShortcuts
  Localizations.cs             # $token translations
  Alive.cs                     # "is the mod alive?" status reporting
  Commands.cs                  # `gungnir` dev console command
  Blueprint.cs                 # one-shot dump of a vanilla prefab's components
  GungnirItem.cs               # the item prefab, cloned from a vanilla spear
  GungnirStandalone.cs         # the same prefab built from nothing, no clone
  GungnirRecipe.cs             # recipe + three upgrade levels
  GungnirVisual.cs             # swaps the custom model in from the AssetBundle
  StaffRegistry.cs             # what counts as a staff
  StaffContainer.cs            # the rack inventory, persisted in m_customData
  StaffSwitcher.cs             # the m_shared projection, plus durability write-back
  GungnirBar.cs                # the rack UI, cloned from vanilla's InventoryGrid
  CrystalColors.cs             # per-staff crystal colour config
  CrystalGlow.cs               # tints the crystal material and casts a light
  CrystalParticles.cs          # code-built motes and sparks around the crystal
  Patches/PlayerPatches.cs         # spawn hook, hotbar suppression
  Patches/InventoryGuiPatches.cs   # Ctrl+click a staff into the rack
  Patches/RecipePatches.cs         # exact per-level upgrade costs
  Assets/gungnir               # the AssetBundle (embedded into the DLL)
art/                           # Blender source, FBX export, icon sources
tools/                         # Dump-GameType.ps1, Verify-Patches.ps1
Thunderstore/manifest.json     # packaging metadata
```

---

## Crafting

Made at the **Galdr Table, level 2** — an upgraded table, unlike the basic staffs.

| Material | Craft | Lv 2 | Lv 3 | Lv 4 |
| --- | --- | --- | --- | --- |
| Yggdrasil wood | 30 | 15 | 25 | 35 |
| Refined eitr | 30 | 20 | 30 | 40 |
| Thunder stone | 3 | 2 | 4 | 6 |
| Black core | 2 | 1 | 2 | 3 |
| Gjall trophy | 1 | — | — | — |
| **Rack slots** | **2** | **4** | **6** | **8** |

Black cores gate it behind actually delving the Infested Mines rather than gathering
on the surface; the Gjall trophy is a mid-Mistlands gate rather than a "finished the
biome" one.

Vanilla computes an upgrade requirement as `m_amount` at level 1 and
`(level - 1) * m_amountPerLevel` above it, a curve that cannot produce the thunder
stone progression 1, 2, 3, 4 at any setting. `RecipePatches.cs` substitutes the exact
numbers, keyed on the requirement *instance*, so no other recipe in the game is
touched.

Material names are resolved against ObjectDB rather than assumed — Valheim's asset
manifest carries both `ThunderStone` and `Thunderstone`, and only one of them is the
item.

---

## Controls

Defaults — every binding is rebindable in Configuration Manager (F1).

| Input | Action |
| --- | --- |
| `Alt` + `1`…`8` | Select that staff (pressing the active one again holsters) |
| `Alt` + `0` | Holster — Gungnir goes back to being a plain spear |
| Right-click a rack slot | Wield that staff |
| Left-click a rack slot | Pick up / drag, exactly like any inventory |
| `Ctrl` + left-click a staff in your inventory | Quick-move it into the rack |
| `F9` | Print the "still alive" status line |

**Alt, not Ctrl**, because Ctrl is Valheim's crouch — `Ctrl+3` also made the character
sneak. Alt is free: vanilla's inventory only reads Shift (split) and Ctrl (quick-move)
as click modifiers. While a slot shortcut fires, the vanilla hotbar is suppressed, so
`Alt+3` selects a staff instead of doing both things.

Vanilla's `Modifier.Move` only knows two destinations — the open container, or the
player — and with no container open it falls through to dropping the item on the
ground. `InventoryGuiPatches.cs` claims that case when a rack is available, which is
what makes Ctrl+click into the rack work.

---

## Configuration

Everything lands in `BepInEx/config/com.samuelhaggren.gungnirstaff.cfg` and is
live-editable in Configuration Manager (F1) under **Gungnir Staff**.

| Section | What's in it |
| --- | --- |
| `General` | `Enabled` master switch, `VerboseLogging` |
| `Staff bar` | `SlotCount` (0 = follow upgrade level), `Activation` (Equipped / Carried), `Visibility` (WhenGungnirActive / InventoryOnly / Always), `PositionX`, `PositionY`, `Scale` |
| `Appearance` | `StandalonePrefab`, `BaseWeaponPrefab`, model offset / scale / grip height, spear and staff stance rotations, back-slot placement, crystal particle mode / rate / scale / brightness, weapon glow effect |
| Crystal colours | One colour per staff, bound lazily from ObjectDB |
| `Keys` | `StatusKey`, `HolsterStaff`, `SelectSlot1`…`SelectSlot8` |

The crystal colour entries are bound one per staff *actually found in ObjectDB*, so
the F1 menu lists exactly the staffs this installation has — including any added by
other mods — rather than a hard-coded set that would go stale. They are stored as hex
strings because BepInEx has no built-in converter for `UnityEngine.Color`, and hex
round-trips cleanly through the config file.

`BaseWeaponPrefab` takes effect on the next game start: the held model comes from the
prefab, which is registered once at load.

### Two ways to build the prefab

By default Gungnir is a runtime clone of a vanilla spear, which hands you working
attacks, animations, colliders and a hundred-odd `SharedData` fields for free.
`StandalonePrefab = true` switches to `GungnirStandalone.cs`, which authors all of
that explicitly instead — from values measured off a real spear rather than guessed
at — so the item depends on no vanilla weapon at all. The attack animations are not a
dependency either way: `spear_poke` is a state in the player's own animator, so
naming it costs nothing. If the standalone build does not complete, the mod falls back
to the clone and says so in the log.

---

## How the staff rack works

**Gungnir does not hold other staffs as items you swap to — it *becomes* them.**

Valheim persists an item's identity through `m_dropPrefab`, but drives all of its
*behaviour* through `m_shared`. Pointing Gungnir's `m_shared` at a stored staff
therefore makes every vanilla system — attack, eitr cost, projectile, skill XP,
left/right/middle click, held model, icon — behave exactly as if that staff were
equipped, with no per-staff code. The item still saves and reloads as a Gungnir
because `Inventory.Save` writes `m_dropPrefab.name`, which never changes.

Storage lives in the item's own `m_customData` dictionary, which vanilla round-trips
for us. The stored staffs survive relogs, follow the item into a chest, and need no
save file of our own.

What counts as a staff is detected from ObjectDB **by skill type**, not from a name
list — so the vanilla staffs are found automatically however many there are, and so
are staffs added by other mods, with no changes here.

The bar is a clone of vanilla's player `InventoryGrid`, so it inherits Valheim's slot
art, tooltips, durability bars, gamepad handling and drag visuals — and vanilla's
`UpdateGui` rebuilds the slots to match whatever inventory is bound, so a one-row grid
needs no layout code. It is parented to the canvas root rather than the inventory
panel, so it can stay on screen while the inventory is closed and still take clicks.

---

## Art pipeline

The Blender source, its FBX export and the icon sources live in `art/`. The built
AssetBundle is `GungnirStaff/Assets/gungnir`, **embedded into the DLL** as
`GungnirStaff.gungnir`, so the mod ships as a single file with no loose assets to
deploy. The bundle is baked in a separate Unity project, which is not part of this
repo; it carries two assets, the `GungnirVisual` prefab and the `GungnirIcon` sprite.

Only the *visual* comes from the bundle — the item itself stays a runtime clone of a
vanilla spear, so all of Valheim's own behaviour (colliders, attacks, animations,
throwing) keeps working untouched and the bundle needs no Valheim components in it.
That also means the bundle can be rebuilt without touching any of the mod's logic.

The crystal particles are built in **code** rather than authored into the bundle, for
two reasons: their colour has to follow whichever staff is selected, which a baked
particle system cannot do; and building them here means they hot-reload, instead of
costing a Blender → Unity → bundle round trip for every tweak.

---

## Prerequisites

- Valheim installed at the path in `Directory.Build.props`
- The **QoL** r2modman profile with **Jötunn** installed (the build references
  `Jotunn.dll` and BepInEx's `0Harmony.dll` straight out of that profile)
- .NET SDK 8 — installed at `%LOCALAPPDATA%\Microsoft\dotnet`

If any of those paths differ, copy `Environment.props.example` to `Environment.props`
and edit. That file is git-ignored, so it never conflicts.

## Build

```bash
dotnet build GungnirStaff.sln -c Debug
```

Every build copies `GungnirStaff.dll` + `.pdb` into
`…\r2modmanPlus-local\Valheim\profiles\QoL\BepInEx\plugins\GungnirStaff\`.

| Command | Effect |
| --- | --- |
| `dotnet build` | install into `BepInEx/plugins/GungnirStaff/` (needs a game restart) |
| `dotnet build -p:HotReload=true` | deploy to `BepInEx/scripts/` — ScriptEngine reloads it live |
| `dotnet build -p:Deploy=false` | build only, no copy |

Each deploy mode **deletes the copy in the other location**. Two copies of the same
plugin GUID collide and the second one silently refuses to load, so the build makes
that impossible rather than leaving you to debug it.

To make *every* build hot-reload — including Rider's Build Solution (Ctrl+F9) — set
`<HotReload>true</HotReload>` in your `Environment.props`. That's the setting to flip
while actively developing.

The build fails fast with a readable message if the Valheim or Jötunn paths are wrong.

## Publicized assemblies

`BepInEx.AssemblyPublicizer.MSBuild` rewrites the referenced game assemblies at build
time so private members are reachable — that's why `MessageHud.m_instance` and
`Player.m_localPlayer` compile. The compiler emits `IgnoresAccessChecksTo` into the
output assembly, so this is legitimate at runtime too, not a hack.

To publicize another assembly, add `Publicize="true"` to its `<Reference>`.

## Hot reload (no game restart)

**ScriptEngine r11.1** is installed at `BepInEx/plugins/ScriptEngine/` and configured
at `BepInEx/config/com.bepis.bepinex.scriptengine.cfg` with:

- `EnableFileSystemWatcher = true` — **reloads automatically 1.5s after the DLL changes**
- `ReloadKey = F6` — manual reload, as a fallback
- `LoadOnStart = false` — deliberately off: ScriptEngine ignores `[BepInDependency]`
  when it loads on start, so the mod could Awake before Jötunn is ready. The watcher
  loads it a moment later instead, with Jötunn already up.

So the loop is: set `<HotReload>true</HotReload>` in `Environment.props`, launch the
game once, then just build. ~1.5 seconds later the game reloads the mod on its own —
no keypress, no restart. Because `LoadOnStart` is off, the first build after launching
(or one press of F6) is what brings the mod up in that session.

`Plugin.OnDestroy()` calls `_harmony.UnpatchSelf()` specifically so reloads don't stack
duplicate patches. Anything else you create (GameObjects, coroutines, event
subscriptions, UI) must also be torn down there, or it will leak on every reload.

**What does not hot-reload:** Jötunn registrations that only run once at
`ObjectDB`/`ZoneSystem` init — custom items, pieces, prefabs and recipes. Those still
need a world reload (main menu → back in) or a game restart. Game logic, Harmony
patches, UI, crystal effects and config changes reload fine.

## Is the mod alive?

`Alive.Announce()` writes the same status line to three places at once — the BepInEx
console window, Valheim's in-game console (F5), and the centre of the screen:

```
[reloaded] Gungnir Staff v0.1.0 | Debug build 2026-08-29 14:10:57 | 1 patched method(s)
```

The build timestamp is baked in at compile time by the `GenerateBuildInfo` MSBuild
target, so after a hot reload you can tell at a glance whether the new DLL actually
took, rather than guessing.

It fires on four occasions:

| When | Tag | Notes |
| --- | --- | --- |
| Game start | `loaded` | console + log only; no HUD exists yet |
| ScriptEngine reload | `reloaded` | detected by a world already running |
| Local player spawns | `spawned` | from the Harmony postfix on `Player.OnSpawned` |
| **F9** | `alive` | on demand — configurable as `Keys/StatusKey` |

F9 works even with `Enabled = false` and even with a menu open — it's a diagnostic,
not gameplay.

The BepInEx console window is already switched on in this profile
(`BepInEx.cfg` → `[Logging.Console] Enabled = true`).

## Network compatibility

Set per build configuration in `Plugin.cs`, so you never have to remember to flip it:

| Build | Level | Effect |
| --- | --- | --- |
| **Debug** | `NotEnforced` / `None` | join any server, including your own, mid-development |
| **Release** | `EveryoneMustHaveMod` / `Minor` | what you ship |

Nothing to change when the mod is finished — just build `-c Release` to package.

---

## Dev tools

### Console

Open the console with F5 (needs `-console` in r2modman's launch parameters). The
command is flagged as a cheat, so vanilla refuses it unless `devcommands` is on — a
normal player cannot conjure a Gungnir from the console.

| Command | Effect |
| --- | --- |
| `gungnir give` | Put a Gungnir in your inventory — a testing shortcut past the recipe |
| `gungnir staffs` | List what the mod detected as a staff |
| `gungnir status` | Dump the rack contents and current selection |
| `gungnir holster` | Holster the active staff |
| `gungnir find` | Locate Gungnirs |

### Scripts

`tools/Dump-GameType.ps1` prints the real fields and method signatures of any Valheim
type, straight out of `assembly_valheim.dll` via Cecil:

```bash
powershell -File tools/Dump-GameType.ps1 -Types InventoryGrid,Humanoid -Filter "Equip"
```

`tools/Verify-Patches.ps1` checks that every Harmony patch target in the built DLL
actually resolves against the game assemblies — catching the most common runtime
failure without launching the game:

```bash
powershell -File tools/Verify-Patches.ps1
```

`Blueprint.cs` dumps a vanilla item prefab's full component tree to the log when
`VerboseLogging` is on. Building a standalone item means reproducing the component set
Valheim expects — miss one and the item is unpickupable, invisible on the ground, or
fails to replicate — and reading a real one is the only reliable way to know what that
set is.

## Debugging

- Log: `…\profiles\QoL\BepInEx\LogOutput.log`
- Enable the in-game console with `-console` in r2modman's launch parameters
- `VerboseLogging` in the config turns on `ModConfig.Trace(...)` output
- Portable PDBs are deployed alongside the DLL, so stack traces carry line numbers

## Packaging

Bump `<Version>` in the csproj, `ModVersion` in `Plugin.cs` and `version_number` in
`Thunderstore/manifest.json` together, then zip the Release DLL with the manifest,
`icon.png` (256×256) and `README.md`.
