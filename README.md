# 🕯️ Ephemeral

> **A 2D roguelite about a miner exploring the mine where he died—and slowly remembering how he got there.**

*This game is made in honor of my grandfather.*

**Ephemeral** is a 2D procedural roguelite inspired by games like *The Binding of Isaac*. You play as a miner who has died inside a mysterious mine. As you explore deeper, fight enemies, and eventually face bosses, pieces of his life before death begin to return to him.

The game is still in a **very early stage of development**. Right now, I'm focusing on building the procedural generation and the systems that will support the gameplay loop before adding the full combat and story experience. Think of it less as "early access" and more as "very, *very* early access" — the miner isn't the only thing that's a work in progress here.

## 🎮 Current Build

The current version is primarily a **technical gameplay prototype**.

You can explore a mine that is generated differently every time you start a run. The game currently generates:

* Procedural room layouts
* Floors
* Walls
* Room decorations
* Environmental obstacles
* Connections between rooms

The rooms are currently mostly empty. This is intentional: I'm still creating the assets and systems for the obstacles, enemies, and other gameplay elements that will eventually populate them. The miner is dead, but at least he's not bored — yet.

There is currently **no complete gameplay loop or story experience yet**.

### Play it in your browser

**[Play Ephemeral on itch.io](https://nicormu.itch.io/ephemeral)**

The current build is available directly in the browser, so no installation is required to try the prototype. No pickaxe needed either.

---

## 🕯️ The Idea

The main idea behind **Ephemeral** is to connect exploration and progression with the protagonist's lost memories.

The miner is already dead when the game begins.

As he explores the mine, he encounters enemies and bosses and gradually remembers fragments of his previous life. Instead of simply progressing through increasingly difficult rooms, the dungeon becomes a way of uncovering **who he was, what happened to him, and why he died**.

The procedural generation supports this idea by making every run feel slightly different while keeping the mine recognizable as the same place.

The story and memory system are planned for later stages of development.

---

## 🧩 How the Procedural Generation Works

The mine is built from reusable room pieces.

When a run starts, the game generates a new arrangement of rooms and connects them together. The individual rooms are designed beforehand, but their positions and connections can change between runs.

This allows me to create a large number of possible mine layouts without manually designing every possible dungeon. It also means I get to blame the RNG instead of my level design whenever a run feels unfair.

The generation system is currently one of the main technical focuses of the project.

### Current generation systems

| System                     | Status            |
| --------------------------- | ----------------- |
| Procedural room generation | ✅ Working         |
| Room connections           | ✅ Working         |
| Floor generation           | ✅ Working         |
| Wall generation            | ✅ Working         |
| Decorations                | ✅ Working         |
| Obstacles                  | ✅ Working         |
| Enemies                    | 🚧 In development |
| Combat                     | 🚧 Planned        |
| Bosses                     | 🚧 Planned        |
| Memory/story system        | 🚧 Planned        |

---

## 🎨 Current Visual State

The project uses a **2D pixel-art style** with a dark, supernatural atmosphere.

The visual direction is still being developed alongside the gameplay systems. Because the project is currently focused on procedural generation and foundational systems, the rooms are not yet populated with the final set of enemies, obstacles, and environmental assets.

**Screenshots will be added here as the project reaches a more presentable milestone.**

---

## 🛠️ Built With

* **Unity 6.4**
* **C#**
* **Unity Tilemaps**
* **2D Physics**
* **Procedural Generation**
* **Pixel Art** (authored in Pixelorama)

No external hardware is required. Just patience, and possibly a small candle for atmosphere while you code.

---

## ▶️ Running the Project

### Option 1 — Play the Current Build (recommended)

The easiest way to try **Ephemeral** is through Itch.io:

**[nicormu.itch.io/ephemeral](https://nicormu.itch.io/ephemeral)**

The current build runs in the browser. No installation, no Git, no digging required.

### Option 2 — Clone and Run It Yourself

If you want to poke around the source, break something, or see how deep this mine actually goes:

1. **Clone the repository**
   ```bash
   git clone https://github.com/Nicormu/Ephemeral.git
   cd Ephemeral
   ```

2. **Open it in Unity**
   * Install **Unity 6.4** (or the closest matching Unity 6 LTS release) via Unity Hub.
   * In Unity Hub, click **Add** → select the cloned `Ephemeral` folder.
   * Open the project. First import may take a few minutes — Unity is reindexing an entire mine, give it a moment.

3. **Open the main scene**
   * In the Project window, navigate to the Scenes folder and open the dungeon/gameplay scene.
   * Press **Play**. A new procedural layout generates each run (seeded, so it's reproducible if you need to debug a specific one).

4. **(Optional) Build it yourself**
   * `File → Build Settings → WebGL` (or your platform of choice) → **Build**.
   * For a browser build like the itch.io one, WebGL is the way to go.

### Deploying a Build to itch.io

1. Build the project for **WebGL** via `File → Build Settings → WebGL → Build`, output to a folder.
2. Zip the contents of that build folder (the `index.html` should be at the root of the zip, not nested in a subfolder).
3. On itch.io, go to your project's **Edit** page → **Uploads** → upload the zip → check **"This file will be played in the browser"**.
4. Set the correct viewport dimensions to match your build's resolution, save, and you're deployed.

---

## 🤖 AI Usage

AI tools (Claude, by Anthropic) were used throughout development to help write, debug, and refactor portions of the codebase — things like the dungeon generation logic, tilemap/wall systems, and health/enemy architecture were built with AI assistance alongside my own design decisions and iteration. All code and art in this repository are original; AI was used as a development tool, not as a source of pre-made assets or someone else's code. I still get the credit (and the blame) for the design choices — the ghosts didn't write themselves.

---

## 💡 Why I Made Ephemeral

I started **Ephemeral** because I wanted to build a game around procedural generation while also learning how the different systems of a roguelite fit together.

The procedural mine is the first major part of that experiment. Instead of creating every level manually, I'm building a system that can assemble the mine dynamically from reusable rooms and their connections.

From there, I want to build the rest of the game around the miner's story: exploration, combat, bosses, and memories that slowly reveal what happened before the game began.

The current build is only the beginning, but the systems being developed now are the foundation for that final experience.

---

## 📜 Credits

**Ephemeral** is an original project by **Nicolás / Nicormu**.

Sprites and ideas by **Juan Andrés Granados** ([@Juanggs777](https://github.com/Juanggs777)) — thanks for helping give this haunted mine some visual soul.

The game is inspired by the roguelite genre and games such as *The Binding of Isaac*, but the game's procedural systems, story, characters, and implementation are being developed specifically for Ephemeral.
