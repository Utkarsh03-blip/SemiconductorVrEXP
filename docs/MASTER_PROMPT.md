# SEMICONDUCTOR VIRTUAL LAB (Unity VR) — MASTER PROMPT

(Save this file as Docs/MASTER_PROMPT.md in the Unity project.)

Build an interactive, academically accurate VR lab in this Unity project via Unity MCP. It teaches: (1) intrinsic semiconductors, (2) extrinsic semiconductors (n-type and p-type doping), (3) p-n junction formation, (4) forward/reverse bias behaviour, (5) the diode I-V characteristic. Follow the workspace rules. No headset is available, so test with the XR Device Simulator (mouse/keyboard). Build everything with XRI so a headset works later without rewrites.

## 0. How to work
- FIRST create Docs/SCIENCE.md (from section 2), Docs/DESIGN.md (from sections 3-8), Docs/CONTROLS.md and Docs/PROGRESS.md. Re-read them at the start of every phase.
- Work phase by phase (section 9). After each phase: Play-mode test, check Console, update PROGRESS.md, summarise, STOP and wait for approval.
- All physics comes from one pure C# class, SemiconductorModel (no UnityEngine dependency). Visuals read from it. Add EditMode tests for the formulas.

## 1. Learning outcomes
After the lab the student can: explain electron-hole pair generation; explain why doping raises conductivity; identify donor/acceptor atoms and majority/minority carriers; explain how the depletion region and built-in potential form; predict what forward and reverse bias do; read an I-V curve (knee voltage, saturation current, breakdown).

## 2. Science reference (authoritative; do not invent facts)
INTRINSIC: Pure Si/Ge, group IV, 4 valence electrons, covalent diamond lattice. At 0 K: no free carriers (insulator). With heat, bonds break, creating free electron + hole pairs (generation), which also recombine. n = p = ni. Eg: Si 1.12 eV, Ge 0.67 eV. ni (300 K): Si ≈ 1.5e10 cm^-3, Ge ≈ 2.4e13 cm^-3. Conductivity rises with temperature.

N-TYPE: pentavalent donors (P, As, Sb). 4 electrons bond; the 5th is free at room temperature. Donor level sits just below the conduction band (~0.045 eV for P in Si). Result: free electron + fixed positive ion. Majority = electrons, minority = holes. n ≈ ND, p = ni²/n. Fermi level shifts toward the conduction band.

P-TYPE: trivalent acceptors (B, Al, Ga, In). One bond is incomplete, giving a hole. Acceptor level sits just above the valence band. Result: free hole + fixed negative ion. Majority = holes, minority = electrons. p ≈ NA, n = ni²/p. Fermi level shifts toward the valence band.

Both extrinsic types stay electrically neutral overall. Mass-action law: n·p = ni². Typical doping 1e14-1e18 cm^-3 (roughly 1 dopant per 10^6-10^8 Si atoms).

JUNCTION AT EQUILIBRIUM: Concentration gradient makes electrons diffuse N→P and holes P→N, where they recombine. This leaves uncovered fixed ions (+ donors on N side, − acceptors on P side) = depletion region (no free carriers). The electric field points N→P and causes drift current opposing diffusion. Equilibrium: net current = 0, depletion width stable. Built-in potential V0 = VT·ln(NA·ND/ni²), VT = kT/q ≈ 25.9 mV at 300 K (Si ≈ 0.7 V, Ge ≈ 0.3 V). Depletion width W = sqrt( 2ε(V0 − V)/q · (1/NA + 1/ND) ), extending further into the lighter-doped side (xn = W·NA/(NA+ND)). Example: Si, NA = ND = 1e16 → V0 ≈ 0.69 V, W ≈ 0.4 µm. Fermi level is flat across the junction and the bands bend by qV0.

FORWARD BIAS (P to +): external field opposes the built-in field, so the barrier drops to V0 − V and the depletion region narrows. Majority carriers cross and current grows exponentially: I = Is(exp(V/(n·VT)) − 1), n = 1 to 2. Knee: Si ≈ 0.7 V, Ge ≈ 0.3 V, LED ≈ 1.8-3 V. A series resistor is needed to limit current.

REVERSE BIAS (P to −): barrier rises to V0 + VR and the depletion region widens. Only a tiny reverse saturation current Is (minority carriers) flows, and it rises with temperature. Breakdown: Zener (heavy doping, lower voltages, tunnelling) and avalanche (lighter doping, higher voltages, impact ionisation). Forward voltage drops about 2 mV per °C.

Visual scale note: show a "not to scale" label on all atomic-scale visuals.

## 3. Lab layout (build from scratch)
- Room about 16 m × 11 m × 3.2 m, clean modern lab: tiled floor, windows, ceiling light panels, periodic-table poster, safety signs, fume hood (decor), storage shelves.
- Player spawns at the entrance. Stations are in a U-shape in learning order: Tutorial Pad, Station 1 to Station 6, Assessment Whiteboard. Each has a floor number sign, a hologram title and a teleport anchor in front.
- Tables are 0.9 m high, clearly labelled, with enough clear surface for grabbable items.
- Stations unlock in order, locked ones are dimmed. A "Skip" option exists in the debug panel only.

## 4. Interaction system (all via XRI)
- Locomotion: teleport (default, with anchors and areas), continuous move (toggle), snap turn, comfort vignette on continuous move.
- Interactors: direct (grab/pinch) and ray (distant pointing, UI, NPC, tooltips) on both hands.
- Grab, hold and drop with Rigidbody physics. Out-of-bounds items respawn at their home spot, and a "return item" button exists on each table.
- Sockets: snap objects into place with a valid/invalid colour ring and a snap sound.
- Controls: rotary knobs, levers, push buttons, sliders, all grabbable or pokeable.
- Pinch: Select via direct or poke interaction. Use hand simulation if XRI supports it, otherwise map to Select and say so.
- Feedback on every interaction: hover highlight/outline, tooltip, procedural sound (generate AudioClips in code), haptic hooks (no-op on desktop).
- Lab Tablet: a grabbable world-space tablet that holds the current objective, step list, glossary, controls help, hint button and settings (text size, locomotion mode).

## 5. NPC scientist
- Placeholder capsule for now, with an NPCModelSlot so my 3D model drops in later. Optional Animator parameters (Idle, Talk, Walk, Point, Nod) with a graceful fallback when missing. Name is configurable.
- Speech bubble: world-space, above the head, billboarded, typewriter text, "Next" via ray or click, auto-size, subtitles-style readable font, optional audio hook.
- NPC walks (NavMesh) to the station the player is at, faces the player, and introduces, guides, reacts to correct/wrong actions, and wraps up.
- Dialogue data is ScriptableObjects (DialogueLine: text, emotion, optional choices, optional audio). Branching "Ask me about…" questions via ray-clicked glossary buttons.
- Hints: first after 20 s of no progress, a stronger hint after 45 s, and the NPC points to the relevant object.
- A LessonManager runs steps (dialogue + required action + completion event) and drives progress HUD and station unlocking.

## 6. Stations
T. TUTORIAL PAD: practise teleport, grab a cube, drop it into a socket, press a button, ray-point at a screen. The NPC checks each skill.

1. INTRINSIC SILICON: wall chart highlights group IV. Grab an Atomic Lens and hold it over a silicon wafer to reveal a lattice hologram (4 bonds per atom). A temperature knob (0-500 K) breaks bonds, spawns electron-hole pairs and recombines them. Live meters show n = p = ni and conductivity. Show a band diagram with the 1.12 eV gap and an Si vs Ge toggle. Ray-point at atoms/bonds to get labels.

2. N-TYPE DOPING: dopant shelf with vials (P, As, Sb, B, Al, Ga, Ge, C). Grab one and drop it into the diffusion-furnace socket with a Si wafer. A wrong choice makes the NPC explain why (wrong group). A correct one shows a pentavalent atom in the lattice with its free electron, the donor level on a band diagram, fixed + ion plus free e-. Doping slider (1e14-1e18): n, p = ni²/n, Fermi-level meter and conductivity update. Output: a grabbable labelled N-type block.

3. P-TYPE DOPING: same flow with trivalent dopants and holes (visualise hole movement as neighbouring electrons hopping into the vacancy). Output: a grabbable labelled P-type block.

4. JUNCTION FORMATION: socket the N and P blocks into the junction jig, then press Join. Time-lapse animation (play/pause/step/speed): t=0 sharp gradient, diffusion, recombination, ions uncovered, depletion region grows, E-field arrow, drift appears, equilibrium. Four live graphs (carrier concentration, charge density, electric field, potential) and a band-bending diagram. Sliders for NA and ND update V0 and W (asymmetric extension into the lighter side). Layer toggles (holes, electrons, ions, field, drift/diffusion arrows).

5. BIASING: battery with a voltage knob and two leads (red/black). The player plugs the leads into the diode's P and N terminals (socket interaction). Polarity determines forward or reverse. Circuit includes a series resistor, ammeter, voltmeter and LED. The junction animation updates depletion width, barrier height and carrier flow live. The LED lights above the knee in forward bias. Reverse shows only a tiny leakage current and a widened depletion region. At high reverse voltage show breakdown (Zener/avalanche label) and a protective-resistor warning, with a Reset button.

6. I-V CHARACTERISTICS: grab and socket a diode type (Si, Ge, LED, Zener). Sweep the voltage by knob or auto-sweep. A world-space graph plots live with knee marker, inset zoom for reverse leakage, a Record Point button and data table (min 8 points), and a temperature knob that shifts the curve. Overlay the ideal Shockley curve for comparison.

A. ASSESSMENT BOARD: 10 MCQs (ray-click) covering all stations, then one practical challenge (build a forward-biased circuit that lights the LED). Show score, explanations for wrong answers and a completion summary.

## 7. Architecture
Namespace PNJunctionLab. Folders under Assets/PNJunctionLab/. Core: SemiconductorModel (+ tests), LessonManager, StationController (base), DialogueSystem, NPCController, LabTablet, ProgressHUD, DebugPanel (editor/dev build only: jump to station/step, time scale, skip dialogue, show model values), InteractionFeedback, RespawnZone. Carrier visuals use pooled particles or instanced meshes with a cap of about 300 particles per view.

## 8. Visual, performance, comfort
- Clean stylised look, consistent palette: electrons blue, holes red, + ions orange, − ions teal. Large readable TextMeshPro text, with minimum 1.5 m reading distance for world UI.
- Quest 2 budget: under 150k triangles in view, baked/simple lighting, one realtime light max, minimal materials, 72 FPS target.
- Seated and standing support (height calibration), subtitles on all dialogue.

## 9. Phases (stop after each)
1. Foundation: packages, docs, scene, XR Origin + simulator, locomotion, lab room, teleport anchors, tutorial pad.
2. Core systems: SemiconductorModel + tests, LessonManager, DialogueSystem, NPC placeholder, Lab Tablet, DebugPanel.
3. Station 1 (intrinsic).
4. Stations 2-3 (doping).
5. Station 4 (junction formation).
6. Stations 5-6 (biasing, I-V).
7. Assessment, polish, performance pass, final docs and test pass.

## 10. Acceptance (per phase and final)
No Console errors or warnings from our code; every interaction works via the Device Simulator; every number shown matches SemiconductorModel; station order and unlocking work; the NPC reacts to correct and wrong actions; CONTROLS.md lists the simulator keys for each interaction; the full lab can be completed start to finish.
