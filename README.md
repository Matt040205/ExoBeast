# ExoBeast

Cooperative tower-defense prototype where commander creatures defend an objective against waves of enemies. Unity 6 project with online multiplayer for up to 4 players.

## Status

Student team project, active development. Current state: vertical-slice prototype with a working multiplayer flow and core gameplay loop.

## Overview

ExoBeast is a 1–4 player cooperative tower-defense game built in Unity 6 (`6000.0.52f1`). Players pick from a roster of commander characters, place towers and traps around a defended objective, and survive waves of enemies. The multiplayer layer uses Unity Netcode for GameObjects in a peer-to-peer host model, with lobby and authentication handled through Epic Online Services.

## Key Features

- EOS-based anonymous authentication (Device ID)
- Online lobby with create / search / join via Epic Online Services
- Character selection flow (Commanders + Towers, scaled to player count)
- Up to 4-player cooperative multiplayer (P2P host)
- Tower placement and trap system with networked state
- Enemy hordes (server-authoritative)
- FMOD audio integration
- Editor multiplayer testing via Unity Multiplayer Play Mode (MPPM)

## Tech Stack

| Area | Technology |
|---|---|
| Engine | Unity 6 (`6000.0.52f1`) |
| Language | C# |
| Networking | Netcode for GameObjects 1.12 + Unity Transport 2.4 |
| Online Services | Epic Online Services (PlayEveryWare plugin) |
| Multi-instance testing | Unity Multiplayer Play Mode 1.6.3 |
| Audio | FMOD |
| Version Control | Git / GitHub |

## Multiplayer System

The multiplayer layer is responsible for EOS authentication, lobby and session lifecycle, scene transitions, and networked state synchronization for players, enemies, and built objects. EOS credentials are loaded from a prioritized chain — environment variables, then a local `EOSCredentials.json`, then runtime configs in `StreamingAssets/EOS/` — and the credential file is never committed to source control.

See [docs/multiplayer.md](docs/multiplayer.md) for an architecture overview and links to the internal technical docs in `Assets/Codigo/Docs/` (Portuguese, maintained alongside the code).

## My Contribution (@Sitr3n01)

I am one of three contributors on this team project. My work has been focused on the multiplayer and networking layer:

- **EOS credentials refactor** — removed three redundant editor scripts, consolidated credential loading into a single chain (env vars → local JSON → StreamingAssets), and marked all credential fields `[NonSerialized]` so they no longer persist into committed assets. Shipped as [PR #4](https://github.com/Matt040205/ExoBeast/pull/4).
- **Sprint 3 network optimization** — work on `LobbyManager`, `NetworkBootstrap`, `NetworkedBuilding`, and enemy spatial partitioning. The sprint docs live in `Assets/Codigo/Docs/sprint3/`.
- **Ability and movement sync fixes** — corrections to `CommanderAbilityController`, `PlayerMovement`, the Polvo dive ability, and the Dragon defensive stance controller to fix host/client desync.
- **Lobby and scene-transition integration** — wiring of `LobbySceneUI`, `MultiplayerRuntimeReset`, and the two-stage scene flow (LobbyScene → EscolherPersonagem → CenaMapaTeste).
- **Recurring multiplayer bug fixes** — host/client input ownership, NetworkObject lifecycle issues, and EOS lobby race conditions.

To inspect my commits directly:

```sh
git log --author="Sitr3n01" --pretty=oneline
```

Some work was integrated through team commits or paired sessions, so not every change is individually attributable through GitHub history. This section summarizes the areas I am responsible for.

## Setup

### Requirements
- Unity 6 (`6000.0.52f1`) — see `ProjectSettings/ProjectVersion.txt`
- An Epic Games developer account with an EOS product configured ([Epic Dev Portal](https://dev.epicgames.com/portal/))
- (Optional) Unity Multiplayer Play Mode for in-editor multi-instance testing

### Opening the project
1. Clone the repository.
2. Open the project root in Unity Hub with the matching Unity version.
3. Set up EOS credentials before entering Play Mode — see [Configuration](#configuration).
4. Open `Assets/Scenes/MenuScene.unity` and press Play.

## Configuration

The project never commits real EOS credentials. Credentials are loaded from one of three sources, in order:

1. **Environment variables** (highest priority — for CI/CD): `EOS_PRODUCT_ID`, `EOS_SANDBOX_ID`, `EOS_DEPLOYMENT_ID`, `EOS_CLIENT_ID`, `EOS_CLIENT_SECRET`, `EOS_ENCRYPTION_KEY`.
2. **Local JSON** at the repo root: copy `EOSCredentials.json.template` to `EOSCredentials.json` and fill in your own values. This file is gitignored.
3. **Runtime configs** in `StreamingAssets/EOS/` — auto-generated from one of the sources above by `Assets/Editor/EOSConfigGenerator.cs`.

For full details (including a GitHub Actions snippet, validation behavior, and the documented refactor history), read [Assets/Codigo/Multiplayer/CREDENTIALS_SETUP.md](Assets/Codigo/Multiplayer/CREDENTIALS_SETUP.md).

## Build Notes

- Builds run `EOSConfigGenerator.OnPreprocessBuild` automatically (`callbackOrder = -100`). Missing credentials abort the build with a clear error — they do not silently produce a broken binary.
- The build pipeline never depends on copying a developer's local JSON; it generates the runtime configs from whichever credential source is available.
- `ClientSecret` and `EncryptionKey` are never written to logs; `ClientId` is masked in log output.

## Known Limitations

- NAT traversal for over-the-internet matches is still pending — LAN works, public-internet host/join is not yet validated.
- The repository is public; running the project requires you to bring your own EOS credentials.
- Several multiplayer subsystems are still prototype-level (traps, host migration, reconnect — see [docs/multiplayer.md](docs/multiplayer.md)).
- Tested primarily on Windows.

## Contributors

| Handle | Focus area |
|---|---|
| [@Matt040205](https://github.com/Matt040205) | Repository owner. Art assets, level/scene work, UI flow, gameplay systems (traps, hordes UI, loading screen). |
| [@Sitr3n01](https://github.com/Sitr3n01) | Multiplayer, networking, EOS integration, sync optimization, bug fixes. |
| [@amigolindu](https://github.com/amigolindu) | Enemy animation, VFX particles. |

## Security note

If you fork this project and ever commit real EOS credentials by mistake, rotate them immediately at the [Epic Developer Portal](https://dev.epicgames.com/portal/) — clearing the commit from the current index does not invalidate secrets that already appeared in the git history.
