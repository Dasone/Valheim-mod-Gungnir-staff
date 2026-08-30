# Changelog

All notable changes to Gungnir Staff. Dates are the release date; versions follow
[semantic versioning](https://semver.org/), and the multiplayer handshake compares
major and minor — so `1.0.x` releases stay compatible with each other, and players on
`1.0.1` and `1.0.2` can play together.

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
