# GameObject Brush Palette Guide

## 목적

이 프로젝트는 일반 Tilemap 타일만으로 맵을 만들지 않습니다.

`WorldSwitchable`, `MapTile`, `FloorCollision`, `BoxCollider` 같은 기능이 붙은 Prefab을 Unity Tile Palette의 GameObject Brush로 찍기 위해 `GOB_` 프리팹 세트를 사용합니다.

## 현재 패키지 상태

현재 `Packages/manifest.json` 기준:

- `com.unity.modules.tilemap` 있음
- `2D Tilemap Extras / Tilemap Extras` 패키지는 manifest에 없음

따라서 Unity Tile Palette에서 `GameObject Brush`가 보이지 않으면 Package Manager에서 Tilemap Extras 계열 패키지를 설치해야 합니다.

설치/확인 순서:

1. Unity 메뉴에서 `Window > Package Manager`를 엽니다.
2. `2D Tilemap Extras` 또는 `Tilemap Extras`를 검색합니다.
3. 설치 후 Unity를 재시작하거나 Tile Palette 창을 다시 엽니다.
4. `Window > 2D > Tile Palette`를 엽니다.
5. Tile Palette 상단 Brush 드롭다운에서 `GameObject Brush`를 선택합니다.

## GameObject Brush 팔레트 폴더

```text
Assets/_Project/Prefabs/Map/GameObjectBrushPalette/
├── VisualTiles/
├── FloorCollision/
├── Walls/
├── Blocks/
├── WorldAB/
└── DetectionBlock/
```

## GameObject Brush 프리팹 목록

### 보이는 타일

- `VisualTiles/GOB_VisualTile_1x1.prefab`
- `VisualTiles/GOB_VisualTile_WorldAB_1x1.prefab`

특징:

- `MapTile`
- `GridSnapper`
- Collider 없음
- `WorldAB` 버전만 `WorldSwitchable.canSwitchByCamera = true`

### 실제 밟는 바닥

- `FloorCollision/GOB_FloorCollision_1x1.prefab`
- `FloorCollision/GOB_FloorCollision_4x1.prefab`
- `FloorCollision/GOB_FloorCollision_8x1.prefab`

특징:

- `FloorCollision`
- `MapTile`
- `GridSnapper`
- `BoxCollider`
- `collisionYOffset = 0.02`
- `canSwitchWorld = false`
- Renderer 없음. Scene View에서는 Gizmo로 충돌판을 확인합니다.

### 실제 막는 벽

- `Walls/GOB_WallTile_1x1.prefab`
- `Walls/GOB_WallTile_WorldAB_1x1.prefab`

특징:

- `MapTile`
- `GridSnapper`
- `BoxCollider`
- `blockMovement = true`
- `blockLineOfSight = true`
- `WorldAB` 버전만 `WorldSwitchable.canSwitchByCamera = true`
- WorldAB 버전은 Visual A/B만 바뀌고 Collider는 유지됩니다.

### 블럭 / WorldAB 블럭

- `Blocks/GOB_BlockTile_Collider_1x1.prefab`
- `WorldAB/GOB_BlockTile_WorldAB_1x1.prefab`
- `WorldAB/GOB_BlockTile_WorldAB_CollisionSwitch_1x1.prefab`

특징:

- `GOB_BlockTile_Collider_1x1`: 일반 충돌 블럭
- `GOB_BlockTile_WorldAB_1x1`: World A/B에서 Visual만 전환
- `GOB_BlockTile_WorldAB_CollisionSwitch_1x1`: World A에서는 Collider ON, World B에서는 Collider OFF

### 감지 차단 블럭

- `DetectionBlock/GOB_DetectionBlock_1x1.prefab`

특징:

- 몬스터 시야/감지 차단용 블럭입니다.
- 기본 `blockMovement = false`, `blockLineOfSight = true`
- 프로젝트에 별도 `DetectionBlock` Layer가 없다면 `EnvironmentObstacle` 또는 기존 감지용 Layer를 수동 지정하세요.

## Tile Palette에서 찍는 방법

1. `Window > 2D > Tile Palette`를 엽니다.
2. Brush 드롭다운에서 `GameObject Brush`를 선택합니다.
3. GameObject Brush에 아래 폴더의 프리팹을 등록합니다.

```text
Assets/_Project/Prefabs/Map/GameObjectBrushPalette/
```

4. 원하는 `GOB_` 프리팹을 선택하고 Scene에 찍습니다.
5. 배치된 오브젝트는 일반 GameObject/Prefab 인스턴스처럼 `MapTile`, `WorldSwitchable`, `FloorCollision`, `Collider` 기능을 유지합니다.

## WorldSwitchable 유지 방식

GameObject Brush로 찍힌 오브젝트도 Prefab 인스턴스입니다.

따라서 찍힌 Prefab에 `WorldSwitchable`이 붙어 있으면 기존 World 전환 시스템의 대상이 됩니다.

규칙:

1. `GOB_VisualTile_WorldAB_1x1`
2. `GOB_WallTile_WorldAB_1x1`
3. `GOB_BlockTile_WorldAB_1x1`
4. `GOB_BlockTile_WorldAB_CollisionSwitch_1x1`

위 프리팹만 기본적으로 World 전환 대상입니다.

`GOB_FloorCollision_*`에는 `WorldSwitchable`을 붙이지 않습니다. 실제 밟는 바닥은 기본적으로 World A/B 전환 대상이 아닙니다.

## FloorCollision 사용 방식

보이는 바닥과 실제 밟는 바닥을 분리합니다.

예시:

1. `GOB_VisualTile_1x1`로 보이는 바닥을 찍습니다.
2. 그 위에 `GOB_FloorCollision_4x1` 또는 `GOB_FloorCollision_8x1`로 실제 충돌 바닥을 찍습니다.
3. FloorCollision Root는 보이는 바닥 타일의 윗면 기준에 맞춥니다.
4. `collisionYOffset = 0.02`로 Collider가 보이는 바닥보다 아주 살짝 위에 위치합니다.

## GridSnapper 규칙

모든 `GOB_` 프리팹은 `GridSnapper`를 유지합니다.

기본값:

- `gridSize = 1`
- `snapX = true`
- `snapY = true`
- `snapZ = false`
- Transform Scale은 건드리지 않습니다.

GameObject Brush가 찍은 위치가 애매해도 `GridSnapper`가 1칸 단위 배치로 보정합니다.

## 수동으로 설정해야 할 Layer

코드에서 Layer를 자동 변경하지 않습니다. Unity Inspector에서 직접 확인하세요.

- `GOB_FloorCollision_*`: `Ground`
- `GOB_WallTile_*`: `Wall`, `MapObstacle`, 또는 `EnvironmentObstacle`
- `GOB_BlockTile_Collider_1x1`: `TileObstacle` 또는 `MapObstacle`
- `GOB_DetectionBlock_1x1`: `DetectionBlock`, `EnvironmentObstacle`, 또는 `MapObstacle`
- `GOB_VisualTile_*`: `Default` 또는 `MapVisual`

MonsterDetection obstacleLayerMask에는 프로젝트에서 사용하는 감지 차단 Layer를 포함해야 합니다.

Player groundLayerMask에는 `Ground` Layer가 포함되어야 합니다.

## 테스트 방법

### 테스트 1: GameObject Brush 사용 가능 여부

1. `Window > 2D > Tile Palette`를 엽니다.
2. Brush 드롭다운에 `GameObject Brush`가 있는지 확인합니다.
3. 없으면 Package Manager에서 Tilemap Extras 계열 패키지를 설치합니다.

### 테스트 2: VisualTile 찍기

1. GameObject Brush에 `GOB_VisualTile_1x1`을 등록합니다.
2. Scene에 찍습니다.
3. 찍힌 오브젝트에 `MapTile`과 `GridSnapper`가 붙어 있는지 확인합니다.
4. Collider가 없는지 확인합니다.

### 테스트 3: FloorCollision 찍기

1. GameObject Brush로 `GOB_FloorCollision_4x1`을 찍습니다.
2. Player가 안정적으로 밟고 지나가는지 확인합니다.
3. Collider가 보이는 바닥보다 아주 살짝 위에 있는지 확인합니다.

### 테스트 4: WorldAB 찍기

1. GameObject Brush로 `GOB_WallTile_WorldAB_1x1`을 찍습니다.
2. 찍힌 오브젝트에 `WorldSwitchable`이 붙어 있는지 확인합니다.
3. World 전환 시 `Visual_A` / `Visual_B`가 바뀌는지 확인합니다.

### 테스트 5: 몬스터 시야 차단

1. `GOB_DetectionBlock_1x1` 또는 `GOB_WallTile_1x1`을 찍습니다.
2. Layer를 감지 차단용 Layer로 수동 설정합니다.
3. MonsterDetection obstacleLayerMask에 해당 Layer를 포함합니다.
4. EyeballFly / Human_Box / Boomber가 벽 뒤 Player를 감지하지 않는지 확인합니다.
