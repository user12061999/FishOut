# Fish Out gameplay alpha

Open `Assets/FishOut/Scenes/FishOut.unity`, enter Play mode, and press PLAY.
The scene is first in Build Settings. Use a 1080 x 1920 Game view.
`Fish Out > Set portrait Game view` sets this up in the editor.

## Implemented

- Hidden 6 x 8 board, four fixed directions, all four edges open.
- Tap a character to move to the first blocker or swim off screen. Positions persist after stopping.
- Ordinary characters and rocks do not cost lives. Each contact with a crab costs one of three lives, including repeated adjacent taps.
- Global tap lock during movement; no time or move limit.
- Automatic deadlock detection only when no character can move or escape.
- Hint and selected-character Rescue: one free successful use of each per stage. Canceling Rescue or an unsuccessful Hint costs nothing.
- Five stages with 3 / 10 / 20 / 20 / 20 characters, tutorial guidance, rocks, crabs and a blocking first-contact warning in stage 4.
- Four illustrated 15 x 21 reef stages (6–9), redesigned against the references: Rainbow Reef, S-curve, Spiral Lagoon and Two Tone Tide. Fish counts follow the whole-cell containment rule at curved banks. Home > LEVEL PREVIEWS opens them directly.
- Home, gameplay HUD, clear, failure, retry confirmation, settings, and local stage progression.
- Persistent sound, background music and vibration settings. An interrupted active stage restarts from its original layout.
- Fish, turtle and octopus color variants from Cube Animals 02; orange crab hazard; idle/swim animation controllers.
- Bubble trails, stopping feedback, hazard flash/life feedback, clear bubbles/light and synthesized prototype audio.
- Forward grid line checks produce a waypoint snapshot before movement. Original stages 1–5 use straight paths/four open edges. Illustrated stages 6–9 join the continuous conveyor curves adapted from Screw, follow them by arc length, turn along the tangent and pass the northern sea gate. Curved pond meshes and whole-cell placement share a signed-distance boundary. Each fish occupies one blue cell entirely inside water.
- uGUI Canvas, Image, Text, Button and Toggle components; portrait layout with safe-area UI.

## Layout notes

The game camera and sunlight are saved in the scene. The owned board and standard uGUI hierarchy are generated at runtime by FishOutGame/FishOutWorld/FishOutHud. Model references and animation controllers are serialized on the Fish Out Game object. Existing third-party assets and SampleScene remain intact.

Stages 4 and 5 each contain one character facing a fixed crab. With unchangeable directions and an immovable crab, that character needs the stage's free Rescue to exit. Every stage has a validated complete sequence with no lost lives. All four illustrated stages also have complete solutions without boosters. Stage 9 is the final authored stage. Coins remain a zero-value placeholder.

The additional reef assets are real reusable native 3D prefabs under `Assets/FishOut/Resources/Reef`: branching pink/gold coral, seaweed, scallop shell, pearl sea gate, and five fish color variants. They use the existing fish model and URP materials. The old yellow blocks remain removed. The latest request restores individual blue water cells, with a full-square containment guarantee at curved banks. Details and Screw provenance are in `ScrewConveyorReuse.md`. Illustration screenshots are under `Captures/Showcase`.

## Validation on 2026-11-05

- Unity 6000.3.26f1: compilation passed, no game warnings/errors in the console.
- 40 EditMode tests passed: original rules, waypoint sampling/offscreen exits, all nine stage solutions, continuous gate routing, the two-tone palette, whole-cell containment and ray checks against the actual water mesh.
- 6 PlayMode tests passed: original five-stage flow, controls/boosters/retry/hazards, smooth waypoint movement, all four illustration scenes loading, and a spiral swimmer following the complete route to the gate before despawning.
- Inspected actual Game view at 1080 x 1920. Captures are under the project's `Captures` directory.
- Android target API is set to 35. No Android device is currently connected, so on-device verification is still outstanding. No APK has been built in this pass.
- Installed editor is 6000.3.26f1; the brief requests 6000.3.27f2. ProjectVersion was not falsified or manually upgraded.
- Package ID, Adjust/AppLovin integration, Trello and CPI/store materials remain deferred as requested.

## Code

- `Runtime/BoardState.cs`: rules and state independent of presentation.
- `Runtime/FishOutLevels.cs`: the five deterministic level layouts.
- `Runtime/ShowcaseLevels.cs`, `ShowcaseScenery.cs`: four illustration recipes, continuous conveyor layouts and reef presentation.
- `Runtime/ScrewConveyorGeometry.cs`, `ConveyorNetwork.cs`: reused Screw curve/ribbon/rounded-rail geometry and continuous route joining.
- `Runtime/ReefWaterDomain.cs`, `CurvedWaterMesh.cs`: curved water boundaries, containment proof and independent-of-grid surface meshing.
- `Editor/ReefAssetBuilder.cs`: repeatable 3D prefab/material generation.
- `Runtime/FishOutGame.cs`: stage flow, input, animation, tutorial and persistence.
- `Runtime/MovementPath.cs`, `WaypointMover.cs`: waypoint construction, whole-model offscreen endpoint, arc-length sampling and smooth path traversal.
- `Runtime/FishOutWorld.cs`, `FishActor.cs`: models, colliders and world effects.
- `Runtime/FishOutHud.cs`: standard uGUI screens and controls.
- `Runtime/FishOutAudio.cs`: prototype sound effects and ambient music.
- `Editor/FishOutSetup.cs`: repeatable scene setup and animation controllers.
- `Tests/Editor`, `Tests/PlayMode`: automated rule and integration coverage.
