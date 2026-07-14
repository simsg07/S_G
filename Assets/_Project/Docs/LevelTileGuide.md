# Level Tile Guide

## 기본 배치 구조

1. 보이는 타일은 `Tile_Visual`을 사용합니다.
2. 실제 Player가 밟는 바닥은 `Floor_Collision`을 사용합니다.
3. `Floor_Collision`의 Root 위치는 `Tile_Visual` / `Floor_Tile`의 윗면 기준으로 둡니다.
4. BoxCollider는 얇은 가로 충돌판이며, 바닥면이 `collisionYOffset=0.02` unit만큼 윗면보다 살짝 위에 오도록 둡니다. 권장 범위는 `0.01~0.03`입니다.
5. `Floor_Collision`은 Renderer가 없는 충돌 전용 오브젝트입니다. `showDebugVisual`은 Scene View Gizmo만 켭니다.
6. `Tile_Visual`에는 기본적으로 Collider를 넣지 않습니다.
7. 실제로 막는 벽은 `Wall_Tile`을 사용합니다.
8. 용도에 따라 충돌·이동 차단·감지 차단을 조절할 블럭은 `Block_Tile`을 사용합니다.

### 5칸 바닥 예시

- 보이는 바닥: `Tile_Visual` 5개를 1칸씩 배치
- 실제 충돌 바닥: 같은 구간에 `Floor_Collision` 하나를 놓고 `width=5`로 설정
- Player는 `Floor_Collision` 위를 걷고 `Tile_Visual`은 보기만 담당합니다.
- Collider의 바닥면이 `0.02`만큼 위로 보정되므로 작은 타일의 경계와 모서리에 덜 걸립니다.

Root Transform Scale은 `(1, 1, 1)`을 유지합니다. 긴 바닥은 Scale 대신 `FloorCollision.width`, `colliderHeight`, `colliderDepth`로 조절하고, 일반 타일 충돌 크기는 `MapTile.tileSize`와 `colliderDepth`로 조절합니다.

## 프리팹별 설치 방법

| 프리팹 | 역할 | 기본 설치 방법 |
|---|---|---|
| `Floor_Collision` | 보이지 않는 실제 보행 충돌 | 구간에 하나를 놓고 `width`를 길이에 맞춤 |
| `Tile_Visual` | 보이는 바닥·배경·벽 모양 | 1칸 단위로 이어 붙이며 Collider는 사용하지 않음 |
| `Wall_Tile` | 이동과 몬스터 감지를 막는 벽 | 1칸 단위로 쌓고 BoxCollider를 유지 |
| `Block_Tile` | 범용 고체 또는 장식 블럭 | 고체는 `useCollider/blockMovement`를 켜고 장식은 끔 |

모든 프리팹은 `GridSnapper.gridSize=1`, X/Y Snap 사용, Z Snap 해제가 기본입니다. Z를 고정해야 할 때만 `zLockEnabled`를 사용합니다. GridSnapper는 위치만 정렬하고 Transform Scale과 Layer는 변경하지 않습니다.

`Tile_Visual`, `Wall_Tile`, `Block_Tile`에는 `Visual_A`와 `Visual_B` 자식 슬롯이 있습니다. 나중에 받은 World A/B 에셋을 각 자식 아래에 넣고 `MapTileWorldVisual` 연결을 유지합니다. 개별 카메라 전환 대상만 `MapPiece.canSwitchWorld`를 켭니다. `Floor_Collision`은 기본적으로 World 전환 대상이 아닙니다.

## Layer 권장값 (Inspector에서 수동 설정)

- `Floor_Collision`: `Ground`
- `Wall_Tile`: 현재 프로젝트의 `Wall` (`MapObstacle` 또는 `EnvironmentObstacle`을 사용하는 프로젝트에서는 해당 차단 Layer)
- `Block_Tile`: 용도에 따라 `Ground` / `TileObstacle` / `EnvironmentObstacle` / `Default`
- `Tile_Visual`: 기본 `Default`; 감지 차단용으로 쓸 때만 몬스터 LayerMask에 포함된 차단 Layer

코드는 Layer를 자동으로 변경하지 않고 권장 Layer가 다를 때 경고만 표시합니다. Player/Monster 이동과 감지 차단이 실제로 동작하려면 선택한 Layer가 각 시스템의 LayerMask에도 포함되어 있어야 합니다.

## 주의

1. 작은 `Tile_Visual`마다 Collider를 붙이지 않습니다.
2. Player가 바닥에서 떨어지거나 멈추면 먼저 `Floor_Collision`이 해당 구간을 덮는지 확인합니다.
3. `collisionYOffset`이 너무 크면 Player가 떠 보입니다.
4. 높이 보정은 `0.01~0.03` 사이를 권장합니다.
5. 감지 차단(`blockLineOfSight`)과 보행 충돌(`Floor_Collision`)은 다른 개념입니다.
6. World A/B 전환은 주로 `Tile_Visual`, `Wall_Tile`, `Block_Tile`에 적용합니다.
7. `Floor_Collision`은 기본적으로 World 전환 대상이 아닙니다.

## 설치형 프리팹 테스트

1. **Floor_Collision 높이:** `alignToTopSurface=true`, `collisionYOffset=0.02`, `colliderHeight=0.08`, `colliderDepth=0.3`인지 확인하고 Player가 타일 모서리에 걸리지 않는지 걷습니다. Game View에는 충돌 오브젝트가 보여서는 안 됩니다.
2. **Tile_Visual:** 여러 개를 움직였을 때 1칸 단위로 정렬되고, Collider가 없으며, `Visual_A / Visual_B` 자식이 있는지 확인합니다.
3. **Wall_Tile:** 1칸 단위 정렬, BoxCollider, `blockMovement=true`, `blockLineOfSight=true`를 확인합니다. Player와 Monster가 통과하지 않고 EyeballFly / Human_Box / Boomber 감지가 차단되어야 합니다.
4. **Block_Tile:** `useCollider`, `blockMovement`, `blockLineOfSight`, `isGround`, `canSwitchWorld`를 용도별로 바꿉니다. 장식 설정에서는 Collider가 비활성화되고 고체 설정에서는 이동을 막아야 합니다.
5. **World A/B:** `MapTileWorldVisual.editorPreviewWorld` 또는 Play Mode의 World A/B 전환으로 `Visual_A`와 `Visual_B`가 교대로 표시되는지 확인합니다. `Floor_Collision`은 계속 유지되어야 합니다.

## 기본 타일

- `Tile_Visual`: 화면에 보이는 1 x 1 타일입니다. Collider 없이 자유롭게 이어 붙입니다.
- `Floor_Collision`: Player와 지상 몬스터가 실제로 밟는 긴 바닥입니다. `Ground` Layer와 단일 BoxCollider를 사용합니다.
- `Floor_Tile`: 바닥 그림을 배치하는 Visual 전용 타일입니다. Collider가 없으며 실제 발판은 `Floor_Collision`이 담당합니다.
- `Wall_Tile`: 이동과 시야를 막는 1 x 1 벽입니다. `Wall` Layer를 사용합니다.
- `Block_Tile`: 반복 배치하는 고체 구조 타일입니다. `TileObstacle` Layer를 사용합니다.

`Tile_Visual`과 `Floor_Tile`은 `MapPiece`, `GridSnapper`, `MapTile`, `WorldSwitchable`, `MapTileWorldVisual`을 사용합니다. `Floor_Collision`은 `MapPiece`, `FloorCollision`, `GridSnapper`, 단일 `BoxCollider`만 사용하며 Renderer가 없습니다.

## Visual과 충돌 분리

1. 먼저 `Tile_Visual`을 1 단위 그리드로 이어 붙여 화면을 구성합니다.
2. Player가 걸을 구간의 보이는 바닥 윗면에 `Floor_Collision`을 하나 배치합니다.
3. `FloorCollision.width`를 구간 길이에 맞게 늘립니다. 기본값은 `4`입니다.
4. `alignToTopSurface=true`, `collisionYOffset=0.02`, `colliderHeight=0.08`, `colliderDepth=0.3`, `isTrigger=false`, Layer=`Ground`를 유지합니다.
5. 작은 Visual 타일에는 Collider를 추가하지 않습니다. 그래야 타일 경계와 모서리에 Player Collider가 걸리지 않습니다.
6. `Floor_Collision.showDebugVisual`은 Scene View Gizmo만 표시하며 빌드 화면에는 렌더링되지 않습니다.

기존 씬의 `Floor_Tile` 인스턴스에 Collider override가 남아 있다면 해당 Collider를 제거하고 같은 구간 아래에 긴 `Floor_Collision`을 배치합니다.

`Tile_Visual` 기본값:

- `useCollider=false`
- `isGround=false`
- `blockMovement=false`
- `blockLineOfSight=true`
- `canSwitchWorld=false`

`Floor_Collision` 기본값:

- `useCollider=true`
- `isGround=true`
- `blockMovement=false`
- `blockLineOfSight=false`
- `canSwitchWorld=false`
- `alignToTopSurface=true`
- `visualTileHeight=1.0`
- `collisionYOffset=0.02`
- `colliderHeight=0.08`
- `colliderDepth=0.3`
- BoxCollider `center.y = collisionYOffset + colliderHeight * 0.5`
- Layer=`Ground`

## 배치 규칙

1. `Assets/_Project/Prefabs/Map`의 타일을 Scene으로 드래그합니다.
2. `GridSnapper.gridSize`는 기본값 `1`을 유지합니다.
3. 타일 Root Scale은 `(1, 1, 1)`을 권장합니다. X/Y 위치를 정수 단위로 배치하면 Collider 경계가 맞습니다.
4. Visual 타일 크기는 기본 `1 x 1`입니다.
5. 충돌은 `Floor_Collision` Root의 BoxCollider 하나만 사용합니다. `Visual` 자식에는 Collider를 추가하지 않습니다.
6. 긴 바닥은 작은 Collider 여러 개 대신 구간 전체를 덮는 하나의 긴 BoxCollider로 구성합니다.

`MapPiece`와 `GridSnapper`는 Edit Mode에서만 위치를 정렬하며, Play 시작 시 배치 위치를 덮어쓰지 않습니다.

### GridSnapper

- `enableSnap`: 해당 오브젝트의 Grid Snap 사용 여부
- `snapInEditMode`: 기획자가 Scene View에서 배치할 때 자동 정렬
- `snapInPlayMode`: 런타임 정렬이며 기본값은 `false`
- `gridSize`: 기본 `1`
- `snapX`, `snapY`, `snapZ`: 축별 정렬 여부
- `zLockEnabled`, `lockedZ`: 필요한 오브젝트만 Z 고정
- `offset`: `0.5` 같은 반 칸 배치 기준

GridSnapper는 위치만 바꾸며 Scale, Rotation, Layer는 수정하지 않습니다.

## World A / World B 비주얼

`Tile_Visual`, `Floor_Tile`, `Wall_Tile`, `Block_Tile`에는 `MapTileWorldVisual`과 두 비주얼 슬롯이 준비되어 있습니다.

- `World A Visual`: 기존 `Visual` 자식입니다. World A용 Renderer 또는 SpriteRenderer를 이 아래에 둡니다.
- `World B Visual`: 비어 있는 `Visual_WorldB` 자식입니다. 나중에 받은 World B 에셋을 이 아래에 둡니다.
- `Follow Global World`: 켜면 기존 `WorldSystem3D`의 활성 World A/B를 따라 비주얼이 자동 전환됩니다.
- `Initial World`: `Follow Global World`를 끈 타일의 시작 비주얼입니다.
- `WorldSwitchable.Editor Preview Mode`: `AlwaysVisible`, `PreviewWorldA`, `PreviewWorldB`로 Scene View 표시를 확인합니다.

타일이 어느 월드에서 시작하고 어디로 전환되는지는 `MapPiece`의 `Current World State`와 `Target World State`에서 정합니다. 카메라 능력으로 개별 전환할 수 있는지는 `MapPiece.canSwitchWorld`에서 설정하며, 이 값은 같은 Root의 `WorldSwitchable.canSwitchByCamera`에 전달됩니다.

`canSwitchWorld`와 World A/B 비주얼 보유 여부는 별도 개념입니다. 전환 불가능한 배경 타일도 활성 월드에 맞는 두 비주얼을 표시할 수 있습니다. 현재 프리팹의 World B 슬롯은 빈 자리만 제공하며 새 이미지 에셋은 포함하지 않습니다.

설정 순서:

1. World A 에셋을 기존 `Visual` 아래에 배치합니다.
2. World B 에셋을 `Visual_WorldB` 아래에 배치합니다.
3. 두 자식을 `MapTileWorldVisual`의 각 슬롯에 연결합니다. 기본 프리팹에는 이미 연결되어 있습니다.
4. 전체 월드를 따라갈 타일은 `Follow Global World=true`로 둡니다.
5. 카메라 능력의 개별 전환 대상만 `MapPiece.canSwitchWorld=true`로 설정합니다.
6. Play Mode에서 World A/B를 전환하고 두 비주얼 중 하나만 활성화되는지 확인합니다.

## Boomber와 선택적 파괴 오브젝트

`BoomberExplosion`은 Player 데미지와 별도로 `IExplosionBreakable`을 구현한 오브젝트만 선택적으로 호출할 수 있습니다.

1. 나중에 만들 `BreakableObject`만 `IExplosionBreakable.ReceiveExplosion`을 구현합니다.
2. 해당 오브젝트를 별도의 Breakable Layer에 둡니다.
3. Boomber의 `Breakable Layer Mask`에 그 Layer를 지정합니다.
4. 일반 Floor, Wall, Tile에는 인터페이스나 체력 컴포넌트를 붙이지 않습니다.

`Breakable Layer Mask`가 비어 있으면 추가 Overlap 검사를 생략하므로 기존 맵 오브젝트에는 영향이 없습니다.

## 감지 차단 규칙

기본적으로 실제 맵 구조물은 Player와 Light 감지를 막습니다.

- 차단 기본값 `true`: 기존 `Floor`, `Wall`, `Tile`(Block 포함), `Platform`, `Structure`
- 차단 기본값 `false`: `VisualTile`, `BackgroundWall`, `Decoration`, 일반 `Object`

`MapPiece.blockLineOfSight`는 타일별 시야 차단 여부입니다. 기획자가 Inspector에서 직접 끌 수 있으며, `OnValidate`가 수동 설정을 계속 덮어쓰지 않습니다. 타입을 처음 지정하거나 컨텍스트 메뉴의 `Apply Recommended Defaults`를 실행할 때만 권장값이 적용됩니다.

감지를 실제로 막으려면 타일 Layer가 몬스터의 `MonsterDetection.obstacleLayerMask`에 포함되어야 합니다. 이 프로젝트에서는 다음 Layer를 공통 시야 차단 마스크로 사용합니다.

- `Ground`: Floor
- `Wall`: Wall
- `TileObstacle`: Tile, Block
- `Platform`: Platform
- `EnvironmentObstacle`: Structure와 기타 고체 구조물

코드는 Layer를 자동 변경하지 않습니다. 각 프리팹과 Scene 인스턴스의 Layer를 Inspector에서 지정합니다. `BackgroundWall`, `Decoration`, Player, Enemy, Light, UI, Terminal은 위 시야 차단 Layer에서 제외하는 것을 권장합니다.

몬스터 설정 권장값:

- `obstacleLayerMask`: Ground, Wall, TileObstacle, Platform, EnvironmentObstacle
- `movementObstacleLayerMask`: Wall, TileObstacle, EnvironmentObstacle
- `groundLayerMask`: Ground, Platform

Floor는 시야를 막지만 수평 이동 장애물로 취급하지 않도록 `movementObstacleLayerMask`에는 넣지 않습니다. Ground 몬스터가 바닥을 밟는 판정은 `groundLayerMask`가 담당합니다.

## 월드 전환

`blockLineOfSight`와 월드 전환 설정은 서로 독립적입니다.

- `MapPiece.blockLineOfSight`: Player/Light 시야를 막는지 여부
- `MapPiece.blockMovement`: 이동을 막는지 여부
- `MapPiece.canSwitchWorld`: 월드 전환 대상인지 여부
- `WorldSwitchable.canSwitchByCamera`: 카메라 능력으로 전환할 수 있는지 여부

예를 들어 `Tile_Visual`은 Collider 없이 `canSwitchWorld=true`로 설정할 수 있고, `Floor_Collision`은 계속 유지할 수 있습니다. 시각 타일 전환과 실제 바닥 충돌 전환은 필요한 경우에만 각각 별도로 설정합니다.

## 테스트

1. Human_Box와 Player 사이에 Floor, Wall 또는 Block 타일을 둡니다.
2. 타일 Layer가 몬스터의 `obstacleLayerMask`에 포함되었는지 확인합니다.
3. `blockLineOfSight=true` 상태에서는 범위 안이어도 추적, 공격, Howling이 실행되지 않아야 합니다.
4. EyeballFly와 Light 사이에서도 같은 방식으로 Light 추적이 차단되는지 확인합니다.
5. `BackgroundWall` 또는 `Decoration`은 `blockLineOfSight=false`로 두고 차단 Layer에서 제외하면 감지를 막지 않아야 합니다.
6. 몬스터 `debugMode`와 `showGizmos`를 켜면 차단 타일 이름, Player/Light Ray, hit 지점을 확인할 수 있습니다.

## 바닥 경계에서 멈출 때

1. 해당 구간의 작은 타일 Collider를 제거하거나 `MapPiece.useCollider=false`로 바꿉니다.
2. 같은 구간을 덮는 `Floor_Collision` 하나를 배치합니다.
3. Collider 바닥면이 보이는 바닥 윗면보다 아주 살짝 위에 있고, Player가 떠 보이지 않는지 확인합니다.
4. Root와 Visual에 Collider가 중복으로 붙어 있지 않은지 확인합니다.
5. 이 프로젝트는 3D/2.5D 물리를 사용하므로 `CompositeCollider2D` 대신 긴 BoxCollider를 사용합니다.
