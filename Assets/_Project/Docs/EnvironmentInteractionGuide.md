# Environment Interaction Guide

## Stone (G_OBJ_001)

1. `Prefabs/Objects/Stone/Stone.prefab`을 천장 위치에 배치한다.
2. `Prefabs/Objects/Stone/StoneTrigger.prefab`을 플레이어가 지나갈 영역에 배치한다.
3. `StoneTrigger.targetStone`에 씬의 Stone 인스턴스를 연결한다.
4. Stone의 `damageLayerMask`에는 Player와 Enemy/Monster 레이어를 지정한다.
5. Stone의 `groundLayerMask`에는 Ground 레이어를 지정한다.

레이어는 코드가 자동 변경하지 않는다. 프로젝트에 해당 레이어가 없다면 먼저 Unity의 Tags and Layers에서 만든 뒤 Inspector에서 직접 지정한다.

## 기본 동작

- 시작 상태는 `Idle`이며 Rigidbody의 Gravity가 꺼지고 Kinematic이 켜진다.
- `TriggerDrop()` 호출 시 `Falling`으로 전환되어 아래로 떨어진다.
- Player 또는 `MonsterHealth`가 있는 몬스터와 충돌하면 `IDamageable.TakeDamage(1)`을 호출한다.
- `canDamageBreakables` 기본값은 false이므로 Breakable/Cracked 오브젝트에는 데미지를 주지 않는다.
- Ground, `FloorCollision`, 또는 `MapPiece.IsGround`와 충돌하면 Stone 자신만 부서지고 기본 0.5초 뒤 제거된다.

## Shutter 연결

Shutter 이벤트에서 다음 public 메서드를 호출한다.

- 닫힘: `PauseByShutter()` 또는 `SetShutterPaused(true)`
- 열림: `ResumeByShutter()` 또는 `SetShutterPaused(false)`

## Animator

Controller: `Animations/Objects/Stone/Stone.controller`

- `IsFalling` Bool
- `IsBroken` Bool
- `Break` Trigger
- `State` Int

Animator의 Apply Root Motion은 꺼져 있으며 실제 낙하는 `StoneTrap`과 Rigidbody가 담당한다.


