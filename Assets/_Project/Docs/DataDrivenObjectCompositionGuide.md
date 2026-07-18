# Data Driven Object Composition Guide

## 목적

오브젝트를 상속 구조가 아니라 공통 컴포넌트 조합으로 만든다.  
기획자는 가능하면 `ObjectData` 값을 바꾸고 `Apply Object Data`를 눌러 동작을 조정한다.

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

## ObjectData에서 조정 가능한 값

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
