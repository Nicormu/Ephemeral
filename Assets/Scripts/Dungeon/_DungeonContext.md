# Dungeon System — Design Context & Open Questions

## Source: Discussion on 2026-07-15 → 2026-07-16 (nicor)

This file captures the context from our conversation about designing the dungeon system for Ephemeral. The remaining open questions are at the end.

---

## Current Project State

- **Project:** Unity project "Ephemeral"
- **Player controller:** `Assets/Scripts/Player/PlayerMovement.cs` — handles walking, rolling, cooldowns
  - `_canRoll` field is dead code (assigned but never read)
  - Uses `Time.unscaledTime` for cooldown (doesn't pause during death screens)

---

## Existing Dungeon Code (already written)

| File | What it does | Needs changes? |
|------|--------------|----------------|
| `RoomType.cs` | Enum with 6 types: Start, Normal, Treasure, Boss, DeadEnd, Corridor | No |
| `RoomConnector.cs` | Validates all rooms reachable via BFS from Start; uses bounding box overlap + 1-cell tolerance | No (still useful) |
| `RoomGenerator.cs` | BSP-like procedural room placer — generates positions/shapes from scratch each run | **Will be replaced** by hand-crafted approach below |
| `SeedManager.cs` | Deterministic seeding, crypto-grade entropy, `System.Random` tied to seed | No |

None of these are currently referenced by any other script in the project. No scenes or prefabs use them yet.

---

## New Design Decisions (Agreed)

### 1. Empty GameObject Manager
- A single empty GameObject will act as the dungeon orchestrator
- `DungeonManager.cs` will live on it (same role `RoomGeneratorManager` originally planned)

### 2. Hand-Crafted Rooms, Random Placement
- **Rooms are pre-designed** — you build them by hand with fixed shapes, floors, walls, portals
- **Which room appears where is random** — procedural selection from a pool of templates at runtime
- This is the "Hades / Dead Cells" pattern: limited library of crafted rooms, shuffled per playthrough

### 3. Fixed Dungeon Size
- Every floor is exactly **13 × 13 grid cells**
- Each floor is different (randomized via seed)
- The dungeon is finite — no infinite corridors

### 4. Room Sizes Vary
- Rooms can be small: 1×1, 2×1, 1×2
- Medium: 2×2
- Potentially larger (3×3, etc.)
- Each room has **portal positions** where connected rooms attach to it

---

## Architecture Overview

```
Assets/Scripts/Dungeon/
├── RoomType.cs                 ← existing (enum + structs)
├── RoomConnector.cs            ← existing (BFS validation)
├── SeedManager.cs              ← existing (deterministic seeding)
├── RoomTemplate.cs             ← NEW: hand-crafted room shapes + portal positions
├── FloorLayout.cs              ← NEW: fills 13x13 grid with templates, ensures connectivity
└── DungeonManager.cs           ← NEW: scene glue — initializes seed, generates floor, spawns visuals
```

### Generation Pipeline (Expected Flow)

```
1. SeedManager.Initialize()     → pick a random seed
2. RoomPool.Build()             → load all hand-crafted templates per type
3. FloorLayout.Generate(seed)   → fill 13x13 grid with rooms, guarantee Start + Boss path
4. DungeonManager.SpawnRooms()  → instantiate prefabs / tiles for each placed room
5. Player spawns at Start       → hand off position to PlayerMovement or SpawnManager
```

### Room Template (What You Need to Provide)

Each `RoomTemplate` represents one pre-designed room:
- **Shape** — which grid cells are walkable floor
- **Type** — Start / Normal / Treasure / Boss / DeadEnd / Corridor
- **Entrance portal** — where a connected room attaches INTO this room (normalized 0–1 in template space)
- **Exit portal(s)** — where this room leads OUT to another room

Examples:
```
  Template "corridor_h" (Normal, 2x1):    Template "chamber_square" (Normal, 2x2):
  ┌────┐                                      ┌───┬───┐
  │🚪 🚩│ ← entrance left, exit right         ├───┤   │
  └────┘                                      ├───┼ 🚩┤
                                               └───┴───┘
```

### Connectivity Rules (Planned but Not Finalized)
- Start must be at grid position (0,0) — top-left corner of the 13x13 grid
- Boss room must be reachable from Start via a continuous path through Normal/Corridor rooms
- Treasure rooms placed in "side branches" off the main path
- DeadEnd rooms at dead ends of branches
- All non-dead-end rooms must be reachable

---

## Open Questions (need answer before continuing implementation)

| # | Question | Why it matters |
|---|----------|----------------|
| **1** | **Room prefabs / tiles**: Do you have tile assets already? Should each cell use Tilemap, individual quads, or chunked meshes? How do room shapes look visually? |
| **2** | **Room variety count**: How many distinct hand-crafted room templates per type? (4 per type? 10?) Does the pool itself vary per seed? |
| **3** | **Connectivity rules**: Every room reachable from Start, or OK for some to be unreachable "decorations"? |
| **4** | **Portal direction rules**: Corridors must connect door-to-door? Boss can only have one entrance? Or full adjacency allowed? |
| **5** | **Player spawn integration**: Should DungeonManager tell PlayerMovement where to stand, or should SpawnManager handle that separately? |
| **6** | **DungeonManager vs RoomGeneratorManager**: Are these two managers (one for generation, one for runtime management) or just one combined? |

---

## Answers to Open Questions

1. I was tinking having different tilemaps, where the objects will be with one tag, void with other, traps with other and so on. By know I dont have any room, but I think the shape could be like a square. Later I could implement the L shape and de I shape.
2. I dont know by now, time could make me add more and more variations
3. I dont understand your question. I would like to have an start room and a boss room. It could be that start room has only one door and after investigating player could find a cross-road that could lead to a dead end or to the boss. This generation has to be randomly
4. Boss can only have one entrance. There might be dead ends.
5. Dungeon manager I think should spawn player at the middle of the starting room. When entering a new one, player should be on the part it should appear normally. For example, if player enter a room that is in the north, after entering, player should be on the south
6. Choose the best structure and logical method.

---


## What Was Already Written (as of this conversation)

### RoomTemplate.cs — COMPLETED
- `RoomTemplate` class: shape, type, entrance/exit portal positions, factory methods
- `RoomPool` static class: builds and stores template collections per room type
- `PlacedRoom` class: runtime representation during generation (position, connections list)
- Portals stored as normalized coordinates in template space (0–1)

### Still To Write
- **FloorLayout.cs** — the 13x13 grid filling algorithm with connectivity guarantees
- **DungeonManager.cs** — scene GameObject glue that ties seed → generation → spawning

---

## Notes About Current Code Issues

- `PlayerMovement._canRoll` is dead code — should be removed (line 32 and line 57 of PlayerMovement.cs)
- The existing `RoomGenerator.cs` uses procedural placement (generates shapes from scratch). It will be replaced by this hand-crafted approach.
- No existing scene or prefab references Dungeon types yet, so there's no migration risk.
