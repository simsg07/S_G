# Rope Object Guide

이 문서는 WireObject와 VineObject를 배치하고 테스트하는 방법을 정리합니다. 두 오브젝트는 3D Collider 기반으로 동작하며 Rigidbody2D, Collider2D, PlayerController, MonsterAI를 직접 참조하지 않습니다.

## WireObject 역할

WireObject는 연결된 오브젝트를 잡고 있는 전선입니다. 기본 설정은 `maxHitCount = 2`이며, 첫 번째 타격에서는 손상 상태만 표시하고 두 번째 타격에서 절단됩니다.

절단되면 다음 순서로 동작합니다.

1. `IsCut` 상태가 켜집니다.
2. Animator에 `IsCut` bool, `Cut` trigger, `HitCount` int를 안전하게 전달합니다.
3. `ConnectedObjectLink.ActivateConnectedObject()`를 호출합니다.
4. `BreakableObject3D.BreakObject()`를 호출합니다.
5. Collider를 비활성화하고 `destroyDelay` 뒤 GameObject를 비활성화합니다.

## VineObject 역할

VineObject는 문, 구조물, 이동 경로를 막고 있는 덩굴입니다. 기본 설정은 `maxHitCount = 1`이며, 한 번 맞으면 바로 절단됩니다.

절단되면 다음 순서로 동작합니다.

1. Animator에 `IsCut` bool, `Cut` trigger, `HitCount` int를 안전하게 전달합니다.
2. `ConnectedObjectLink.ActivateConnectedObject()`를 호출합니다.
3. `OpenPathOnBreak.OpenPath()`를 호출해 길을 엽니다.
4. `BreakableObject3D.BreakObject()`를 호출합니다.
5. Collider를 비활성화하고 `destroyDelay` 뒤 GameObject를 비활성화합니다.

## Wire와 Vine 차이

| 항목 | WireObject | VineObject |
| --- | --- | --- |
| 기본 ID | `R_OBJ_001` | `R_OBJ_002` |
| 기본 타격 횟수 | 2회 | 1회 |
| 첫 타격 반응 | 손상 상태 표시 | 즉시 절단 |
| 주 용도 | CircleSpike, 낙하 오브젝트, 장치 연결 | 문, 길막 구조물, 이동 경로 개방 |
| 길 열기 | 연결 오브젝트 실행 중심 | `OpenPathOnBreak` 직접 실행 가능 |

## 필요한 공통 컴포넌트

Wire 권장 구조:

- `WireObject`
- `HitReceiver`
- `ConnectedObjectLink`
- `BreakableObject3D`
- `BoxCollider`
- 자식 `Visual`
- 자식 `Visual` 아래 `MeshRenderer` 또는 `SpriteRenderer`
- 자식 `Visual` 아래 `Animator`

Vine 권장 구조:

- `VineObject`
- `HitReceiver`
- `ConnectedObjectLink`
- `BreakableObject3D`
- `OpenPathOnBreak`
- `BoxCollider`
- 자식 `Visual`
- 자식 `Visual` 아래 `MeshRenderer` 또는 `SpriteRenderer`
- 자식 `Visual` 아래 `Animator`

기본 프리팹은 아래 경로에 준비되어 있습니다.

- `Assets/_Project/Prefabs/Objects/Rope/Wire.prefab`
- `Assets/_Project/Prefabs/Objects/Rope/Vine.prefab`

`WireObject`와 `VineObject`를 GameObject에 추가하면 필요한 공통 컴포넌트는 가능한 범위에서 자동으로 찾거나 추가합니다. 그래도 Prefab 저장 전에는 Inspector에서 참조가 정상으로 잡혔는지 확인하는 것이 좋습니다.

## 연결 오브젝트 설정

Wire에서 연결된 오브젝트를 실행하려면 `ConnectedObjectLink.connectedBehaviour`에 실행할 MonoBehaviour를 연결합니다. 연결 대상이 `ITriggerableObject`를 구현하면 `TriggerObject()`가 호출되고, 그렇지 않으면 `SendMessage("TriggerObject")` 방식으로 호출됩니다.

Vine에서 길을 열려면 `OpenPathOnBreak.objectsToDisable`에 막고 있는 오브젝트를 넣고, `objectsToEnable`에는 절단 후 켜질 길 표시나 이동 가능 오브젝트를 넣습니다. Collider만 끄고 싶다면 `collidersToDisable`에 넣습니다.

## HitReceiver.maxHitCount 설정

`WireObject.maxHitCount`는 최소 2로 보정됩니다. `VineObject.maxHitCount`는 최소 1로 보정됩니다. 각 오브젝트는 Awake, OnEnable, OnValidate 시점에 `HitReceiver.ConfigureHitRules()`를 호출해 HitReceiver 설정을 맞춥니다.

직접 HitReceiver 값을 수정해도 되지만, 최종 기준 값은 WireObject/VineObject 쪽 Inspector 값을 기준으로 맞춰집니다.

## ContextMenu 테스트

Inspector 컴포넌트 오른쪽 메뉴에서 다음 테스트를 실행할 수 있습니다.

WireObject:

- `Test Hit`: 한 번 타격합니다. 두 번 실행하면 Wire가 절단됩니다.
- `Test Cut Wire`: 즉시 절단합니다.
- `Reset Wire`: 타격 수와 절단 상태를 초기화합니다.

VineObject:

- `Test Hit`: 한 번 타격합니다. 기본 설정에서는 바로 절단됩니다.
- `Test Cut Vine`: 즉시 절단합니다.
- `Reset Vine`: 타격 수와 절단 상태를 초기화합니다.

## 몬스터 공격과 연결하는 방법

몬스터 공격 판정이 `DamageDealer`를 사용한다면, 공격 Collider가 Wire/Vine의 Collider와 충돌하거나 Trigger 진입할 때 `IDamageable` 또는 `HitReceiver` 경로로 타격이 전달됩니다.

직접 호출이 필요한 공격 스크립트라면 다음 중 하나를 사용합니다.

- `HitReceiver.RegisterHit(DamageInfo damageInfo)`
- `WireObject.RegisterHit(DamageInfo damageInfo)`
- `VineObject.RegisterHit(DamageInfo damageInfo)`
- `IDamageable.TakeDamage(DamageInfo damageInfo)`

공격 주체 제한은 특정 몬스터 타입을 직접 참조하지 말고 LayerMask 또는 `DamageInfo.damageType` 기준으로 분기하는 방식이 좋습니다.

## 주의사항

- Rigidbody2D, Collider2D는 사용하지 않습니다.
- PlayerController와 MonsterAI 타입을 직접 참조하지 않습니다.
- Animator 파라미터가 없어도 에러가 나지 않도록 존재 여부를 확인한 뒤 호출합니다.
- 코드에서 Layer/Tag를 자동 변경하지 않습니다. 필요한 레이어와 태그는 Unity Editor에서 설정합니다.
- 현재 Prefab의 Visual은 임시 SpriteRenderer와 Animator 자리만 잡아둔 상태입니다. 전용 Wire/Vine 스프라이트와 Animator Controller가 생기면 `Visual` 자식에 연결하면 됩니다.

## 다음 추천 작업

Wire/Vine 이후에는 `CircleSpike` 또는 `FallingBox`를 만드는 순서가 좋습니다. Wire가 연결 오브젝트를 실행하는 흐름을 이미 갖췄기 때문에, 다음 단계에서 Wire 절단 후 CircleSpike 낙하 또는 Box 낙하를 연결해 실제 퍼즐 흐름을 만들 수 있습니다.
