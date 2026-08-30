# Changelog

All notable changes to Gungnir Staff. Dates are the release date; versions follow
[semantic versioning](https://semver.org/), and the multiplayer handshake compares
major and minor — so `1.0.x` releases stay compatible with each other, and players on
`1.0.1` and `1.0.2` can play together.

## 1.0.3 — 2026-08-30

### Added

- `gungnir give` takes an optional upgrade level, `gungnir give 1` through
  `gungnir give 3`, spawning the staff already upgraded so the rack comes with 4, 6 or
  8 slots. Without a level it still gives a level 1 staff.

### Fixed

- **The crystal's particles could come out the wrong size** after switching staffs, and
  stayed wrong until you unequipped or changed stance. The effect hangs off a model
  whose mesh carries a 100x scale from its import, and the compensation for that was
  only applied when the effect was built — so a scale read on the wrong frame was baked
  in for good. It is now re-asserted continuously, and the particles' size no longer
  depends on the transform at all.
- **Effects could be left behind on the hand the weapon just came from.** Selecting a
  staff can move Gungnir between hands, and the effect built on the old model was never
  told to go — so it kept drawing alongside the new one. Both hands are now swept.
- Effects are torn down immediately rather than at the end of the frame, so a rebuild
  can no longer put the replacement on screen next to the original.
- The mod no longer reports a page of warnings and one error while sitting in the main
  menu. It was registering against the menu's item database, which holds a fraction of
  the game's items; it now waits for the real one. Nothing was broken by it, but it
  looked alarming and did the work twice.

### Changed

- **The blade glow is off by default** (`Weapon effect / BladeGlowStyle = None`). It can
  still render at the wrong size after some stance changes, and every measurable
  property of the particle systems is identical between a good one and a bad one — so
  the cause is not yet found, and shipping it on by default would mean shipping a
  visible bug. The crystal effects are unaffected. Set `Lightning` or `SoftGlow` to opt
  back in.

## 1.0.2 — 2026-08-30

### Fixed

- **Gungnir and every staff stored in it were lost when you restarted the game.** The
  item prefab was registered after the player's inventory had already been read, so
  the game could not resolve the saved Gungnir and dropped it — taking the whole staff
  rack with it, because the stored staffs live inside the item. It is now registered
  with `ObjectDB` itself, before any inventory is loaded.

**If you are on 1.0.1, update before you next play.** Loading discards the item, and
the next save — a logout or an autosave — writes the inventory back without it. Until
that save happens, nothing is lost: update first and your Gungnir returns intact. If
you have already played and logged out since it vanished, Valheim's automatic
character backups (`<name>_backup_auto-*.fch`, next to your `.fch` save) may still have
it.

## 1.0.1 — 2026-08-30

### Changed

- The recipe no longer requires a **Gjall trophy**. It was gating the recipe behind an
  item many players had never picked up, and in Valheim a recipe stays invisible until
  you have had all of its materials. The remaining four are unchanged.

## 1.0.0 — 2026-08-30

Initial public release.

### The weapon

- **Gungnir**, craftable at a level 2 Galdr Table, handling as a full Mistlands-tier
  spear — vanilla attacks, animations and colliders.
- **Storm touch**: a chance on every hit to add lightning damage *on top of* the
  weapon's own damage, with the game's own lightning impact effect and sound played
  only when it triggers. Chance and damage are configurable.
- Three upgrade levels, buying rack slots rather than numbers: **4 → 6 → 8**.

### The staff rack

- Gungnir carries your magic staffs inside itself and **becomes** whichever one you
  select — its attack, eitr cost, projectile, skill XP, icon and held model all become
  that staff's. Holster, and it is a spear again.
- A second hotbar for the rack, on screen whenever Gungnir is with you, with each slot
  labelled by its shortcut. Select with `Alt`+`1`…`8`, holster with `Alt`+`0`.
- **Middle-drag the bar** while the inventory is open to move it anywhere on screen.
- The crystal glows in a colour per selected staff, configurable for every staff your
  installation has — including staffs added by other mods.
- Whatever staff is selected, Gungnir is held the same way. Vanilla poses some magic
  items differently (the Dead Raiser is a skull), which looked wrong on a spear.

### Console

- `gungnir status` — damage, hit feedback, rack contents and current selection.
- `gungnir empty` — move every staff out of the rack into your inventory. **Do this
  before uninstalling the mod**: the staffs are stored inside the item, so uninstalling
  takes them with it.
- `gungnir recover`, `gungnir staffs`, `gungnir holster`, `gungnir find`.

### Multiplayer

- Everyone must have the mod, including the server. Works on dedicated servers as well
  as host-and-play.
