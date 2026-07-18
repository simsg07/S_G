# Map Palette Guide

## 최종 결정

- Tile Palette는 맵 이미지를 빠르게 칠하는 용도입니다.
- Tile Palette로 칠한 타일은 Player가 밟을 수 없습니다.
- Tile Palette로 칠한 타일은 Player / Monster 이동을 막지 않습니다.
- Tile Palette로 칠한 타일은 Monster 시야 차단이나 Light 시야 차단 기능을 만들지 않습니다.
- 실제 게임 기능은 별도 프리팹 오브젝트로 배치합니다.
- 맵 타일 이미지는 World 전환 대상이 아닙니다.
- World 전환은 Door, Shutter, Button, Box, Laser, Stone, Monster 같은 별도 오브젝트에서 처리합니다.

## Tile Palette 사용 대상

사용:

- `Tilemap_Background`: 배경 이미지
- `Tilemap_GroundVisual`: 보이는 바닥 이미지
- `Tilemap_WallVisual`: 보이는 벽 이미지
- `Tilemap_Decoration`: 장식 이미지

사용하지 않음:

- `Tilemap_GroundCollision`
- `Tilemap_WallCollision`
- `Tilemap_MonsterSightBlock`
- `Tilemap_LightSightBlock`
- `Generated_GroundCollision`
- Tile Palette 기반 자동 3D Collider 생성

## 씬 기본 Tilemap 구조

`_Project > Map > Setup Visual Tilemaps In Current Scene` 메뉴를 실행하면 아래 구조만 생성합니다.

```text
Grid_Map
├── Tilemap_Background
├── Tilemap_GroundVisual
├── Tilemap_WallVisual
└── Tilemap_Decoration
```

이 메뉴는 Collider, 기능 Tilemap, Generated Collider 오브젝트를 만들지 않습니다.

## 맵 제작 순서

1. Tile Palette로 보이는 바닥, 벽, 배경, 장식을 칠합니다.
2. Player가 밟을 위치에는 `Floor_Collision` 프리팹을 배치합니다.
3. 이동을 막을 벽에는 `Wall_Tile` 또는 `Block_Tile` 프리팹을 배치합니다.
4. Monster 시야를 막을 위치에는 `Wall_Tile`, `Block_Tile`, 또는 DetectionBlock 계열 프리팹을 배치합니다.
5. Light 시야를 막을 위치에는 LightSightBlock 계열 프리팹이 있으면 사용하고, 없으면 DetectionBlock 또는 Wall/Block 계열 프리팹으로 대체합니다.
6. World 전환 오브젝트는 `Prefabs/Objects` 또는 `Prefabs/Enemies`에서 별도 배치합니다.

## 실제 기능 프리팹 기준

### Floor_Collision

- Player가 실제로 밟는 바닥입니다.
- 보이는 바닥보다 아주 살짝 위에 배치합니다.
- 권장 Layer: `Ground`
- 긴 바닥은 Transform Scale 대신 `FloorCollision.width`로 조절합니다.

### Wall_Tile / Block_Tile

- Player / Monster 이동 차단용입니다.
- Monster 시야 차단용으로도 사용할 수 있습니다.
- 권장 Layer: 프로젝트의 `Wall`, `MapObstacle`, `EnvironmentObstacle`, 또는 감지 LayerMask에 포함된 차단 Layer

### DetectionBlock / LightSightBlock

- 보이지 않는 인식 차단 전용 프리팹이 필요할 때 사용합니다.
- MonsterDetection의 `obstacleLayerMask` 또는 `lightObstacleLayerMask`에 포함된 Layer를 사용해야 합니다.

## 주의

- `Tilemap_GroundVisual`은 보이는 바닥 그림일 뿐입니다.
- Player가 밟으려면 반드시 `Floor_Collision`이 필요합니다.
- Tile Palette 타일에는 `WorldSwitchable`을 붙이지 않습니다.
- Tile Palette 타일 이미지는 World A/B 전환으로 바뀌지 않습니다.
- 기능용 Tile Palette / 자동 Collider Baker 구조는 현재 기획에서 사용하지 않습니다.

## Deprecated 처리된 자동 기능

아래 구조는 현재 기획에서 제외되어 `_Deprecated/MapTilemapAutoCollision`로 이동했습니다.

- Tilemap 기반 자동 3D BoxCollider Baker
- 기능 Tile Palette
- GroundCollision / WallCollision / MonsterSightBlock / LightSightBlock 기능 타일
- 기능 Tilemap을 생성하던 자동 Setup 메뉴

필요해지면 Deprecated 폴더에서 확인할 수 있지만, 현재 메인 제작 방식에서는 사용하지 않습니다.
