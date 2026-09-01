# Ephemeral

**A game dedicated to my grandfather.**

A 2D roguelite about a dead miner descending into the mine where he died, slowly piecing together who he was before the end.

![Main menu](docs/Menu.png)

## Play

**[Play Ephemeral on itch.io](https://nicormu.itch.io/ephemeral)** — runs in your browser, no download needed.

![Gameplay](docs/Gameplay_0.png)

![Gameplay](docs/Gameplay_1.png)

![Gameplay](docs/Gameplay_2.png)

## Controls

* **WASD** — Move
* **Left Click** — Attack *(visual feedback is not implemented yet)*
* **R** — Reset the dungeon

## Current state

Ephemeral is currently in an early alpha stage. Core gameplay is under active development, so you may encounter bugs, unfinished features, and balance issues. Feedback is greatly appreciated.

The current build focuses primarily on **procedural mine generation**: randomly generated layouts, room connections with proper doors, floor and wall tiles, environmental decorations, obstacles, and an animated player character.

**Still in progress:** boss encounters, and the memory/story system.

## The idea

You play as a miner who's already dead when the game starts, exploring a mine that should feel familiar but isn't quite right. As he explores, he'll encounter enemies, bosses, and fragments of memories from before his death — pieces that gradually reveal what happened to him.

The procedural generation supports this idea: every run takes you through the same mine, but your path and discoveries shift enough to make each run feel different.

## Running locally

1. Install **Unity 6.4+** through Unity Hub or as a standalone installation.
2. Clone this repository and open it in the Unity Editor.
3. Open the main scene and press **Play**.

No external dependencies or additional build steps are required.

## Credits

Made by **Nicolás (Nicormu)**.

**Juan Andres Granados ([Juanggs777](https://github.com/Juanggs777))** — thanks for helping with some of the sprites.

Inspired by roguelites such as *The Binding of Isaac* and games that make empty spaces feel intentional. This project is built from scratch, with no assets or systems borrowed from other games.
