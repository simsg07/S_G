# Data Only Object / Monster Guide

## 목적

몬스터와 오브젝트 설정값을 여러 기능 스크립트 Inspector에 흩어두지 않고, Data asset 하나로 모아 관리하기 위한 1차 기반입니다.

이번 단계는 기존 구조를 삭제하거나 교체하지 않습니다. 기존 `MonsterDetection`, `MonsterMovement`, `MonsterAttack`, `MonsterHealth`, `HitReceiver`, `DamageDealer`, `GravityObject3D` 같은 실행 컴포넌트는 그대로 두고, 새 DataDriven Controller가 Data asset 값을 읽어 해당 컴포넌트에 적용합니다.

## 생성된 구조

스크립트:

- `Assets/_Project/Scripts/DataDriven/Monster/MonsterData.cs`
- `Assets/_Project/Scripts/DataDriven/Monster/DataDrivenMonsterController.cs`
- `Assets/_Project/Scripts/DataDriven/Object/ObjectData.cs`
- `Assets/_Project/Scripts/DataDriven/Object/DataDrivenObjectController.cs`

예시 Data:

- `Assets/_Project/Data/Monsters/EyeballFlyData.asset`
- `Assets/_Project/Data/Monsters/HumanBoxData.asset`
- `Assets/_Project/Data/Monsters/BoomberData.asset`
- `Assets/_Project/Data/Objects/WireData.asset`
- `Assets/_Project/Data/Objects/VineData.asset`
- `Assets/_Project/Data/Objects/StoneData.asset`
- `Assets/_Project/Data/Objects/FallingBoxData.asset`
- `Assets/_Project/Data/Objects/UnstableTileData.asset`

## 데이터 중심 구조

기존 방식:

- 감지 거리는 `MonsterDetection`
- 이동 속도는 `MonsterMovement`
- 공격 범위와 데미지는 `MonsterAttack`
- 체력은 `MonsterHealth`
- 오브젝트 피격 횟수는 `HitReceiver`
- 낙하 속도는 `GravityObject3D`
- 파괴 지연은 `BreakableObject3D`

새 방식:

- 기획자는 `MonsterData` 또는 `ObjectData`를 수정합니다.
- 프리팹에는 `DataDrivenMonsterController` 또는 `DataDrivenObjectController`를 추가합니다.
- Controller의 Data 칸에 Data asset을 연결합니다.
- Play 또는 ContextMenu `Apply Data` 실행 시 기존 컴포넌트 값에 반영됩니다.

## MonsterData에 들어가는 값

`MonsterData`는 아래 설정을 한곳에 모읍니다.

- Identity: ID, 표시 이름, 몬스터 종류
- Stats: HP, 접촉 데미지
- Detection: Player/Light 감지 여부, 감지 거리, 추적 거리, 시야 차단 LayerMask
- Movement: 이동 타입, 이동 속도, 복귀 속도, 정지 거리, Z 고정, 중력 사용 여부
- Attack: 공격 타입, 데미지, 공격 범위, 쿨다운, 공격 지속 시간, HitReceiver 공격 여부
- Special: HumanBox 하울, Boomber 자폭/가속 같은 전용값을 담을 자리
- Animation: 공통 Animator 파라미터 이름
- Debug: 로그 출력 여부

현재 `DataDrivenMonsterController`가 실제로 자동 적용하는 값은 기존 공통 컴포넌트가 안정적으로 받을 수 있는 값입니다.

- `MonsterDetection`
- `MonsterMovement`
- `MonsterAttack`
- `MonsterHealth`
- `MonsterAnimatorBridge` debug 값

HumanBox 하울, Boomber 폭발 세부값, EyeballFly 전용 object attack 세부값은 Data에 자리를 만들어두었지만, 이번 1차 기반에서는 기존 개별 AI 내부 로직을 자동으로 전부 덮어쓰지는 않습니다.

## ObjectData에 들어가는 값

`ObjectData`는 아래 설정을 한곳에 모읍니다.

- Identity: ID, 표시 이름, 오브젝트 종류
- Target / Hit: 타겟 가능 여부, 최대 피격 횟수, 1회 트리거 여부
- Damage: 데미지, Player/Monster 피해 여부, 중복 피해 방지 여부
- Gravity: 중력 사용, 시작 고정, 수동 낙하 속도, X/Z 고정
- Pause: 카메라/셔터 정지 가능 여부
- Break: 파괴 방식, 파괴 지연, 제거 지연, Collider/Renderer 비활성화 여부
- Connection: 피격/최대 피격/파괴 시 수행할 액션의 설계값
- Debug: 로그 출력 여부

현재 `DataDrivenObjectController`가 실제로 자동 적용하는 값은 다음입니다.

- `HitReceiver.ConfigureHitRules`
- `DamageDealer.ConfigureDamage`
- `GravityObject3D.ConfigureGravity`
- `PausablePhysicsObject.ConfigurePause`
- `BreakableObject3D.ConfigureBreakable`
- `TriggerZone3D.ConfigureTrigger`

`ObjectTriggerAction`은 아직 설계값입니다. UnityEvent 자동 연결까지는 이번 단계에서 하지 않습니다. 연결 오브젝트는 기존처럼 `ConnectedObjectLink`, `OpenPathOnBreak`, `BreakableObject3D.onBreak`에서 수동 연결합니다.

## 몬스터 제작 방식

1. Project 창에서 `_Project/Data/Monster Data`로 새 MonsterData를 만들거나 예시 Data를 복사합니다.
2. 몬스터 프리팹에 `DataDrivenMonsterController`를 추가합니다.
3. `monsterData`에 Data asset을 연결합니다.
4. 기존 `MonsterDetection`, `MonsterMovement`, `MonsterAttack`, `MonsterHealth`가 프리팹에 있으면 자동으로 찾습니다.
5. Play 시 `applyOnAwake`가 켜져 있으면 값이 적용됩니다.
6. 에디터에서 즉시 확인하려면 컴포넌트 메뉴의 `Apply Monster Data`를 실행합니다.

## 몬스터 프리팹 1차 적용 현황

아래 몬스터 프리팹에는 `DataDrivenMonsterController`가 1차 적용되어 있습니다.

| Prefab | MonsterData | 연결된 기존 컴포넌트 |
| --- | --- | --- |
| `Assets/_Project/Prefabs/Enemies/EyeballFly.prefab` | `EyeballFlyData.asset` | `MonsterDetection`, `MonsterMovement`, `MonsterAttack`, `MonsterAnimatorBridge`, `EyeballFlyAI` |
| `Assets/_Project/Prefabs/Enemies/Human_Box.prefab` | `HumanBoxData.asset` | `MonsterDetection`, `MonsterMovement`, `MonsterAttack`, `MonsterAnimatorBridge`, `HumanBoxAI` |
| `Assets/_Project/Prefabs/Enemies/Boomber.prefab` | `BoomberData.asset` | `MonsterDetection`, `MonsterMovement`, `MonsterAttack`, `MonsterHealth`, `MonsterAnimatorBridge`, `BoomberBrain` |

`EyeballFly`와 `Human_Box`는 현재 공통 `MonsterHealth`가 아니라 각각 전용 체력/AI 구조를 사용합니다. 그래서 `DataDrivenMonsterController.health`는 비워두었고, 검증 시 Warning만 출력될 수 있습니다. 이 Warning은 현재 단계에서 정상입니다.

### 몬스터 프리팹 확인 방법

1. 대상 몬스터 프리팹을 엽니다.
2. `DataDrivenMonsterController`가 붙어 있는지 확인합니다.
3. `monsterData`가 해당 Data asset으로 연결되어 있는지 확인합니다.
4. 컴포넌트 메뉴에서 `Validate Monster Data Setup`을 실행합니다.
5. 필요한 경우 `Apply Monster Data`를 실행해 기존 컴포넌트 값에 Data 값을 반영합니다.

정상 로그 예시:

- `[DataDrivenMonsterController] MonsterData: EyeballFlyData`
- `[DataDrivenMonsterController] Detection found: MonsterDetection`
- `[DataDrivenMonsterController] Movement found: MonsterMovement`
- `[DataDrivenMonsterController] Attack found: MonsterAttack`
- `[DataDrivenMonsterController] Brain found: EyeballFlyAI`
- `[DataDrivenMonsterController] Validate complete. Missing optional components are warnings only.`

주의:

- `DataDrivenMonsterController`는 기존 몬스터 AI를 대체하지 않습니다.
- 기존 AI, Rigidbody, Collider, Animator Controller, Layer, Tag, WorldPresence 설정은 유지합니다.
- 누락된 선택 컴포넌트는 Error가 아니라 Warning으로 확인합니다.

## 오브젝트 제작 방식

1. Project 창에서 `_Project/Data/Object Data`로 새 ObjectData를 만들거나 예시 Data를 복사합니다.
2. 오브젝트 프리팹에 `DataDrivenObjectController`를 추가합니다.
3. `objectData`에 Data asset을 연결합니다.
4. 필요한 기존 공통 컴포넌트를 프리팹에 붙입니다.
5. Play 시 `applyOnAwake`가 켜져 있으면 값이 적용됩니다.
6. 에디터에서 즉시 확인하려면 컴포넌트 메뉴의 `Apply Object Data`를 실행합니다.

## 장점

- 기획자가 수정할 위치가 Data asset으로 모입니다.
- 같은 몬스터나 오브젝트의 변형을 Data 복사로 만들 수 있습니다.
- 기존 실행 스크립트를 삭제하지 않아 현재 구현을 크게 흔들지 않습니다.
- 보고할 때 “기능 스크립트 나열”이 아니라 “Data 중심 제작 구조”로 설명할 수 있습니다.

## 아직 자동화하지 않은 부분

- Controller가 없는 컴포넌트를 자동으로 추가하지 않습니다.
- Layer/Tag를 코드에서 자동 변경하지 않습니다.
- HumanBox, Boomber, EyeballFly 전용 세부 로직 전체를 Data로 완전히 이전하지 않았습니다.
- `ObjectTriggerAction`에 따른 UnityEvent 자동 연결은 아직 하지 않습니다.
- 기존 프리팹에 DataDriven Controller를 대량으로 자동 부착하지 않았습니다.

## 다음 단계 추천

1. `EyeballFly.prefab`, `Wire.prefab`, `Vine.prefab`에 Controller를 수동으로 붙여 Data 적용 흐름을 검증합니다.
2. 검증 후 각 몬스터 전용 AI에 Data 적용 메서드를 조금씩 추가합니다.
3. `ObjectTriggerAction`을 실제 연결 자동화로 확장합니다.
4. 기획자가 볼 Inspector를 더 줄이고 싶다면 기존 기능 컴포넌트는 접고 DataDriven Controller만 노출하는 Editor UI를 만듭니다.
