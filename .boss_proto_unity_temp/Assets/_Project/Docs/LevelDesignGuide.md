# Level Design Guide

이 문서는 기획자가 코드 수정 없이 프리팹 배치와 Inspector 설정만으로 스테이지를 만드는 기준입니다.

## 1. 새 스테이지 씬 만들기

1. `Assets/_Project/Scenes` 폴더에 `Stage_01`, `Stage_02`처럼 새 씬을 만든다.
2. 기존 테스트 씬의 Player, Camera, World 관련 기본 오브젝트가 필요하면 복사해서 사용한다.
3. 씬 이름은 `StageExitTrigger.nextSceneName`에 입력할 이름과 정확히 같아야 한다.

## 2. Floor / Wall / Tile 배치

1. `Assets/_Project/Prefabs/Map` 폴더의 테스트용 프리팹을 씬에 배치한다.
2. Floor는 플레이어가 밟는 바닥으로 사용한다.
3. Wall은 좌우 이동을 막는 벽으로 사용한다.
4. Tile은 장식 또는 구조 타일로 사용하되, 충돌이 필요하면 Collider를 켠다.
5. Wall / Tile / Floor처럼 몬스터와 플레이어가 통과하면 안 되는 오브젝트는 `EnvironmentObstacle` 레이어로 설정한다.

## 3. PlayerSpawnPoint 배치

1. `Assets/_Project/Prefabs/Map/PlayerSpawnPoint.prefab`을 씬에 배치한다.
2. Inspector에서 `spawnPointId`를 설정한다.
3. 기본 시작점은 `Default`를 사용한다.
4. 한 씬에 여러 개를 둘 수 있으며, 문마다 다른 `spawnPointId`로 연결할 수 있다.

## 4. StageExitTrigger 배치

1. `Assets/_Project/Prefabs/Map/StageExitTrigger.prefab`을 출구 위치에 배치한다.
2. Inspector에서 `nextSceneName`에 이동할 씬 이름을 입력한다.
3. `targetSpawnPointId`에는 다음 씬의 `PlayerSpawnPoint.spawnPointId`와 같은 값을 입력한다.
4. `requireInteraction`이 꺼져 있으면 닿는 즉시 이동한다.
5. `requireInteraction`이 켜져 있으면 플레이어가 범위 안에 있을 때 `interactionKey`를 눌러 이동한다.

## 5. 씬 연결 예시

`Stage_01`의 출구:

- `nextSceneName`: `Stage_02`
- `targetSpawnPointId`: `FromStage01`

`Stage_02`의 시작 위치:

- `PlayerSpawnPoint.spawnPointId`: `FromStage01`

두 값이 다르면 플레이어가 지정 위치로 이동하지 않는다.

## 6. Build Settings 등록

씬 이동에 사용할 모든 씬은 반드시 Build Settings에 추가해야 한다.

1. Unity 메뉴에서 `File > Build Profiles` 또는 `File > Build Settings`를 연다.
2. `Stage_01`, `Stage_02` 같은 스테이지 씬을 Scenes 목록에 추가한다.
3. 목록에 없는 씬 이름을 `nextSceneName`에 넣으면 이동하지 않는다.

## 7. 작업 규칙

- 기획자는 코드를 수정하지 않는다.
- 맵 제작은 프리팹 배치, Transform 조정, Inspector 값 설정으로 처리한다.
- PlayerController, Camera, World 전환, MonsterAI 스크립트는 맵 제작 중 수정하지 않는다.
- 씬 이동이 안 되면 먼저 `nextSceneName`, `targetSpawnPointId`, `spawnPointId`, Build Settings 등록 여부를 확인한다.

