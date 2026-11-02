# Word Play - Archipelago Multiworld Randomizer

An Archipelago multiworld randomizer mod for the word-building deckbuilder game **Word Play** (Unity Mono / Steam).

Spell high-scoring words, survive challenging rounds, and unlock game-changing modifiers, bonus letter racks, and tile upgrades while sending checks and receiving items to and from other games in your Archipelago multiworld.

---

## Installation & Setup

Go to the [**Releases**](https://github.com/Roulesss/WordPlayArchipelago/releases) page and download:
   - **`word_play.apworld`** — The Archipelago world definition package.
   - **`WordPlayArchipelago_vX.X.X_with_BepInEx.zip`** — Complete bundle containing BepInEx 5 and the mod.

#### 1. Archipelago Server Setup
- Place **`word_play.apworld`** into your Archipelago `custom_worlds/` directory (or double-click to install).
- Generate and place your configured **`WordPlay.yaml`** into your Archipelago `Players/` folder.
- Generate and start your Archipelago multiworld room.

#### 2. Word Play Game Setup
1. Open your game root folder:
   - In Steam: Right-click **Word Play** $\rightarrow$ **Manage** $\rightarrow$ **Browse local files**
   - Default path: `C:\Program Files (x86)\Steam\steamapps\common\Word Play`
2. Extract the contents of **`WordPlayArchipelago_vX.X.X_with_BepInEx.zip`** directly into your game folder.
3. Your game directory should now look like this:
   ```
   Word Play/
   ├── BepInEx/
   │   ├── core/
   │   └── plugins/
   │       └── WordPlayArchipelago/
   │           ├── WordPlayArchipelago.dll
   │           ├── Archipelago.MultiClient.Net.dll
   │           ├── Newtonsoft.Json.dll
   │           └── Assets/
   ├── winhttp.dll
   ├── doorstop_config.ini
   ├── .doorstop_version
   └── Word Play.exe
   ```

---

## Launching, Connecting, and Playing

1. **Configure Connection:**
   Edit `BepInEx/config/com.archipelago.wordplay.cfg` (or configure via in-game UI):
   ```ini
   [Archipelago]
   ServerAddress = archipelago.gg
   ServerPort = 38281
   SlotName = Player1
   Password = 
   DeathLink = false
   ```
2. Launch **Word Play** via Steam.
3. From the Main Menu, choose **Play Game** to open the Mode Selection screen.
4. On the mode selector rack, click the **`AP`** tile under the **`ARCHIPELAGO`** section:
   - Displays your Server Address, Port, and Slot Name.
   - Click **`CONNECT`**: the mod establishes connection in the background without blocking the UI.
   - Any manual edits to the config file auto-reload when you click **`CONNECT`** / **`RETRY CONNECT`**.
5. Once connected:
   - The top banner shows **`ARCHIPELAGO: <DIFFICULTY> MODE`**, your slot name, room address, total rounds, and victory progress (`Wins: X/Y`).
   - Click **`PLAY`** to start your run!
6. **In-Game Overlay:**
   - The HUD features two panels for Traps and Items (For testing purposes, can be used if a problem occurred) - Both panels can be hidden with the button at the bottom of their list.

---

## Gameplay & Mechanics

### Locations (Checks)

1. **Round Clears**: Clearing each round awards a check (e.g. Rounds 1–10 on Easy, up to Round 20 on Marathon).
2. **Shop Draft Purchases (Dynamic Rotation)**:
   - Between rounds, the shop offers 3 cards corresponding to checks in the multiworld.
   - **Dynamic Pool**: Shop checks are drawn from your remaining unchecked shop locations.
   - **Reroll / Refresh Rotation**: Refreshing modifiers in the shop also rolls fresh AP checks, ensuring you can discover new locations.
   - **Run Retry Rotation**: Each time you retry after a loss or win, the shop check pool rotates and shuffles so early rounds do not repeatedly present the same completed locations.
   - **Scouting**: Cards display the scouted item name, recipient player, and classification (Progression, Useful, Trap, or Filler).
   - **Check-Only Placeholder Cards**: If you have not yet unlocked enough modifiers to fill all shop slots, the empty slots appear as check-only AP cards so you can still check locations.
   - **Modifier Refresh Guarantee**: If you have 3 or fewer modifiers unlocked, refreshing your modifiers re-rolls them without turning them into placeholders.
3. **Single-Word Score Milestones**: Spelling high-value words awards checks immediately when submitted:
   - `Word Score: 30 Points`
   - `Word Score: 50 Points`
   - `Word Score: 80 Points`
   - `Word Score: 150 Points`
   - `Word Score: 300 Points`
   - `Word Score: 500 Points` *(and up to 1000 on higher difficulties)*
4. **Cumulative Run Score Milestones**: Reaching cumulative score thresholds across a run:
   - `Run Score: 250 Points`
   - `Run Score: 500 Points`
   - `Run Score: 1000 Points`
   - `Run Score: 1500 Points`
   - `Run Score: 2000 Points`
   - `Run Score: 3000 Points` *(and up to 7500 on higher difficulties)*

---

### Items Received from Multiworld

- **Progressive Word Length**: Increases your maximum allowed word length capacity permanently (from starting preset 5 up to 20 length).
- **Extra Board Slot**: Grants permanent extra tile slots on your rack.
- **Extra Plays (+2)** & **Extra Refreshes (+2)**: Supplements your pool of plays and discards.
- **Tile Packs**: Injects special tiles into your letter bag (Golden, Diamond, Emerald, and Dot tiles).
- **Perk Unlocks**: Grants specific modifiers, tile changes, or tile additions to appear in shops (when perk locking is enabled).
- **Filler Buffs**:
  - `+5` or `+10` Word Score Buff (applied to your next submitted word).
  - `+1 Play` & `+1 Refresh`.
  - Single special tile injections.

---

### Traps & Hazards

- **Screamer Trap**: har har har har.
- **Bad Letter Slot Trap**: Neutralizes a random letter slot on your rack so it yields 0 points for the next word.
- **Lose Play & Lose Refresh Traps**: Instantly drains 1 play or 1 discard.
- **Delete Bag Letter Trap**: Permanently removes a random tile from your bag.
- **Vowels Zero Score Trap**: All vowels score 0 points on the next word.
- **Scramble Board Trap**: Shuffles and replaces your current hand of tiles.

---

### Death Link (Optional)

- Configurable via `WordPlay.yaml`.
- **Outgoing**: If you run out of plays (`GameOver`), a death signal is broadcast to all Death Link players.
- **Incoming**: When another player dies, an alert banner is displayed and your run terminates immediately.

---

### Victory Condition

- Clearing the final round of your multiworld difficulty counts as 1 completed run.
- When you reach the required number of victories specified by `wins_required` in your YAML, the multiworld goal is completed!

---

## Archipelago Options (`WordPlay.yaml`)

| Option | Type | Default | Description |
|---|---|---|---|
| `difficulty` | Choice | `normal` | Difficulty mode: `easy` (10 rounds), `normal` (12 rounds), `hard` (12 rounds), `legendary` (14 rounds), `marathon` (20 rounds). |
| `wins_required` | Range (1–10) | `1` | Number of completed runs required to satisfy the goal. |
| `starting_word_length` | Range (3–7) | `5` | Initial maximum letters allowed per word before Progressive upgrades. |
| `trap_chance` | Range (0–100) | `20` | Percentage of filler items replaced with traps. |
| `death_link` | Boolean | `false` | Enable Death Link synchronization. |
| `lock_modifiers` | Boolean | `false` | When true, modifiers are locked and must be unlocked as AP items. |
| `starting_modifiers` | Range (0–20) | `3` | Number of unlocked modifiers granted at the start when modifiers are locked. |
| `lock_tile_changes` | Boolean | `false` | When true, tile change perks must be unlocked as AP items. |
| `starting_tile_changes` | Range (0–20) | `2` | Starting tile change perks granted when locked. |
| `lock_tile_additions` | Boolean | `false` | When true, tile addition perks must be unlocked as AP items. |
| `starting_tile_additions` | Range (0–20) | `2` | Starting tile addition perks granted when locked. |


## Known bugs/problems

### 1. WebSocket Compression Warning when connecting to game

Can it be added without sacrificing features? No.

Explanation:
- Archipelago.MultiClient.Net runs on Unity's Mono runtime using .NET Standard 2.0. The underlying .NET WebSocket client (System.Net.WebSockets.ClientWebSocket) only gained support for RFC 7692 permessage-deflate (WebSocket compression) in .NET 7+.
- Supporting compression inside Unity Mono would require ripping out Archipelago.MultiClient.Net's built-in session transport and substituting a custom, unmanaged/native C++ library.
- What would be sacrificed: connection stability, automatic reconnect handling, thread safety with Unity's main thread, and cross-platform compatibility.
- Server Impact: The server message is only an advisory notice. Archipelago servers natively support uncompressed WebSockets; DeathLink, hint checks, item sync, and bounce packets work with zero functional difference.

### 2. Client-side history

There's currently no Client-side history of all checks sent and received. Something will very probably be implemented for it in the coming days/weeks.

### 3. The mod may be ugly

I don't really care that much.