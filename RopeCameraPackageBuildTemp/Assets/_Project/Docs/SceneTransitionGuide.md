# Scene Transition Guide

## 기본 구조

한 씬에 `StageExitTrigger`와 `PlayerSpawnPoint`를 여러 개 둘 수 있다. 각 출구는 씬 이름뿐 아니라 도착 지점 ID까지 지정해야 한다.

- `StageExitTrigger.nextSceneName`: 이동할 씬 이름
- `StageExitTrigger.targetSpawnPointId`: 도착 씬의 SpawnPoint ID
- `PlayerSpawnPoint.spawnPointId`: 출구가 요청하는 ID
- `PlayerSpawnPoint.isDefaultSpawn`: 요청 ID를 찾지 못했을 때 사용할 기본 지점

SceneLoader는 씬을 로드하기 전에 ID를 저장하고, 로드 완료 후 같은 ID의 `PlayerSpawnPoint`로 Player를 이동시킨다. 같은 ID가 없으면 `isDefaultSpawn` 지점을 사용한다.

## 추천 ID

- `Spawn_Default`
- `Spawn_FromLeft`
- `Spawn_FromRight`
- `Spawn_FromTop`
- `Spawn_FromBottom`

한 씬 안에서 ID는 중복되지 않게 설정한다.

## 양방향 연결 예시

Scene_A 오른쪽 출구:

- `nextSceneName = Scene_B`
- `targetSpawnPointId = Spawn_FromLeft`

Scene_B 왼쪽 출구:

- `nextSceneName = Scene_A`
- `targetSpawnPointId = Spawn_FromRight`

두 씬은 Build Settings에 등록되어 있어야 한다. 출구의 BoxCollider는 `isTrigger=true`여야 하며, 상호작용형 출구는 `requireInteraction`을 켜고 지정 키를 사용한다.

## 리스폰 지점과 구분

`PlayerSpawnPoint`는 씬 이동과 리스폰에 함께 사용할 수 있다. 리스폰에도 사용할 지점만 `canUseAsRespawnPoint=true`로 설정한다. 씬 이동용 ID와 리스폰용 ID가 다르면 Player의 `PlayerDamageReceiver.respawnPointId`를 별도로 지정한다.
