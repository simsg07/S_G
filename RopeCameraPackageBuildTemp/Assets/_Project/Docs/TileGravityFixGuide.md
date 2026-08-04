# Tile / Gravity Object Fix Guide

이번 수정은 기능 확장이 아니라, 현재 테스트에서 확인된 3가지 문제를 우선 안정화하는 목적이다.

## 1. 타일 / 블록 정렬

블록 프리팹에 `GridPlaceableObject3D`를 추가했다.

- `Floor_Collision_Long`: 5 x 1 그리드 콜라이더
- `Wall_Collision_Long`: 1 x 4 그리드 콜라이더
- `Block_Breakable`: 1 x 1 그리드 콜라이더
- `Block_SightBlock`: 1 x 1 그리드 콜라이더

기획자가 Scene에 배치할 때는 Transform 위치를 정수 단위로 두고, 필요하면 Inspector 우클릭 메뉴에서 다음을 실행한다.

- `Snap To Grid`
- `Apply Collider Size`
- `Apply Grid Setup`

`Floor_Collision_Long` / `Wall_Collision_Long`은 기존처럼 얇은 충돌판이 아니라 실제 맵 셀 크기 기준의 콜라이더를 갖는다. 그래서 부서지는 블록이 사라진 뒤에도 다른 블록과 충돌 기준이 어긋나는 문제를 줄인다.

## 2. 중력 오브젝트 플레이어 감지

`Stone`과 `FallingBox` 프리팹에 `GravityDropSensor`를 추가했다.

기본 동작:

1. 오브젝트 아래쪽 박스 영역을 검사한다.
2. `playerLayerMask`에 해당하는 Collider가 들어오면 감지한다.
3. Layer 설정이 비어 있는 경우를 대비해 `Player` 태그 fallback도 사용할 수 있다.
4. 감지되면 `StoneObject.TriggerDrop()` 또는 `FallingBoxObject.TriggerDrop()`을 호출한다.

기본 감지 박스:

- Center Offset: `(0, -2, 0)`
- Size: `(1, 4, 1)`
- Detect Only Once: `true`

씬에서 테스트할 때는 `GravityDropSensor > Test Check Player Below`와 `Test Trigger Drop`을 사용할 수 있다.

주의: Layer/Tag는 코드에서 자동 변경하지 않는다. Player가 별도 Layer를 쓰면 `playerLayerMask`를 그 Layer로 직접 맞춘다.

## 3. 중력 오브젝트 스포너

`GravityObjectSpawner.prefab`을 추가했다.

사용 방법:

1. `Assets/_Project/Prefabs/Objects/Gravity/GravityObjectSpawner.prefab`을 Scene에 배치한다.
2. `Object Prefab`에 `Stone.prefab` 또는 `FallingBox.prefab`을 연결한다.
3. child `SpawnPoint` 위치를 실제 생성 위치로 옮긴다.
4. Play 시 `spawnOnStart`가 켜져 있으면 해당 위치에 중력 오브젝트가 생성된다.

기본 정책:

- `spawnOnStart = true`
- `respawnOnResetOnly = true`
- `autoRespawn = false`

즉, 한 번 떨어진 오브젝트를 계속 자동 재생성하지 않는다. 기획 리셋 또는 수동 테스트 때 `Respawn Object` / `Reset Spawner`로 다시 만들 수 있다.

## 이번 수정에서 건드리지 않은 범위

- PlayerController
- MonsterAI
- Camera / World 전환
- Rigidbody2D / Collider2D
- Layer / Tag 자동 변경
- Wire / Vine / CircleSpike / Crane
