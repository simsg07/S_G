# 중간발표용 기획자 Unity 설정 및 테스트 가이드

> 작성 기준: 2026-07-23 현재 프로젝트의 스크립트, 프리팩, MonsterData, Animator Controller, 씬, Build Settings를 직접 대조했다. 기존 `Docs` 문서 중 예전 파일명이나 삭제된 기능을 설명하는 부분은 실제 실행 코드를 우선했다.

## 1. 가장 먼저 알아야 할 내용

- 씬 열기: Project 창에서 `Assets/_Project/Scenes` 아래 `.unity` 파일을 더블 클릭한다. 저장하지 않은 현재 씬이 있으면 먼저 저장 물음에 대답한다.
- Edit Mode: 재생 버튼이 꺼진 설정 모드다. 영구적인 배치·Inspector 변경은 여기서 한다.
- Play Mode: 재생 버튼이 파란색인 테스트 모드다. 이때 바꾼 Position과 Inspector 값은 종료하면 돌아갈 수 있다.
- 씬 저장: Edit Mode에서 `Ctrl+S`. Hierarchy의 씬 이름 옆 `*`가 없는지 확인한다.
- Prefab 원본: Project 창의 파란 큐브 파일. 원본을 바꾸면 여러 씬의 같은 Prefab에 영향을 준다.
- Prefab Instance: 씬에 배치된 개별 사본. Inspector의 굵은 글씨는 원본과 다른 Override다.
- `Apply`: 현재 Instance의 Override를 Prefab 원본으로 올린다. **전체 씬에 같은 값을 적용할 때만** 누른다.
- `Revert`: Instance의 수동 변경을 원본값으로 되돌린다. Entrance, Exit, 순찰 포인트 위치에서 함부로 누르지 않는다.
- Console: `Window > General > Console`. 빨간 Error가 있으면 발표 전 반드시 원인을 확인한다. Clear는 기록만 지우며 문제를 고치지는 않는다.

## 2. 발표 전 필수 확인 목록

- [ ] `MainMenu` 또는 `Start_Room` 중 의도한 시작 씬을 열었는가?
- [ ] `File > Build Profiles > Scene List`(버전에 따라 Build Settings)에 발표 씬이 전부 등록되었는가?
- [ ] **현재 미등록인 `Boss_Room.unity`를 추가했는가?**
- [ ] `Start_Room` 시작 시 Player가 하나만 생성되는가?
- [ ] Main Camera가 Player를 추적하고 `Q`로 World B 흑백 효과가 나오는가?
- [ ] A/D 이동, Space 점프, W/S 바라보기, S+Space 일방향 발판 내려가기가 정상인가?
- [ ] Ground/Wall Collider가 통로를 막지 않고 틈이 없는가?
- [ ] Human_Box가 3 이내에서 감지 → Howling → 추적 → 공격하는가?
- [ ] EyeballFly와 Boomber가 실제 씬 배치 위치에서 정상 감지하는가?
- [ ] 순찰을 쓰는 몬스터의 `Enable Patrol`, `Patrol Path`, Point_00/01이 저장되었는가?
- [ ] 각 Exit가 정확한 Entrance ID로 스폰시키는가?
- [ ] 이동 직후 이전 씬으로 자동 복귀하지 않는가?
- [ ] `hallwa_06 ↔ Boss_Room` 연결을 직접 완성했는가?
- [ ] Console에 빨간 Error와 `Scene couldn't be loaded`가 없는가?

## 3. Inspector 값 읽는 방법

| 형식 | 뜻 | 프로젝트 예 |
|---|---|---|
| 체크박스/Bool | 기능 켜기·끄기 | `Enable Patrol`, `Require Line Of Sight` |
| 숫자 | 거리, 속도, 시간, 피해량 | `Move Speed`, `Howl Duration` |
| LayerMask | 검사할 물리 레이어 목록 | Ground, Wall, Player, EnvironmentObstacle |
| Tag | 오브젝트 씬분 | Player Root의 `Player` |
| Object Reference | 다른 오브젝트/에셋 연결 | Patrol Path, Rail Path, Animator |
| Transform Position | 씬 내 위치 | Entrance, Exit, Patrol Point |
| Box Collider Center/Size | 로컬 판정 중심/크기 | Exit Trigger, 바닥, 벽 |
| Is Trigger | 충돌벽이 아닌 진입 감지 | Exit는 On, Ground/Wall은 Off |
| Animator Controller | 상태별 Animation Clip 연결 | PlayerAnimator, HumanBox, EyeballFly, Boomber |

코드는 Unity 월드 단위와 초를 사용한다. 거리/속도를 cm로 환산하지 말고 Scene Grid와 현재 프리팩값을 기준으로 비교한다. Duration, Cooldown, Interval, Delay는 초 단위다.

## 4. 값 변경 시 안전 규칙

1. Play Mode를 끜 뒤 변경한다.
2. 해당 인스턴스만 바꿀 때는 Apply를 누르지 않는다.
3. 전체 프리팩 기본값을 바꿀 때만 Prefab Mode 또는 Apply를 쓴다.
4. Entrance/Exit/Patrol Point 위치는 Instance Override로 남겨두고 Revert하지 않는다.
5. `순찰 경로 생성`은 기존 Patrol Path가 있으면 덮어쓰지 않는다. 없을 때만 Point_00과 Point_01을 생성한다.
6. 씬 자동 설정 Editor 메뉴는 여러 오브젝트를 바꿀 수 있으므로 백업/소스제어 상태 확인 후 사용한다.
7. 동일 `.unity` 파일을 두 명이 동시에 수정하지 않는다. 씬별 담당을 나눈다.

## 5. 플레이어 설정

- Prefab: `Assets/_Project/Prefabs/Player/Player.prefab`
- 이동: `Assets/_Project/Scripts/Player/PlatformerPlayer3D.cs`
- 피격/리스폰: `PlayerDamageReceiver.cs`
- 경직: `PlayerStunReceiver.cs`
- 애니메이션: `PlayerAnimationController.cs`
- 카메라 기능: `CameraAbilitySystem3D.cs`
- Animator: `Assets/_Project/Animations/Player/PlayerAnimator.controller`
- Root Tag: `Player`. 현재 Prefab Root Layer는 Default(0)이며 `groundLayer` Mask는 512, 즉 Layer 9를 바닥으로 검사한다.

| Inspector 표시 | 실제 변수명 | 기능 | 높이면 | 낮추면 | 발표 권장값 | 주의 |
|---|---|---|---|---|---:|---|
| Move Speed | `moveSpeed` | 좌우 속도 | 빠라짐 | 느려짐 | 6 | 런타임에 `UnitBalanceDatabase3D` 값이 덮어쓸 수 있음 |
| Jump Force | `jumpForce` | 상승 초기 속도 | 높게 점프 | 낮게 점프 | 13.3 | 발판 높이와 함께 테스트 |
| 더블 점프 사용 | `enableDoubleJump` | 공중 1회 추가 점프 | - | - | Off | 발표에 쓸 경우 Edit Mode에서 On |
| Separate Double Jump Force | `useSeparateDoubleJumpForce` | 2단 점프 힘 분리 | - | - | Off | Off면 Jump Force 사용 |
| Double Jump Force | `doubleJumpForce` | 분리된 2단 점프 힘 | 높아짐 | 낮아짐 | 13.3 | 위 토글 On일 때만 의미 |
| Ground Layer | `groundLayer` | 착지 판정 | - | - | 512 | 비우면 점프/착지 오류 |
| Ground Check Distance | `groundCheckDistance` | 발 아래 추가 검사 | 착지가 느슨해짐 | 경사/틈에 민감 | 0.08 | 큰 변경 금지 |
| Gravity Scale | `gravityScale` | 중력 배율 | 상승/낙하 빨라짐 | 느려짐 | 3 | 점프 힘과 같이 테스트 |
| Infinite Health | `infiniteHealth` | HP 감소 방지 | - | - | On | 발표 안정성에 유리 |
| Max Hp | `maxHp` | 무한 체력 Off 시 최대 HP | 버티는 횟수 증가 | 감소 | 999 | `currentHp`는 플레이 중 확인용 |
| Hit Blink Duration | `hitBlinkDuration` | 피격 깜빡임 길이 | 길어짐 | 짧아짐 | 0.3초 | 0 이상 |
| Respawn Point | `respawnPoint` | 직접 리스폰 위치 | - | - | None | None이면 ID `Default` 스폰 탐색 |
| Manual Respawn Key | `manualRespawnKey` | 테스트 리스폰 | - | - | T | 발표 중 오입력 주의 |
| Allow Look While Moving | `allowLookWhileMoving` | 이동 중 W/S 모션 | - | - | Off | Off면 Run이 W/S보다 우선 |

W/↑는 LookUp, S/↓는 LookDown이다. 좌우 이동·공중·경직·씬 전환 입력 잠금이 바라보기보다 우선한다. `PlayerHealth3D`는 **체력 기능이 아닌 기존 참조 보호용 빈 컴포넌트**다. 삭제하지 말고 실제 HP는 PlayerDamageReceiver에서 바꿄다.

Main Camera는 `SummerCampStageBootstrap3D` 실행 시 `CameraFollow3D`, URP Camera Data, `WorldVisualEffects3D`를 보장하고 Player를 추적한다. `Q`는 전역 월드 A/B, `R`은 카메라 라이트, 마우스 우클릭은 카메라 모드다.

## 6. 공통 오브젝트 설정 방법

조사 범위는 `Prefabs/Objects` 21개, `Prefabs/World` 4개, `Prefabs/Map` 9개다. 아래는 기능별로 묶은 17개 배치 유형이다.

| 오브젝트 | Prefab 경로 | 핵심 기능 | 구현 | 발표 |
|---|---|---|---|---|
| 파괴 블록 | `Objects/Blocks/Block_Breakable.prefab` | Hit/Explosion으로 충돌·시각 해제 | 구현 | 사용 가능, 타격 소스 테스트 |
| 시야 차단 블록 | `Objects/Blocks/Block_SightBlock.prefab` | 몬스터 LOS 차단 | 구현 | 사용 가능 |
| 바닥 충돌 | `Objects/Blocks/Floor_Collision_Long.prefab` | Ground 충돌 | 구현 | 사용 가능 |
| 벽 충돌 | `Objects/Blocks/Wall_Collision_Long.prefab` | Wall/이동/LOS 차단 | 구현 | 사용 가능 |
| Crane Set | `Objects/Crane/Crane_Set.prefab` | 레버로 A/B 지점 이동 | 구현 | 사용 가능, 씬별 테스트 |
| Crane 부품 | `Objects/Dynamic/Crane/*` | Body/Rail/Cable/수직 크레인 | 구현 | Set 프리팩 우선 |
| Lever | `Objects/Dynamic/Crane/Lever.prefab` | F로 Target Crane 명령 | 구현 | 참조 필수 |
| FallingBox | `Objects/Gravity/FallingBox.prefab` | 감지 후 낙하, 착지 후 발판 | 구현 | 사용 가능 |
| Stone | `Objects/Gravity/Stone.prefab` | 감지 후 낙하, 바닥에서 파괴 | 구현 | 사용 가능 |
| Gravity Spawner | `Objects/Gravity/GravityObjectSpawner.prefab` | Prefab 생성/재생성 | 구현 | 추가 테스트 |
| Vine | `Objects/Rope/Vine.prefab` | 1회 타격 기본, 연결 오브젝트 작동 | 구현 | 타격 루트 테스트 |
| Wire | `Objects/Rope/Wire.prefab` | 2회 타격 기본 | 구현 | 타격 루트 테스트 |
| Shutter | `Objects/Shutter/Shutter.prefab` | 월드 소속/카메라 반응 | 구현 | 추가 테스트 |
| Flash Target | `Objects/FlashTarget.prefab` | 카메라 플래시 반응 | 구현 | 현재 Active Ability는 Shutter/Focus만이므로 발표 제외 권장 |
| Goal Marker | `Objects/GoalMarker.prefab` | 테스트 목표물 | 구현 | 테스트 전용 |
| World State Item/Platform/Wall | `Objects/WorldStateItem.prefab`, `World/WorldState*.prefab` | A/B별 표시·충돌·작동 | 구현 | Q 전환 실전 테스트 필수 |
| Common/DropThrough Platform | `World/CommonPlatform.prefab`, `DropThroughPlatform.prefab` | 공통 발판, S+Space 통과 | 구현 | 사용 가능 |

### 배치 공통 절차

1. Project에서 Prefab을 Scene 또는 Hierarchy로 드래그한다.
2. Edit Mode에서 Move Tool(W)로 위치를 맞춘다. 2.5D 플레이 평면 Z는 보통 0을 유지한다.
3. Collider, Rigidbody, Tag, Layer가 Prefab 기본값에서 빠지지 않았는지 확인한다.
4. 연결 참조(Target Crane, Connected Object, Spawn Prefab 등)를 드래그해 넣는다.
5. Scene 저장 후 Play Mode로 테스트한다.

### 핵심 Inspector

| 컴포넌트 | 주요 값 | 현재 코드 기본/의미 | 주의 |
|---|---|---|---|
| BlockObject | Can Block Player/Monster/Sight/Light | 해당 대상 차단 | Sight/Light는 Layer/LOS Mask와 함께 확인 |
| BreakableObject3D | Disable Colliders, Disable Renderers, Destroy, Destroy Delay | 파괴 결과 | Destroy를 켜면 런타임 복구 불가 |
| CraneObject | Rail Path, Move Speed, Arrival Distance | 경로/이동 속도/도착 판정 | Rail Path None이면 이동 불가 |
| CraneLeverSwitch | Target Crane, Require Player In Range, Fallback Key | 대상/범위/F 키 | Crane이 여러 개면 Auto Find에 의존 금지 |
| GravityDropSensor | Detection Center Offset, Detection Box Size, Detect Only Once | 낙하 감지 박스 | Gizmo로 Player 통과 영역 확인 |
| StoneObject | Ground Layer Mask, Break On Ground Hit, Destroy Delay | 바닥 판정/파괴 | Ground Mask 비우지 말 것 |
| FallingBoxObject | Remain As Platform On Ground, Ground Layer Mask | 착지 후 발판 | 스폰을 벽 내부에 두지 말 것 |
| GravityObjectSpawner | Object Prefab, Spawn Point, Spawn On Start, Auto Respawn, Delay | 생성 설정 | Prefab/Point None 확인 |
| Vine/Wire | Max Hit Count, Can Be Targeted, Connected Object, Destroy Delay | 절단 횟수/결과 | Player 기본 공격은 발표 안정성 추가 테스트 |
| WorldPresence | Presence Mode | Both/A Only/B Only | Renderer·Collider·AI를 함께 제어 |
| WorldStateObject3D | World A/World B state | Enabled/Renderer/Collision/Operation/Color/Position | Preview In Edit Mode는 편집 상태를 바꿀 수 있음 |

**버튼, 일반 문, 레이저, 컨베이어 벨트 전용 Prefab/완성된 전용 스크립트는 현재 `Prefabs/Objects`에 없다.** `TriggerZone3D`, `AbilityGate3D`, `ResearchDevice3D`는 코드는 있지만 전용 발표 Prefab이 확인되지 않아 추가 설정 없이 완성 기능처럼 사용하지 않는다.

## 7. MapGroundBlock 및 MapWallBlock 사용법

현재 파일명은 `MapGroundBlock`/`MapWallBlock`이 아니다.

- Ground: `Assets/_Project/Prefabs/Objects/Blocks/Floor_Collision_Long.prefab`
- Wall: `Assets/_Project/Prefabs/Objects/Blocks/Wall_Collision_Long.prefab`
- 시각용 맵 Prefab과 혼동하지 말 것: `Prefabs/Map/Tile_Visual.prefab`

Ground는 Player/지상 몬스터의 바닥 판정에 쓴다. Wall은 좌우 이동과 몬스터 LOS를 막는다. 빛 인식 차단은 `MonsterDetection` 장애물 Mask/별도 Light Mask와 MapPiece의 Block Line Of Sight 규칙을 따르므로 씬에서 실제 테스트한다.

1. 알맞은 Prefab을 Hierarchy로 드래그한다.
2. `Ctrl+D`로 복제하고 Move Tool(W)로 타일 경계에 배치한다.
3. Box Collider의 `Edit Collider`로 Size/Center를 맞춘다. Transform Scale을 무지성으로 큰 값으로 만들지 않는다.
4. Ground/Wall Layer와 `Is Trigger = Off`를 확인한다.
5. Entrance/Exit 통로를 Collider가 덮지 않게 끝을 조정한다.
6. 인접 Collider 사이의 작은 틈/단차로 Player가 걸리지 않는지 Play Mode로 왕복한다.
7. `Ctrl+S`로 저장한다.

맵 전체를 하나의 거대 Box Collider로 덮으면 통로·Entrance·낭하 공간까지 막고, LOS가 항상 차단될 수 있다. 바닥/벽 구간별로 나눈다.

## 8. 몬스터 공통 설정

공통 컴포넌트는 `MonsterCore`, `MonsterDetection`, `MonsterMovement`, `MonsterFacing`, `MonsterAttack`, `MonsterAnimatorBridge`, `MonsterDebugInfo`, 종류별 Brain/AI다. `DataDrivenMonsterController.applyOnAwake = On`이므로 **MonsterData가 Detection/Movement/Attack 일부를 플레이 시작 시 덮어쓴다.** 영구 튜닝은 `Assets/_Project/Data/Monsters/*.asset`을 함께 확인한다.

| Inspector | 변수 | 기능 | 높이면 | 낮추면 | 주의 |
|---|---|---|---|---|---|
| Enable Detection | `enableDetection` | 모든 타깃 감지 | - | - | Off면 감지 안 함 |
| Player Detect Range | `playerDetectRange` | 최초 인식 거리 | 멀리서 인식 | 가까워야 인식 | Chase Range와 다름 |
| Chase Range | `chaseRange` | 인식 후 유지 거리 | 오래 추적 | 빨리 포기 | 최초 감지 범위가 아님 |
| Require Line Of Sight | `requireLineOfSight` | 벽 너머 감지 차단 | - | - | On일 때 Obstacle Mask 필수 |
| Obstacle Layer Mask | `obstacleLayerMask` | LOS 차단 Layer | 대상 증가 | 차단 감소 | 0이면 LOS 검사 실패 |
| Move Speed | `moveSpeed` | 추적 속도 | 빠라짐 | 느려짐 | Data가 덮어쓸 수 있음 |
| Return Speed | `returnSpeed` | 원위치 복귀 | 빨라짐 | 느려짐 | Return Home On일 때 |
| Attack Range | `attackRange` | 공격 진입 거리 | 멀리서 공격 | 가까워야 공격 | Collider 크기와 함께 테스트 |
| Attack Damage | `attackDamage` | 1회 피해 | 피해 증가 | 감소 | Player Infinite Health On이면 HP는 안 줄어듦 |
| Attack Cooldown/Interval | `attackCooldown`, `attackInterval` | 재공격 대기 | 느려짐 | 빨라짐 | 초 단위 |
| Invert Facing | `invertFacing` | 스프라이트 좌우 반전 | 토글 | 토글 | 이동 방향과 반대면 조정 |
| Show Gizmos/Debug Mode | 각 debug 필드 | Scene 범위/Console 로그 | - | - | 발표 직전엔 Console 스팸 주의 |

Player Tag가 없으면 자동 탐색이 실패할 수 있다. Player Layer는 일부 판정에 쓰지만 AI는 Tag/컴포넌트 fallback도 사용한다. 장애물 Mask에 Player/몬스터 자씬 Layer를 넣지 말고 Ground, Wall, TileObstacle, Platform, EnvironmentObstacle를 기준으로 한다.

## 9. Human_Box 설정

- Prefab: `Assets/_Project/Prefabs/Enemies/Human_Box.prefab`
- AI: `HumanBoxAI.cs`, `HumanBoxHowling.cs`, `HumanBoxBrain.cs`
- Animator: `Assets/_Project/Animations/Enemies/Human_Box/HumanBox.controller`
- Data: `Assets/_Project/Data/Monsters/HumanBoxData.asset`
- 상태: Idle/Patrol → Detect → Howling → Walk(Chase) → Attack → 실패 시 AttackFalse → Walk/Idle. 타깃을 잃으면 Idle로 가고 순찰을 재개한다.

| 값 | 변수 | Prefab | 실제 실행 기준 | 발표 중 변경 |
|---|---|---:|---:|---|
| Max HP | `maxHp` | 4 | HumanBoxData 4 | 가능 |
| Player Detect Range | `playerDetectRange` | 3 | Data 3 | 가능, Data에서 |
| Chase Range | `chaseRange` | 5 | Data 5 | 가능, Data에서 |
| Require LOS | `requireLineOfSight` | On | Data On | 가능, 발표 직전 테스트 |
| Move Speed | `moveSpeed` | 0.03, Test Speed 1 On | Data가 MonsterMovement를 1로 설정 | Data 1 유지 권장 |
| Howl Duration | `howlDuration` | 1초 | 1초 | 가능 |
| Howl Stun Duration | `howlStunDuration` | 1.5초 | 1.5초 | 가능 |
| Attack Range | `attackRange` | AI 3 / MonsterAttack 3 | **Data가 0.5로 덮어쓸** | Data에서만 조정 권장 |
| Attack Damage | `attackDamage` | 1 | Data 1 | 가능 |
| Attack Cooldown | `attackCooldown` | 1초 | Data 1초 | 가능 |
| Enable Patrol | `enablePatrol` | Off | Off | Edit Mode에서 가능 |
| Patrol Speed | `patrolSpeed` | 1 | 1 | 가능 |

### Human_Box가 인식하지 않을 때

1. Player Root의 Tag가 `Player`인지 확인한다.
2. 플레이어가 최초 감지 거리 3 이내인지 본다. Chase Range 5는 인식 후 유지 범위다.
3. MonsterDetection의 Enable Detection/Can Detect Player가 On인지 본다.
4. Obstacle Layer Mask와 Detection Origin(`Line Of Sight Start Offset`), Target Check Offset을 확인한다.
5. 불필요하게 큰 Wall Collider가 레이를 막는지 Scene Gizmo/로그의 `SightBlockedBy`를 본다.
6. Animator의 `State`, `IsHowling`, `Howling` Trigger와 Howling Motion이 있는지 본다. Howling 종료는 Animation Event가 아니라 `howlDuration` 타이머다.
7. Player에 `PlayerStunReceiver`가 있는지 확인한다.
8. 이동하지 않으면 Data Move Speed, MonsterMovement Enable Movement, Test Speed, Rigidbody Freeze Position X를 확인한다.

## 10. EyeballFly 설정

- Prefab: `Assets/_Project/Prefabs/Enemies/EyeballFly.prefab`
- AI/HP: `EyeballFlyAI.cs`, `EyeballFlyHealth.cs`, `EyeballFlyBrain.cs`
- Animator: `Assets/_Project/Animations/Enemies/EyeballFly/EyeballFly.controller`
- Data: `EyeballFlyData.asset`

| 항목 | Prefab | 실제 실행/Data | 설명 |
|---|---:|---:|---|
| HP | 별도 Health | 1 | Dead 상태 제공 |
| Player Detect | 1.5 | 1.5 | 최초 인식 |
| Light Detect | 4 | 4, Can Detect Light On | 카메라 Light가 켜진 타깃을 감지 |
| Chase Range | 2.5 | 2.5 | 추적 유지 |
| Move Speed | 1 | 1 | 비행 추적/복귀 |
| Attack Range | Prefab 0.5 | **Data 1** | DataDrivenMonsterController가 덮어쓸 |
| Attack Damage/Interval | 1 / 1초 | 1 / 1초 | Player, Light, 설정된 HitReceiver |
| Return Home | On | On | 타깃 상실 시 초기 위치 복귀 |
| Facing | Right Default, Invert Off | 동일 | 반대로 보면 Invert |

빛 모드는 삭제된 것이 아니라 현재 코드/Data에서 활성화되어 있다. 다만 `CameraAbilitySystem3D` 활성 슬롯은 Shutter/Focus로 제한되어 있어, 발표 씬의 R 카메라 Light와 연동되는지 직접 테스트한 후에만 시연한다. 순찰 Controller는 현재 EyeballFly Prefab에 확인되지 않아 기본 순찰은 미설정이다.

## 11. Boomber 설정

현재 실제 이름과 파일명은 **Boomber**다.

- Prefab: `Assets/_Project/Prefabs/Enemies/Boomber.prefab`
- Brain/Explosion: `BoomberBrain.cs`, `BoomberExplosion.cs`
- Animator: `Assets/_Project/Animations/Enemies/Boomber/Boomber.controller`
- Data: `BoomberData.asset`
- 상태: Idle → Detect → Run → PreAttack → AttackLeap → Explosion → Dead/제거

| Inspector | 변수 | Prefab | 실제/ 주의 |
|---|---|---:|---|
| HP | `maxHp` | 1 | Data 1 |
| Detect Range | `playerDetectRange` | 4 | **Data 1.5로 덮어쓸** |
| Locked Run Direction | `lockedRunDirection` | 0 | 최초 인식 시 Player의 좌/우로 한 번 결정 |
| Base Run Speed | `baseRunSpeed` | 3 | 돌진 시작 속도 |
| Speed Increase Per Second | `speedIncreasePerSecond` | 1 | 매초 가속량 |
| Max Run Speed | `maxRunSpeed` | 7 | 최대 속도 |
| Test Speed Multiplier | `testSpeedMultiplier` | 20, Use Off | On은 발표 제외 권장 |
| Attack Range | `attackRange` | 0.8 | Data 0.5로 덮어쓸 |
| Fuse Duration | `fuseDuration` | 1초 | PreAttack+Leap 전체 대기 |
| Attack Leap Duration | `attackLeapDuration` | 0.35초 | Fuse 마지막 구간 |
| Explosion Visual Duration | `explosionVisualDuration` | 0.625초 | 폭발 시퀀스 유지 |
| Explosion Radius | `explosionRadius` | 1.25 | BoomberExplosion 실제 반경. Data의 1.5는 이 컴포넌트에 적용하지 않음 |
| Explosion Damage | `explosionDamage` | 2 | Brain이 MonsterAttack 피해로 Configure. Data 적용 순서 민감 가능성으로 실행 확인 필수 |
| Destroy On Explosion | `destroyOnExplosion` | On | Visual 종료 후 0.25초 뒤 제거 |
| Breakables | `affectBreakableObjects` | On | `IExplosionBreakable`+Breakable Mask에만 적용 |

Animation Event가 폭발 타이밍을 발생시키는 구조가 아니다. Coroutine이 `fuseDuration` 내에서 PreAttack/AttackLeap을 나누고 폭발 이벤트로 Animator 상태를 바꿄다. Boomber Prefab에 순찰 Controller는 확인되지 않아 현재 순찰은 미설정이다.

## 12. 몬스터 순찰 경로 설정

Human_Box Prefab에 `MonsterPatrolController`가 있다. Inspector 실제 버튼명은 한글이다.

1. 씬의 Human_Box Instance를 선택한다.
2. `Enable Patrol`을 켜고 `순찰 경로 생성`을 누른다.
3. 같은 부모 아래 `<몬스터이름>_PatrolPath/Point_00`, `Point_01`이 생성되고 Patrol Path에 자동 연결되는지 본다.
4. Edit Mode Scene 창에서 Point를 Move Tool로 옮긴다. Ground 몬스터는 바닥 위 X 위치를 중심으로 놓는다.
5. 필요하면 `포인트 추가`, `선택 포인트 뒤에 추가`, `포인트 번호 다시 정렬`을 쓴다.
6. `순찰 경로 검사`로 2개 이상인지 Console에서 확인한다.
7. `Ctrl+S` 후 Play Mode로 테스트한다.

| 순찰 값 | 의미 | 현재 Human_Box |
|---|---|---:|
| Patrol Mode: PingPong | 끝에서 반대 방향 | 기본 |
| Loop | 마지막 → 0번 | 선택 가능 |
| Once | 마지막에서 종료 | 선택 가능 |
| Patrol Speed | 순찰 속도 | 1 |
| Arrival Distance | 도착으로 볼 거리 | 0.1 |
| Wait Time At Point | 포인트 대기 | 0초 |
| Start Point Index | 첫 지점 | 0 |
| Start From Nearest Point | 처음에 가장 가까운 포인트 | Off |
| Resume Patrol After Losing Player | 전투 후 복귀 | On |
| Resume: Nearest Point | 현재 위치에서 최단 | 기본 |
| Last Point | 전투 전 index 유지 | 선택 가능 |
| Start Point | 설정된 시작 index | 선택 가능 |
| Show Patrol Gizmos | 경로·현재 목표 표시 | On |

## 13. 몬스터 배치 방법

1. `Assets/_Project/Prefabs/Enemies` 아래 Human_Box, EyeballFly, Boomber 중 하나를 Scene으로 드래그한다. `MonsterAni1`은 별도 표시 오브젝트 성격이므로 주요 AI 3종과 구분한다.
2. Ground형(Human_Box/Boomber)은 Collider 밑면이 바닥 위에 오게 놓고, EyeballFly는 비행 홈 위치에 놓는다.
3. Z=0 플레이 평면을 유지한다.
4. 몬스터 Collider가 Ground/Wall 내부에 시작하지 않는지 본다.
5. MonsterDetection Gizmo와 LOS 선을 켜고 감지 범위를 본다.
6. Human_Box에만 필요하면 순찰 경로를 생성한다.
7. Visual 자식의 Animator Controller가 해당 종류인지 확인한다.
8. Play Mode에서 Player를 감지 범위 안/밖으로 이동시켜 상태를 확인한다.

필수 Object Reference는 Prefab에 이미 연결되어 있다. Patrol Path는 씬별 새 경로를 쓸 때만 수동/버튼으로 연결한다. Player Target은 런타임에 Tag/이름/`PlatformerPlayer3D`로 자동 탐색한다.

## 14. 씬 연결 시스템 개요

실제 여름합숙 씬은 `SceneConnections.prefab`의 루트 구조와 `PlayerSpawnPoint` + `StageExitTrigger`를 사용한다.

```text
SceneConnections
├─ SpawnPoints
│  ├─ LeftEntrance
│  └─ RightEntrance
└─ Exits
   ├─ LeftExit
   └─ RightExit
```

`middle_Room`은 Left와 UpperRight/CenterRight/LowerRight 세 분기를 쓴다. SceneConnections Root의 `SceneConnectionsAuthoring`은 배치 표식자일 뿐 런타임 이동 로직은 없다.

- Entrance: `PlayerSpawnPoint.spawnPointId`. 도착 위치, 기본 스폰, 바라볼 방향을 가진다.
- Exit: `StageExitTrigger`. `Target Scene`(에디터 SceneAsset), `Next Scene Name`, `Target Spawn Point Id`, `Connection Enabled`를 쓴다.
- Trigger: Exit의 Box Collider는 `Is Trigger = On`이어야 한다. OnValidate/Awake도 Trigger로 바꾼다.
- 페이드: `Use Fade` 필드는 있지만 StageExitTrigger의 전환 Type은 현재 `Immediate`만 존재한다. **별도 페이드 실행은 이 스크립트에서 확인되지 않음**.
- Player: `Start_Room`의 Player가 `DontDestroyOnLoad`로 유지되며 중복 Player를 정리한다.
- Camera: 씬 로드 후 Main Camera를 활성화하고 Follow/URP/월드 효과를 보장한다.
- 스폰 안전: 이동 직후 0.35초 전환 차단과 Exit을 한 번 빠져나오는 조건으로 즉시 복귀를 방지한다.

## 15. 새 씬 양방향 연결 방법

1. 두 씬을 먼저 저장하고 `File > Build Profiles > Scene List`에 체크된 상태로 추가한다.
2. 각 씬에 `Assets/_Project/Scenes/SceneConnections/Prefabs/SceneConnections.prefab`을 배치한다.
3. A 씬 `SceneConnections/Exits/RightExit` 선택 → `Target Scene` = B SceneAsset, `Target Spawn Point Id` = `LeftEntrance`, `Connection Enabled` = On.
4. B 씬 `LeftExit` 선택 → `Target Scene` = A, `Target Spawn Point Id` = `RightEntrance`, `Connection Enabled` = On.
5. `Next Scene Name`은 Target Scene을 넣으면 OnValidate가 파일명으로 맞춘다. 대소문자/오타를 수동으로 만들지 않는다.
6. Entrance와 Exit를 통로에 배치하고 양쪽 씬에서 `Ctrl+S`.
7. Exit 컴포넌트 메뉴의 `Validate Scene Connection`으로 Console을 확인한다.
8. Play Mode에서 A→B, B→A를 모두 테스트한다.

## 16. Entrance와 Exit 위치 조절

Entrance는 Player Root가 도착하는 위치다. Player 발이 바닥 위에 오고 Z=0이며 Wall/Ground Collider 내부와 Exit Trigger 내부가 아닌 곳에 놓는다. 도착 방향은 `Face Right On Spawn`/프리팩 필드로 확인한다.

Exit는 통로의 끝에 두고 Box Collider `Is Trigger = On`을 유지한다. Edit Collider로 Player가 확실히 지나는 크기만 주고 다른 방/스폰 지점을 덮지 않는다. 위치는 반드시 Edit Mode에서 바꾸고 `Ctrl+S`한다. Prefab Revert는 씬별 위치 Override를 잃게 할 수 있다.

## 17. 현재 전체 씬 연결표

씬 Prefab Override의 `nextSceneName`/`targetSpawnPointId`를 직접 읽은 결과다.

| 출발 씬 | Exit | 목적지 | Entrance | 양방향 | 상태 |
|---|---|---|---|---|---|
| Start_Room | RightExit | hallwa_01 | LeftEntrance | 예 | 연결 |
| hallwa_01 | LeftExit | Start_Room | RightEntrance | 예 | 연결 |
| hallwa_01 | RightExit | middle_Room | LeftEntrance | 예 | 연결 |
| middle_Room | LeftExit | hallwa_01 | RightEntrance | 예 | 연결 |
| middle_Room | UpperRightExit | hallwa_02 | LeftEntrance | 예 | 연결 |
| hallwa_02 | LeftExit | middle_Room | UpperRightEntrance | 예 | 연결 |
| hallwa_02 | RightExit | Item_Room_01 | LeftEntrance | 예 | 연결 |
| Item_Room_01 | LeftExit | hallwa_02 | RightEntrance | 예 | 연결 |
| middle_Room | CenterRightExit | hallwa_03 | LeftEntrance | 예 | 연결 |
| hallwa_03 | LeftExit | middle_Room | CenterRightEntrance | 예 | 연결 |
| hallwa_03 | RightExit | Item_Room_02 | LeftEntrance | 예 | 연결 |
| Item_Room_02 | LeftExit | hallwa_03 | RightEntrance | 예 | 연결 |
| Item_Room_02 | RightExit | hallwa_04 | LeftEntrance | **아니오** | hallwa_04→Item만 있음 |
| hallwa_04 | LeftExit | Item_Room_02 | RightEntrance | **아니오** | 역방향 누락 |
| middle_Room | LowerRightExit | hallwa_05 | LeftEntrance | 예 | 연결 |
| hallwa_05 | LeftExit | middle_Room | LowerRightEntrance | 예 | 연결 |
| hallwa_05 | RightExit | Boss_Hint_Room | LeftEntrance | 예 | 연결 |
| Boss_Hint_Room | LeftExit | hallwa_05 | RightEntrance | 예 | 연결 |
| Boss_Hint_Room | RightExit | hallwa_06 | LeftEntrance | **아니오** | hallwa_06의 복귀 Exit 누락 |
| hallwa_06 | RightExit | Boss_Room | LeftEntrance | **아니오** | **미설정** |
| Boss_Room | LeftExit | hallwa_06 | RightEntrance | **아니오** | **미설정 + Build 미등록** |

파일명은 `Start_Room`, `middle_Room`, `Item_Room_01`, `Item_Room_02`, `Boss_Hint_Room`, `Boss_Room`처럼 대소문자가 섞여 있다. 표의 정확한 이름을 사용한다. 연결에 실제로 참여하는 파일은 11개이며, 목표 `Boss_Room`까지 합치면 12개다.

## 18. 씬 이동이 안 될 때

| 증상 | 예상 원인 | 확인 위치 | 해결 |
|---|---|---|---|
| Scene couldn't be loaded | 이름 오타/미등록 | Console, Build Scene List | 정확한 `.unity` 이름으로 등록 |
| Build Profile 미등록 | 씬 목록에 없음 | File > Build Profiles | 씬 추가 및 체크 |
| No cameras rendering | Main Camera 비활성/Tag 오류 | Hierarchy/Camera Inspector | Camera Active, Tag MainCamera |
| 이동 후 Player 안 보임 | 잘못된 Spawn ID/카메라 참조 | Exit, Entrance, Main Camera | ID 일치, CameraFollow 확인 |
| 잘못된 Entrance | Target Spawn Point Id 오타 | 출발 Exit | 목적지 ID와 문자열 일치 |
| Collider 안에 스폰 | Entrance 배치 오류 | 목적지 Scene | Edit Mode에서 바닥 위로 이동 |
| 즉시 이전 씬 복귀 | Entrance가 Exit Trigger 내부 | Scene 배치 | 두 영역을 분리, Exit을 한 번 빠져나오기 |
| 메인 메뉴 UI 남음 | 메뉴 Root가 잘못 유지 | Hierarchy DontDestroyOnLoad | 코드 수정 없이 재실행, 중복 Root 보고 |
| Player 중복 | 각 방에 Player 배치 | Hierarchy | Start_Room의 영속 Player 루트를 기준으로 씬 인스턴스 정리 |
| Camera 미추적 | MainCamera Tag/Follow 누락 | Main Camera | Bootstrap 재실행을 위해 Start_Room에서 시작 |
| Exit 무반응 | Connection Off, Locked, Trigger Off, Player 미인식 | StageExitTrigger/Collider | Validate Scene Connection, Tag/Layer/Is Trigger 확인 |

## 19. Animator 확인 방법

1. 대상의 Visual 자식을 선택하고 Animator의 Controller가 아래 경로와 일치하는지 본다.
2. `Window > Animation > Animator`를 열고 Play Mode에서 현재 주황색 상태를 본다.
3. Parameters에서 Bool/Trigger/State 값이 코드 상태와 같이 변하는지 본다.
4. State를 눌러 Inspector의 Motion이 `None (Motion)`이면 Missing Motion으로 보고 시연에서 제외한다.
5. Clip Inspector의 Loop Time을 본다. Idle/Walk/Run은 보통 Loop, Attack/Howling/Explosion은 실제 Clip 타이밍과 코드 타이머를 함께 본다.
6. Console의 `Animator parameter ... does not exist`, Missing Motion, Missing Script를 확인한다.

| 대상 | Controller | 핵심 Parameters/상태 | 주의 |
|---|---|---|---|
| Player | `Animations/Player/PlayerAnimator.controller` | State 0 Idle, 1 Run, 2 Jump, 3 Fall, 4 LookUp, 5 LookDown, 6 Dead | 일부 상태에 Motion None 기록이 있어 실행 점검 필수 |
| Human_Box | `Animations/Enemies/Human_Box/HumanBox.controller` | State, IsMoving, IsAttacking, IsHowling, IsAttackFalse, IsDead | Howling 종료는 1초 코드 타이머 |
| EyeballFly | `Animations/Enemies/EyeballFly/EyeballFly.controller` | IsMoving, IsAttacking, IsDead, Attack | Idle/Move가 같은 Clip을 쓰는 구간 있음 |
| Boomber | `Animations/Enemies/Boomber/Boomber.controller` | State, IsMoving, IsAttacking, IsDead, Attack | PreAttack/AttackLeap/Explosion은 Coroutine 이벤트와 연동 |

Animation Event를 추가/삭제하기 전에 코드가 시간을 제어하는지 확인한다. Human_Box/Boomber의 핵심 타이밍은 현재 코드 타이머다.

## 20. 중간발표용 3~5분 추천 시연 순서

1. MainMenu에서 Start_Room 시작.
2. A/D 이동, Space 점프, W/S 바라보기. 더블 점프를 시연하려면 미리 Enable Double Jump를 On하고 리허설한다.
3. Q로 World A/B 전환과 World B 흑백/노이즈 효과를 보여준다.
4. 마우스 우클릭 카메라 모드 + Shutter 정지를 미리 검증된 대상에게만 사용한다.
5. Human_Box의 Patrol → Player 3 이내 진입 → Howling → 경직 → 추적 → 공격.
6. Boomber 1.5 이내 진입 → 방향 고정 돌진 → 장애물 앞 PreAttack → Leap → Explosion.
7. Start_Room → hallwa_01 → middle_Room 이동과 정확한 Entrance 스폰을 보여준다.

Flash/Relay, 일반 문/레이저/컨베이어, Boss_Room 진입은 현재 연결/활성 상태가 완전히 검증되기 전에는 시연에서 제외한다.

## 21. 발표 직전 빠른 복구

1. Console `Clear` 후 씬을 다시 Play하고 첫 빨간 Error를 읽는다.
2. `Start_Room` 또는 의도한 시작 씬인지 확인한다.
3. Player Root Active/Tag Player, Main Camera Active/Tag MainCamera를 확인한다.
4. Build Scene List에 현재/다음 씬이 있는지 본다.
5. Human_Box Animator와 MonsterDetection Enable을 본다.
6. Patrol이 안 되면 Enable Patrol, Patrol Path, Point 2개 이상을 본다.
7. 이동이 안 되면 Exit Target Scene/Spawn ID/Connection Enabled와 Collider Is Trigger를 본다.
8. 스폰이 엉키면 Entrance를 Collider 밖으로 조금 옮긴 뒤 저장한다.

씬/Prefab을 삭제하거나 Revert/Reset, 소스제어 강제 되돌리기를 발표 직전 복구법으로 사용하지 않는다.

## 22. 구현 상태 요약

| 기능 | 상태 | 확인 근거 | 발표 주의 |
|---|---|---|---|
| Player 이동/점프/W/S | 발표 사용 가능 | Player Prefab+PlatformerPlayer3D+Animator | 더블 점프 기본 Off |
| Player 체력/리스폰 | 발표 사용 가능 | PlayerDamageReceiver | Infinite Health On, T 리스폰 |
| Q 월드 전환/흑백 | 추가 리허설 필요 | 입력, WorldSystem, 공통 Camera VFX 코드 | 씬별 A/B 대상 설정 필요 |
| Shutter/Focus | 추가 테스트 필요 | Active mask에 Shutter/Focus | 대상 Tag/WorldSwitchable 확인 |
| Flash/Relay | 현재 발표 제외 권장 | Active mask에서 제외 | 코드/에셋 존재만으로 완성 판정 금지 |
| Human_Box AI/Howling | 발표 사용 가능 | Prefab/Data/Animator/실행 로그 | 감지 3, 추적 5, 공격 실행 0.5 |
| Human_Box Patrol | 발표 사용 가능 | Controller+Editor 버튼 | 씬별 Path 설정 필수 |
| EyeballFly | 추가 테스트 필요 | Prefab/Data/AI/Animator | 빛 타깃 실전 확인 |
| Boomber 돌진/폭발 | 추가 테스트 필요 | Brain/Explosion/Animator | Data/Prefab 값 차이와 적용 순서 |
| 크레인 | 발표 사용 가능 | Crane Set/Rail/Lever 프리팩 | 씬 배치와 장애물 Mask 확인 |
| 낙하 Stone/FallingBox | 발표 사용 가능 | Prefab+Sensor+Ground mask | Player 감지 박스 배치 |
| Vine/Wire/파괴 블록 | 추가 테스트 필요 | HitReceiver/Breakable 코드 | 실제 타격 수단 연결 |
| 11개 씬 연결 | 부분 사용 가능 | 20개 StageExit Override | hallwa_04 역방향, hallwa_06 누락 |
| Boss_Room 진입 | 미구현/미설정 | Exit 없음, Build 미등록 | 발표 전 반드시 직접 완성 |
| 일반 버튼/문/레이저/컨베이어 | 미구현 또는 전용 Prefab 없음 | 프로젝트 Prefab 인벤토리 | 시연 제외 |

## 23. 한 장 요약표

| 할 일 | 어디서 | 핵심 |
|---|---|---|
| Player 속도/점프 | Player Instance > PlatformerPlayer3D | 6 / 13.3, Data 덮어쓰기 주의 |
| 더블 점프 | PlatformerPlayer3D | Enable Double Jump On, 기본 Off |
| 무한 체력/리스폰 | PlayerDamageReceiver | Infinite Health On, T, Default Spawn |
| Human_Box | HumanBoxData + Prefab | Detect 3, Chase 5, Move 1, Howl 1, Stun 1.5, Attack 0.5 |
| EyeballFly | EyeballFlyData | Detect 1.5, Light 4, Chase 2.5, Move 1, Attack 1 |
| Boomber | BoomberData + Prefab Explosion | Detect 1.5, Run 3→7, Fuse 1, Radius 1.25 |
| 순찰 | MonsterPatrolController | Enable → `순찰 경로 생성` → Point 이동 → Ctrl+S |
| 오브젝트 배치 | Project > Prefabs/Objects | Drag → 위치/Collider/Layer/Reference → 저장 |
| 바닥/벽 | Floor/Wall_Collision_Long | Is Trigger Off, 통로·Entrance 덮지 않기 |
| 씬 연결 | SceneConnections/Exits | Target Scene + Target Spawn Point Id + Connection On |
| 스폰 수정 | SceneConnections/SpawnPoints | 바닥 위, Z=0, Exit 밖, Ctrl+S |
| 저장 | Edit Mode | Ctrl+S, 씬 이름에 `*` 없음 확인 |

가장 흔한 오류 5개:

1. Play Mode에서 바꾼 값을 영구 변경으로 오해.
2. DataDrivenMonsterController가 Prefab Inspector 값을 MonsterData로 덮어쓰는 것을 놓침.
3. Detect Range와 Chase Range를 혼동.
4. Exit의 Target Spawn ID 오타 또는 Entrance를 Collider/Exit 안에 배치.
5. Build Scene List 미등록. 현재 특히 `Boss_Room`.

### 발표 전 기획자가 반드시 직접 할 일

- `Boss_Room` Build Scene List 등록.
- `hallwa_06` RightExit → Boss_Room LeftEntrance, Boss_Room LeftExit → hallwa_06 RightEntrance 양방향 설정.
- `Item_Room_02 ↔ hallwa_04`, `Boss_Hint_Room ↔ hallwa_06` 역방향 연결 추가.
- 발표용 Human_Box에 Enable Patrol/Patrol Path/Point 저장.
- 몬스터별 MonsterData 실행값을 기준으로 배치 거리 조정.
- Q 월드 흑백, Human_Box Howling/Stun, Boomber Explosion, 씬 왕복 이동을 실제 발표 순서로 1회 이상 리허설.
- Console 빨간 Error 0개 확인.

## 24. 벽 끼임 / 셔터 / Box 재생성 설정

- Player는 `PlayerNoStick` Physics Material(Static/Dynamic Friction 0, Minimum 결합)을 사용합니다. Player 루트의 Rigidbody는 Interpolate + Continuous Dynamic과 Z/회전 고정을 유지합니다.
- 셔터 시간 정지는 `Shutter Freeze Duration` 동안 물리·애니메이션·AI를 멈추며 `Shutter Cooldown`이 끝나면 다시 사용할 수 있습니다. Mark는 별도 상태로 `Shutter Mark Duration` 동안 청록 표시와 후속 Relay 대상 정보를 유지하고, 만료/소비 뒤 `Shutter Remark Cooldown` 동안 주황 표시가 유지됩니다.
- 기본 설정에서는 Mark가 남아 있거나 재마크 대기 중이어도 같은 대상의 시간 정지는 다시 사용할 수 있습니다. `Refresh Freeze While Frozen`과 `Refresh Mark On Shutter`는 기본 Off이므로 진행 중인 정지나 Mark 시간이 재촬영으로 무한 연장되지 않습니다.
- 셔터는 한 번 누름 입력과 기존 Cooldown을 사용하며, 화면 프레임 안 대상 중 Ground(9)와 Wall(10)에 가려지지 않은 대상만 촬영합니다. 문제 확인 시 Player의 `CameraAbilitySystem3D`에서 `Show Shutter Debug`, `Log Shutter Events`를 켭니다.
- `FallingBox`의 `DestructibleBox3D`에서 파괴 가능, 최대 체력, 체력 0 시 제거, 제거 방식(Destroy/Disable), 지연과 애니메이션을 설정합니다. 기본값은 체력 1, Destroy입니다.
- `GravityObjectSpawner`는 기존 프로젝트의 실제 스폰 컴포넌트입니다. `Spawn Prefab`, `Spawn On Start`, `Respawn After Despawn`, `Respawn Delay`, `Allow Repeated Respawn`을 설정하십시오.
- 생성 위치는 Spawner 자식 `SpawnPoint`를 Edit Mode에서 직접 이동하고 Ctrl+S로 저장합니다. 코드가 이 Transform을 초기화하거나 런타임에 이동하지 않습니다.
- 대상의 루트 GameObject가 Destroy 또는 Disable될 때 `SpawnedObjectLifecycle` 이벤트로 한 번만 감지합니다. Collider 자식 제거와 착지 상태는 소멸로 보지 않습니다. 재생성 Coroutine은 하나만 유지되고 최소 0.02초 뒤 실행되며 씬 언로드/Spawner 비활성화 시 취소됩니다.

기획자 테스트: 벽 옆 점프·낙하 → 셔터 프레임 안/밖 및 벽 앞/뒤 대상 촬영 → Boomber 폭발로 FallingBox 체력 0 → 설정한 지연 후 SpawnPoint에서 한 번만 재생성되는 순서로 확인합니다. 특정 타일 경계에서만 걸리면 해당 씬의 수동 Collider 겹침/틈을 별도로 확인하고 Collider를 일괄 재생성하지 마십시오.
