# 프로젝트 진행 보고서

> **부제:** 2주차 이후 시스템 개선 및 기능 구현 현황  
> **작성 기준일:** 2026년 7월 15일  
> **대상 프로젝트:** Unity 6.3 (`6000.3.19f1`) / `S_G`

## 보고 범위 및 판정 기준

본 보고서는 2주차에 이미 보고한 기본 플레이 구조를 반복하지 않고, 이후 확인된 시스템 구조화, 몬스터별 로직, 월드 존재 제어, 맵 제작 방식, 씬 연결 방식의 개선 사항만을 다룬다. 판정은 현재 `Assets/_Project` 아래의 스크립트·프리팹·문서와 Unity Editor 로그를 기준으로 하였다.

- **완성:** 코드와 프리팹 구성이 확인되고 핵심 동작 흐름이 구현된 항목
- **개선:** 기존 기능을 공통 구조나 기획자용 설정 방식으로 정리한 항목
- **진행 중:** 구현 파일은 있으나 최신 Play Mode 통합 검증이 필요한 항목
- **이슈:** 컴파일 로그 또는 현재 구조상 후속 확인이 필요한 항목

> 정확한 2주차 스냅샷이나 당시 보고서 파일은 프로젝트에서 확인되지 않았으므로, 첨부된 보고 범위와 현재 파일 구조를 기준으로 중복 내용을 제외하였다.

---

## 1. 프로젝트 개요

본 프로젝트는 Unity 6 기반의 2.5D 메트로베니아·퍼즐 플랫폼 게임이다. 화면 구성은 2D 사이드뷰이지만 이동과 충돌은 3D 물리를 사용한다. 현재 개발은 플레이어 이동 자체보다 월드 전환, 퍼즐 오브젝트, 몬스터 AI, 맵 구성 및 씬 연결 구조를 안정적으로 결합하는 데 초점을 두고 있다. 전투를 중심으로 진행하기보다는 이동 경로 탐색과 기믹 해결이 플레이의 중심이 된다. 기획자가 코드를 직접 수정하지 않고도 Inspector에서 수치와 연결 관계를 조정할 수 있도록 시스템을 정리하고 있다.

---

## 2. 2주차 이후 주요 개선 사항

### 2-1. 몬스터 시스템 구조화 — **개선**

몬스터의 모든 기능을 하나의 AI 스크립트에 집중시키지 않고, 공통 기능과 몬스터별 판단 로직을 분리하는 구조가 추가되었다. 세 몬스터 프리팹에서 공통 컴포넌트 연결도 확인하였다.

| 구분 | 실제 파일 | 역할 |
|---|---|---|
| 기본 참조 | `MonsterCore.cs` | Player, Light, Visual, Rigidbody, Collider 등 공통 참조 관리 |
| 감지 | `MonsterDetection.cs` | Player·Light 감지 거리, 추적 유지 거리, 시야 차단 LayerMask 관리 |
| 이동 | `MonsterMovement.cs` | 지상형·공중형 이동, 이동 속도, 귀환, 장애물 충돌 설정 |
| 방향 | `MonsterFacing.cs` | 이동 또는 목표 방향에 따른 비주얼 반전 |
| 공격 | `MonsterAttack.cs` | 공격 활성화, 범위, 피해량, 공격 간격 관리 |
| 체력 | `MonsterHealth.cs` | 공통 HP, 사망 및 Collider 비활성 처리 |
| 애니메이션 | `MonsterAnimatorBridge.cs` | 이동·공격·사망 상태를 Animator 파라미터와 연결 |
| 공통 AI 기반 | `MonsterAIBase.cs` | 목표 선택, 시야 판정, 이동 충돌 등 공통 AI 기반 제공 |

몬스터별 판단은 `EyeballFlyBrain.cs`, `HumanBoxBrain.cs`, `BoomberBrain.cs`로 구분되어 있다. 다만 EyeballFly와 Human_Box의 Brain은 현재 기존 `EyeballFlyAI.cs`, `HumanBoxAI.cs` 상태 머신을 감싸는 연결 역할도 수행하므로, 모든 판단이 Brain에만 완전히 이전된 상태는 아니다.

`MonsterDetection`, `MonsterMovement`, `MonsterAttack` 등에 Header, Tooltip, Debug 및 Gizmo 설정이 있어 감지 거리, 이동 속도, 공격 범위, 차단 레이어를 프리팹별로 조절할 수 있다. 공통 컴포넌트 중 `MonsterHealth`는 현재 `Boomber.prefab`에서 직접 확인되며, EyeballFly와 Human_Box는 각각 `EyeballFlyHealth.cs`, `HumanBoxAI.cs` 내부 체력 로직도 함께 유지하고 있다. 따라서 체력 구조는 아직 완전히 단일화된 상태로 보기는 어렵다.

### 2-2. 몬스터별 로직 구현 현황

#### EyeballFly — **구현 완료, 통합 검증 필요**

- **특징:** `EyeballFlyAI.cs`와 `EyeballFlyBrain.cs`가 연결된 공중형 몬스터이다. Player와 Light를 모두 감지하며 Player를 우선 선택한다.
- **동작 흐름:** `IDLE → MOVE → ATTACK` 상태를 사용하고, 사망 시 `DEAD`로 전환한다. 목표가 감지되면 공중 이동으로 접근하고, 공격 범위에 들어오면 공격한다. 목표를 잃었을 때는 기본 위치로 돌아가는 흐름이 구현되어 있다.
- **감지 조건:** `MonsterDetection.cs`와 공통 AI에서 Raycast 기반 Line of Sight를 검사한다. 차단 LayerMask에 포함된 벽 뒤의 목표는 감지하지 않는다. Light는 실제 Light 컴포넌트가 활성화되어 있어야 유효한 목표가 되며, 꺼지거나 시야가 차단되면 추적 대상에서 제외된다.
- **조절 가능 값:** Player·Light 감지 거리, 추적 유지 거리, 장애물 및 Light 전용 차단 LayerMask, 이동 속도, 정지 거리, 공중 움직임, 공격 범위·피해량·간격, Hover 진폭·주기 등을 Inspector에서 조정할 수 있다.
- **현재 완성도:** 스크립트, `EyeballFly.prefab`, 공통 컴포넌트 및 애니메이션 연결이 확인된다. 다만 현재 프로젝트의 최신 Play Mode 성공 여부가 확인되지 않아 실제 씬 단위 검증은 남아 있다.

#### Human_Box — **구현 완료, 통합 검증 필요**

- **특징:** 상자 형태의 Idle 상태로 대기하다 Player를 감지하면 Howling을 거쳐 지상 추적을 시작하는 몬스터이다.
- **상태 흐름:** `Idle → Howling → Walk → Attack`이 기본 흐름이며, 공격이 빗나가면 `AttackFalse`, 체력이 소진되면 `Dead`로 전환된다. 감지 범위와 시야 조건을 잃으면 Idle로 복귀한다.
- **Player 상호작용:** `HumanBoxHowling.cs`가 Player의 `IStunnable` 구현을 찾아 설정된 시간 동안 Stun을 요청한다. 공격 시에는 Player가 공격 범위 안에 있고 시야가 유지되는지 다시 확인한 뒤 피해를 적용한다.
- **시야 처리:** 감지 및 추적 과정에서 Line of Sight를 확인하므로 차단 오브젝트 뒤의 Player는 계속 추적하지 않도록 구성되어 있다.
- **조절 가능 값:** 감지·추적·공격 범위, 이동 속도, Howling 및 Stun 시간, 공격 선딜레이·피해량·재사용 시간, 사망 처리를 Inspector에서 조정할 수 있다.
- **현재 완성도:** `HumanBoxAI.cs`, `HumanBoxBrain.cs`, `HumanBoxHowling.cs`, `Human_Box.prefab` 및 공통 컴포넌트 연결이 확인된다. 실제 애니메이션 타이밍과 Stun 해제까지 포함한 Play Mode 검증은 남아 있다.

#### Boomber — **구현 완료, 통합 검증 필요**

- **특징:** Player를 감지하면 방향을 한 번 결정한 뒤 방향 전환 없이 돌진하는 자폭형 지상 몬스터이다.
- **상태 흐름:** `Idle → Run → Attack → Dead` 순서로 진행된다. Idle에서는 수평 이동을 고정해 불필요한 물리 미끄러짐을 막고, Player가 시야 안에 들어왔을 때만 Run으로 전환한다.
- **돌진 방식:** Run 진입 시 Player의 좌우 방향을 `lockedRunDirection`에 저장한다. 이후 방향은 다시 계산하지 않으며, 경과 시간에 따라 `baseRunSpeed`에서 `maxRunSpeed`까지 단계적으로 증가한다.
- **폭발 방식:** Player, 벽 또는 지정된 오브젝트 레이어와의 충돌·전방 BoxCast 차단을 감지하면 공격 상태로 들어간다. `BoomberExplosion.cs`가 Fuse 이후 폭발 범위 안의 Player에게 피해를 주고, 선택적으로 폭발 대응 오브젝트도 처리한다.
- **현재 완성도:** `BoomberBrain.cs`, `BoomberExplosion.cs`, `BoomberState.cs`, `Boomber.prefab` 및 공통 컴포넌트 연결이 확인된다. 폭발 범위, 충돌 LayerMask, 애니메이션 및 파괴 타이밍의 테스트 씬 검증은 필요하다.

---

## 3. 월드 전환 및 오브젝트 존재 구조 개선 — **개선 / 진행 중**

월드 전환 시 “존재 여부”와 “같은 오브젝트의 상태·외형 변화”가 별도 개념으로 정리되었다.

| 컴포넌트 | 적용 목적 |
|---|---|
| `WorldPresence.cs` | 오브젝트가 World A, World B 또는 양쪽에 존재하는지 결정 |
| `WorldSwitchable.cs` | 동일 오브젝트의 월드별 상태, Collider, Renderer, Visual을 전환 |
| `WorldPresenceRegistry.cs` | 비활성 상태를 포함한 WorldPresence 등록 및 일괄 갱신 |

`WorldPresence`는 Renderer, Collider, Rigidbody, 일반 Behaviour 및 몬스터 AI를 선택적으로 비활성화할 수 있다. 현재 `EyeballFly`, `Human_Box`, `Boomber`, `Stone`, `StoneTrigger`, `Shutter`, `WorldStateItem`, `GoalMarker`, `FlashTarget` 등의 프리팹에 실제 연결이 확인된다. 반면 Door, Button, Laser, Box용 메인 프리팹은 현재 구조에서 확인되지 않으므로 이 보고서에서는 적용 완료 대상으로 판단하지 않는다.

Player, Camera, UI, GameManager는 `WorldPresenceGuide.md`상 공통 유지 대상으로 분류되어 월드 존재 전환 대상에서 제외한다. 기획자는 오브젝트별로 `WorldAOnly`, `WorldBOnly`, `Both`를 선택할 수 있다.

여기서 “맵 타일은 월드 전환 대상이 아니다”라는 원칙은 **Tile Palette로 칠한 Visual Tilemap**에 적용된다. 현재 `Tile_Visual`, `Floor_Tile`, `Wall_Tile`, `Block_Tile` 프리팹에는 `WorldSwitchable`이 연결되어 있어, 개별 프리팹 기반 맵 조각의 World A/B 비주얼 전환은 별도로 지원한다. 즉, Palette 타일맵은 고정 시각 요소이고, 전환이 필요한 퍼즐·개별 프리팹만 선택적으로 월드 상태를 가진다.

---

## 4. 맵 제작 방식 정리 — **방향 확정 / 프리팹 정리 진행 중**

맵의 시각 요소와 실제 플레이 기능을 분리하는 방향이 문서와 프리팹에 반영되었다. 작은 Visual 타일마다 Collider를 붙이는 방식은 모서리 걸림, 기능 중복, LayerMask 설정 혼란을 만들 수 있으므로, 기능별 오브젝트를 따로 배치하도록 정리하였다.

### Visual Tilemap

- Tile Palette는 보이는 바닥, 벽, 배경, 장식 이미지를 빠르게 칠하는 용도로만 사용한다.
- Palette로 칠한 타일 자체에는 Player 충돌, 몬스터 인식 차단, Light 차단, World 전환 기능을 부여하지 않는다.
- 실제 파일로 `Tiles/Background`, `Tiles/Decoration`, `Tiles/Ground`, `Tiles/Wall` 아래의 Tile Asset과 Visual Tilemap 프리팹이 존재한다.

### Function Object

- `Floor_Collision.prefab`: Player와 지상 몬스터가 실제로 밟는 보이지 않는 바닥 충돌
- `Wall_Tile.prefab`: 이동과 시야를 막는 벽
- `Block_Tile.prefab`: 반복 배치하는 고체 구조 및 감지 차단용 블록
- `Floor_Tile.prefab`, `Tile_Visual.prefab`: Collider 없이 시각 요소를 배치하는 프리팹

메인 `Prefabs/Map`에는 `DetectionBlock`과 `LightSightBlock`이라는 이름의 전용 프리팹이 현재 존재하지 않는다. 관련 자동 기능 프리팹은 `_Deprecated/MapTilemapAutoCollision` 또는 이전 구조에만 남아 있으므로 완성된 메인 제작 자산으로 계산하지 않는다. 현재는 `Wall_Tile`·`Block_Tile`과 적절한 LayerMask를 이용하는 방식이 실제 사용 가능한 경로이다.

Tilemap 기반 자동 Collider 생성 및 기능 Tile Palette 구조는 `_Deprecated/MapTilemapAutoCollision`로 분리되어 현재 제작 방식에서 제외되었다. 다만 `TilemapTo3DBoxColliderBaker.cs` 등 관련 스크립트가 메인 Scripts/Map에도 남아 있어, 사용 중단 범위를 후속 정리할 필요가 있다.

---

## 5. 씬 연결 시스템 개선 — **구현 완료, 왕복 테스트 필요**

메트로베니아 구조에 맞춰 한 씬에 여러 출구를 둘 수 있고, 입장한 방향에 따라 서로 다른 SpawnPoint로 이동하도록 구성되었다.

- `StageExitTrigger.cs`: 다음 씬 이름과 도착 SpawnPoint ID를 저장하고, 즉시 이동 또는 상호작용 키 방식으로 씬 전환 요청
- `SceneLoader.cs`: Build Settings 등록 여부를 확인한 뒤 비동기 씬 로드 및 Player 위치 이동
- `PlayerSpawnPoint.cs`: 씬 안의 도착 지점을 문자열 ID로 식별하고 중복 ID를 검사
- `StageExitTriggerEditor.cs`, `PlayerSpawnPointEditor.cs`: Inspector 검증과 기획자용 안내 제공
- `SceneConnectionGuide.md`: 다중 출구와 양방향 연결 절차 문서화
- `SceneLoader.prefab`, `StageExitTrigger.prefab`, `PlayerSpawnPoint.prefab`: 실제 배치용 프리팹 제공

연결 예시는 다음과 같다.

```text
Stage_01 오른쪽 출구
nextSceneName = Stage_02
targetSpawnPointId = From_Stage01_Right

Stage_02 도착 지점
spawnPointId = From_Stage01_Right

Stage_02 → Stage_01 되돌아가기
targetSpawnPointId = From_Stage02_Left
```

씬 이름이 Build Settings에 없거나 SpawnPoint ID가 비어 있거나 일치하지 않을 때 예외로 게임을 중단시키지 않고 Warning을 출력한다. SpawnPoint를 찾지 못하면 `Default` 지점을 한 번 더 찾고, 그것도 없으면 Player의 기존 위치를 유지한다. 코드·프리팹·문서 구성은 완료되었으나, 컴파일 상태가 확정된 뒤 실제 다중 출구·양방향 이동 테스트가 필요하다.

---

## 6. 기획자 친화적인 Inspector 구조 — **개선**

주요 시스템에 `Header`, `Tooltip`, Debug 로그 및 Scene View Gizmo 옵션이 추가되어 설정 목적과 결과를 Inspector에서 확인할 수 있다.

- `MonsterDetection`: Player·Light 감지 거리, 추적 유지 거리, 일반/Light 전용 시야 차단 LayerMask
- `MonsterMovement`: Ground/Flying 방식, 이동·귀환 속도, 정지 거리, 장애물·지면 LayerMask
- `MonsterAttack`: 공격 활성화, 범위, 피해량, 공격 간격
- `WorldPresence`: World A/B/Both, Renderer·Collider·Rigidbody·AI 적용 범위
- `StageExitTrigger`: 다음 씬, 도착 SpawnPoint, 상호작용 키, 1회 작동 여부
- `PlayerSpawnPoint`: SpawnPoint ID와 중복 ID 확인

이를 통해 코드 수정 없이 프리팹과 씬 인스턴스의 값을 조절하며 테스트할 수 있다. 단, LayerMask와 프리팹 참조가 잘못되면 기능이 동작하지 않을 수 있으므로 Warning과 Gizmo를 함께 확인해야 한다.

---

## 7. 현재 파일 구조

아래는 보고 범위와 직접 관련된 실제 메인 구조이다. `_Deprecated`에는 이전 맵 제작 구조가 별도로 보관되어 있다.

```text
Assets/_Project/
├─ Scripts/
│  ├─ Core/
│  │  ├─ SceneLoader.cs
│  │  └─ PlayerSpawnPoint.cs
│  ├─ Player/
│  ├─ Enemies/
│  │  ├─ Common/
│  │  │  ├─ MonsterCore.cs
│  │  │  ├─ MonsterDetection.cs
│  │  │  ├─ MonsterMovement.cs
│  │  │  ├─ MonsterFacing.cs
│  │  │  ├─ MonsterAttack.cs
│  │  │  ├─ MonsterHealth.cs
│  │  │  ├─ MonsterAnimatorBridge.cs
│  │  │  └─ MonsterAIBase.cs
│  │  ├─ EyeballFly/
│  │  ├─ Human_Box/
│  │  └─ Boomber/
│  ├─ Objects/
│  │  ├─ Shutter/
│  │  └─ Stone/
│  ├─ Map/
│  │  └─ StageExitTrigger.cs
│  ├─ World/
│  │  ├─ WorldPresence.cs
│  │  ├─ WorldPresenceMode.cs
│  │  ├─ WorldPresenceRegistry.cs
│  │  └─ WorldSwitchable.cs
│  ├─ Editor/
│  ├─ UI/
│  └─ Data/
├─ Prefabs/
│  ├─ Core/SceneLoader.prefab
│  ├─ Player/Player.prefab
│  ├─ Enemies/
│  │  ├─ EyeballFly.prefab
│  │  ├─ Human_Box.prefab
│  │  └─ Boomber.prefab
│  ├─ Objects/
│  │  ├─ Shutter/
│  │  └─ Stone/
│  └─ Map/
│     ├─ Floor_Collision.prefab
│     ├─ Floor_Tile.prefab
│     ├─ Tile_Visual.prefab
│     ├─ Wall_Tile.prefab
│     ├─ Block_Tile.prefab
│     ├─ StageExitTrigger.prefab
│     └─ PlayerSpawnPoint.prefab
├─ Scenes/
├─ Art/
├─ Animations/
├─ Tiles/
├─ Data/
├─ Docs/
└─ _Deprecated/
```

현재 구조는 기존 `Scripts/Objects/Enemy`와 `Scripts/Scene`의 파일을 `Scripts/Enemies`, `Scripts/Core`, `Scripts/Map`으로 옮겨 정리하는 과정의 Git 변경이 다수 남아 있다. 실제 파일은 새 구조에 존재하지만 아직 커밋되지 않은 항목이 많으므로, 팀 공유 전 Unity 메타 파일과 이동 결과를 함께 점검해야 한다.

---

## 8. 현재 이슈

### 해결 및 재확인 필요 Error

| 파일 | 코드 | 원인 | 영향 | 해결·확인 방향 |
|---|---|---|---|---|
| `Scripts/Objects/Stone/StoneTrigger.cs` | `CS0246` | Unity Editor 로그에는 69행의 `PlayerController` 타입을 찾지 못했다는 기록이 남아 있음 | 해당 오류가 현재 컴파일에도 남아 있다면 Play Mode 진입 불가 | 현재 저장된 소스에는 `PlayerController` 직접 참조가 없고 Tag/Layer 방식으로 변경되어 있다. Unity가 변경 파일을 다시 컴파일한 뒤 Console에서 오류가 사라졌는지 확인해야 함 |

즉, 오류 원인에 해당하는 코드는 현재 파일에서 제거된 것으로 보이지만, 최신 성공 컴파일 기록은 확인되지 않았다. 따라서 보고 시점 상태는 **“소스상 수정 반영, Unity 재컴파일 확인 대기”**로 판단한다. Stone 기능은 전체 시연 안정화보다 우선순위가 낮으므로, 컴파일 확인 후 세부 동작 보완은 후순위로 둔다.

### 경고 Warning

| 파일 | 코드 | 영향 | 추후 정리 방향 |
|---|---|---|---|
| `Core/PlayerSpawnPoint.cs` | `CS0618` | Play Mode를 막지는 않으나 오래된 `FindObjectsOfType` API 사용 | `Object.FindObjectsByType(..., FindObjectsSortMode.None)`로 교체 |
| `Core/SceneLoader.cs` | `CS0618` | 동일 | 정렬이 필요하지 않은 방식의 `FindObjectsByType`으로 교체 |
| `Map/StageExitTrigger.cs` | `CS0618` | Play Mode를 막지는 않으나 오래된 `FindObjectOfType` API 사용 | `FindFirstObjectByType` 또는 `FindAnyObjectByType`으로 교체 |
| `Data/EnemyTuningDatabase3D.cs` | `CS0414` | `displayName` 필드가 실행 기능에 사용되지 않음 | 기획 데이터로 사용할지 결정 후 연결 또는 제거 |
| `UI/MainMenuController.cs` | `CS0414` | `mode` 필드가 실행 기능에 사용되지 않음 | 실제 메뉴 상태에 연결하거나 불필요하면 정리 |

`CS0618`과 `CS0414`는 경고이므로 단독으로 Play Mode 진입을 막지는 않는다. 다만 API 정리와 미사용 필드 정리는 본 기능 검증 이후 수행하는 것이 적절하다.

### 추가 확인 사항

- 세 몬스터는 코드와 프리팹 연결이 확인되었지만 최신 통합 Play Mode 결과는 확인되지 않았다.
- `DetectionBlock`·`LightSightBlock` 전용 메인 프리팹이 없어 맵 제작 문서의 선택 항목과 실제 자산 구성을 일치시킬 필요가 있다.
- World A/B 비주얼 슬롯이 준비된 타일 프리팹과 “Palette 타일맵은 전환하지 않는다”는 규칙을 혼동하지 않도록 문서 설명을 유지해야 한다.
- 대규모 폴더 이동·삭제·신규 파일이 아직 Git 작업 트리에 남아 있어, 팀 병합 전에 메타 파일 누락과 프리팹 참조를 확인해야 한다.

---

## 9. 다음 작업 계획

1. Unity 재컴파일로 `StoneTrigger.cs`의 `CS0246`가 실제로 해소되었는지 확인하고 남은 Error를 제거한다.
2. Error가 없는 상태에서 Play Mode 진입 가능 여부를 확인한다.
3. `StageExitTrigger`와 `PlayerSpawnPoint`를 이용해 다중 출구, 잘못된 ID, Stage 간 왕복 이동을 테스트한다.
4. Tile Palette는 Visual 전용으로 유지하고, `Floor_Collision`, `Wall_Tile`, `Block_Tile` 배치 규칙과 메인 프리팹 구성을 최종 정리한다.
5. EyeballFly, Human_Box, Boomber를 동일 테스트 씬에 배치하여 감지 차단, 상태 전환, 공격·Stun·폭발을 검증한다.
6. Stone 함정 오브젝트는 핵심 시연 기능 안정화 후 후순위로 재검증한다.
7. 위 항목이 통과하면 월드 전환, 몬스터, 씬 이동을 한 흐름에서 보여 주는 시연용 테스트 씬을 구성한다.

---

## 종합 평가

2주차 이후의 핵심 성과는 개별 기능의 추가보다 **시스템을 기획자가 조절 가능한 공통 구조로 재정리한 것**에 있다. 몬스터 공통 컴포넌트와 세 종류의 상태 로직, WorldPresence와 WorldSwitchable의 역할 분리, Visual Tilemap과 기능 오브젝트의 분리, ID 기반 씬 연결 시스템이 실제 코드·프리팹·문서로 확인된다.

현재 단계는 주요 구현 파일과 배치 자산이 준비된 상태이며, “최종 완성”보다는 **구현 완료 후 통합 검증 단계**로 보는 것이 정확하다. 먼저 최신 컴파일 상태와 Play Mode 진입을 확정하고, 이후 씬 연결·맵 차단·몬스터 상호작용을 테스트 씬에서 순서대로 검증하는 것이 필요하다.
