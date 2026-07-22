# Scene Connection Guide

## 핵심 개념

`ScenePortal3D`는 출구이고 `SceneSpawnPoint3D`는 대상 씬의 도착 위치다. Portal의 `targetSpawnId`와 대상 씬 SpawnPoint의 `spawnId`는 대소문자까지 정확히 같아야 한다. 한 씬에 Portal과 SpawnPoint를 여러 개 둘 수 있어 왕복형 메트로베니아 연결을 구성할 수 있다.

## 추천 ID 규칙

- Scene: `Stage_01_Forest`, `Stage_02_LabEntrance`, `Stage_03_Cave`
- Portal: `Portal_Right_To_Stage02`, `Portal_Left_To_Stage01`, `Portal_Top_To_Stage04`, `Portal_Bottom_To_Stage03`
- Spawn: `Spawn_Default`, `Spawn_From_Left`, `Spawn_From_Right`, `Spawn_From_Top`, `Spawn_From_Bottom`, `Spawn_From_Stage01`

예: Stage 01 오른쪽 Portal은 `targetSceneName=Stage_02_LabEntrance`, `targetSpawnId=Spawn_From_Left`로 설정한다. Stage 02 왼쪽 Portal은 `targetSceneName=Stage_01_Forest`, `targetSpawnId=Spawn_From_Right`로 설정한다.

## 씬 연결 순서

1. 메뉴 `_Project/Scene/Create Scene Transition Folders`로 현재 씬에 `Scene_Transitions/Portals/SpawnPoints`를 만든다.
2. `Prefabs/Scene/SpawnPoint`를 대상 씬에 배치하고 고유 `spawnId`를 지정한다. 씬마다 하나는 `isDefaultSpawn`을 켠다.
3. `Prefabs/Scene/Portal_Exit`를 출발 씬에 배치하고 `portalId`, `targetSceneName`, `targetSpawnId`를 입력한다.
4. 즉시 진입은 `requireInteract=false`, F키 진입은 `requireInteract=true`로 둔다. BoxCollider는 반드시 `isTrigger=true`여야 한다.
5. 최초 시작 씬 또는 공통 매니저 씬에 `SceneTransitionManager` 프리팹 하나를 둔다. 없어도 첫 이동 때 자동 생성된다.
6. 메뉴 `_Project/Scene/Add Stage Scenes To Build Settings`로 Stage 씬을 등록한다.
7. 메뉴 `_Project/Scene/Validate Current Scene Connections`로 현재 씬을 검사한다. 전체 검사는 `Validate All Stage Scene Connections`를 사용한다.

## SpawnPoint 옵션

`spawnOffset`은 실제 도착 위치를 보정한다. `faceRight`는 기획 정보이며 PlayerController를 직접 뒤집지 않는다. `canUseAsRespawnPoint`는 향후 리스폰 연동용 표시다. 기존 `PlayerSpawnPoint`도 Manager의 fallback으로 계속 지원한다.

## Validator 검사

빈 Scene/Spawn ID, Portal Collider/Trigger, 중복 Spawn ID, Default Spawn 누락, Build Settings 누락, Missing Script를 Warning으로 출력한다. 전체 Stage 검사는 저장 여부를 먼저 묻고 원래 열려 있던 씬 구성을 복원한다.

## 자주 나는 문제

- 엉뚱한 위치 도착: `targetSpawnId` 오타 또는 중복 Spawn ID 확인
- 씬 이동 안 됨: `targetSceneName`과 Build Settings 확인
- Player가 이동하지 않음: Player Tag가 `Player`인지 확인
- Trigger가 작동하지 않음: 3D BoxCollider와 `isTrigger=true` 확인
- 여러 SpawnPoint 중 잘못 선택: 씬 안의 `spawnId`를 고유하게 수정

2D Rigidbody/Collider는 사용하지 않는다. 맵 충돌은 기존 3D BoxCollider 방식을 유지한다.
