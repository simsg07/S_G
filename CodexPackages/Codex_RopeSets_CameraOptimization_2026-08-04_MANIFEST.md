# Codex RopeSets + Camera Optimization package manifest

- Package: `Codex_RopeSets_CameraOptimization_2026-08-04.unitypackage`
- Created: 2026-08-04 (Asia/Seoul)
- Unity editor used for isolated validation/export: `6000.3.19f1`
- Included asset count: `25`
- Package size: `3,469,022 bytes`
- SHA-256: `0a4862074281f6fbfa0a357cd705113e21f097a59738fa7cd7fc9da600dad8cf`
- Export mode: explicit asset list with recursive metadata/GUID preservation; automatic dependency expansion disabled

## Included paths

### Shared art and configuration

- `Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd`
- `Assets/_Project/Art/Sprites/Objects/CircleSpike.png`
- `Assets/_Project/Art/Sprites/Objects/WarningBox.png`
- `Assets/_Project/Data/World/CameraWorldSwitchSettings.asset`

`crane.psd` is included only because all four RopeSets prefabs directly share its rope Sprite sub-asset. Crane prefabs and crane control code are not included.

### RopeSets and carried-object prefabs

- `Assets/_Project/Prefabs/Objects/Gravity/CircleSpike.prefab`
- `Assets/_Project/Prefabs/Objects/Gravity/FallingBox.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Vine_Box_Set.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Vine_CircleSpike_Set.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Wire_Box_Set.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Wire_CircleSpike_Set.prefab`

### RopeSets runtime/editor code

- `Assets/_Project/Scripts/Editor/RopeLengthController3DEditor.cs`
- `Assets/_Project/Scripts/Editor/RopeSetPrefabSetupUtility.cs`
- `Assets/_Project/Scripts/Objects/Common/Physics/GravityObject3D.cs`
- `Assets/_Project/Scripts/Objects/Common/Trigger/ConnectedObjectLink.cs`
- `Assets/_Project/Scripts/Objects/Gravity/CircleSpikeObject.cs`
- `Assets/_Project/Scripts/Objects/Gravity/CircleSpikeProjectile3D.cs`
- `Assets/_Project/Scripts/Objects/Rope/RopeLengthController3D.cs`

### Camera repeated-use optimization code

- `Assets/_Project/Scripts/Camera/CameraWorldSwitcher.cs`
- `Assets/_Project/Scripts/Editor/CameraToggleLeakValidationUtility.cs`
- `Assets/_Project/Scripts/Interaction/CameraHighlightSharedResources3D.cs`
- `Assets/_Project/Scripts/Interaction/CameraMarkState3D.cs`
- `Assets/_Project/Scripts/Interaction/CameraObjectTag3D.cs`
- `Assets/_Project/Scripts/Interaction/CameraTargetHighlightManager3D.cs`
- `Assets/_Project/Scripts/Player/CameraAbilitySystem3D.cs`
- `Assets/_Project/Scripts/World/Shutter/ShutterTarget3D.cs`

## Explicit exclusions

- Every `.unity` scene and all user scene/prefab-instance overrides
- `Packages/`, `ProjectSettings/`, `UserSettings/`, and project-wide defaults
- Every `Deprecated` path
- XY crane/magnet assets and scripts, including `CraneXYController3D`, `CraneXYLeverSwitch3D`, and `CraneXYObjectMoverPrefabBuilder`
- Crane prefabs and vertical-crane implementation, including `Crane_Set.prefab` and `VerticalCrane_Set.prefab`
- Unrelated player, checkpoint, map, enemy, UI, rendering, animation, and test changes
- `PlayerDamageReceiver.cs` because its current change set also contains unrelated checkpoint/respawn behavior
- The isolated temporary exporter itself

## Prerequisites and dependencies

- Intended for the same S_G project baseline and GUID-compatible assets.
- Use Unity `6000.3.19f1` or a project-approved compatible Unity 6 editor.
- Existing project classes and packages referenced by these changed scripts are prerequisites; this lean package deliberately does not duplicate unchanged dependencies.
- In particular, the baseline must already contain the unchanged world-switch, damage, rope-cut, rendering, Input System, and URP dependencies used by the included scripts/prefabs.
- Importing into a different or older project may produce missing types/references even though the package itself preserves all included GUIDs and metadata.

## Import cautions

- Back up or commit the target project before import.
- Unity will offer to overwrite assets with matching paths/GUIDs. Review the import list and accept only the intended 25 paths above.
- Do not import this package together with an older overlapping Codex package without reviewing which version should win.
- This package contains no scenes, so scene instances and Prefab Overrides are not intentionally changed. Existing scene instances will pick up prefab asset changes according to normal Unity prefab rules.
- After import, allow script compilation to finish before entering Play Mode.

## Verification performed

- Exported from an isolated full project copy; the source project's code, prefabs, images, scenes, settings, and existing package were not modified.
- Unity script compilation completed with no `error CS` entries and batch mode exited successfully with return code 0.
- All four RopeSets prefabs loaded with zero Missing Script components.
- Box prefabs reference `WarningBox.png`; CircleSpike prefabs reference `CircleSpike.png`.
- All four RopeSets prefabs reference the shared rope Sprite sub-asset from `crane.psd`.
- Exactly one `CameraTargetHighlightManager3D` script was found.
- Package archive pathnames were extracted after export: exactly 25 paths, matching the list above.
- Archive inspection found zero scenes, `Packages/`, `Deprecated`, XY crane, vertical crane, and crane prefab paths.
- No source-project Play Mode or user scene save was performed during packaging.

## Recommended post-import checks

1. Confirm Console has no compile errors, Missing Script, MissingReferenceException, or Kinematic Rigidbody velocity warnings.
2. Exercise Vine/Wire cut, drop, landing, Box behavior, and CircleSpike roll/visual rotation.
3. Repeatedly enter/exit camera mode and verify time scale/fixed delta restoration and stable highlight/helper object counts.
4. Use the included camera validation utility/profiler markers for repeated-use allocation and duplicate-manager checks.
