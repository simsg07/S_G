# Map Hierarchy Guide

## 왜 Hierarchy를 정리하는가

`Block_Tile`, `Wall_Tile`, `Floor_Collision` 같은 기능 프리팹은 맵 제작에 계속 사용한다. 다만 씬 루트에 같은 이름의 오브젝트가 많이 쌓이면 기획자가 월드 오브젝트, 전환 트리거, 충돌 오브젝트를 구분하기 어렵다.

이 가이드는 프리팹 방식은 유지하면서 Hierarchy만 보기 좋게 정리하는 기준을 설명한다. 정리 도구는 Transform, Layer, Tag, Collider 값을 바꾸지 않고 부모만 바꾼다.

## Map_Visual과 Map_Collision

`Map_Visual`은 눈에 보이는 맵 이미지와 시각용 Tilemap을 모아두는 루트다. Tile Palette는 빠르게 배경과 바닥 이미지를 칠하기 위한 visual-only 도구이며, 자동 Collider 방식으로 사용하지 않는다.

`Map_Collision`은 실제 플레이에 영향을 주는 3D Collider 프리팹을 모아두는 루트다. Player가 밟는 바닥, 이동을 막는 벽, 몬스터/빛 시야 차단용 오브젝트는 이 아래에 정리한다.

## 배치 기준

`Floor_Collision`은 Player가 실제로 밟는 바닥 충돌용 프리팹이다. 정리 후에는 `Map_Collision/Floor_Collisions` 아래에 둔다.

`Wall_Tile`은 벽이나 이동 차단용 타일 프리팹이다. 정리 후에는 `Map_Collision/Wall_Collisions` 아래에 둔다.

`Block_Tile`은 블록형 이동 차단 및 맵 충돌용 프리팹이다. 정리 후에는 `Map_Collision/Block_Collisions` 아래에 둔다.

`MonsterSightBlock` 또는 `DetectionBlock`은 몬스터 시야 차단용 오브젝트로 보고 `Map_Collision/MonsterSight_Blocks` 아래에 둔다.

`LightSightBlock`은 빛 시야 차단용 오브젝트로 보고 `Map_Collision/LightSight_Blocks` 아래에 둔다.

## Scene_Transitions

`Scene_Transitions`는 씬 이동과 시작 위치를 한곳에서 확인하기 위한 루트다.

`PlayerSpawnPoint` 이름의 오브젝트는 `Scene_Transitions/SpawnPoints` 아래에 둔다. 플레이어 시작 위치를 씬 전환 트리거와 분리해서 찾기 쉽게 하기 위함이다.

`StageExitTrigger` 이름의 오브젝트는 `Scene_Transitions/ExitTriggers` 아래에 둔다. 어느 지점이 다음 씬으로 이어지는지 기획자가 바로 확인할 수 있다.

## 메뉴 사용 방법

Unity 상단 메뉴에서 `_Project > Map > Organize Current Scene Hierarchy`를 실행한다.

메뉴를 실행하면 현재 열린 씬에서 이름 기준으로 정리 가능한 오브젝트를 찾아 아래 구조로 이동한다. 이미 필요한 부모 오브젝트가 있으면 재사용하고, 없으면 새로 만든다.

```text
AbilityTestScene
├─ Shared
├─ World_A_Current
├─ World_B_Past
├─ Map_Visual
├─ Map_Collision
│  ├─ Floor_Collisions
│  ├─ Wall_Collisions
│  ├─ Block_Collisions
│  ├─ MonsterSight_Blocks
│  └─ LightSight_Blocks
└─ Scene_Transitions
   ├─ SpawnPoints
   └─ ExitTriggers
```

## 정리 후 씬 저장

정리 도구는 씬을 자동 저장하지 않는다. Hierarchy를 확인한 뒤 문제가 없으면 `Ctrl+S` 또는 `File > Save`로 현재 씬을 저장한다.

정리 직후 되돌리고 싶으면 Unity의 Undo를 사용할 수 있다.

## 주의사항

정리 도구는 `Transform.SetParent(parent, true)` 방식으로 부모만 바꾼다. 월드 위치, 회전, 스케일은 유지된다.

Layer, Tag, Collider 설정은 변경하지 않는다. Prefab 연결도 끊지 않는다.

`World_A_Current` 또는 `World_B_Past` 안에 이미 들어가 있는 오브젝트는 무리하게 옮기지 않는다. 월드 전환용 배치가 이미 잡혀 있을 수 있기 때문이다.

Player, Camera, Managers는 자동으로 옮기지 않는다. 참조가 얽혀 있을 수 있으므로 필요하면 별도 확인 후 수동으로 정리한다.

기존 `TilemapTo3DBoxColliderBaker` 계열 자동 Collider 기능은 이 정리 도구와 관계없다. 현재 기준에서는 Tile Palette는 시각용, 실제 기능은 프리팹 Collider 방식으로 유지한다.
