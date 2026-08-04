# MapPiece Prefab Guide

Use `MapPiece` on flat 3D objects that should behave like 2D side-view map pieces.

## Basic Types

- `BackgroundWall`: visual background, no collider, Z `2`
- `Floor`: walkable platform, collider on, Z `0`
- `Wall`: blocking wall, collider on, Z `0`
- `Tile`: repeatable tile, collider optional, Z `0`
- `Object`: doors, boxes, buttons, terminals, collider optional, Z `-0.2`
- `Decoration`: pipes, wires, vents, lights, no collider, Z `1`

## World Switching

Every `MapPiece` has world-switch metadata:

- `canSwitchWorld`: enable if the camera/world ability can switch this object.
- `worldSwitchCategory`: classify the target, such as `Structure`, `PuzzleObject`, `Door`, `Box`, `Hazard`, `Decoration`, or `Terminal`.
- `currentWorldState`: normally `Current`.
- `targetWorldState`: normally `Past`.

When `canSwitchWorld` and `autoConfigureWorldSwitchable` are enabled, `MapPiece` adds or configures `WorldSwitchable` on the same GameObject.

Terminals can stay `canSwitchWorld = false` for now and be handled later with a dedicated rule.

## Placement

- Keep gameplay movement on X/Y.
- Use Z only for visual layering.
- Leave object scale untouched; resize prefabs manually in the Inspector.
- Enable `snapToGrid` for tile-like placement.
- Use `MapLayerSettings` only if a map needs custom Z layer values.
