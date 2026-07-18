# Rope Connection Test Guide

이 문서는 EyeballFly 공격으로 Wire/Vine을 피격시키고 연결 오브젝트가 실행되는지 확인하는 절차입니다.

## 수정된 공격 흐름

EyeballFly 공격은 이제 `StartAttack`에서 한 번만 Attack trigger를 호출합니다. `attackDuration` 동안만 `IsAttacking=true`가 유지되고, 시간이 지나면 `EndAttack`에서 `IsAttacking=false`로 내려갑니다.

Attack 애니메이션 클립은 `Loop Time = false`이고, Animator Controller는 Attack 상태에서 `IsAttacking=false`가 되면 Idle 또는 Move로 빠져나가도록 구성되어 있습니다. 따라서 7번째 프레임 근처에서 멈추는 문제는 `IsAttacking`이 내려가지 않던 코드 흐름이 원인이었습니다.

## EyeballFly Inspector 설정

EyeballFly 프리팹의 `EyeballFlyAI`에서 아래 값을 확인합니다.

- `Attack Duration`: 기본 `0.5`
- `Attack Interval`: 기본 `1`
- `Attack Objects`: 켜기
- `Can Attack Hit Receivers`: 켜기
- `Object Attack Range`: 기본 `0.5`
- `Object Attack Layer Mask`: Wire/Vine이 있는 레이어 포함
- `Debug Attack Hit`: 켜기

처음 테스트할 때는 `Object Attack Layer Mask`를 Everything에 가깝게 열어두고, 동작을 확인한 뒤 기획 레이어에 맞게 좁히는 방식이 편합니다.

## Wire 테스트

1. 씬에 `Assets/_Project/Prefabs/Enemies/EyeballFly.prefab`을 배치합니다.
2. 씬에 `Assets/_Project/Prefabs/Objects/Rope/Wire.prefab`을 배치합니다.
3. Wire의 `HitReceiver.maxHitCount`와 `WireObject.maxHitCount`가 `2`인지 확인합니다.
4. Wire의 Collider가 켜져 있는지 확인합니다.
5. Wire의 Layer가 EyeballFly `Object Attack Layer Mask`에 포함되는지 확인합니다.
6. EyeballFly와 Wire를 `Object Attack Range` 안에 배치합니다.
7. Play 후 EyeballFly가 공격하면 Wire hit count가 `1 / 2`가 됩니다.
8. 두 번째 공격 후 `CutWire`가 실행되고 `ConnectedObjectLink`가 호출됩니다.

## Vine 테스트

1. 씬에 `Assets/_Project/Prefabs/Objects/Rope/Vine.prefab`을 배치합니다.
2. Vine의 `HitReceiver.maxHitCount`와 `VineObject.maxHitCount`가 `1`인지 확인합니다.
3. Vine의 Layer가 EyeballFly `Object Attack Layer Mask`에 포함되는지 확인합니다.
4. EyeballFly와 Vine를 `Object Attack Range` 안에 배치합니다.
5. Play 후 EyeballFly가 한 번 공격하면 `CutVine`이 실행됩니다.
6. `OpenPathOnBreak`에 대상이 연결되어 있다면 길 열기 동작도 함께 실행됩니다.

## 연결 오브젝트 테스트

`TestTriggerableObject`를 빈 GameObject에 붙이고, Wire 또는 Vine의 `ConnectedObjectLink.connectedBehaviour`에 연결합니다.

정상 동작 시 Console에 아래 흐름이 보입니다.

- `[ConnectedObjectLink] ActivateConnectedObject called.`
- `[TestTriggerableObject] TriggerObject called. Count=1`

`TestTriggerableObject.objectsToEnable` 또는 `objectsToDisable`에 임시 큐브를 연결하면 Wire/Vine 절단 후 켜짐/꺼짐도 바로 확인할 수 있습니다.

## 정상 Console 로그 예시

Wire 첫 번째 공격:

- `[EyeballFlyAttack] Attack started.`
- `[EyeballFlyAttack] HitReceiver found: Wire`
- `[HitReceiver] Wire received hit from EyeballFly. Count: 1 / 2`
- `[WireObject] First hit received.`
- `[EyeballFlyAttack] Attack ended.`

Wire 두 번째 공격:

- `[HitReceiver] Wire received hit from EyeballFly. Count: 2 / 2`
- `[HitReceiver] Wire reached max hit count. Invoking onMaxHit.`
- `[WireObject] CutWire executed.`
- `[ConnectedObjectLink] ActivateConnectedObject called.`

Vine 공격:

- `[HitReceiver] Vine received hit from EyeballFly. Count: 1 / 1`
- `[VineObject] CutVine executed.`
- `[ConnectedObjectLink] ActivateConnectedObject called.`

## 남은 확인 사항

- Wire/Vine 전용 레이어를 만들었다면 `Object Attack Layer Mask`에 반드시 포함해야 합니다.
- 공격 애니메이션이 아직 멈춘다면 Animator의 Attack 상태 전환 조건에서 `IsAttacking=false` 조건이 유지되어 있는지 확인합니다.
- Wire/Vine이 너무 멀면 피격되지 않습니다. Scene view에서 주황색 `Object Attack Range` Gizmo를 확인합니다.
