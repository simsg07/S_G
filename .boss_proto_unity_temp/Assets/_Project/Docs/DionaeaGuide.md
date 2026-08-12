# Dionaea (M_OBJ_004)

Dionaea는 3D 물리를 사용하는 고정형 파리지옥 몬스터다. 위쪽 감지 박스 안의 Player만 찾으며, Dionaea와 Player 사이의 Wall, Ground, TileObstacle, Platform, EnvironmentObstacle에 시야가 막히면 공격하지 않는다.

## 동작

- 빛을 받지 않고, 전방 Player가 LOS 및 공격 거리 조건을 만족할 때 공격한다.
- 공격 기본 데미지는 2이며 `DionaeaAttack`의 `Physics.OverlapBox`로 `IDamageable`에 전달한다.
- 빛을 받는 즉시 공격을 중단한다.
- 기본 1초간 연속으로 빛을 받으면 `Retracted` 상태가 되어 공격하지 않는다.
- 빛이 사라진 뒤 `recoverFromLightDelay`가 지나면 `Idle`로 복귀한다.
- Rigidbody는 kinematic/Frozen 상태이며 이동하지 않는다. Animator는 선택 사항이다.

## Player 이동이 막힐 때

- Detection과 Attack 범위는 Collider가 아니라 `Physics.OverlapBox` Gizmo다. 범위용 BoxCollider를 추가하지 않는다.
- `BodyCollider`는 작은 Trigger이며 Player를 물리적으로 밀거나 막지 않는다.
- Dionaea 루트에는 일반 Collider가 없어야 한다. 프리팹 Override로 큰 Collider가 추가되었는지 확인한다.
- Dionaea 스크립트는 PlayerController, 입력, Player Rigidbody, 이동 속도와 `Time.timeScale`을 변경하지 않는다. 공격은 `IDamageable`에 데미지 2만 전달한다.

## Animator 연결

- 원본 PNG 17장은 `C:/Users/tprud/Downloads`에서 `Assets/_Project/Art/Enemies/Dionaea`로 복사한다. Unity는 Downloads 파일을 직접 참조하지 않는다.
- PNG Import 기준은 Sprite/Single, PPU 384, Bottom Center Pivot, Bilinear, Alpha Transparency ON, Mipmap OFF다.
- Animator와 SpriteRenderer는 `Visual` 자식에만 둔다. 루트 Rigidbody/Collider Transform은 애니메이션하지 않는다.
- `Apply Root Motion`은 반드시 꺼 둔다. Downloads 원본을 Sprite로 Import한 `Dionaea_Idle`, `Dionaea_Attack`, `Dionaea_Retracted` 클립과 `Dionaea.controller`를 사용한다.
- `Dionaea.controller`의 기본 상태는 반복 재생되는 `Idle`이며 파라미터는 `Attack`(Trigger), `IsAttacking`, `IsRetracted`, `IsRecovering`(Bool)다.
- `DionaeaAnimatorBridge`는 Controller나 파라미터가 없으면 아무 작업도 하지 않으므로 Animator 없이도 안전하다.
- 전이는 `Idle -> Attack`에만 `Attack` Trigger를 사용한다. `Idle/Attack -> Retracting`은 `IsRetracted=true`, `Retracting -> Retracted`는 Clip Exit Time, `Retracted -> Recovering`은 `IsRecovering=true`, `Recovering -> Idle`은 Clip Exit Time을 사용한다. Any State 전이는 없으며 Dionaea에는 Dead 상태와 `IsDead` 파라미터가 없다.
- `Prepare Dionaea Animation Assets`는 Dionaea 전용 Controller가 없을 때만 생성하고 Visual Animator에 연결한다. 다른 몬스터나 공통 Controller는 수정하지 않는다. 파라미터 이름을 바꾸면 `DionaeaAnimatorBridge` Inspector의 이름 필드도 맞춘다.
- `Dionaea_Retracted.anim`은 `Dionaea_Retracted_01 → 02 → 03 → 04 → 05 → 06 → 07 → 08`을 8 FPS, 1초로 재생하고 08번 자세를 유지한다. 빛이 사라지면 `Dionaea_Recover.anim`이 `08 → 07 → 06 → 05 → 04 → 03 → 02 → 01` 순서로 1초 재생된 뒤 Idle로 돌아간다. 기존 `Dionaea_Dead_01.png` 역시 의미상 Retracted/수축 이미지이며 Dead 상태로 사용하지 않는다.

## 피격과 사망 규칙

- Dionaea는 죽는 몬스터가 아니므로 `MonsterHealth`와 `IDamageable`을 갖지 않는다. Stone 충돌도 명시적으로 무시한다.
- 빛을 받으면 사망하지 않고 `IsRetracted=true`가 되어 수축하며, 빛이 사라진 뒤 복구한다.
- CircleSpike는 별도 즉사 오브젝트로 `PlayerDamageReceiver.KillAndRespawn()`을 사용한다.
- 일반 몬스터 공격은 `PlayerDamageReceiver.TakeDamage()`를 사용한다. Player 프리팹의 `infiniteHealth=true` 설정에서는 HP가 줄지 않고 피격 깜빡임만 보이는 것이 정상이다. 이를 끈 경우 HP가 0 이하가 되었을 때만 일반 사망/리스폰을 수행한다.

## Dionaea 수축 규칙

- Dionaea는 죽는 몬스터가 아니다. Inspector에서도 `canDie=false`, `isIndestructible=true`를 유지한다.
- Dionaea는 빛을 받으면 수축한다.
- 파일명이 `Dionaea_Dead_01.png`여도 실제 용도는 Retracted/수축 이미지다. 원본 파일은 삭제하거나 이름을 바꾸지 않는다.
- Animator에서는 Dead가 아니라 `Retracted` 상태로 사용한다.
- `IsDead`가 아니라 `IsRetracted`를 사용한다.
- Stone, CircleSpike, 일반 데미지로 Dionaea는 죽지 않는다.
- Retracted 상태에서는 공격하지 않는다.
- 빛이 사라지고 회복 시간이 지나면 Idle로 돌아간다.

## 수축 시간 조정

- Dionaea는 빛을 받으면 죽는 것이 아니라 `Retracting` 상태로 들어가 수축한다.
- 수축 동작 시간은 `DionaeaAI.retractAnimationDuration`으로 조정하며 기본값은 1초다.
- `waitRetractAnimationBeforeFullRetracted=true`일 때 1초가 끝나기 전에는 완전한 `Retracted` 상태로 판정하지 않는다.
- 수축 중과 완전 수축 후에는 모두 공격할 수 없다.
- 새 8프레임 Clip이 있으므로 Visual scale 보간은 사용하지 않는다. 단일 프레임만 남는 경우에는 1초 전환 대기 또는 Visual 자식 scale 보간을 대체 수단으로 사용한다.
- `Dionaea_Dead_01.png`는 파일명과 무관하게 Retracted 이미지로만 취급하며 `IsDead`는 사용하지 않는다.

Animator가 재생되지 않으면 `DionaeaAnimatorBridge > Validate Animator Setup`을 실행해 Visual, 기본 Sprite, Animator, Controller, Root Motion, 파라미터, 기본 Idle 상태 및 Sprite 키프레임을 확인한다.

## Inspector 설정

- `DionaeaAI.playerLayerMask`: 현재 Player 프리팹의 레이어를 포함한다. 레이어나 태그 자체는 Dionaea 도구가 변경하지 않는다.
- `DionaeaAI.obstacleLayerMask`: Wall, Ground, TileObstacle, Platform, EnvironmentObstacle
- `DionaeaAI.detectionBoxOffset / detectionBoxSize`: 전방 감지 영역
- `DionaeaAI.attackCooldown / attackWindup / attackDamage`: 공격 설정
- `DionaeaAI.requiredLightExposureTime / recoverFromLightDelay`: 수축 및 복구 시간
- `DionaeaAttack.attackBoxOffset / attackBoxSize / playerLayerMask`: 실제 피해 영역
- `Head_AttackOrigin`: 머리 높이의 중심축 기준점 `(0, 0.8, 0)`. 기본 오프셋 `(0, 0.8, 0)`, 크기 `(1.2, 1, 1)`로 공격 범위는 머리 위쪽에 놓인다. AI의 공격 거리 판정도 이 박스와 동일하다.
- `DionaeaLightReceiver.lightLayerMask`: 프로젝트의 광원 감지용 레이어. Collider가 없는 활성 Point/Spot Light도 감지하므로 런타임 `Camera Toggle Light`가 범위 안에서 켜지면 별도 연결 없이 반응한다. Directional Light는 게임플레이 광원에서 제외한다.

레이어나 태그는 스크립트가 생성 또는 변경하지 않는다. 프로젝트에 `Light` 태그가 없다면 `useTagFallback`을 끄고 광원 LayerMask 또는 `MonsterCore.lightTarget`을 사용한다.

Player 감지가 안 되면 `DionaeaAI > Debug Detection Conditions`를 실행한다. 로그의 `Hits`, `PlayerResolved`, `InFront`, `LOS`, `Lit`, `Retracted`, `CanAttack`을 순서대로 확인하면 Detection Box/LayerMask, Player Tag·DamageReceiver, 전방, 벽 차단, 빛 잠금을 분리해서 진단할 수 있다.

## Gizmo와 테스트

1. `Dionaea.prefab`을 테스트 씬에 배치한다.
2. Player/Obstacle/Light LayerMask 연결을 확인한다.
3. 위쪽에 Player를 두면 녹색 Detection Gizmo 안에서 공격하는지 확인한다.
4. 벽이나 타일을 사이에 두면 감지하지 않는지 확인한다.
5. 주황색 Attack Gizmo 안의 Player가 2 데미지를 받는지 확인한다.
6. 광원을 1초 이상 비춰 수축하는지 확인한다.
7. 수축 중 공격하지 않고, 빛 제거 후 복구하는지 확인한다.
8. Animator를 제거한 상태에서도 Console Error가 없는지 확인한다.
