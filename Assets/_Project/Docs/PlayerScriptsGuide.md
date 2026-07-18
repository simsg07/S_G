# Player Scripts Guide

## PLAYER_Main.prefab

- 위치: `Assets/_Project/Prefabs/Player/PLAYER_Main.prefab`
- 역할: 게임에서 재사용할 기본 Player 프리팹입니다.
- 현재 기준: 프로젝트에 `PlayerController.cs` 파일은 없고, 실제 이동/점프 컨트롤러는 `PlatformerPlayer3D.cs`입니다. 파일명/클래스명을 무리하게 바꾸면 기존 씬과 코드 참조가 깨질 수 있어 이름은 유지했습니다.

필수 설정:

- Tag: `Player`
- Layer: `Player`
- Layer 변경 방식: 코드는 Layer를 자동 변경하지 않습니다. 씬 오브젝트는 Inspector에서 수동 지정하세요.
- Rigidbody: Root에 둡니다.
- Collider: Root에 둡니다.
- Visual: 자식 오브젝트로 분리하고 Collider를 넣지 않습니다.
- GroundCheck: 자식 오브젝트로 명확히 둡니다.
- InteractionCheck: 상호작용 기준점 확인용 자식 오브젝트입니다.

현재 프리팹 Root 컴포넌트:

- `PlatformerPlayer3D`
- `PlayerDamageReceiver`
- `PlayerStunReceiver`
- `PlayerInteraction3D`
- `PlayerAnimationController`
- `PlayerHealth3D`
- `Rigidbody`
- `BoxCollider`

공격/대쉬/넉백/무적 컴포넌트는 기본 Player 프리팹에 넣지 않았습니다.

## PlatformerPlayer3D.cs

PlayerController 역할을 담당합니다.

역할:

- 이동
- 점프
- 기본 조작 입력
- Ground 체크
- 2.5D Z축 고정
- 로봇 다리 점프

기획자가 만져도 되는 값:

- `moveSpeed`
- `jumpHeight`
- `gravityScale`
- `fallGravityMultiplier`
- `maxFallSpeed`
- `coyoteTime`
- `jumpBufferTime`
- `maxAirJumps`
- `useRobotLegJump`
- `maxLegExtension`
- `legExtendSpeed`
- `legRetractSpeed`

건드리면 위험한 값:

- `colliderSize`
- `gameplayPlaneZ`
- Rigidbody / Collider 자동 구성 로직
- 입력 처리 방식
- Ground 체크 로직

## PlayerDamageReceiver.cs

역할:

- 데미지 받기
- HP 처리
- 피격 깜빡임
- 사망/리스폰
- 리스폰 시 Rigidbody velocity 초기화

기획자가 만져도 되는 값:

- `infiniteHealth`
- `maxHp`
- `hitBlinkDuration`
- `hitBlinkInterval`
- `deathBlinkDuration`
- `deathBlinkInterval`
- `respawnDelay`
- `respawnPoint`

건드리면 위험한 값:

- `currentHp`: Play Mode 확인용에 가깝습니다.
- Renderer 참조/깜빡임 코루틴
- Respawn 로직
- Rigidbody 초기화 로직

## PlayerStunReceiver.cs

역할:

- Human_Box Howling 같은 스턴 처리
- 일정 시간 Player 이동 불가 처리
- Rigidbody velocity 정리

기획자가 만져도 되는 값:

- `debugMode`

건드리면 위험한 값:

- `movementController`: 비워두면 자동으로 `PlatformerPlayer3D`를 찾습니다.
- Stun 코루틴
- 이동 컨트롤러 잠금/속도 배율 복구 로직

## PlayerInteraction3D.cs

역할:

- 터미널, 버튼, 문, 오브젝트 상호작용
- `F` 또는 지정된 키로 주변 `IInteractable3D` 감지

기획자가 만져도 되는 값:

- `interactKey`
- `interactRange`
- `interactBoxSize`
- `interactMask`

건드리면 위험한 값:

- `interactMask`를 0으로 비우면 상호작용 대상을 찾지 못할 수 있습니다.
- 감지 박스 계산 로직

## PlayerAnimationController.cs

PlayerAnimatorBridge 역할을 담당합니다.

역할:

- Player 상태를 Animator 파라미터로 전달
- `Speed`, `YVelocity`, `IsGrounded`, `IsDead` 파라미터가 있을 때만 안전하게 갱신
- Root Motion이 Player 이동을 건드리지 않게 제어

기획자가 만져도 되는 값:

- `speedDampTime`
- `yVelocityDampTime`

건드리면 위험한 값:

- `animator`
- `movement`
- `body`
- `disableRootMotion`
- Animator Controller 교체

## PlayerHealth3D.cs

역할:

- 기존 `DamageBlock3D.ApplyDamageTo(PlayerHealth3D health)` 참조 보호용 호환 컴포넌트입니다.

기획자가 만져도 되는 값:

- 없음

건드리면 위험한 값:

- 삭제 금지. 기존 코드 참조가 남아 있습니다.

## 기본 프리팹에서 제외한 기능

아래 기능은 현재 `PLAYER_Main.prefab`에는 넣지 않았습니다.

- `PlayerAttack3D`
- `PlayerAttackAnimation3D`
- `Assets/_Project/Resources/PlayerAttack/attack_01~04.png`

단, 위 파일들은 `GameScene`, `AbilityTestScene`의 기존 Player 오브젝트에서 아직 참조 중입니다. Missing Script 방지를 위해 이번 작업에서는 삭제하거나 이동하지 않았고, `Assets/_Project/_Deprecated/DeprecatedList.md`에 보류 사유를 기록했습니다.

## 씬 사용 기준

현재 주요 씬의 기존 Player는 아직 프리팹 인스턴스가 아닙니다.

- `Assets/_Project/Scenes/Stages/GameScene.unity`: Player Tag는 `Player`, Layer는 기존 값 유지, 프리팹 인스턴스 아님
- `Assets/_Project/Scenes/Test/AbilityTestScene.unity`: Player Tag를 `Player`로 수정, Layer는 기존 값 유지, 프리팹 인스턴스 아님
- `Stage_01`, `Stage_02`: 현재 프로젝트에 해당 씬 파일 없음

씬을 대량 수정하지 않기 위해 기존 씬 Player를 자동 교체하지 않았습니다. 새 씬 또는 정리 작업 시에는 `PLAYER_Main.prefab`을 배치하고 Tag를 `Player`로 유지하세요.
