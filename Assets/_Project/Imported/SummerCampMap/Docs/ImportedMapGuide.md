# Summer Camp Map 가져오기 가이드

## 목적과 범위

이 폴더는 `map.unitypackage`에서 맵의 시각 요소만 분리한 clean export 대상이다. Player, Camera, SceneLoader, Enemy, UI 및 프로젝트 기능 스크립트는 포함하지 않는다.

정리된 씬은 플레이용 완성 씬이 아니다. 각 씬에는 `Visual_Map_Grid`와 그 자식 `Visual_Tilemap`만 남아 있으며, 실제 플레이 씬에 필요한 Player, Camera, 조명, 스폰, 충돌과 게임 시스템은 메인 여름합숙 프로젝트의 기존 구성을 사용한다.

## 가져온 맵 씬

위치: `Assets/_Project/Imported/SummerCampMap/Scenes/`

- `Imported_Start_Room.unity`
- `Imported_hallwa_01.unity`
- `Imported_hallwa_02.unity`
- `Imported_hallwa_03.unity`
- `Imported_hallwa_04.unity`
- `Imported_hallwa_05.unity`
- `Imported_hallwa_06.unity`
- `Imported_middle_Room.unity`
- `Imported_Item_Room_01.unity`
- `Imported_Item_Room_02.unity`
- `Imported_Boss_Hint_Room.unity`
- `Imported_Boss_Room.unity`

모든 씬에서 제거한 오브젝트:

- `Player`
- `Main Camera`
- `Directional Light`
- 위 오브젝트에 연결된 Rigidbody, Collider, SpriteRenderer, Camera, AudioListener 및 URP 추가 컴포넌트

## Tile Palette

위치: `Assets/_Project/Imported/SummerCampMap/TilePalettes/`

- `Palette_PixelFantasy_Caves.prefab`

사용 방법:

1. Unity 메뉴에서 `Window > 2D > Tile Palette`를 연다.
2. Palette 선택 목록에서 `Palette_PixelFantasy_Caves`를 선택한다.
3. 플레이 씬에 바로 칠하기 전에 별도의 시각용 Grid/Tilemap에서 배치를 확인한다.
4. Tilemap에는 `TilemapCollider2D`, `Rigidbody2D`, `CompositeCollider2D`를 추가하지 않는다.
5. 실제 바닥과 벽 충돌은 메인 프로젝트의 `Floor_Collision` 및 `Wall_Collision` 3D BoxCollider 오브젝트로 별도 배치한다.

## Tile, Sprite, PSD 위치

- Tile 에셋 211개: `Assets/_Project/Imported/SummerCampMap/Tiles/`
- 타일 시트 PNG: `Assets/_Project/Imported/SummerCampMap/Art/Tiles/PixelFantasy_Caves_1.0/mainlev_build.png`
- 타일 원본 PSD: `Assets/_Project/Imported/SummerCampMap/Art/Tiles/PixelFantasy_Caves_1.0/Source/`
- 배경 PNG/PSD: `Assets/_Project/Imported/SummerCampMap/Art/Backgrounds/PixelFantasy_Caves_1.0/`
- 장식 PNG/PSD: `Assets/_Project/Imported/SummerCampMap/Art/Decorations/PixelFantasy_Caves_1.0/`
- 커스텀 Material: 없음. 씬과 Palette는 프로젝트의 기본 Sprite Material을 사용한다.

모든 Tile 에셋의 `Collider Type`은 `None`으로 정리했다. 타일은 시각 표현 전용이다.

## 메인 프로젝트로 가져올 폴더

다음 루트 폴더 하나만 가져온다.

`Assets/_Project/Imported/SummerCampMap/`

포함 대상:

- `Scenes`
- `Art`
- `Tiles`
- `TilePalettes`
- `Materials`
- `Docs`

폴더와 파일의 `.meta`를 함께 유지해야 Tile, Sprite, Scene 참조 GUID가 보존된다.

## 가져오면 안 되는 항목

- `Assets/_Project/Scripts`
- `Assets/_Project/Scenes/Stages`의 기존 시스템 씬
- `Assets/_Project/Prefabs`의 기존 시스템 Prefab
- `ProjectBackups`
- `_Deprecated`
- Player, Camera, GameManager, SceneLoader, PlayerSpawnPoint, Respawn, Enemy, Monster, UI, Tutorial, Input 관련 파일
- 임시 프로젝트의 `Packages`, `ProjectSettings`, `Library`, `Temp`, `Logs`

## Missing Script 또는 Missing Tile 확인

1. `SummerCampMap` 폴더만 가져왔는지 확인한다.
2. `.meta` 파일을 제외하거나 새로 생성하지 않았는지 확인한다.
3. `Palette_PixelFantasy_Caves.prefab`이 `Tiles/mainlev_build_*.asset`을 참조하는지 확인한다.
4. Tile 에셋이 `Art/Tiles/PixelFantasy_Caves_1.0/mainlev_build.png`의 Sprite를 참조하는지 확인한다.
5. 씬 Hierarchy에 `Visual_Map_Grid`와 `Visual_Tilemap` 외 오브젝트가 생겼는지 확인한다.
6. Missing Script가 보이면 clean 폴더 밖의 스크립트나 Prefab을 추가로 가져오지 말고, 잘못 포함된 기능 오브젝트를 제거한다.

## 메인 프로젝트 적용 순서

1. 메인 프로젝트를 백업하거나 작업 브랜치를 만든다.
2. `Assets/_Project/Imported/SummerCampMap/` 전체를 `.meta`와 함께 가져온다.
3. Unity 재임포트가 끝날 때까지 기다리고 Console을 Clear한다.
4. Tile Palette 창에서 `Palette_PixelFantasy_Caves`가 열리는지 확인한다.
5. 각 imported 씬을 열어 `Visual_Map_Grid/Visual_Tilemap`만 존재하는지 확인한다.
6. 실제 플레이 씬에는 필요한 Tilemap 시각 오브젝트만 복사하거나 additive 방식으로 배치한다.
7. 기존 Player, Camera, SceneLoader, Respawn 및 기타 시스템을 그대로 사용한다.
8. `Floor_Collision`과 `Wall_Collision` 3D BoxCollider로 이동 가능 영역과 벽 충돌을 별도 제작한다.
9. Console Error, Missing Script, Missing Tile을 다시 확인한 뒤 Play Mode 테스트를 진행한다.

## Burst compilation 오류 처리

`Unexpected error in Burst compilation` 또는 `Unable to load unmanaged library`는 이 clean map 폴더의 스크립트에서 발생하는 오류가 아니다. `SummerCampMap`에는 `.cs`, Burst, Jobs 코드가 없으므로 기존 Player, Camera, Monster, Crane 및 다른 게임 스크립트를 수정하지 않는다.

다음 순서대로 처리한다.

1. Unity 상단 메뉴에서 `Jobs > Burst > Clear Cache`를 실행한다.
2. Unity를 완전히 종료한 뒤 다시 실행한다.
3. 계속 발생하면 Unity 종료 상태에서 프로젝트의 `Library/BurstCache`를 삭제하거나 프로젝트 밖으로 이동한 뒤 재실행한다. 이 폴더는 Unity가 다시 생성한다.
4. 원인 분리가 급하면 `Jobs > Burst > Enable Compilation` 체크를 잠시 해제하고 Play Mode를 확인한다. 확인 후 다시 활성화한다.
5. 그래도 해결되지 않으면 Package Manager에서 Burst 패키지를 재설치한다.
6. 마지막 수단으로 Unity를 종료하고 `Library` 전체를 백업/삭제한 뒤 전체 재임포트한다.

## Console 검수 순서

1. Unity의 에셋 Import와 스크립트 컴파일이 끝날 때까지 기다린다.
2. Console의 `Clear`를 누른다.
3. `Palette_PixelFantasy_Caves`를 열고 Missing Tile이 없는지 확인한다.
4. imported 씬 12개를 차례로 열어 Missing Script가 없는지 확인한다.
5. 각 씬 Hierarchy에 `Visual_Map_Grid`와 `Visual_Tilemap`만 있는지 확인한다.
6. Play Mode에 진입해 새 Error가 발생하는지 확인한다.

## 플레이 씬으로 시각 맵 옮기기

1. imported 씬과 대상 플레이 씬을 함께 연다.
2. imported 씬의 루트 `Visual_Map_Grid`를 선택한다.
3. Hierarchy에서 대상 플레이 씬으로 이동하거나 복사한다. 자식 `Visual_Tilemap`도 함께 유지한다.
4. imported 씬의 Player, Camera, SceneLoader 또는 충돌 시스템을 추가로 가져오지 않는다.
5. 대상 플레이 씬의 기존 Player, Camera, SceneLoader, Respawn 시스템을 그대로 사용한다.
6. 타일 시각에 맞춰 기존 `Floor_Collision`과 `Wall_Collision` 3D BoxCollider를 별도로 배치한다.

## 검수 주의사항

- imported 씬 단독 Play는 지원 대상이 아니다. Camera와 Player를 의도적으로 제거했다.
- Tilemap은 장식/시각 전용이며 물리 충돌을 담당하지 않는다.
- `SummerCampMap` 밖의 imported Scripts, Prefabs, Scenes는 clean export에 포함하지 않는다.
- 메인 프로젝트의 기존 시스템 스크립트를 이 맵에 맞추기 위해 수정하지 않는다.
