<p align="center">
  <img src="content/logo_game.png" alt="Playlist Stuck on Repeat Logo" width="500" />
</p>

# Playlist Stuck on Repeat

[![Website](https://img.shields.io/badge/website-psor.vectxyz.com-blue.svg)](https://psor.vectxyz.com)
[![Releases](https://img.shields.io/github/v/release/Aethertenshi/Playlist-stuck-on-repeat__Art2Framework.svg)](https://github.com/Aethertenshi/Playlist-stuck-on-repeat__Art2Framework/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)
[![Framework](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Powered by](https://img.shields.io/badge/powered%20by-Art2Framework-brightgreen.svg)](https://github.com/Aethertenshi/ArtFrameEXT)

A free-to-play C# rhythm game. Music is just a *tap* away!

**Playlist Stuck on Repeat** (PSoR) is a premium, high-performance Taiko-style rhythm game built from the ground up for ultimate responsiveness, fluid animations, and high-fidelity gameplay. 

Visit the official website at [psor.vectxyz.com](https://psor.vectxyz.com) to view the global leaderboards, customize your profile, and download the latest beatmaps.

---

## Status

This project is actively developed. We focus on providing a polished and competitive rhythm gaming experience with a dedicated online backend for leaderboards and score tracking.

A few resources are available to get you started:

- **Official Website:** [psor.vectxyz.com](https://psor.vectxyz.com)
- **Framework Source:** [ArtFrameEXT (Art2Framework)](https://github.com/Aethertenshi/ArtFrameEXT)
- **Report Issues:** Submit feedback or bug reports via GitHub Issues.

---

## Running the Game

If you want to play the game immediately, you can download the installer/executables from our website or download the latest release below:

### Latest release:

| Windows 10/11 (x64) |
|---------------------|
| [Download Installer (EXE)](https://psor.vectxyz.com) |

*Alternatively, you can build the project from the source code following the instructions below.*

---

## Core Features

- **Taiko Style Gameplay:** Hit center (Don - red) and rim (Ka - blue) notes to the beat. Supports large notes that require double-key inputs.
- **Osu! Beatmap Integration:** Drag-and-drop support for `.osz` beatmap files. The game automatically parses and loads them into your local song library.
- **Synced Background Video:** Real-time video playback powered by LibVLC, dynamically matching your gameplay speed and drift-correcting to remain perfectly synced with the audio.
- **Online Leaderboard & Profiles:** Powered by a Supabase backend for secure login, account management, and real-time score tracking.
- **Fluid UI & Audio Effects:** Includes premium transitions, dynamic UI rendering, and immersive BASS audio effects such as underwater low-pass filter transitions.
- **Dynamic Performance Pipeline:** Seamless transition between standard VSync frame rates in menus and ultra high polling rate frames during gameplay for minimal input latency.

---

## Game Showcase

See the premium interface, fluid visual design, and real-time gameplay trackers in action:

<p align="center">
  <img src="media/Screenshot%202026-06-11%20215911.png" width="48%" alt="Gameplay Mode Selection" />
  <img src="media/Screenshot%202026-06-11%20215917.png" width="48%" alt="Song Select & Leaderboard" />
</p>
<p align="center">
  <img src="media/Screenshot%202026-06-11%20222234.png" width="75%" alt="In-game Taiko Action" />
</p>

---

## Developing

### Prerequisites

Please make sure you have the following installed:

- A desktop platform with the **.NET 10.0 SDK** (or higher).
- An IDE with C# support, such as [Visual Studio 2022](https://visualstudio.microsoft.com/vs/), [JetBrains Rider](https://www.jetbrains.com/rider/), or [Visual Studio Code](https://code.visualstudio.com/) with C# extensions.

### Downloading the source code

Clone this repository alongside the framework repository in adjacent directories:

```shell
# Create a root project folder and clone both repos
git clone https://github.com/Aethertenshi/Playlist-stuck-on-repeat__Art2Framework.git
git clone https://github.com/Aethertenshi/ArtFrameEXT.git Art2Framework
```

Your folder structure should look like this:
```text
├── Playlist-stuck-on-repeat__Art2Framework/  # This repository
└── Art2Framework/                             # The framework repository
```

### Building

#### From an IDE

Open the Visual Studio solution selection file `Playlist-stuck-on-repeat__Art2Framework.slnx` in your IDE of choice. 

Build or Run the `Playlist-stuck-on-repeat__Art2Framework` project.

#### From CLI

Run the game directly from the command line:

```shell
dotnet run --project Playlist-stuck-on-repeat__Art2Framework.csproj
```

For performance testing, build using the `Release` configuration:

```shell
dotnet run -c Release --project Playlist-stuck-on-repeat__Art2Framework.csproj
```

---

## Code Style & Formatting

Before committing code changes, please format your files using the standard dotnet tool:

```shell
dotnet format
```

We adhere to clean coding conventions, minimizing warnings, and keeping class boundaries well-documented.

---

## License

This project is licensed under the MIT License. See [LICENSE.txt](LICENSE.txt) for more details. 

*Note: The core game framework (Art2Framework / ArtFrameEXT) is licensed separately under its respective repository.*
