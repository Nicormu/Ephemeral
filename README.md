# 🕯️ Ephemeral

> **A 2D roguelite about a miner exploring the mine where he died—and slowly remembering how he got there.**

**Ephemeral** is a 2D procedural roguelite inspired by games like *The Binding of Isaac*. You play as a miner who has died inside a mysterious mine. As you explore deeper, fight enemies, and eventually face bosses, pieces of his life before death begin to return to him.

The game is still in a **very early stage of development**. Right now, I'm focusing on building the procedural generation and the systems that will support the gameplay loop before adding the full combat and story experience.

## 🎮 Current Build

The current version is primarily a **technical gameplay prototype**.

You can explore a mine that is generated differently every time you start a run. The game currently generates:

* Procedural room layouts
* Floors
* Walls
* Room decorations
* Environmental obstacles
* Connections between rooms

The rooms are currently mostly empty. This is intentional: I'm still creating the assets and systems for the obstacles, enemies, and other gameplay elements that will eventually populate them.

There is currently **no complete gameplay loop or story experience yet**.

### Play it in your browser

**[Play Ephemeral on itch.io](https://nicormu.itch.io/ephemeral)**

The current build is available directly in the browser, so no installation is required to try the prototype.

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

This allows me to create a large number of possible mine layouts without manually designing every possible dungeon.

The generation system is currently one of the main technical focuses of the project.

### Current generation systems

| System                     | Status            |
| -------------------------- | ----------------- |
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
* **Pixel Art**

No external hardware is required.

---

## ▶️ Running the Project

### Option 1 — Play the Current Build

The easiest way to try **Ephemeral** is through Itch.io:

**[nicormu.itch.io/ephemeral](https://nicormu.itch.io/ephemeral)**

The current build runs in the browser.

### Option 2

Why you need another option when the game is in the browser?  😌

---

## 💡 Why I Made Ephemeral

I started **Ephemeral** because I wanted to build a game around procedural generation while also learning how the different systems of a roguelite fit together.

The procedural mine is the first major part of that experiment. Instead of creating every level manually, I'm building a system that can assemble the mine dynamically from reusable rooms and their connections.

From there, I want to build the rest of the game around the miner's story: exploration, combat, bosses, and memories that slowly reveal what happened before the game began.

The current build is only the beginning, but the systems being developed now are the foundation for that final experience.

---

## 📜 Credits

**Ephemeral** is an original project by **Nicolás / Nicormu**.

The game is inspired by the roguelite genre and games such as *The Binding of Isaac*, but the game's procedural systems, story, characters, and implementation are being developed specifically for Ephemeral.
