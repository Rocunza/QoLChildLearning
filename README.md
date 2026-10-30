# QoL: Child Learning

**RimWorld 1.6 / 1.7** · **Requires Biotech and Harmony** · Author: **ZCN**

A quality-of-life mod for managing children's learning without changing the vanilla learning rate, growth points, or growth-tier mechanics.

## Features

- Show Learning percentage on child colonist portraits, with configurable appearance and an in-learning highlight.
- Show the child's current learning desires using **the original game icons** in a small box above the vanilla growth gizmo.
- Notify when a reading learning desire becomes active; optionally show a reading icon on the child portrait.
- Attempt automatic wake-ups based on configurable Learning and Rest thresholds, with cooldown and safeguards.
- Assist eating at natural job transitions when the child's Food need drops below a configurable threshold; preserve existing jobs, queued orders, and food restrictions.
- Control reading alerts, auto wake, and auto eat for each child using individual toggles.

Default thresholds: Learning **89.9%** to attempt wake-up, minimum Rest **50%**, wake cooldown **1 in-game hour**, and Food **55%** for auto-eat assistance. The settings and per-child toggles jointly determine which features actually run.

## Installation

1. Install **Harmony** and enable the **Biotech DLC**.
2. Download the mod ZIP from the GitHub **Releases** page (do **not** use the auto-generated *Source code* ZIP to install the mod).
3. Extract the top-level `QoLChildLearning` folder into `RimWorld/Mods/`.
4. Enable **QoL: Child Learning** in the RimWorld mod list.

This distribution includes separate `1.6/Assemblies/` and `1.7/Assemblies/` DLLs. Do not replace one with the other.

## Source layout

- `1.6/QoLChildLearning/` — RimWorld 1.6 C# project (.NET Framework 4.7.2)
- `1.7/Projects/QoLChildLearning/` — RimWorld 1.7 C# project (.NET 10)
- `ModContent/` — common About metadata, language XML and PNG textures

### Local build requirements

Game DLLs and Harmony binaries are **not included** in this repository. Copy the example property file to a local property file and replace the example paths:

- For 1.6, `1.6/RimWorld.local.props.example` → `1.6/RimWorld.local.props`.
- For 1.7, `1.7/RimWorld17.local.props.example` → `1.7/RimWorld17.local.props`.

Open the relevant `.csproj` with a supported Visual Studio / .NET environment and build **Release**. Build artifacts and machine-specific paths are intentionally excluded from version control.

## Language support

English and Korean.

## Steam Workshop

**[Subscribe to QoL: Child Learning on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3829534633)**  
Workshop item ID: `3829534633`.

## License

This project is licensed under the **[MIT License](LICENSE)**.  
Copyright (c) 2026 ZCN.

The MIT license covers original project code and original assets. RimWorld, Biotech, Harmony, and other third-party materials remain subject to their respective owners' terms.
