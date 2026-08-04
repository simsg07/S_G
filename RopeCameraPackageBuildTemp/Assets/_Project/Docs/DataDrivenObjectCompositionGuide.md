# Data Driven Object Composition Guide

## 목적

오브젝트를 상속 구조가 아니라 공통 컴포넌트 조합으로 만든다.  
`ObjectData`는 오브젝트 생성/초기 세팅용 템플릿이며 런타임 고정 데이터가 아니다. 실제 밸런스 값은 프리팹에 직렬화된 각 컴포넌트 값이다.

## ObjectData 사용 흐름

1. `ObjectData` asset을 만든다.
2. Prefab에 `DataDrivenObjectController`를 붙인다.
3. `objectData`에 원하는 `ObjectData`를 연결한다.
4. ContextMenu의 `Apply Object Data Once`를 실행한다.
5. 각 컴포넌트에 초기값이 복사된다.
6. 이후 기획자는 Prefab Inspector에서 값을 직접 조정한다.
7. 기본 `applyMode = ManualOnly`이므로 Play 시작 시 `ObjectData`가 값을 다시 덮어쓰지 않는다.

다시 템플릿 값을 적용해야 한다면 `Reset Applied Flag` 후 `Apply Object Data Once`를 사용한다. `ApplyOnAwake` 또는 `ApplyOnStart`는 특별한 런타임 초기화가 필요한 경우에만 선택하며, `allowRuntimeOverwrite`도 명시적으로 켜야 한다.

현재 흐름은 `ObjectData → Apply Object Data Once → Prefab Inspector 조정`이다. 향후 생성 도구는 `ObjectData → Generate Prefab → 필요한 컴포넌트 자동 부착 → Apply Once → Prefab Inspector 조정` 순서로 확장한다.

## 기본 조합

| 오브젝트 | 컴포넌트 조합 |
| --- | --- |
| 부서지는 타일 | `DataDrivenObjectController` + `BlockObject` + `HitReceiver` + `BreakableObject3D` + `OpenPathOnBreak` |
| Wire / Vine | 기존 `WireObject` / `VineObject` + `HitReceiver` + `ConnectedObjectLink` + 선택적 `BreakableObject3D` |
| Stone | `DataDrivenObjectController` + `StoneObject` + `GravityObject3D` + `DamageDealer` + `PausablePhysicsObject` + `BreakableObject3D` |
| FallingBox | `DataDrivenObjectController` + `FallingBoxObject` + `GravityObject3D` + `DamageDealer` + `PausablePhysicsObject` |

## 공격 반응 규칙

`HitReceiver`는 `DamageInfo.hitSourceType`을 보고 맞을지 무시할지 결정한다.

- `Block_Breakable`: `BoomberExplosion`, `BoomberContact` 허용
- `Wire`: `EyeballFlyAttack` 허용
- `Vine`: `EyeballFlyAttack` 허용
- 일반 바닥/벽/시야 블록: 공격 반응 없음

이 규칙은 `ObjectData`의 hit rule 값으로 조정할 수 있다.

## ObjectData로 초기 세팅 가능한 값

- `canBeTargeted`
- `maxHitCount`
- `acceptGenericHit`
- `acceptEyeballFlyAttack`
- `acceptBoomberContact`
- `acceptBoomberExplosion`
- `acceptMonsterAttack`
- `damage`
- `useGravity`
- `startAttached`
- `disableGravityOnStart`
- `dropSpeed`
- `breakOnGroundHit`
- `remainAsPlatformOnGround`
- `blockType`
- `removeColliderOnBreak`
- `hideVisualOnBreak`
- `clearPlayerOverlapOnBreak`
- `safePushDistance`

## 기획자용 프리팹 조정표

| 대상 | 조정 위치 | 주요 조정값 |
| --- | --- | --- |
| `Block_Breakable` | Prefab > `BlockObject` / `HitReceiver` | `maxHitCount`, `removeColliderOnBreak`, `hideVisualOnBreak`, `visualHideDelay`, `clearPlayerOverlapOnBreak`, `safePushDistance`, `acceptBoomberExplosion` |
| `Floor_Collision_Long` | Prefab > `BlockObject` | block rules, collider/reference, debug 값 |
| `Wall_Collision_Long` | Prefab > `BlockObject` | block rules, collider/reference, debug 값 |
| `Block_SightBlock` | Prefab > `BlockObject` | `canBlockSight`, `canBlockLight`, collider/reference 값 |
| `Stone` | Prefab > `GravityDropSensor` / `GravityObject3D` / `StoneObject` / `GravityObjectDamageDealer` | `detectionCenterOffset`, `detectionBoxSize`, `startAttached`, `groundLayerMask`, `destroyDelay`, `instantKillPlayer`, `damageOnlyWhileFalling`, `playerLayerMask` |
| `FallingBox` | Prefab > `GravityDropSensor` / `GravityObject3D` / `FallingBoxObject` / `GravityObjectDamageDealer` | `detectionCenterOffset`, `detectionBoxSize`, `startAttached`, `groundLayerMask`, `canBecomePlatform`, `instantKillPlayer`, `damageOnlyWhileFalling`, `playerLayerMask` |
| `RespawnPoint` | Scene/Prefab > `PlayerSpawnPoint` 또는 테스트용 `PlayerRespawnPoint` | `spawnPointId`, `isDefaultSpawn`, `canUseAsRespawnPoint`, `showGizmos` |
| Scene Exit | Scene object > `StageExitTrigger` | `exitId`, `nextSceneName`, `targetSpawnPointId`, `requireInteraction`, `isLocked` |

`ObjectData` 연결은 초기값의 출처를 기록하기 위해 유지한다. 수동 적용 후에는 위 컴포넌트의 Prefab Inspector 값이 실제 조정값이다.

## Boomber가 Block_Breakable을 부수는 방식

1. `BoomberExplosion`이 폭발한다.
2. `breakableLayerMask` 범위 안의 Collider를 `Physics.OverlapSphere`로 찾는다.
3. Collider 주변의 `HitReceiver`를 찾는다.
4. `DamageInfo.hitSourceType = BoomberExplosion`으로 `RegisterHit`을 보낸다.
5. `Block_Breakable`의 `HitReceiver`가 해당 출처를 허용하면 카운트가 증가한다.
6. `maxHitCount`에 도달하면 `BlockObject.BreakBlock()`이 실행되고 Collider가 즉시 꺼진다.

## EyeballFly가 Wire/Vine을 공격하는 방식

1. EyeballFly 공격 시점에 공격 반경 안의 Collider를 찾는다.
2. Collider 주변의 `HitReceiver`를 찾는다.
3. `HitReceiver.CanAcceptHitSource(EyeballFlyAttack)`이 true인 대상만 공격 대상으로 본다.
4. `DamageInfo.hitSourceType = EyeballFlyAttack`으로 `RegisterHit`을 보낸다.
5. Wire는 2회, Vine은 1회 hit count 기준으로 기존 이벤트/파괴 흐름을 탄다.

## 주의

- Layer/Tag는 코드에서 자동 변경하지 않는다.
- PlayerController, MonsterAI 대규모 구조, Camera, World 전환 시스템은 직접 수정하지 않는다.
- Rigidbody2D / Collider2D는 사용하지 않는다.
