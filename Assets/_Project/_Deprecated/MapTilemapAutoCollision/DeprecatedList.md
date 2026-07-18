# Deprecated: Map Tilemap Auto Collision

기획 변경으로 Tile Palette는 이미지 색칠 전용으로 사용합니다. 충돌, 이동 차단, Monster 시야 차단, Light 시야 차단은 Tilemap 자동 생성 방식이 아니라 별도 프리팹 오브젝트로 배치합니다.

## 이동한 파일

### Scripts

- `TilemapTo3DBoxColliderBaker.cs.disabled`
  - 기존 위치: `Assets/_Project/Scripts/Map/TilemapTo3DBoxColliderBaker.cs`
  - 이동 이유: Tile Palette로 칠한 타일을 자동 3D Collider로 변환하는 구조를 더 이상 사용하지 않음
  - 대체 기능: `Floor_Collision`, `Wall_Tile`, `Block_Tile`, DetectionBlock 계열 프리팹
  - 완전 삭제 가능 여부: 추후 확인
  - 참조 여부: Scene/Prefab GUID 검색에서 직접 참조 없음

- `TilemapTo3DBoxColliderBakerEditor.cs.disabled`
  - 기존 위치: `Assets/_Project/Scripts/Editor/TilemapTo3DBoxColliderBakerEditor.cs`
  - 이동 이유: Baker Inspector 버튼이 더 이상 필요하지 않음
  - 대체 기능: 없음
  - 완전 삭제 가능 여부: Baker 완전 삭제 시 가능
  - 참조 여부: Baker 전용 Editor

- `TilemapGeneratedColliderMarker.cs.disabled`
  - 기존 위치: `Assets/_Project/Scripts/Map/TilemapGeneratedColliderMarker.cs`
  - 이동 이유: 자동 생성 Collider를 구분하는 Marker가 더 이상 필요하지 않음
  - 대체 기능: 없음
  - 완전 삭제 가능 여부: Baker 완전 삭제 시 가능
  - 참조 여부: Baker 전용

- `TilemapDebugVisibility.cs.disabled`
  - 기존 위치: `Assets/_Project/Scripts/Map/TilemapDebugVisibility.cs`
  - 이동 이유: 기능 Tilemap 표시/숨김 구조를 더 이상 사용하지 않음
  - 대체 기능: 없음
  - 완전 삭제 가능 여부: 기능 Tilemap 완전 삭제 시 가능
  - 참조 여부: 자동 Setup 기능 전용

### Tile Palettes

- `Palette_Function.prefab`
- `Palette_GroundCollision.prefab`
- `Palette_WallCollision.prefab`
- `Palette_MonsterSightBlock.prefab`
- `Palette_LightSightBlock.prefab`

이동 이유: 기능 타일을 팔레트로 칠해서 충돌/시야 차단을 만드는 구조를 더 이상 사용하지 않음.

### Function Tiles

- `TILE_GroundCollision_Function.asset`
- `TILE_WallCollision_Function.asset`
- `TILE_MonsterSightBlock_Function.asset`
- `TILE_LightSightBlock_Function.asset`

이동 이유: 기능 Tilemap을 사용하지 않기로 결정함.

### Temp Function Sprites

- `TEMP_Function_GroundCollision.png`
- `TEMP_Function_WallCollision.png`
- `TEMP_Function_MonsterSightBlock.png`
- `TEMP_Function_LightSightBlock.png`

이동 이유: 기능 타일 시각 표시용 임시 이미지가 더 이상 필요하지 않음.
