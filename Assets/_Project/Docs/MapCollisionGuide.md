# Map Collision Guide

Use the `EnvironmentObstacle` layer for map pieces that should block monster sight and movement.

## Blocking Pieces

- `Wall`
- `Tile`
- `Floor`
- `Platform`
- `Structure`

These should have:

- `MapPiece.useCollider = true`
- `MapPiece.blockMovement = true`
- `MapPiece.blockLineOfSight = true`
- `Collider.isTrigger = false`
- `Layer = EnvironmentObstacle`

## Monster Setup

For `EyeballFlyAI`, `HumanBoxAI`, and other `MonsterAIBase` enemies:

- `requireLineOfSight = true`
- `obstacleLayerMask = EnvironmentObstacle`
- `blockMovementByObstacles = true`
- `movementObstacleLayerMask = EnvironmentObstacle`

If a mask is empty, monster sight or movement blocking cannot work reliably.

## Test Prefabs

Unity creates these test prefabs through `MapTestPrefabBuilder`:

- `Assets/_Project/Prefabs/Map/Wall_Test.prefab`
- `Assets/_Project/Prefabs/Map/Tile_Test.prefab`
- `Assets/_Project/Prefabs/Map/Floor_Test.prefab`

Use them between a monster and Player/Light to test detection blocking and movement blocking.
