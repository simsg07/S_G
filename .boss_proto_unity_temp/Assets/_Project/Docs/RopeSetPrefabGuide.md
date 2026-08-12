# Rope Set Prefab Guide

## 목적

Wire/Vine을 단독으로 연결하면 기획자가 `ConnectedObjectLink` 참조를 빠뜨리기 쉬우므로, 자주 쓰는 조합은 연결이 끝난 세트 프리팹으로 제공합니다.

## 프리팹 위치

- `Assets/_Project/Prefabs/Objects/RopeSets/Wire_Box_Set.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Wire_CircleSpike_Set.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Vine_Box_Set.prefab`
- `Assets/_Project/Prefabs/Objects/RopeSets/Vine_CircleSpike_Set.prefab`

각 세트의 루트 바로 아래에는 `CeilingAnchor`, `Rope`, `ConnectedObjectAttachPoint`, `ConnectedObject`, `Link`가 있습니다. Link는 연결 오브젝트와 AttachPoint를 미리 참조합니다.

## 천장 연결 구조

- `CeilingAnchor`: 줄 시작점
- `Rope`: Wire/Vine 로직, HitReceiver, RopeLengthController3D
- `Rope/Rope_Debug_Visual`: RopeHitCollider와 같은 위치·크기로 표시되는 불투명 Cube
- `Rope/RopeHitCollider`: 길이에 맞춰 늘어나는 3D 피격 Collider
- `ConnectedObjectAttachPoint`: 줄 끝점
- `ConnectedObject`: Box 또는 CircleSpike

## 줄 길이 조정 방법

1. `CeilingAnchor`를 천장 위치에 맞춥니다.
2. `ConnectedObjectAttachPoint`를 연결 오브젝트 위쪽에 맞춥니다.
3. `RopeLengthController3D > Apply Rope Length`를 실행합니다.
4. Rope_Debug_Visual과 RopeHitCollider가 두 점 사이에서 같은 위치·크기인지 확인합니다.

`Update On Validate`는 기본 OFF입니다. `Update On Start`는 Debug Visual/Collider만 갱신하며 ConnectedObject 위치·크기는 변경하지 않습니다. 완전 수동 값을 유지하려면 `Update On Start`도 끕니다.

## Wire / Vine 시각 표현 변경

Wire와 Vine은 더 이상 Sprite 이미지로 표시하지 않습니다. 기존 이미지 파일은 삭제하지 않았으며, 프리팹의 SpriteRenderer를 비활성화하고 Sprite 참조를 해제했습니다. 현재는 기능 확인과 기획자 배치를 위해 `RopeHitCollider`와 같은 크기의 불투명 Cube `Rope_Debug_Visual`을 Game View에 표시합니다.

- Wire: `MAT_Debug_Wire` 불투명 Material
- Vine: `MAT_Debug_Vine` 불투명 Material
- 실제 피격 판정: `RopeHitCollider`의 3D BoxCollider
- `Rope_Debug_Visual`: MeshRenderer만 사용하는 시각 요소이며 중복 Collider 없음

## 연결 오브젝트 크기 조정 방법

1. `ConnectedObject`의 Transform Scale 또는 Visual/Collider를 조정합니다.
2. `ConnectedObjectLink` 참조는 그대로 둡니다.
3. RopeLengthController는 ConnectedObject 크기를 읽거나 변경하지 않습니다.
4. 줄 끝 위치가 달라져야 하면 `ConnectedObjectAttachPoint`만 다시 맞추고 `Apply Rope Length`를 실행합니다.

Detach 시에도 `Preserve Connected Object Scale`이 작성된 Local Scale을 보존합니다. Play 시작 시 크기를 원상복구하지 않습니다.

## 사용 방법

1. 원하는 세트 프리팹을 씬에 배치합니다.
2. `CeilingAnchor`와 `ConnectedObjectAttachPoint` 위치를 맞춥니다.
3. `Apply Rope Length`를 실행하고 `ConnectedObject` 위치와 크기를 조정합니다.
4. Wire는 `Max Hit Count = 2`, Vine은 `Max Hit Count = 1`인지 확인합니다.
5. `Link`의 `ConnectedObjectLink`가 `ConnectedObject`를 가리키는지 확인합니다.
6. `ConnectedObjectLink`의 Context Menu에서 `Validate Link Setup`을 실행합니다.
7. 몬스터 공격으로 Rope가 끊기고 연결 오브젝트가 낙하하는지 테스트합니다.

Box 세트의 `GravityDropSensor`는 꺼져 있습니다. 따라서 플레이어 감지가 아니라 Rope 절단으로만 낙하합니다. 위치를 바꿀 때는 세트 연결을 해제하지 말고 각 자식의 Local Transform을 조정합니다.

## 단독 프리팹과 세트 프리팹 차이

- `Wire.prefab` / `Vine.prefab`: 개발자용 또는 특수 연결용이며 수동 연결이 필요합니다.
- `RopeSets` 프리팹: 기획자용이며 기본 연결과 3D 낙하 설정이 완료되어 있습니다.

## ConnectedObjectLink 연결 방식

Wire/Vine 절단 시 `ActivateConnectedObject()`가 호출됩니다. 연결된 Behaviour가 `ITriggerableObject`이면 `TriggerObject()`를 우선 호출합니다. 대상이 인터페이스를 제공하지 않고 `Release Physics On Cut`이 켜져 있으면 `FallingBoxObject`, `GravityObject3D`, 3D `Rigidbody` 순서로 찾아 해제합니다. 대상이 없거나 기능이 없으면 Warning만 남기며 연결 오브젝트를 Destroy하지 않습니다.

## CircleSpike 처리

CircleSpike는 `CircleSpikeObject`, `GravityObject3D`, `GravityObjectDamageDealer`, 3D `Rigidbody`/`Collider`로 구현되어 있습니다. 절단 전에는 kinematic/gravity off 상태이고 절단 후 3D 중력으로 낙하합니다. `Visual`은 교체 가능한 임시 3D 메시이며 새 이미지 에셋은 생성하지 않았습니다.

## 테스트 순서

1. `Wire_Box_Set`을 배치하고 `Rope`를 선택합니다.
2. `WireObject > Test Hit`을 한 번 실행해 손상 상태를 확인합니다.
3. 다시 한 번 실행해 Wire 절단과 FallingBox 낙하를 확인합니다.
4. `Vine_Box_Set`은 `Test Hit` 한 번으로 절단·낙하하는지 확인합니다.
5. CircleSpike 세트도 같은 순서로 낙하를 확인합니다.
6. 구조 자동 검사는 `Tools > Project > Objects > Validate Rope Set Prefabs`를 실행합니다.

자동 검사는 Anchor/Rope/AttachPoint/ConnectedObject/Link 계층, 긴 줄의 Debug Visual/Collider 일치, Sprite 미사용, 불투명 Material, 연결 오브젝트 Scale 독립성, 3D Rigidbody 낙하와 Reset 흐름을 확인합니다.

## 주의

- `Rigidbody2D`와 `Collider2D`를 사용하지 않습니다.
- Wire/Vine 이미지 에셋 파일은 유지하지만 프리팹에서는 사용하지 않습니다.
- `Rope_Debug_Visual`은 기능 확인용이며 실제 판정은 `RopeHitCollider`가 담당합니다.
- 연결 대상이 없으면 낙하하지 않으며 Warning만 출력합니다.
- 줄 길이를 늘려도 HitReceiver는 Rope에 유지되며, ConnectedObject Scale을 바꿔도 Link 참조는 유지됩니다.
- 기존 PlayerController, MonsterAI, Camera, WorldSwitch, SceneTransition, Crane 시스템은 수정하지 않습니다.
- 세트를 다시 생성하려면 `Tools > Project > Objects > Create Or Update Rope Set Prefabs`를 사용합니다. 기존 자산을 중복 생성하지 않고 같은 경로를 보완합니다.
