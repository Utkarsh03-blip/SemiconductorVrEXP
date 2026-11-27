# P-N Junction VR Lab — Design Specification

This document consolidates Sections 3–8 of [MASTER_PROMPT.md](MASTER_PROMPT.md) into the concrete architecture and implementation plan for the project.

---

## 1. Lab Layout & Physical World

* **Scale & Units:** $1\text{ Unity unit} = 1\text{ m}$. Floor at $y = 0$.
* **Room Dimensions:** Approximately $16\text{ m} \times 11\text{ m} \times 3.2\text{ m}$ clean modern semiconductor physics laboratory:
  * Tiled floor ($y = 0$), clean matte walls, side windows, overhead ceiling light panels, periodic table wall poster (highlighting Group III, IV, V in relevant stations), lab safety signs, decorative fume hood, and storage shelves.
* **Station Flow (U-Shaped Learning Order):**
  1. **Entrance / Player Spawn**
  2. **T. Tutorial Pad** (practice core VR interactions)
  3. **Station 1: Intrinsic Silicon**
  4. **Station 2: N-Type Doping**
  5. **Station 3: P-Type Doping**
  6. **Station 4: Junction Formation**
  7. **Station 5: Biasing**
  8. **Station 6: I-V Characteristics**
  9. **A. Assessment Whiteboard**
* **Station Construction:**
  * Each station is self-contained with its own prefab and controller.
  * Workbenches/tables are **$0.9\text{ m}$ high** with clear flat surfaces for grabbable props.
  * Each station includes a **floor number sign**, a **floating hologram title**, and a **teleport anchor** positioned comfortably in front of the workbench.
  * Stations unlock sequentially via `LessonManager`; locked stations are visually dimmed. A **Skip** button is available only in the Debug Panel (Editor / Development build).

---

## 2. Interaction System (100% XRI + Input System)

* **Headset-Agnostic XRI Stack:**
  * Built strictly with XR Interaction Toolkit (XRI), OpenXR, and the Unity Input System (`InputSystem_Actions` / XRI Default Input Actions). No legacy `UnityEngine.Input` calls and no hard-coded gameplay keyboard/mouse checks.
  * Desktop testing uses the **XR Device Simulator** only.
* **Locomotion:**
  * **Teleportation** (default): `TeleportationProvider` with `TeleportationArea` on the lab floor and `TeleportationAnchor` pads at each station.
  * **Continuous Move** (toggleable in Lab Tablet settings): `ContinuousMoveProvider` paired with a **comfort tunneling vignette** while moving.
  * **Snap Turn**: `SnapTurnProvider` (no camera shake).
  * Seated & standing comfort supported via height calibration / `TrackingOriginMode`.
* **Hand Interactors (Both Left & Right Controllers):**
  * **Direct Interactor** (`XRDirectInteractor`) for grabbing, holding, dropping, and direct poke/pinch interaction.
  * **Ray Interactor** (`XRRayInteractor`) for distant pointing, world-space UI interaction, NPC dialogue navigation, and ray-hover tooltips/labels.
  * **Pinch Input:** Mapped to XRI **Select** action on direct/poke interaction (with hand-expression simulation if supported by the installed XRI package; otherwise mapped cleanly to Select and documented in [CONTROLS.md](CONTROLS.md)).
* **Interactables & Physics:**
  * Minimum interactable dimension $\ge 5\text{ cm}$ (`0.05 m`) with `Rigidbody` and `Collider`.
  * `RespawnZone`: Items dropped out of lab/table bounds automatically respawn at their home table position; each table also provides a physical **"Return Item"** button.
  * **Sockets** (`XRSocketInteractor`): Snap objects into place with a valid/invalid color ring and procedural snap sound.
  * **Physical Controls:** Rotary knobs, levers, push buttons, and sliders — all grabbable or pokeable via XRI.
* **Interaction Feedback (`InteractionFeedback`):**
  * Visual hover highlight/outline, ray-hover tooltip, procedural `AudioClip` generation in code (no external audio assets), and controller haptic hooks (graceful no-op on desktop simulator).
* **Lab Tablet (`LabTablet`):**
  * Grabbable world-space tablet containing current objective, step list, semiconductor glossary, controls reference, hint button, and comfort/accessibility settings (text size, locomotion mode).

---

## 3. NPC Scientist (`NPCController` & `DialogueSystem`)

* **Visuals & Model Slot:**
  * Placeholder capsule rig with an `NPCModelSlot` transform so a user-supplied 3D model can drop in later without breaking scripts.
  * Optional `Animator` parameters (`Idle`, `Talk`, `Walk`, `Point`, `Nod`) with safe parameter-existence checks and graceful fallback when missing.
  * Configurable NPC name.
* **Speech Bubble & Subtitles:**
  * World-space billboarded bubble positioned above the NPC's head with auto-sizing, large readable TextMeshPro subtitles font, typewriter reveal, **"Next"** button clickable by ray or direct poke, and optional audio playback hook.
* **Navigation & Behaviour:**
  * Walks via `NavMeshAgent` to the active station's NPC marker, faces the player, introduces concepts, guides steps, reacts to correct/wrong actions, and wraps up each station.
* **Data-Driven Dialogue & Lessons:**
  * `DialogueLine` ScriptableObjects (`text`, `emotion`, optional `choices`, optional `audioClip`), plus branching **"Ask me about..."** glossary buttons.
  * `LessonStep` ScriptableObjects driven by `LessonManager` (dialogue + required action + completion event -> updates `ProgressHUD` and station unlock state).
* **Adaptive Hints:**
  * Soft hint after **20 s** without progress; stronger hint after **45 s** with the NPC pointing toward the relevant target object.

---

## 4. Station Summaries (Phases 3–7)

| ID | Station | Core Interactions & Visuals |
| :--- | :--- | :--- |
| **T** | **Tutorial Pad** | Practise teleporting to an anchor, grabbing a test cube, dropping it into a socket, pressing a push button, and ray-pointing at a target screen. |
| **1** | **Intrinsic Silicon** | Periodic table Group IV highlight; hold Atomic Lens over Si wafer to reveal 4-bond lattice hologram ("Not to scale"); 0–500 K temperature knob breaking bonds and creating/recombining e⁻/h⁺ pairs; live $n = p = n_i$ and conductivity meters; $1.12\text{ eV}$ band diagram; Si/Ge toggle; ray tooltips on atoms/bonds. |
| **2** | **N-Type Doping** | Shelf of dopant vials (P, As, Sb, B, Al, Ga, Ge, C); drop into diffusion-furnace socket with Si wafer; wrong-group feedback from NPC; pentavalent lattice with donor level ($E_D$), fixed $+$ ion and free e⁻; $10^{14}\text{–}10^{18}\text{ cm}^{-3}$ doping slider updating $n$, $p = n_i^2/n$, $E_F$, conductivity; produces grabbable N-type block. |
| **3** | **P-Type Doping** | Same furnace flow for trivalent acceptors; visualises hole motion as neighbouring valence electrons hopping into the vacancy; produces grabbable P-type block. |
| **4** | **Junction Formation** | Socket N & P blocks into junction jig and press Join; time-lapse simulation (play/pause/step/speed) from $t=0$ sharp gradient through diffusion, recombination, uncovered fixed ions, depletion growth, $N\to P$ E-field, drift, and equilibrium; 4 live plots ($n/p$, $\rho$, $E$, $V$) + band-bending diagram; $N_A, N_D$ sliders updating $V_0, W$ asymmetrically; layer toggles. |
| **5** | **Biasing** | Battery with voltage knob + red/black leads plugged into diode P/N sockets; circuit with series resistor, ammeter, voltmeter, LED; live depletion width, barrier height, carrier flow; LED lights above knee in forward bias; reverse leakage & widened depletion; Zener/Avalanche breakdown + protective-resistor warning & Reset button. |
| **6** | **I-V Characteristics** | Socket diode cartridges (Si, Ge, LED, Zener); sweep voltage by knob or auto-sweep; world-space I-V curve with knee marker, reverse leakage inset zoom, Record Point button + min 8-point data table, temperature knob shifting curve, ideal Shockley curve overlay. |
| **A** | **Assessment Board** | 10 ray-clicked MCQs covering all stations + 1 practical circuit challenge (forward-bias LED); score, wrong-answer explanations, completion summary. |

---

## 5. Architecture & Folder Structure

* **Namespace:** `PNJunctionLab` (tests in `PNJunctionLab.Tests`).
* **Asset Root:** `Assets/PNJunctionLab/`
  * `Scripts/` (Core, XR, Physics, Stations, NPC, UI, Audio, Debug, Editor/Tests)
  * `Prefabs/`
  * `Scenes/` (Main Lab scene: `PNJunctionLab.unity`)
  * `Materials/`
  * `Data/` (ScriptableObjects: lessons, dialogue, quiz questions)
  * `Audio/` (procedural audio generator/configs)
  * `UI/` (TMP fonts/presets, world-space UI prefabs)
  * `NPC/` (placeholder rig + future user model slot)
* **Core Classes:** `SemiconductorModel`, `LessonManager`, `StationController`, `DialogueSystem`, `NPCController`, `LabTablet`, `ProgressHUD`, `DebugPanel`, `InteractionFeedback`, `RespawnZone`.
* **Performance & Visual Standards:**
  * Palette: Electrons **Blue**, Holes **Red**, $+$ Ions **Orange**, $-$ Ions **Teal**.
  * World-space TMP text sized for **$\ge 1.5\text{ m}$** reading distance.
  * Quest 2 budget: **$< 150\text{k}$ triangles** in view, **max 1 realtime directional light** + baked/simple lighting, minimal shared materials, **$\le 300$ particles per view** via pooling/instancing, **72 FPS** target.
