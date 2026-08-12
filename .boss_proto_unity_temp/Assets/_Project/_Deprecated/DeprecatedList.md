# Deprecated List

이 폴더는 바로 삭제하기 위험하지만 현재 기본 제작 흐름에서는 쓰지 않는 파일을 모아둔 곳입니다. 완전 삭제 전에는 씬, 프리팹, Animator Controller, 코드 참조를 다시 확인합니다.

## 2026-07-15 이동

### Floor_Test.prefab
- 기존 위치: `Assets/_Project/Prefabs/Map/Floor_Test.prefab`
- 이동 이유: 새 맵 배치 프리팹 `Floor_Collision`, `Tile_Visual`, `Wall_Tile`, `Block_Tile`로 대체된 테스트용 프리팹
- 대체 파일: `Assets/_Project/Prefabs/Map/Floor_Collision.prefab`
- 완전 삭제 가능 여부: 나중에 확인 필요

### Tile_Test.prefab
- 기존 위치: `Assets/_Project/Prefabs/Map/Tile_Test.prefab`
- 이동 이유: 새 맵 배치 프리팹 `Tile_Visual`로 대체된 테스트용 프리팹
- 대체 파일: `Assets/_Project/Prefabs/Map/Tile_Visual.prefab`
- 완전 삭제 가능 여부: 나중에 확인 필요

### Wall_Test.prefab
- 기존 위치: `Assets/_Project/Prefabs/Map/Wall_Test.prefab`
- 이동 이유: 새 맵 배치 프리팹 `Wall_Tile`로 대체된 테스트용 프리팹
- 대체 파일: `Assets/_Project/Prefabs/Map/Wall_Tile.prefab`
- 완전 삭제 가능 여부: 나중에 확인 필요

### MapTest generated assets
- 기존 위치: `Assets/_Project/Art/Generated/MapTest/`
- 이동 파일: `MapTestCubeMesh.asset`, `Floor_Test_Material.mat`, `Tile_Test_Material.mat`, `Wall_Test_Material.mat`
- 이동 이유: `Floor_Test`, `Tile_Test`, `Wall_Test` 전용 생성 산출물
- 대체 파일: 실제 맵 프리팹의 제작용 Visual/Collider 구성
- 완전 삭제 가능 여부: 테스트 프리팹을 완전 삭제할 때 함께 확인

### MapTestPrefabBuilder.cs
- 기존 위치: `Assets/_Project/Scripts/Editor/MapTestPrefabBuilder.cs`
- 이동 이유: 위 테스트 프리팹을 자동 생성하는 Editor 전용 도구
- 대체 파일: 현재 기획용 맵 프리팹 세트
- 완전 삭제 가능 여부: 테스트 프리팹 재생성이 더 필요 없으면 삭제 가능

## 2026-07-15 Player 정리 보류

### PlayerAttack3D.cs
- 기존 위치: `Assets/_Project/Scripts/Player/PlayerAttack3D.cs`
- 이동 이유: 현재 기본 `PLAYER_Main.prefab`에서는 공격 기능을 제외했습니다.
- 현재 대체 기능: 없음. 기본 이동/점프은 `PlatformerPlayer3D`가 담당합니다.
- 완전 삭제 가능 여부: 아직 불가
- 참조 여부: `GameScene.unity`, `AbilityTestScene.unity`의 기존 Player 오브젝트가 참조 중입니다. Missing Script 방지를 위해 이번 작업에서는 이동/삭제하지 않았습니다.

### PlayerAttackAnimation3D.cs
- 기존 위치: `Assets/_Project/Scripts/Animation/PlayerAttackAnimation3D.cs`
- 이동 이유: 현재 기본 `PLAYER_Main.prefab`에서는 공격 애니메이션 기능을 제외했습니다.
- 현재 대체 기능: 없음
- 완전 삭제 가능 여부: 아직 불가
- 참조 여부: `GameScene.unity`, `AbilityTestScene.unity`의 기존 Player 오브젝트와 `PlayerAttack3D.cs`가 참조 중입니다.

### PlayerAttack resource sprites
- 기존 위치: `Assets/_Project/Resources/PlayerAttack/attack_01.png` ~ `attack_04.png`
- 이동 이유: 현재 기본 `PLAYER_Main.prefab`에서는 임시 공격/Idle Sprite 표시를 제외했습니다.
- 현재 대체 기능: `Visual` 자식의 Animator/SpriteRenderer 구성
- 완전 삭제 가능 여부: 아직 불가
- 참조 여부: `PlayerAttackAnimation3D.cs`가 `Resources/PlayerAttack` 경로로 로드합니다. 관련 스크립트 정리가 끝난 뒤 이동/삭제하세요.

## 2026-07-15 Map Brush 구조 Deprecated

### GameObjectBrushPalette
- 기존 위치: `Assets/_Project/Prefabs/Map/GameObjectBrushPalette/`
- 이동 위치: `Assets/_Project/_Deprecated/MapBrushOldStructure/Prefabs/Map/GameObjectBrushPalette/`
- 이동 이유: 기획 결정 변경으로 GameObject Brush / Prefab Brush 방식은 메인 맵 제작 방식에서 제외
- 현재 대체 기능: Unity Tile Palette 기반 시각 타일 배치 + 기존 FloorCollision / Wall Collider 프리팹
- 완전 삭제 가능 여부: Tile Palette 중심 제작 방식이 확정된 뒤 삭제 가능
- 참조 여부: 이동 전 검색 기준, 해당 구조와 관련 문서 외 씬/기존 프리팹 참조 없음

### Prefab Palette
- 기존 위치: `Assets/_Project/Prefabs/Map/Palette/`
- 이동 위치: `Assets/_Project/_Deprecated/MapBrushOldStructure/Prefabs/Map/Palette/`
- 이동 이유: PAL_* 프리팹 팔레트는 더 이상 메인 제작 방식이 아님
- 현재 대체 기능: `Assets/_Project/Tiles/`의 Tile Palette용 타일 구조
- 완전 삭제 가능 여부: Tile Palette 구조 안정화 뒤 삭제 가능
- 참조 여부: 이동 전 검색 기준, 해당 구조와 관련 문서 외 씬/기존 프리팹 참조 없음

### GameObjectBrushPaletteGuide.md
- 기존 위치: `Assets/_Project/Docs/GameObjectBrushPaletteGuide.md`
- 이동 위치: `Assets/_Project/_Deprecated/MapBrushOldStructure/Docs/GameObjectBrushPaletteGuide.md`
- 이동 이유: GameObject Brush 방식 폐기
- 현재 대체 문서: `Assets/_Project/Docs/MapPaletteGuide.md`
- 완전 삭제 가능 여부: 나중에 삭제 가능

