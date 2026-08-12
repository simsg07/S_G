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
- 닫힌 Idle에서 최초 감지 시 독립 Howling을 1회 재생
- Howling 40% 시점의 포효 프레임에서 Player 스턴을 한 번 적용
- Walk 추적
- Attack / AttackFalse 처리

애니메이션 구성:
- `HumanBox_Idle`: `Human_Box_Wake_Up_01` 단일 프레임, 반복
- `HumanBox_Howling`: `Human_Box_Howl_01`~`05`, 비반복
- `HumanBox_Walk`: `Human_Box_Run_01`~`08`, 반복
- `HumanBox_Attack` / `HumanBox_AttackFalse`: 새 공격 이미지가 없어 기존 Bite 모션 유지
- `HumanBox_Dead`: 제공된 Dead 01~05, 07~08, 비반복

상태 흐름은 `Idle → Howling → Walk → Attack/AttackFalse`이며, 감지를 완전히 잃고 Idle로 돌아간 뒤 재감지하면 다시 Howling할 수 있다. 이미지 교체는 `Tools/Project/Apply New Human Box Animation Set`에서 다시 적용할 수 있다.

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
# Human_Box 감지와 공통 순찰

Human_Box 감지는 `Player` 태그로 플레이어 루트를 찾고, 감지 거리와 3D Line of Sight를 모두 통과할 때 시작됩니다. LOS 장애물 마스크에는 Ground, Wall, TileObstacle, Platform, EnvironmentObstacle만 사용하며 Human_Box 자신의 Collider와 플레이어 계층 Collider는 검사에서 제외됩니다. 감지 결과와 같은 프레임의 캐시된 선택 타깃을 이중으로 요구하던 조건을 제거해 씬 전환 직후에도 정상적으로 Howling으로 진입합니다.

순찰 관련 스크립트는 `Scripts/Enemies/Common/MonsterPatrolPath.cs`, `MonsterPatrolController.cs`이며 Human_Box에 연결되어 있습니다. 컴포넌트가 독립되어 있어 Eyeball_Fly/Boomber에도 붙일 수 있지만, 해당 AI가 순찰 이동 허용을 명시적으로 연동하기 전에는 자동으로 움직이지 않습니다. 모든 프리팹의 Enable Patrol 기본값은 꺼짐입니다.

기획자 사용 방법:

1. 씬의 몬스터에 `Monster Patrol Controller`를 추가하고 Enable Patrol을 켭니다.
2. Inspector에서 `순찰 경로 생성`을 누릅니다.
3. Scene 뷰에서 Point_00, Point_01을 Move Tool로 옮기고 필요한 포인트를 추가합니다.
4. Ping Pong/Loop/Once, 속도·도착 거리·대기시간·복귀 방식을 설정합니다.
5. Edit Mode에서 Ctrl+S로 씬을 저장한 뒤 Play Mode로 확인합니다.

포인트는 버튼을 눌렀을 때만 생성되며 Awake, Start, OnEnable, Reset, OnValidate에서 위치를 재설정하지 않습니다. `순찰 경로 검사`는 설정만 검사하고 위치를 변경하지 않습니다.
