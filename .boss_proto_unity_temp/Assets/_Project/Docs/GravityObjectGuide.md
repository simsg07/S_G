# Gravity Object Guide

## Player가 리스폰하지 않을 때

`GravityObjectDamageDealer`는 낙하 중 Player 충돌을 확인한 뒤 `PlayerDamageReceiver.KillAndRespawn()`을 호출한다. 다음 항목을 순서대로 확인한다.

1. Player에 활성화된 `PlayerDamageReceiver`가 있는가?
2. `respawnPoint`가 연결되어 있거나 사용할 수 있는 기본 `PlayerSpawnPoint`가 있는가?
3. `GravityObjectDamageDealer.playerLayerMask`가 Player Layer인가? 비어 있다면 Player 태그가 설정되어 있는가?
4. 충돌 순간 `StoneObject.IsFalling` 또는 `FallingBoxObject.IsFalling`이 true인가?
5. Console에 `Player collision detected`, `Falling state valid`, `KillAndRespawn called` 로그가 순서대로 출력되는가?

## 중력 오브젝트 감지 범위 설정

감지 범위는 `GravityDropSensor` 컴포넌트에서 직접 조정하지 않고 각 오브젝트의 `ObjectData`에서 조정한다.

- Stone: `Assets/_Project/Data/Objects/StoneData.asset`
- FallingBox: `Assets/_Project/Data/Objects/FallingBoxData.asset`
- 조정 값: `gravityDetectionCenterOffset`, `gravityDetectionBoxSize`

`DataDrivenObjectController`가 Awake 또는 `Apply Object Data` 실행 시 이 값을 `GravityDropSensor`에 적용한다. 감지는 Raycast가 아니라 `Physics.OverlapBox`를 사용하며, Player가 박스 안에 들어오면 즉시 낙하한다.

주의: Stone 프리팹을 중복 생성하지 말고 `Assets/_Project/Prefabs/Objects/Gravity/Stone.prefab` 하나만 사용한다.

## 목적

중력 오브젝트는 경고, 대기 애니메이션, 흔들림 연출 없이 단순하게 동작한다.

1. Player가 감지 박스 안에 들어온다.
2. `GravityDropSensor`가 즉시 `TriggerDrop()`을 호출한다.
3. Stone 또는 FallingBox가 바로 떨어진다.
4. 낙하 중 Player와 충돌하면 Player를 리스폰시킨다.
5. 바닥에 닿으면 Stone은 부서지고, FallingBox는 발판으로 남는다.

## GravityDropSensor

사용 스크립트:

- `Assets/_Project/Scripts/Objects/Gravity/GravityDropSensor.cs`

감지 방식:

- Raycast가 아니라 `Physics.OverlapBox`를 사용한다.
- 감지 박스 안 어디든 Player가 들어오면 즉시 발동한다.
- 기본 감지 박스는 `detectionCenterOffset = (0, -2, 0)`, `detectionBoxSize = (3, 4, 1)`이다.
- `playerLayerMask`가 설정되어 있으면 해당 Layer로 감지한다.
- Layer 설정이 비어 있을 때를 대비해 `Player` 태그 fallback을 사용할 수 있다.

사용하지 않는 것:

- warningDelay
- Warning 애니메이션
- Idle 애니메이션
- Shake / 흔들림 연출

ContextMenu:

- `Test Check Player In Detection Box`
- `Test Trigger Drop`
- `Reset Sensor`
- `Validate Drop Sensor`

## Stone

사용 프리팹:

- `Assets/_Project/Prefabs/Objects/Gravity/Stone.prefab`

동작:

1. 시작 시 붙어 있는 상태로 대기한다.
2. `GravityDropSensor`가 Player를 감지하면 즉시 떨어진다.
3. 낙하 중 Player와 충돌하면 `GravityObjectDamageDealer`가 Player의 `KillAndRespawn()`을 호출한다.
4. 바닥과 충돌하면 `BreakStone()`이 실행된다.
5. Stone은 부서진 뒤 `destroyDelay` 후 비활성화된다.
6. Spawner가 연결되어 있으면 완료 상태를 Spawner에 알려준다.

## FallingBox

사용 프리팹:

- `Assets/_Project/Prefabs/Objects/Gravity/FallingBox.prefab`

동작:

1. 시작 시 붙어 있는 상태로 대기한다.
2. `GravityDropSensor`가 Player를 감지하면 즉시 떨어진다.
3. 낙하 중 Player와 충돌하면 `GravityObjectDamageDealer`가 Player의 `KillAndRespawn()`을 호출한다.
4. 바닥과 충돌하면 `LandAsPlatform()`이 실행된다.
5. FallingBox는 부서지지 않고 Rigidbody를 멈춘 뒤 Collider를 유지해서 발판으로 남는다.
6. Spawner가 연결되어 있으면 완료 상태를 Spawner에 알려준다.

## GravityObjectDamageDealer

사용 스크립트:

- `Assets/_Project/Scripts/Objects/Gravity/GravityObjectDamageDealer.cs`

역할:

- Stone / FallingBox가 낙하 중일 때만 Player 충돌을 처리한다.
- PlayerController를 직접 참조하지 않는다.
- Player 쪽의 `PlayerDamageReceiver`를 찾아 `KillAndRespawn()`을 호출한다.
- FallingBox가 착지해 발판이 된 뒤에는 Player에게 피해를 주지 않는다.

## GravityObjectSpawner

사용 프리팹:

- `Assets/_Project/Prefabs/Objects/Gravity/GravityObjectSpawner.prefab`

사용 방법:

1. Scene에 `GravityObjectSpawner.prefab`을 배치한다.
2. `Object Prefab`에 `Stone.prefab` 또는 `FallingBox.prefab`을 연결한다.
3. child `SpawnPoint`를 생성 위치로 옮긴다.
4. Play 시 `spawnOnStart`가 켜져 있으면 오브젝트를 생성한다.
5. 자동 반복 생성이 필요하면 `autoRespawn`을 켠다.
6. 수동 테스트는 `Respawn Object`를 사용한다.

## Unity에서 직접 설정할 값

- `GravityDropSensor.playerLayerMask`: Player Layer
- `GravityObjectDamageDealer.playerLayerMask`: Player Layer
- `StoneObject.groundLayerMask`: Ground / Map Layer
- `FallingBoxObject.groundLayerMask`: Ground / Map Layer
- `PlayerDamageReceiver.respawnPoint`: 돌아갈 위치

Layer / Tag는 코드에서 자동 변경하지 않는다.

## 테스트 순서

1. `GravityObjectSpawner.prefab`을 Scene에 배치한다.
2. `Object Prefab`에 Stone 또는 FallingBox를 연결한다.
3. `SpawnPoint`를 천장 또는 낙하 시작 위치로 옮긴다.
4. Player LayerMask와 Ground LayerMask를 Inspector에서 맞춘다.
5. Play한다.
6. Player가 감지 박스 안에 들어가면 즉시 떨어지는지 확인한다.
7. 낙하 중 Player와 충돌하면 Player가 리스폰되는지 확인한다.
8. Stone은 바닥에서 부서지는지 확인한다.
9. FallingBox는 바닥에서 발판으로 남는지 확인한다.
