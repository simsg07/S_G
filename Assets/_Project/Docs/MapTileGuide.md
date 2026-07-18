# Map Tile Guide

맵 제작자는 `Assets/_Project/Prefabs/Map`에서 배치용 프리팹을 찾습니다.

## 주요 프리팹

- `Floor_Collision.prefab`: Player가 실제로 밟는 보행 충돌
- `Tile_Visual.prefab`: 보이는 바닥/벽/배경용 Visual
- `Wall_Tile.prefab`: 이동과 감지를 막는 벽
- `Block_Tile.prefab`: 범용 고체/장식 블럭
- `Floor_Tile.prefab`: 기존 Visual 전용 바닥 타일
- `BackgroundWall.prefab`: 현재 별도 파일 없음. 필요 시 추가 예정

## 기획자가 만지는 값

- `GridSnapper.gridSize`: 기본 1 유지
- `GridSnapper.snapX`, `snapY`: 보통 켬
- `MapPiece.blockMovement`: 이동 차단 여부
- `MapPiece.blockLineOfSight`: 몬스터 감지 차단 여부
- `MapPiece.canSwitchWorld`: World A/B 전환 대상 여부
- `FloorCollision.width`: 긴 바닥 충돌 길이
- `FloorCollision.collisionYOffset`: 보이는 바닥 윗면보다 살짝 위로 올리는 값

## 주의

- `Tile_Visual`에는 Collider를 추가하지 않습니다.
- 실제 발판은 `Floor_Collision`이 담당합니다.
- Layer는 코드에서 자동 변경하지 않습니다. Inspector에서 수동 설정합니다.
- Root Scale은 `(1, 1, 1)`을 유지하고 크기는 전용 필드로 조절합니다.
