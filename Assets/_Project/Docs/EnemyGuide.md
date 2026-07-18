# Enemy Guide

몬스터 관련 파일은 `Prefabs/Enemies`, `Animations/Enemies`, `Art/Enemies`, `Scripts/Enemies`에서 찾습니다. 스크립트 class 이름은 변경하지 않았고, 이동할 때 `.meta`를 함께 유지했습니다.

## 공통 컴포넌트

### MonsterDetection
역할:
- Player 감지
- Light 감지
- Line of Sight 검사
- 벽/타일/바닥 뒤 대상 감지 차단

기획자가 만지는 값:
- `canDetectPlayer`
- `canDetectLight`
- `playerDetectRange`
- `lightDetectRange`
- `requireLineOfSight`
- `obstacleLayerMask`

### MonsterMovement
역할:
- Ground/Flying 이동 처리
- 벽/타일 통과 방지
- 타겟을 잃었을 때 홈 위치 복귀

기획자가 만지는 값:
- `movementType`
- `moveSpeed`
- `returnSpeed`
- `groundLayerMask`
- `movementObstacleLayerMask`

### MonsterFacing
역할:
- 몬스터가 타겟 방향을 바라보게 함
- Root Scale 대신 Visual만 뒤집음

기획자가 만지는 값:
- `visualRoot`
- `visualFacesRightByDefault`
- `invertFacing`

### MonsterHealth
역할:
- 몬스터 체력
- 사망 시 Collider 비활성화 또는 제거 처리

기획자가 만지는 값:
- `maxHp`
- `destroyOnDeath`
- `destroyDelay`

## EyeballFly

프리팹 위치:
- `Assets/_Project/Prefabs/Enemies/EyeballFly.prefab`

Art 위치:
- `Assets/_Project/Art/Enemies/EyeballFly/`

Animation 위치:
- `Assets/_Project/Animations/Enemies/EyeballFly/`

Script 위치:
- `Assets/_Project/Scripts/Enemies/EyeballFly/`

역할:
- 비행 몬스터
- Player 또는 Light 감지
- Player 우선 추적
- 벽/타일/바닥 뒤 대상은 감지하지 않음

기획자가 만지는 값:
- `playerDetectRange`
- `lightDetectRange`
- `attackRange`
- `moveSpeed`
- `obstacleLayerMask`
- `invertFacing`

## Human_Box

프리팹 위치:
- `Assets/_Project/Prefabs/Enemies/Human_Box.prefab`

Art 위치:
- `Assets/_Project/Art/Enemies/Human_Box/`

Animation 위치:
- `Assets/_Project/Animations/Enemies/Human_Box/`

Script 위치:
- `Assets/_Project/Scripts/Enemies/Human_Box/`

역할:
- 지상 몬스터
- Player 감지
- Howling으로 Player 스턴
- Walk 추적
- Attack / AttackFalse 처리

기획자가 만지는 값:
- `playerDetectRange`
- `chaseRange`
- `attackRange`
- `howlStunDuration`
- `moveSpeed`
- `obstacleLayerMask`

## Boomber

프리팹 위치:
- `Assets/_Project/Prefabs/Enemies/Boomber.prefab`

Art 위치:
- `Assets/_Project/Art/Enemies/Boomber/`

Animation 위치:
- `Assets/_Project/Animations/Enemies/Boomber/`

Script 위치:
- `Assets/_Project/Scripts/Enemies/Boomber/`

역할:
- 자폭형 지상 몬스터
- 활성화 후 Player 감지
- 감지한 순간 방향 고정 돌진
- 충돌 또는 공격 상태에서 폭발

기획자가 만지는 값:
- `startArmed`
- `requireActivationBeforeDetection`
- `playerDetectRange`
- `baseRunSpeed`
- `speedIncreasePerSecond`
- `maxRunSpeed`
- `fuseDuration`
- `explosionRadius`
- `runObstacleLayerMask`

## MonsterAni1

- 현재 사용 여부: 사용 중
- 사용하는 대상: `Assets/_Project/Scenes/Stages/GameScene.unity`의 `MonsterAni1` 오브젝트
- 사용 스크립트: `MonsterAni1Object3D.cs`
- 리소스 경로: `Assets/_Project/Resources/MonsterAni1/`
- 처리: 삭제 금지, rename 보류

## LayerMask 확인

- `obstacleLayerMask`, `movementObstacleLayerMask`는 주요 Enemy 프리팹에 설정되어 있습니다.
- 일부 `groundLayerMask`, `breakableLayerMask`, `damageLayerMask`는 0입니다.
- 이번 정리는 기존 동작 변경을 피하기 위해 LayerMask 값을 자동 변경하지 않았습니다.
- 지상형 몬스터가 바닥을 못 찾으면 Inspector에서 `Ground` / `Platform`을 수동 지정합니다.
