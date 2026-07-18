# Object Common Scripts Guide

이 문서는 Stone, Box, CircleSpike, Wire, Vine, UnstableTile, Crane, CraneSwitch 같은 개별 오브젝트를 만들기 전에 공통으로 사용할 기반 스크립트를 정리합니다.

현재 기준은 Unity 6, 3D 물리 기반 2.5D 사이드뷰입니다. Rigidbody2D, Collider2D는 사용하지 않고 Rigidbody, Collider, BoxCollider, SphereCollider 같은 3D 물리 컴포넌트를 사용합니다.

## 생성된 파일

| 계열 | 파일 | 역할 |
| --- | --- | --- |
| Damage | `IDamageable.cs` | 데미지를 받을 수 있는 대상의 공통 인터페이스입니다. 기존 `TakeDamage(int)`와 새 `TakeDamage(DamageInfo)`를 함께 지원합니다. |
| Damage | `DamageInfo.cs` | 데미지량, 공격자, 소스 오브젝트, 피격 위치, 방향, 데미지 타입을 전달합니다. |
| Damage | `DamageDealer.cs` | Collision/Trigger 진입 시 대상 레이어를 확인하고 `IDamageable`에게 데미지를 전달합니다. |
| Damage | `HitReceiver.cs` | Wire, Vine, UnstableTile처럼 공격 횟수나 타격 횟수를 세는 오브젝트에 붙입니다. |
| Trigger | `ITriggerableObject.cs` | Trigger/Reset을 받을 수 있는 오브젝트의 공통 인터페이스입니다. |
| Trigger | `TriggerZone3D.cs` | Player가 3D Trigger 영역에 들어오면 연결된 오브젝트를 실행합니다. |
| Trigger | `ConnectedObjectLink.cs` | Wire, Vine, Switch 등이 다른 오브젝트를 실행하거나 리셋할 때 쓰는 연결 컴포넌트입니다. |
| Physics | `GravityObject3D.cs` | 천장에 붙어 있다가 Trigger 후 떨어지는 3D 물리 오브젝트 기반입니다. |
| Physics | `PausablePhysicsObject.cs` | Camera/Shutter 같은 시스템이 물리 오브젝트를 일시정지/재개할 수 있게 합니다. |
| Breakable | `BreakableObject3D.cs` | 일정 타격 후 부서지는 오브젝트의 공통 기반입니다. |
| Breakable | `OpenPathOnBreak.cs` | 부서진 뒤 길을 열기 위해 오브젝트/콜라이더를 켜고 끕니다. |

## 오브젝트별 추천 조합

| 오브젝트 | 붙일 공통 스크립트 | 설명 |
| --- | --- | --- |
| Stone | `GravityObject3D`, `DamageDealer`, `PausablePhysicsObject`, `BreakableObject3D` | 떨어지고, 맞으면 데미지를 주고, 카메라/셔터로 멈출 수 있고, 바닥 충돌 후 부서지는 구조의 기반입니다. |
| Box | `GravityObject3D`, `PausablePhysicsObject`, `BreakableObject3D` | 낙하/정지/파괴가 필요한 상자류에 사용합니다. |
| CircleSpike | `DamageDealer` | 닿으면 데미지를 주는 함정류에 사용합니다. 회전/이동 로직은 별도 개별 구현에서 붙입니다. |
| Wire | `HitReceiver`, `ConnectedObjectLink` | 공격 횟수를 세고 연결된 장치를 실행하는 구조에 사용합니다. |
| Vine | `HitReceiver`, `BreakableObject3D`, `OpenPathOnBreak` | 베거나 공격해서 끊고, 끊긴 뒤 통로를 여는 구조에 사용합니다. |
| UnstableTile | `HitReceiver`, `BreakableObject3D`, `OpenPathOnBreak` | 일정 충격 후 타일이 사라지고 길이 열리는 구조에 사용합니다. |
| Crane | `ITriggerableObject` 구현 스크립트 + `PausablePhysicsObject` | 실제 이동/집게 로직은 개별 구현에서 만들고, 공통 정지 제어만 가져갑니다. |
| CraneSwitch | `TriggerZone3D`, `ConnectedObjectLink` | 플레이어 진입 또는 상호작용 후 Crane 계열 오브젝트를 실행합니다. |

## 3D 물리를 쓰는 이유

- 프로젝트의 플레이어, 몬스터, 맵 충돌이 이미 3D Rigidbody/Collider 기준입니다.
- 2.5D 사이드뷰는 Z축을 고정해서 구현하고, 충돌 계산은 Unity 3D 물리에 맡기는 방식이 현재 구조와 맞습니다.
- Rigidbody2D/Collider2D를 섞으면 충돌 이벤트가 서로 맞지 않아 Player/Monster/World 시스템과 연결이 복잡해집니다.

## 제작 순서

1. 공통 기반 스크립트는 `Assets/_Project/Scripts/Objects/Common/` 아래에서 유지합니다.
2. 먼저 `DamageDealer`, `HitReceiver`, `TriggerZone3D`를 테스트용 큐브에 붙여 이벤트 흐름을 확인합니다.
3. Stone/Box처럼 떨어지는 오브젝트는 `GravityObject3D`로 낙하만 검증합니다.
4. 카메라/셔터로 멈춰야 하는 오브젝트는 `PausablePhysicsObject`를 추가하고 `SetCameraPaused`, `SetShutterPaused` 호출 연결을 확인합니다.
5. 파괴가 필요한 오브젝트는 `HitReceiver.onMaxHit`에 `BreakableObject3D.BreakObject`를 연결합니다.
6. 길을 여는 오브젝트는 `BreakableObject3D.onBreak`에 `OpenPathOnBreak.OpenPath`를 연결합니다.
7. 마지막에 Stone, Wire, Vine, Crane 같은 개별 오브젝트 전용 스크립트를 만듭니다.

## Inspector 연결 항목

- `DamageDealer.targetLayerMask`: 데미지를 받을 Player/Monster/Breakable 레이어를 지정합니다.
- `DamageDealer.damageOnCollision` / `damageOnTrigger`: Collider가 Trigger인지 아닌지에 맞춰 하나만 켭니다.
- `HitReceiver.maxHitCount`: 몇 번 맞아야 이벤트가 발생하는지 정합니다.
- `HitReceiver.onMaxHit`: 파괴, 길 열기, 연결 오브젝트 실행 같은 후속 이벤트를 연결합니다.
- `TriggerZone3D.targetBehaviour`: `ITriggerableObject`를 구현한 대상 또는 `TriggerObject` 메서드가 있는 MonoBehaviour를 연결합니다.
- `ConnectedObjectLink.connectedBehaviour`: Wire/Switch가 실행할 오브젝트를 연결합니다.
- `GravityObject3D.rb`: 대상 Rigidbody를 연결합니다. 비워두면 같은 GameObject에서 자동으로 찾습니다.
- `PausablePhysicsObject.rb`: 멈출 Rigidbody를 연결합니다.
- `BreakableObject3D.collidersToDisable`: 부서진 뒤 막으면 안 되는 Collider를 넣습니다.
- `OpenPathOnBreak.objectsToDisable` / `objectsToEnable`: 길이 열릴 때 꺼질 벽, 켜질 길 표시 등을 연결합니다.

## 아직 만들지 않은 개별 오브젝트

아래 스크립트는 이번 단계에서 만들지 않았습니다. 공통 기반 검증 후 각각 별도 규칙을 정해 구현합니다.

- `StoneTrap`
- `FallingBox`
- `CircleSpike`
- `WireObject`
- `VineObject`
- `UnstableTile`
- `Crane`
- `CraneSwitch`
- `MovingPlatform`
- `RailMover`

## 주의사항

- 코드에서 Layer/Tag를 자동 변경하지 않습니다. Unity Editor의 Project Settings와 Inspector에서 직접 설정합니다.
- PlayerController, MonsterAI, Camera, World 전환 시스템은 직접 수정하지 않습니다.
- 기존 Player/Monster 데미지 흐름과 호환되도록 `TakeDamage(int)`는 유지했습니다.
- 새 오브젝트는 Rigidbody2D/Collider2D가 아닌 3D 물리 컴포넌트를 사용해야 합니다.
