# P-N Junction VR Lab (Unity) — Project Rules

These rules are always active. Follow them in every task.

## Project documents
- The full specification is Docs/MASTER_PROMPT.md. Read it at the start of every session and before each phase.
- Do ONLY the phase the user names. After finishing a phase: test in Play mode, check the Console, update Docs/PROGRESS.md, summarise what was done and what was tested, then STOP and wait for approval.
- Docs/SCIENCE.md is the authority on all science content. Keep it updated.

## Stack
- Use the project's existing Unity version and URP. Never upgrade Unity or packages without asking.
- Use XR Interaction Toolkit (XRI), OpenXR, Input System (new) and TextMeshPro. No legacy Input Manager.
- Before writing XR code, check the installed XRI version in Package Manager and use that version's API and namespaces (2.x and 3.x differ).

## Unity MCP workflow
- Do all editor actions (scenes, GameObjects, components, packages) through Unity MCP instead of describing manual steps.
- After every script change, wait for compilation and read the Console. Fix all errors before continuing.
- Enter Play mode to test each feature and report what passed and what failed.
- If MCP cannot do something, say so and give exact manual steps. Never claim success without testing.

## Architecture
- Everything lives in Assets/PNJunctionLab/ (Scripts, Prefabs, Scenes, Materials, Data, Audio, UI, NPC).
- Namespace PNJunctionLab. Small single-purpose MonoBehaviours with [SerializeField] private fields.
- Use C# events or UnityEvents between systems. No FindObjectOfType in Update.
- Dialogue and lesson steps are ScriptableObjects, not hard-coded.
- Keep each station self-contained (own prefab and controller) so it can be tested alone.
- Create assets from scratch using primitives, materials, shaders and particles. No paid or external assets. Generate sounds procedurally in code. Only the NPC model is user-supplied. Use a placeholder capsule until it is provided.

## Science accuracy
- All physics lives in one pure C# class, SemiconductorModel, with EditMode tests.
- Never hard-code physics numbers inside visual scripts. Read them from SemiconductorModel.
- If a science fact is uncertain, state the assumption in Docs/SCIENCE.md instead of guessing.

## Headset-agnostic input
- All interaction uses XRI components (XRGrabInteractable, XRSocketInteractor, XRRayInteractor, XRDirectInteractor, TeleportationProvider, locomotion providers).
- Desktop play uses the XR Device Simulator only. Never hard-code keyboard or mouse logic in gameplay scripts.
- Pinch = Select action on direct or poke interaction. Use hand-expression simulation if the installed XRI supports it, otherwise map to Select and say so.

## World and comfort
- 1 Unity unit = 1 m. Floor at y = 0, tables 0.9 m high, interactables at least 5 cm with Rigidbody and Collider.
- Teleportation plus snap turn by default, continuous move optional. No camera shake.
- Quest-friendly: URP, baked or simple lighting, one realtime light max, low particle counts (about 300 per view), few materials.
- Dropped objects that leave the room bounds respawn at their table.

## Safety
- Never delete or overwrite user files (especially the NPC model) without asking.
- Keep Docs/CONTROLS.md and Docs/PROGRESS.md current.
