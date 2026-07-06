# UI

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **R.E.P.O.** that adds a clean two-panel Tab overlay showing your Improve level, map value, combat/tumble stats, and a live player list.

## What This Mod Does

Hold **Tab** during gameplay to see two compact panels on the right side of your screen, positioned to stay clear of the game's own HUD:

- **Top-right — Info** — sits just below the game's HUD: Improve level, map value, and combat/tumble stats.
- **Bottom-right — Players** — a live player list that grows **upward** as more players join, so it never collides with the info panel.

<p align="center">
  <img src="https://raw.githubusercontent.com/headclef/Repo-UI/core/screenshots/ui-in-level.jpg" width="48%" alt="Overlay during a level" />
  <img src="https://raw.githubusercontent.com/headclef/Repo-UI/core/screenshots/ui-in-truck.jpg" width="48%" alt="Overlay back in the truck" />
</p>

> **Map Level** and **Haul** are intentionally not shown — the game already displays these natively in the Tab menu and top-right HUD.

### Improve Level

- **Improve Level** — your current Improve mod level *(requires [Improve](https://github.com/headclef/Repo-Improve))*.

### Map Value

Tracks the total remaining dollar value of all valuable items on the current map. Updates in real time as items are broken, destroyed, or extracted.

### Combat

Displays real-time combat information:

- **Held weapon** — name, ammo bars, and damage when holding a gun (supports bullet-based and laser guns).
- **Tumble launch damage** — shows your tumble damage including scaling from [Increase Tumble Damage](https://github.com/headclef/Repo-IncreaseTumbleDamage) upgrades and [Improve](https://github.com/headclef/Repo-Improve) stat points *(requires [Character Stats](https://github.com/headclef/Repo-CharacterStats))*.

### Player List

Lists all connected players with their current status:

- **Alive** (green)
- **Dead** (red)

### With Other Mods Installed

The overlay only shows the sections it has data for. Install [Improve](https://github.com/headclef/Repo-Improve) and an **Improve Level** line appears; add [Increase Tumble Damage](https://github.com/headclef/Repo-IncreaseTumbleDamage) and the **Tumble Launch** value scales with your upgrades.

<p align="center">
  <img src="https://raw.githubusercontent.com/headclef/Repo-UI/core/screenshots/ui-improve-in-level.jpg" width="48%" alt="With Improve — in a level" />
  <img src="https://raw.githubusercontent.com/headclef/Repo-UI/core/screenshots/ui-improve-in-truck.jpg" width="48%" alt="With Improve — in the truck" />
</p>
<p align="center">
  <img src="https://raw.githubusercontent.com/headclef/Repo-UI/core/screenshots/ui-improve-tumble-in-level.jpg" width="48%" alt="With Improve and Increase Tumble Damage — in a level" />
  <img src="https://raw.githubusercontent.com/headclef/Repo-UI/core/screenshots/ui-improve-tumble-in-truck.jpg" width="48%" alt="With Improve and Increase Tumble Damage — in the truck" />
</p>

> Top row adds the **Improve Level** line; bottom row shows **Tumble Launch** scaled by Increase Tumble Damage (15 → 82 dmg with five upgrades).

## Usage

Just hold **Tab** (the map key) during any level. The overlay appears on the right side of your screen and disappears when you release Tab.

> The overlay also shows when the map is toggled open.

## Requirements

- [BepInEx 5.x](https://github.com/BepInEx/BepInEx) installed for R.E.P.O.

### Optional (soft dependencies)

- **[Improve](https://github.com/headclef/Repo-Improve)** — enables the Improve Level display.
- **[Increase Tumble Damage](https://github.com/headclef/Repo-IncreaseTumbleDamage)** — enables tumble damage scaling display.
- **[Character Stats](https://github.com/headclef/Repo-CharacterStats)** — provides accurate upgrade levels from all mods for combat info.

The UI mod works without any of the above — those sections are simply hidden when the corresponding mod isn't installed.

## Installation

1. Install via **Thunderstore** (recommended).
2. Or manually: place `UI.dll` into your `BepInEx/plugins` folder.
3. Launch the game — no configuration needed.

## Multiplayer

- The overlay runs per-client — shows data available to your game client.
- Player list shows all connected players and their alive/dead status.
- Map value tracking works the same in multiplayer.

## Changelog

### v1.3.0

- Added a **Berserk banner** — shows the live Strength and Tumble Launch bonus while the Berserk state is active.
- Wired up **Berserk** as a soft dependency, so the overlay still loads and works without it.

### v1.2.0

- **Two-panel layout** — an info panel (top-right, just below the game HUD) and a player list (bottom-right) that grows upward as players join, so the overlay never collides with the game's UI.
- Removed the **Map Level** and **Haul** sections — the game now shows these natively in the Tab menu.
- Simplified the Improve section to a single **Improve Level** line (removed Available Points).
- Smaller fonts and tighter spacing for a more compact overlay.
- Tumble launch damage now reflects live upgrade levels (via the Character Stats fix).

### v1.1.0

- Added **combat section** — shows held weapon name, ammo, and damage.
- Gun damage reads from bullet prefab (bullet-based guns) and laser component (laser guns).
- Gun detection works for guns held in hand via PhysGrabber and scene-wide grabbedLocal fallback.
- Added **Improve Level** and **available points** display (soft dependency).
- Added **tumble launch damage** display with scaling from Increase Tumble Damage and Improve mods.
- Uses Character Stats API for accurate upgrade levels across all mods.
- All mod integrations are soft dependencies — UI loads and works without them.
- Reduced spacing between overlay sections for a more compact layout.

### v1.0.0

- Initial release with map level, map value, haul progress, and player list.

## Development

### Project Structure

```text
├── HeadclefUI.cs                   # Plugin entry point
├── MapValueTracker.cs              # Tracks remaining valuable item values
├── TabOverlay.cs                   # Tab overlay panels — rendering & input
└── README.md
```

### Building

This project is part of the `Repo.slnx` solution and references Character Stats / Improve / Increase Tumble Damage at compile time. Build the **whole solution** so dependencies build in the correct order:

```bash
dotnet build ../Repo.slnx
```

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

These mods (all) generated by using AI tools. If there's anything wrong use NexusMods to give a feedback, I use there more than ThunderStore.
