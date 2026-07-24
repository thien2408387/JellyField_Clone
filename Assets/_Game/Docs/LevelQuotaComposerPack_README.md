# Level Quota Composer Pack

## A* Grid Generation

When `Generate As Level` is enabled, `Level Quota Composer` creates a full level prefab and ensures that level has an `AStarGrid`.

The generation flow lives in:

- `Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.Export.cs`
- `GenerateLevelPackage()`
- `EnsureAStarGridOnLevelRoot(GameObject levelRootInstance, GameObject pixelCubeRoot)`

Flow:

1. Generate the pixel block prefab from the painted top grid.
2. Instantiate the assigned `Level Root Prefab`.
3. Instantiate the generated pixel block prefab under the level root.
4. Call `EnsureAStarGridOnLevelRoot(levelRootInstance, nestedBlockInstance)`.
5. Reuse an existing child `AStarGrid` if the level root already has one.
6. If no grid exists, create a new child object named `AStarGrid` and add the `AStarGrid` component.
7. Apply default centered manual grid settings through `AStarGridEditorSerializationUtility`.
8. Set the A* cell size from `_cubeSize + _cubeSpacing`.
9. Apply pixel-cube-grid settings from the generated pixel block root.
10. Snap the serialized grid world offset to pixel cells through `AStarGridPixelBlockPlacementUtility`.

Relevant settings are shown in the tool under:

`Output > A* Pixel Cube Grid (level prefab + JSON)`

- `Build From Pixel Cube Grid`
- `Grid Position Match Radius`

Required grid scripts included in the pack:

- `Assets/_Game/Scripts/Gameplay/JellyCapsule/AStarGrid.cs`
- `Assets/_Game/Scripts/Gameplay/JellyCapsule/AStarGridGizmos.cs`
- `Assets/_Game/Scripts/Gameplay/JellyCapsule/AStarGridProvider.cs`
- `Assets/_Game/Scripts/Editor/AStarGridEditorSerializationUtility.cs`
- `Assets/_Game/Scripts/Editor/AStarGridPixelBlockPlacementUtility.cs`

