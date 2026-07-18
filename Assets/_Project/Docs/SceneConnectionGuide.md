# Scene Connection Guide

## 메트로배니아식 씬 연결 규칙

이 프로젝트의 씬 연결은 한 방향 이동이 아니라, 한 씬에 여러 출구가 있고 이전 씬으로 되돌아갈 수도 있는 구조를 기준으로 합니다.

- 한 씬에 `StageExitTrigger`를 여러 개 둘 수 있습니다.
- 각 출구는 서로 다른 `nextSceneName`과 `targetSpawnPointId`를 가질 수 있습니다.
- `targetSpawnPointId`는 이동할 대상 씬의 `PlayerSpawnPoint.spawnPointId`와 정확히 같아야 합니다.
- 씬 이름은 Build Settings에 등록된 씬 이름과 정확히 같아야 합니다.
- 잘못 설정된 경우 게임을 멈추지 않고 Warning을 출력합니다.

## 파일 위치

- `SceneLoader`: `Assets/_Project/Prefabs/Core/SceneLoader.prefab`
- `StageExitTrigger`: `Assets/_Project/Prefabs/Map/StageExitTrigger.prefab`
- `PlayerSpawnPoint`: `Assets/_Project/Prefabs/Map/PlayerSpawnPoint.prefab`

## 기본 배치

1. 시작 씬에 `SceneLoader.prefab`을 하나 배치합니다.
2. 각 출구 위치에 `StageExitTrigger.prefab`을 배치합니다.
3. 각 씬의 입장 위치마다 `PlayerSpawnPoint.prefab`을 배치합니다.
4. `StageExitTrigger.targetSpawnPointId`와 대상 씬의 `PlayerSpawnPoint.spawnPointId`를 맞춥니다.

## 예시: Stage_01 → Stage_02

Stage_01의 오른쪽 출구:

- `nextSceneName = Stage_02`
- `targetSpawnPointId = From_Stage01_Right`

Stage_02의 SpawnPoint:

- `spawnPointId = From_Stage01_Right`

## 예시: Stage_02 → Stage_01

Stage_02의 왼쪽 출구:

- `nextSceneName = Stage_01`
- `targetSpawnPointId = From_Stage02_Left`

Stage_01의 SpawnPoint:

- `spawnPointId = From_Stage02_Left`

## 여러 갈래 출구 예시

Stage_01:

- `Exit_Right` → `Stage_02` / `From_Stage01_Right`
- `Exit_Left` → `Stage_00` / `From_Stage01_Left`
- `Exit_Down` → `Cave_01` / `From_Stage01_Down`

출구마다 `StageExitTrigger`를 따로 배치하고 값을 따로 설정합니다.

## 추천 이름 규칙

출구 이름:

- `Exit_Left`
- `Exit_Right`
- `Exit_Up`
- `Exit_Down`
- `Exit_BossRoom`
- `Exit_Cave`

SpawnPoint ID:

- `From_Stage01_Left`
- `From_Stage01_Right`
- `From_Stage02_Left`
- `From_Cave01`
- `From_BossRoom`
- `Default`

`Default`는 fallback용으로 씬마다 하나 정도만 두는 것을 권장합니다.

## Inspector 검증 버튼

### StageExitTrigger

버튼:

- `Validate Scene Connection`

확인 항목:

- `nextSceneName`이 비어 있지 않은지
- `nextSceneName`이 Build Settings에 등록되어 있는지
- `targetSpawnPointId`가 비어 있지 않은지
- 현재 씬에 `SceneLoader`가 있는지
- Collider가 `isTrigger = true`인지
- `playerLayerMask`가 설정되어 있는지

### PlayerSpawnPoint

버튼:

- `Validate Spawn Point`

확인 항목:

- `spawnPointId`가 비어 있지 않은지
- 같은 씬에 같은 `spawnPointId`가 중복되는지
- Scene View에 ID와 방향 표시가 보이는지

## 오류 방어 처리

- `nextSceneName`이 비어 있으면 이동하지 않고 Warning 출력
- `nextSceneName`이 Build Settings에 없으면 이동하지 않고 Warning 출력
- `targetSpawnPointId`가 비어 있으면 `Default` 사용
- 대상 SpawnPoint가 없으면 `Default` SpawnPoint 시도
- `Default`도 없으면 Player 위치 유지
- 같은 SpawnPoint ID가 중복되면 첫 번째를 사용하고 Warning 출력
- 씬 로딩 중 추가 이동 요청은 무시하고 Warning 출력

## 체크리스트

1. 씬이 Build Settings에 등록되어 있는가?
2. `StageExitTrigger.nextSceneName`이 정확한가?
3. `StageExitTrigger.targetSpawnPointId`가 대상 씬의 `PlayerSpawnPoint.spawnPointId`와 같은가?
4. 같은 씬 안에 중복된 `spawnPointId`가 없는가?
5. `StageExitTrigger` Collider가 `isTrigger`인가?
6. Player Layer가 `playerLayerMask`에 포함되어 있는가?
7. 시작 씬에 `SceneLoader`가 있는가?

## 테스트 방법

### 테스트 1: Stage_01 → Stage_02

1. Stage_01 출구에 `StageExitTrigger` 배치
2. `nextSceneName = Stage_02`
3. `targetSpawnPointId = From_Stage01_Right`
4. Stage_02에 `PlayerSpawnPoint` 배치
5. `spawnPointId = From_Stage01_Right`
6. Player가 Stage_02의 해당 SpawnPoint로 이동하는지 확인

### 테스트 2: Stage_02 → Stage_01

1. Stage_02 출구에 `StageExitTrigger` 배치
2. `nextSceneName = Stage_01`
3. `targetSpawnPointId = From_Stage02_Left`
4. Stage_01에 `PlayerSpawnPoint` 배치
5. `spawnPointId = From_Stage02_Left`
6. Player가 Stage_01의 해당 SpawnPoint로 이동하는지 확인

### 테스트 3: 오류 방어

1. `nextSceneName`을 일부러 틀리게 입력
2. Warning만 출력되고 게임이 멈추지 않는지 확인
3. `targetSpawnPointId`를 일부러 틀리게 입력
4. Default SpawnPoint를 찾거나 Player 위치가 유지되는지 확인
