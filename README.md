# UI

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **R.E.P.O.** that adds a unified Tab overlay with level info, map value, haul progress, and a player list.

## What This Mod Does

Hold **Tab** during gameplay to see a clean info panel on the right side of your screen:

### Level Number
Shows the current level you're on (Level 1, Level 2, etc.).

### Map Value
Tracks the total remaining dollar value of all valuable items on the current map. Updates in real time as items are broken, destroyed, or extracted.

### Haul Progress
Shows your current haul versus the extraction goal with color-coded text:
- 🟥 **Red** — below 50% of goal
- 🟨 **Yellow** — 50–99% of goal
- 🟩 **Green** — goal reached!

### Player List
Lists all connected players with their current status:
- **Alive** (green)
- **Dead** (red)

## Usage

Just hold **Tab** (the map key) during any level. The overlay appears on the right side of your screen and disappears when you release Tab.

> The overlay also shows when the map is toggled open.

## Requirements

- [BepInEx 5.x](https://github.com/BepInEx/BepInEx) installed for R.E.P.O.

## Installation

1. Install via **Thunderstore** (recommended).
2. Or manually: place `UI.dll` into your `BepInEx/plugins` folder.
3. Launch the game — no configuration needed.

## Multiplayer

- The overlay runs per-client — shows data available to your game client.
- Player list shows all connected players and their alive/dead status.
- Map value tracking and haul progress work the same in multiplayer.

## Development

### Project Structure
```
├── HeadclefUI.cs                   # Plugin entry point
├── MapValueTracker.cs              # Tracks remaining valuable item values
├── TabOverlay.cs                   # Tab overlay panel — rendering & input
└── README.md
```

### Building
```bash
dotnet build
```

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
