# Gungnir Staff

A Valheim mod that adds **Gungnir**, Odin's spear — a Mistlands-tier weapon that
carries other magic staffs inside itself and *becomes* whichever one you select.

Built on **BepInEx 5.4.23.3** + **Jötunn 2.29.2**, targeting `net462` (the framework
Jötunn and Valheim's Unity Mono runtime use).

Source: <https://github.com/Dasone/Valheim-mod-Gungnir-staff>

---

## What the mod does

Gungnir is craftable at an upgraded **Galdr Table** and behaves as a spear in the
hand — full vanilla attacks, throwing, animations and colliders, because the item is
a runtime clone of a vanilla spear with its own model, damage and flavour text
applied on top.

Its own trick is the **staff rack**: a one-row inventory that lives inside the item.
Drop a magic staff into the rack, select the slot, and Gungnir turns into that staff
— its attack, eitr cost, projectile, skill XP, icon and held model all become the
staff's. Holster, and it is a spear again. A new Gungnir has four rack slots and each
upgrade adds two (4 → 6 → 8), so the item's quality is what governs how many staffs it
carries.

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

| Material | Craft | Lv 2 | Lv 3 |
| --- | --- | --- | --- |
| Yggdrasil wood | 30 | 15 | 25 |
| Refined eitr | 30 | 20 | 30 |
| Thunder stone | 3 | 2 | 4 |
| Black core | 2 | 1 | 2 |
| **Rack slots** | **4** | **6** | **8** |

Three levels rather than four, because the levels exist to buy rack slots and the rack
is full at level 3. The 8-slot ceiling is itself pinned by the `Alt`+`1`…`Alt`+`8`
shortcuts, so a fourth upgrade could only have cost materials and granted nothing.

Black cores gate it behind actually delving the Infested Mines rather than gathering
on the surface.

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
| Middle-drag a rack slot (inventory open) | Move the bar to a new place on screen |

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

Everything lands in `BepInEx/config/dev.samspel.gungnirstaff.cfg` and is
live-editable in Configuration Manager (F1) under **Gungnir Staff**.

| Section | What's in it |
| --- | --- |
| `General` | `Enabled` master switch, `VerboseLogging` |
| `Staff bar` | `SlotCount` (0 = follow upgrade level), `Activation` (Carried / Equipped), `Visibility` (WhenGungnirActive / InventoryOnly / Always), `PositionX`, `PositionY`, `Scale`, `MoveBarMouseButton`, `ShowSlotKeys`, `SlotKeyScale` |
| `Appearance` | `StandalonePrefab`, `BaseWeaponPrefab`, `NormaliseStaffStance`, model offset / scale / grip height, spear and staff stance rotations, back-slot placement, crystal particle mode / rate / scale / brightness, weapon glow effect |
| Crystal colours | One colour per staff, bound lazily from ObjectDB |
| `Keys` | `StatusKey`, `HolsterStaff`, `SelectSlot1`…`SelectSlot8` |

The crystal colour entries are bound one per staff *actually found in ObjectDB*, so
the F1 menu lists exactly the staffs this installation has — including any added by
other mods — rather than a hard-coded set that would go stale. They are stored as hex
strings because BepInEx has no built-in converter for `UnityEngine.Color`, and hex
round-trips cleanly through the config file.

`BaseWeaponPrefab` takes effect on the next game start: the held model comes from the
prefab, which is registered once at load.

`NormaliseStaffStance` keeps Gungnir held the same way whichever staff is selected.
Projecting a staff brings its `m_animationState` along, and not every magic item is
staff-shaped — the Dead Raiser is a skull and vanilla poses the character to match, so
a Gungnir impersonating it was carried like a skull. The wanted stance is *measured*
from the staffs actually installed (the majority wins, falling back to `Staves`) rather
than hard-coded, so a modded staff set still moves the odd one out rather than the
group. Only `m_animationState` is overridden, on a private copy of the SharedData, and
the whole game reads that field in exactly two places — `Humanoid.SetupAnimationState`
for the pose and `KeyHints.UpdateHints` for the hints. Nothing about damage, casting,
eitr or which hand the item goes in is affected.

### Two ways to build the prefab

By default (`StandalonePrefab = true`) Gungnir is built from nothing by
`GungnirStandalone.cs`, which authors every stat explicitly — from values measured off
a real spear rather than guessed at — so the item depends on no vanilla weapon at all.

`StandalonePrefab = false` switches back to the original path: a runtime clone of a
vanilla spear, which hands you working attacks, animations, colliders and a hundred-odd
`SharedData` fields for free. It remains as a fallback in both senses — set it
deliberately, or the mod drops to it automatically and says so in the log if a
standalone build ever fails to complete.

The attack animations are not a dependency either way: `spear_poke` is a state in the
player's own animator, so naming it costs nothing.

Either path takes effect on the next game start, since the prefab is registered once
at load.

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
for us. The stored staffs survive relogs, follow the item into a chest, are written to
the ZDO when the item is dropped, and need no save file of our own.

**Upgrading replaces the item.** `InventoryGui.DoCrafting` does not raise the level of
the weapon you hand it — it unequips it, removes it from the inventory, and adds a
brand new one built from the recipe prefab at the next level, carrying over only the
grid position and variant. The rack lives in `m_customData`, which the replacement does
not have, so an upgrade used to eat every stored staff. `CraftingUpgradePatch` copies
that data onto the replacement, holstering first so vanilla reads Gungnir's own level
rather than the projected staff's.

Gungnir's own level is also kept in custom data (`gungnir.q`), because projecting a
staff overwrites `m_quality` with that staff's level. It is not a cosmetic number: it
decides the rack width and it is what an upgrade increments.

**The trade-off: the staffs share the Gungnir's fate.** They are part of the item, not
a separate container, so anything that destroys the Gungnir destroys them with it — and
uninstalling the mod takes the whole item, staffs included, because the prefab no longer
exists to load. Run `gungnir empty` to move them into your own inventory *before*
uninstalling. There is no way for the mod to rescue them afterwards; by then it is not
running.

What counts as a staff is detected from ObjectDB **by skill type**, not from a name
list — so the vanilla staffs are found automatically however many there are, and so
are staffs added by other mods, with no changes here.

The bar can be dragged to a new place with the middle mouse button while the inventory
is open — that is the only time the cursor is free, and dragging with it captured by the
camera would fling the bar around as the player looks. The drag reads the cursor through
`RectTransformUtility.ScreenPointToLocalPointInRectangle` into the bar's parent space
rather than working in raw pixels, so it tracks correctly at any UI scale or resolution,
and it uses the delta between two points in that space, which is what makes it immune to
the parent's local origin differing from the bar's own anchored one. The result is
written back to `PositionX`/`PositionY` on release, so a dragged position survives a
relog and is still editable from F1. `MoveBarMouseButton = -1` turns it off.

Starting a drag hit-tests the actual slot rectangles, not the bar's own. The bar is a
clone of the player's 8×4 grid, so its rectangle is several rows taller than the one
visible row — generous is right for catching a dropped item, and wrong for starting a
drag, where it would let a middle-click in empty space above the bar pick it up.

Each slot is labelled underneath with its shortcut — `Alt+1`, `Alt+2` and so on. The
text is read from the live `KeyboardShortcut`, so rebinding a slot in F1 relabels it and
the label can never drift out of step with the key that actually works. The label is
cloned from the slot's own stack-count text, which brings Valheim's font, outline and
material with it, and its size is derived from the slot's height rather than a fixed
number — so it tracks `Scale` and any UI scale the game is running at. `SlotKeyScale`
nudges it relative to that.

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
| `dotnet build -p:OverwriteInstalled=true` | drop the build on top of the copy installed from Thunderstore |
| `dotnet build -p:Deploy=false` | build only, no copy |

Each deploy mode **deletes the copies in the other locations**. Two copies of the same
plugin GUID collide and which one wins is not something to rely on, so the build makes
that impossible rather than leaving you to debug it.

`OverwriteInstalled` is for testing a local build while the *published* mod is
installed from Thunderstore. r2modman installs under `plugins/<Author>-<Mod>/`, which
is neither of the paths the other two modes manage, so without this the local build
would simply be a second copy of the same GUID fighting the released one. The path is
matched with a glob rather than assumed, so it finds the install whatever the author
namespace is, and it warns instead of silently doing nothing when no install is there.
r2modman is not upset by it — it only sees its own file — and reinstalling or updating
the mod from Thunderstore restores it.

Note this deploys whichever configuration you build, so by default that is **Debug**,
whose `NetworkCompatibility` is `NotEnforced`. Add `-c Release` when what you want to
test is the shipping behaviour.

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

`Alive.StatusLine()` says exactly which build is running:

```
Gungnir Staff v0.1.0 | Debug build 2026-08-29 14:10:57 | 8 patched method(s)
```

`Alive.Announce()` puts that line in three places at once — the BepInEx console window,
Valheim's in-game console (F5), and the centre of the screen.

The build timestamp is baked in at compile time by the `GenerateBuildInfo` MSBuild
target, so after a hot reload you can tell at a glance whether the new DLL actually
took, rather than guessing.

It is deliberately quiet. A released mod should not announce itself on the player's
screen, so the line is only shown when it is asked for:

| When | Where | Notes |
| --- | --- | --- |
| Game start / ScriptEngine reload | BepInEx log only | one `LogInfo` line, no HUD, no game console |
| `gungnir` in the F5 console | log + console | also prints damage, rack and hit-feedback state |
| `Keys/StatusKey` | log + console + HUD | **unbound by default** — bind a key to use it |

The status key is not bound out of the box: it is a diagnostic, and a released mod has
no business claiming a key on everyone's keyboard for one. Bound, it works even with
`Enabled = false` and even with a menu open.

The BepInEx console window is already switched on in this profile
(`BepInEx.cfg` → `[Logging.Console] Enabled = true`).

## Network compatibility

Set per build configuration in `Plugin.cs`, so you never have to remember to flip it:

| Build | Level | Effect |
| --- | --- | --- |
| **Debug** | `NotEnforced` / `None` | join any server, including your own, mid-development |
| **Release** | `EveryoneMustHaveMod` / `Minor` | what you ship |

`EveryoneMustHaveMod` means exactly that — the **server** needs it too, not just the
clients. That makes the `[BepInProcess]` attributes part of the multiplayer contract:
the plugin declares both `valheim.exe` and `valheim_server.exe`, because a dedicated
server runs the latter and the attribute is a hard filter on which process BepInEx will
load the plugin into. With only `valheim.exe` declared, a dedicated server could have
the files installed and still never load them, Jötunn would find the mod missing on the
server's side of the handshake, and every player who installed it would be locked out.

`VersionStrictness.Minor` compares major and minor, so `1.0.0` and `1.0.1` can play
together but `1.1.0` cannot. The version compared is `ModVersion` in `Plugin.cs`.

The mod is otherwise well behaved headless. `Update` returns immediately with no local
player, so none of the UI, model or crystal work runs on a server; the damage patch
rolls only for `Player.m_localPlayer`, so a strike is rolled once, by the attacking
client, and replicates as part of the hit; and the art bundle is skipped entirely
(`GUIManager.IsHeadless()`), since a server has no renderer to draw it with.

Two things worth knowing rather than being surprised by:

- The item prefab is registered when a local player spawns, which never happens on a
  dedicated server, so the server does not carry it. This is benign:
  `ZNetScene.CreateObject` returns null for an unknown prefab **without touching the
  ZDO**, so a dropped Gungnir still persists in the world and still replicates between
  clients — the server simply does not instantiate it locally.
- Damage and chance values are ordinary client-side config, not server-synced. A client
  editing `LightningDamage` affects its own hits. Fine among friends; worth knowing if
  you run a public server.

Nothing to change when the mod is finished — just build `-c Release` to package.

---

## Dev tools

### Console

Open the console with F5 (needs `-console` in r2modman's launch parameters). The command
itself is not a cheat — `empty` and `recover` are the two a player reaches for when
their staffs are at risk, and gating those behind `devcommands` would be backwards.
Spawning is guarded separately: `give` refuses unless the game was launched with
`-console` *and* `devcommands` is on, so a normal playthrough cannot conjure a Gungnir.

| Command | Effect |
| --- | --- |
| `gungnir status` | Damage, hit feedback, rack contents and current selection |
| `gungnir empty` | Move every staff out of the rack into your inventory — **do this before uninstalling** |
| `gungnir recover` | Hand back Gungnirs stranded in the world by the pre-fix drop bug (same session only) |
| `gungnir staffs` | List what the mod detected as a staff |
| `gungnir holster` | Holster the active staff |
| `gungnir find` | Search ObjectDB prefab names |
| `gungnir give` | Put a Gungnir in your inventory — a testing shortcut past the recipe |

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

## Debugging

- Log: `…\profiles\QoL\BepInEx\LogOutput.log`
- Enable the in-game console with `-console` in r2modman's launch parameters
- `VerboseLogging` in the config turns on `ModConfig.Trace(...)` output
- Portable PDBs are deployed alongside the DLL, so stack traces carry line numbers

## Packaging

One command produces the upload-ready zip in `dist/`:

```bash
dotnet build GungnirStaff/GungnirStaff.csproj -t:Package
```

```
dist/GungnirStaff-<version>.zip
├── manifest.json          # from Thunderstore/
├── icon.png               # art/Gungnir staff.png, renamed
├── README.md              # this file
├── CHANGELOG.md           # rendered as its own tab on the package page
└── plugins/
    └── GungnirStaff.dll   # Release
```

`CHANGELOG.md` gets its own tab on Thunderstore and is shown by r2modman, so it is the
one place a player reads before deciding whether to update. Write the release's entry
before packaging: the target refuses to build without the file, and an entry missing
when the version ships cannot be added to a package that is already published.

The target builds Release through a nested MSBuild rather than depending on the normal
`Build`, which forces two things it would be easy to get wrong by hand:

- **Configuration is forced to Release.** `Plugin.cs` switches `NetworkCompatibility` on
  the build configuration, so a Debug package ships with the multiplayer handshake
  *disabled* — it would not require other players to have the mod.
- **`Deploy` is forced off.** An ordinary build copies into the live r2modman profile;
  packaging must never quietly swap the DLL you are playing with for a different
  configuration.

It refuses to run rather than producing a broken package if the csproj `<Version>` and
the manifest's `version_number` disagree — the manifest version is read out of the JSON
at evaluation time and compared — or if the icon is missing. Bump `<Version>`,
`ModVersion` in `Plugin.cs` and `version_number` together; the first two are compared
by the version check, and `ModVersion` is what the multiplayer handshake compares
between clients.

`icon.png` must be exactly 256×256 or Thunderstore's own validator rejects the upload.
`art/Gungnir staff.png` already is, and the target renames it on the way into the zip.
