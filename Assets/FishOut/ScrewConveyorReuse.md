# Screw conveyor reuse and curved-water layouts

Source inspected: `D:/Project_Unity/screw-bubble-sort/Assets/Game/ScrewBubbleSort3D/Scripts/Runtime/FunnelBoardView.cs` and `FunnelBoardSprites.cs` on 2026-11-05. The Screw project was read only; no source files there were modified.

`Runtime/ScrewConveyorGeometry.cs` adapts the original `Rounded`, `Ribbon` and `Tube` implementation. Curves are resampled at a maximum 0.12-unit pitch. The original conveyor's arc-length movement is handled by the existing `MovementPath`/`WaypointMover`, including rotation along the tangent. This reuses Screw's conveyor geometry and movement approach, rather than deforming its fixed oval model prefab.

The fish approach the nearest continuous conveyor centerline, follow it in the authored direction, join the exit stem and leave the camera. There is no grid BFS staircase on the conveyor. Raised rails are clipped at intersections so they do not cross the exit junction.

Layouts 6–9 now use a 15 x 21 placement domain with rounded pond banks, S-shaped or spiral cuts and a perimeter conveyor. A continuous signed-distance field drives both the rendered pond contour and cell acceptance. A fish cell is 0.86 units wide; acceptance requires clearance for a containing disk plus a margin, proving that the full square is inside water, including concave banks. The fish artwork is normalized to 0.78 units. Cells use a blue material instead of the removed yellow blocks.

Every retained cell holds exactly one initial fish. Direction assignment records a complete removal sequence, so rejecting partial edge cells does not introduce unavoidable deadlocks. Validation includes both dense continuous-domain sampling and ray tests against the actual rendered water mesh at cell corners/edges.
