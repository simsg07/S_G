# Object Guide

오브젝트 프리팹은 `Assets/_Project/Prefabs/Objects` 아래에서 찾습니다.

맵 타일과 달리, World 전환은 오브젝트 프리팹에서만 처리합니다.

## World 전환 가능 오브젝트

World 전환 기능을 붙일 수 있는 대상:

- Door
- Shutter
- Button
- Terminal
- Laser
- Box
- Stone
- MovingPlatform
- SpecialObject

기준:

- 실제 게임 규칙상 World A/B에 따라 상태나 외형이 바뀌어야 하는 오브젝트에만 `WorldSwitchable`을 붙입니다.
- 맵 타일 / Ground / Wall / Background / Decoration에는 `WorldSwitchable`을 붙이지 않습니다.
- 맵 타일에는 `MapTileWorldVisual`, `Visual_A`, `Visual_B` 구조를 만들지 않습니다.

## Stone

프리팹:

- `Assets/_Project/Prefabs/Objects/Stone/Stone.prefab`
- `Assets/_Project/Prefabs/Objects/Stone/StoneTrigger.prefab`

스크립트:

- `Assets/_Project/Scripts/Objects/Stone/StoneTrap.cs`
- `Assets/_Project/Scripts/Objects/Stone/StoneTrigger.cs`

역할:

- `Stone`: 천장에 매달렸다가 떨어지는 함정
- `StoneTrigger`: Player 진입 시 연결된 Stone을 떨어뜨리는 트리거

기획자가 만져도 되는 값:

- `StoneTrap.damage`
- `StoneTrap.damagePlayer`
- `StoneTrap.damageMonster`
- `StoneTrap.destroyTime`
- `StoneTrap.canBeControlledByShutter`
- `StoneTrigger.targetStone`

## Shutter / Button / Terminal

프리팹:

- `Assets/_Project/Prefabs/Objects/Shutter/Shutter.prefab`
- `Assets/_Project/Prefabs/Objects/Button/Button.prefab`
- `Assets/_Project/Prefabs/Objects/Terminal/Terminal.prefab`

역할:

- `Shutter`: 카메라/World 상태에 따라 작동하는 셔터
- `Button`: 연결된 오브젝트를 작동시키는 버튼
- `Terminal`: Player 상호작용으로 이벤트를 실행하는 터미널

주의:

- 기존 class 이름은 바꾸지 않습니다.
- World / Camera 연결 참조를 임의로 끊지 않습니다.
- World 전환이 필요하면 오브젝트 프리팹 쪽에 `WorldSwitchable`을 붙입니다.

## 기타

- `FlashTarget.prefab`: 카메라 플래시 테스트/대상
- `GoalMarker.prefab`: 목표 지점 표시
- `WorldStateItem.prefab`: World 상태에 따라 바뀌는 아이템

## 맵 타일과 오브젝트 구분

맵 타일:

- Tile Palette로 빠르게 배치합니다.
- 고정 지형 / 고정 시각 요소입니다.
- World 전환 대상이 아닙니다.

오브젝트:

- Prefab으로 따로 배치합니다.
- 상호작용, 상태 변화, World 전환을 담당합니다.
- 필요할 때만 `WorldSwitchable`을 사용합니다.
