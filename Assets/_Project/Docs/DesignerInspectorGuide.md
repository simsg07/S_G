# Designer Inspector Guide

기획자가 Unity Inspector에서 자주 만져도 되는 값과 건드리면 위험한 값을 정리합니다.

## Player

기획자가 만져도 되는 값:

- `PlatformerPlayer3D.moveSpeed`
- `PlatformerPlayer3D.jumpHeight`
- `PlatformerPlayer3D.gravityScale`
- `PlatformerPlayer3D.fallGravityMultiplier`
- `PlatformerPlayer3D.maxFallSpeed`
- `PlatformerPlayer3D.coyoteTime`
- `PlatformerPlayer3D.jumpBufferTime`
- `PlatformerPlayer3D.maxAirJumps`
- `PlayerDamageReceiver.infiniteHealth`
- `PlayerDamageReceiver.maxHp`
- `PlayerDamageReceiver.hitBlinkDuration`
- `PlayerDamageReceiver.respawnDelay`
- `PlayerInteraction3D.interactRange`
- `PlayerInteraction3D.interactBoxSize`
- `PlayerInteraction3D.interactMask`
- `PlayerAnimationController.speedDampTime`
- `PlayerAnimationController.yVelocityDampTime`

Player에서 건드리면 위험한 값:

- Rigidbody 참조
- Collider 참조
- Animator Controller 참조
- GroundCheck Transform 삭제
- InteractionCheck Transform 삭제
- Visual 자식 오브젝트 삭제
- Player Tag 변경
- Player Layer 변경
- Script 컴포넌트 삭제
- `PlatformerPlayer3D.gameplayPlaneZ`
- `PlatformerPlayer3D.colliderSize`
- `PlayerStunReceiver.movementController`
- `PlayerAnimationController.movement`
- `PlayerAnimationController.body`
- `PlayerAnimationController.animator`

주의:

- `PLAYER_Main.prefab`의 Tag는 반드시 `Player`로 유지하세요.
- `PLAYER_Main.prefab`은 `Player` Layer를 사용합니다. 기존 씬 오브젝트의 Layer 변경은 Inspector에서 수동으로 처리합니다.
- Visual 자식에는 Collider를 넣지 않습니다. Rigidbody와 Collider는 Root에 둡니다.

## Enemy

기획자가 만져도 되는 값:

- Detection Range
- Move Speed
- Attack Range
- HP
- Animator Visual Root
- Invert Facing
- Boomber explosion radius / damage

건드리면 위험한 값:

- `obstacleLayerMask`를 비우는 것
- `movementObstacleLayerMask`를 비우는 것
- `visualRoot` 연결 해제
- Animator Controller 참조 변경

## Map Tile

기획자가 만져도 되는 값:

- `GridSnapper.gridSize`
- `MapPiece.blockLineOfSight`
- `MapPiece.blockMovement`
- `MapPiece.canSwitchWorld`
- `FloorCollision.width`
- `FloorCollision.collisionYOffset`

건드리면 위험한 값:

- Collider `isTrigger`
- Root Scale
- Layer 자동 변경 기대
- `Floor_Collision`을 World 전환 대상으로 켜는 것

## Object

기획자가 만져도 되는 값:

- `StoneTrap.damage`
- `StoneTrap.destroyTime`
- `StoneTrap.canBeControlledByShutter`
- `StoneTrigger.targetStone`

건드리면 위험한 값:

- Rigidbody Constraints
- Animator Controller 참조
- Collider 참조
- Trigger Collider의 `isTrigger`

## World Switch

기획자가 만져도 되는 값:

- `WorldSwitchable.canSwitchByCamera`
- `MapPiece.canSwitchWorld`
- Visual A/B 여부

건드리면 위험한 값:

- World Manager 참조
- 카메라 전환 시스템 참조
- Floor 충돌 오브젝트를 시각 전환과 엮는 것

## LayerMask

자주 확인할 값:

- Enemy `obstacleLayerMask`
- Enemy `movementObstacleLayerMask`
- Ground 몬스터 `groundLayerMask`
- Player/Object 상호작용 LayerMask

건드리면 위험한 값:

- LayerMask를 0으로 비우면 감지/충돌/상호작용이 실패할 수 있습니다.
- Layer는 코드에서 자동 변경하지 않습니다. Inspector에서 수동으로 맞춥니다.
