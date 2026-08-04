# World Presence Guide

## 목적

`WorldPresence`는 오브젝트가 World A / World B / Both 중 어디에 존재하는지 정하는 공통 컴포넌트입니다.

기존 `WorldSwitchable`과 역할이 다릅니다.

- `WorldPresence`: 이 오브젝트가 어느 월드에 존재하는지 결정합니다.
- `WorldSwitchable`: 같은 오브젝트가 월드 전환에 따라 상태나 비주얼이 바뀌는 경우에 사용합니다.

## 사용 가능 대상

- Monster
- Door
- Shutter
- Button
- Terminal
- Laser
- Box
- Stone
- MovingPlatform
- Trigger
- PuzzleObject
- Decoration

사용하지 않는 대상:

- Player
- Camera
- GameManager
- UI
- 맵 Tile Palette 타일
- 기능 Tilemap

## Presence Mode

### Both

World A / World B 모두 존재합니다. 기본값입니다.

### WorldAOnly

World A에서만 존재합니다. World B에서는 Renderer, Collider, Monster AI 등이 꺼집니다.

### WorldBOnly

World B에서만 존재합니다. World A에서는 Renderer, Collider, Monster AI 등이 꺼집니다.

## Inspector 주요 값

- `presenceMode`: Both / WorldAOnly / WorldBOnly 중 선택합니다.
- `affectRootActive`: 오브젝트 전체를 SetActive로 끌지 여부입니다. 기본값 false를 권장합니다.
- `affectRenderers`: 존재하지 않는 월드에서 Renderer를 끕니다.
- `affectColliders`: 존재하지 않는 월드에서 Collider를 끕니다.
- `affectBehaviours`: 직접 지정한 일반 스크립트를 켜고 끕니다.
- `affectMonsterAI`: Monster 감지/이동/공격/AI 계열 스크립트를 켜고 끕니다.
- `affectRigidbody`: 존재하지 않는 월드에서 Rigidbody 속도를 0으로 만들고 Kinematic으로 둡니다.

기본값:

- `presenceMode = Both`
- `affectRootActive = false`
- `affectRenderers = true`
- `affectColliders = true`
- `affectBehaviours = false`
- `affectMonsterAI = true`
- `affectRigidbody = true`

## WorldSwitchable과 차이

### WorldPresence만 쓰는 경우

- World A에만 있는 Monster
- World B에만 있는 Box
- World A에만 있는 Door

### WorldSwitchable만 쓰는 경우

- 두 월드 모두 존재하지만 A/B에 따라 모습이나 상태가 바뀌는 오브젝트
- A에서는 열린 문, B에서는 닫힌 문
- A에서는 켜진 Laser, B에서는 꺼진 Laser

### 둘 다 쓸 수 있는 경우

- 특정 월드에만 존재하면서, 그 월드 안에서 별도 상태 변화도 필요한 특수 오브젝트

## Monster 처리

Monster가 현재 월드에 존재하지 않으면:

- Renderer OFF
- Collider OFF
- MonsterDetection OFF
- MonsterMovement OFF
- MonsterAttack OFF
- MonsterAIBase 계열 OFF
- Rigidbody velocity 0
- Rigidbody isKinematic true

다시 존재하는 월드로 돌아오면 원래 켜져 있던 컴포넌트만 복원합니다.

MonsterHealth 또는 다른 Health 컴포넌트에 `IsDead`가 true이면, WorldPresence가 Monster AI를 다시 켜지 않습니다.

## 오브젝트 처리

일반 오브젝트가 현재 월드에 존재하지 않으면:

- Renderer OFF
- Collider OFF
- 지정한 Behaviour OFF
- Rigidbody velocity 0
- Rigidbody isKinematic true

상호작용 스크립트를 같이 끄고 싶다면 `affectBehaviours`를 켜고 `targetBehaviours`에 직접 넣습니다.

## WorldManager / WorldSystem 연동

`WorldManager`가 월드를 전환하면 모든 `WorldPresence`가 갱신됩니다.

기존 `WorldSystem3D`가 월드를 전환해도 모든 `WorldPresence`가 갱신됩니다.

`WorldPresenceRegistry`가 존재하므로, `affectRootActive = true`로 Root가 꺼진 오브젝트도 다음 전환 때 다시 켤 수 있습니다. 그래도 기본 사용은 Root Active를 끄지 않는 방식을 권장합니다.

## Gizmo 색상

- Both: 회색
- WorldAOnly: 파랑
- WorldBOnly: 보라

Scene View에서 `Both`, `World A`, `World B` 라벨로 구분합니다.

## 테스트 방법

### Door WorldAOnly

1. Door Root에 `WorldPresence`를 추가합니다.
2. `presenceMode = WorldAOnly`로 설정합니다.
3. World A에서는 Door가 보이고 Collider가 켜져야 합니다.
4. World B에서는 Door가 보이지 않고 Collider가 꺼져야 합니다.

### Box WorldBOnly

1. Box Root에 `WorldPresence`를 추가합니다.
2. `presenceMode = WorldBOnly`로 설정합니다.
3. World A에서는 Box가 비활성 상태처럼 보여야 합니다.
4. World B에서는 Box가 정상 동작해야 합니다.

### Monster WorldAOnly

1. EyeballFly Root에 `WorldPresence`를 추가합니다.
2. `presenceMode = WorldAOnly`로 설정합니다.
3. World A에서는 Player/Light 감지가 가능해야 합니다.
4. World B에서는 Renderer/Collider/Detection/Attack이 꺼져야 합니다.
5. World B에서 Player를 감지하거나 공격하면 실패입니다.

### Both

1. Stone Root에 `WorldPresence`를 추가합니다.
2. `presenceMode = Both`로 둡니다.
3. World A/B 모두에서 동일하게 존재해야 합니다.

### 기존 WorldSwitchable 유지

1. 기존 `WorldSwitchable` 오브젝트를 테스트합니다.
2. 월드 전환에 따른 상태/비주얼 변경이 기존처럼 동작해야 합니다.
3. `WorldPresence` 추가로 기존 `WorldSwitchable` 기능이 깨지면 안 됩니다.
