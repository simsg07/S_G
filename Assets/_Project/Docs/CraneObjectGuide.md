# Crane Object Guide

## 동작 개요

Crane은 자유 상하좌우 조작 오브젝트가 아니라 `Point_A`와 `Point_B` 사이만 왕복하는 3D 물리 기반 이동 플랫폼이다. 실제 사용은 `Crane_Set` 하나를 강제하지 않고, 부분별 프리팹을 씬에 따로 배치한 뒤 Inspector에서 연결한다. 기존 스위치 오브젝트인 `Lever`의 `CraneLeverSwitch`가 명령을 보내면 현재 위치에서 더 가까운 끝점의 반대편으로 이동한다. 이동 중 재입력 허용 옵션을 켜면 현재 목표를 반대로 바꾼다.

`CraneRailPath3D`가 직선 구간에 위치를 제한하고, `CraneObject`는 kinematic `Rigidbody.MovePosition`으로 이동한다. Z 위치와 회전은 고정된다. Wire와 CarryPlatform은 `Crane_Body` 자식이므로 함께 이동한다.

Lever와 Crane 사이의 전선은 `CableVisual/CraneCableVisual3D`가 LineRenderer로 표시한다. 시작점은 `Lever/CablePoint`, 끝점은 `Crane/CablePoint`이며 Crane이 이동하면 끝점도 매 프레임 갱신된다. 이 전선은 시각 요소 전용이며 로프 물리 시뮬레이션을 하지 않는다.

## Crane은 부분별로 연결해서 사용한다

`Crane_Set`은 샘플/테스트용으로만 둔다. 실제 맵 배치는 아래 부분 프리팹을 씬에 따로 배치해서 연결한다.

- `Assets/_Project/Prefabs/Objects/Dynamic/Crane/Crane_RailPath.prefab`
- `Assets/_Project/Prefabs/Objects/Dynamic/Crane/Crane_RailVisual.prefab`
- `Assets/_Project/Prefabs/Objects/Dynamic/Crane/Crane_Body.prefab`
- `Assets/_Project/Prefabs/Objects/Dynamic/Crane/Lever.prefab`
- `Assets/_Project/Prefabs/Objects/Dynamic/Crane/Crane_CableVisual.prefab`

분리 이유는 레일 길이를 맵마다 다르게 잡기 쉽고, Lever 위치를 자유롭게 배치할 수 있으며, 전선 연결도 상황에 맞게 조정할 수 있기 때문이다. `Crane_Body`만 실제 이동하고 나머지는 이동 범위, 장식, 제어, 연결선 역할로 나뉜다.

## 부분별 연결 방법

1. `Crane_RailPath`를 배치하고 `Point_A`, `Point_B`를 이동 범위 끝에 둔다.
2. `Crane_RailVisual`을 배치하고 `CraneRailVisualBuilder3D.pointA`, `pointB`에 같은 `Point_A`, `Point_B`를 연결한다.
3. `CraneRailVisualBuilder3D > Rebuild Rail Visual`을 실행해 rail.psd 조각을 반복 배치한다.
4. `Crane_Body`를 배치하고 `CraneObject.railPath`에 `Crane_RailPath`의 `CraneRailPath3D`를 연결한다.
5. `Lever`를 원하는 위치에 배치하고 `CraneLeverSwitch.targetCrane`에 `Crane_Body`의 `CraneObject`를 연결한다.
6. `Crane_CableVisual`을 배치하고 `startPoint`는 `Lever/CablePoint`, `endPoint`는 `Crane_Body/CablePoint`로 연결한다.
7. `CraneCableVisual3D > Refresh Cable`을 실행한다.

## 이미지 적용 방법

- `Assets/_Project/Art/Objects/Dynamic/Crane/rail.psd`: `Crane_Rail/Rail_Visual`의 SpriteRenderer
- `Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd`: `Crane/Visual`의 SpriteRenderer
- `Assets/_Project/Art/Objects/Dynamic/Crane/lever_01.png`: `Lever/Visual`의 SpriteRenderer

Downloads 폴더는 Unity AssetDatabase 범위 밖이므로 직접 참조하지 않는다. rail/crane 두 파일은 PNG로 변환하지 않고 다운로드 원본 PSD를 위 Assets 경로에 넣어 사용한다. 현재 2D PSD Importer가 각 레이어를 `Texture Type = Sprite (2D and UI)`, `Sprite Mode = Multiple`로 임포트한다. Unity에서 Sprite가 비어 보이면 PSD를 선택해 Sprite 설정을 확인한 뒤 Apply/Reimport한다.

PSD가 Project 창에 보이지 않거나 Sprite 하위 에셋이 생성되지 않으면 `Window > Package Manager > Unity Registry > 2D PSD Importer`가 설치되어 있는지 확인한다. 이후 `Tools > _Project > Crane > Repair PSD And Lever References`를 실행하면 실제 PSD 하위 Sprite와 Lever의 Crane 참조를 다시 저장한다.

`Tools > _Project > Crane > Create Or Update Part Prefabs`를 실행하면 부분별 프리팹을 생성/갱신한다. `Tools > _Project > Crane > Repair PSD And Lever References`는 기존 `Crane_Set` 샘플 프리팹의 이미지와 참조를 보정하는 용도다.

## Crane이 안 보일 때 확인

1. 두 PSD가 `Assets/_Project/Art/Objects/Dynamic/Crane` 안에 있는가?
2. `Rail_Visual`과 `Crane/Visual`의 SpriteRenderer에 Sprite가 연결되어 있는가?
3. SpriteRenderer가 Enabled 상태인가?
4. Visual GameObject가 Active 상태인가?
5. Transform Scale이 0이 아닌가?
6. Sorting Order가 배경보다 앞인가?
7. Main Camera Culling Mask에 Crane GameObject의 Layer가 포함되는가?

## Lever 상호작용 확인

1. Lever에 `CraneLeverSwitch`가 붙어 있는가?
2. `targetCrane`에 같은 씬의 `CraneObject`가 연결되어 있는가?
3. Lever의 BoxCollider가 Trigger인가?
4. `playerLayerMask`를 사용할 경우 Player Layer가 포함되어 있는가?
5. LayerMask를 비워 두었다면 Player Tag가 `Player`인가?
6. Play Mode에서 `Test Interact`로 Crane이 움직이는가?
7. Player가 Trigger 근처에서 F키로 작동할 수 있는가?

## Cable / Wire가 안 보일 때 확인

1. `CableVisual` 오브젝트가 Active인가?
2. `CableVisual`에 LineRenderer와 `CraneCableVisual3D`가 붙어 있는가?
3. LineRenderer Width가 0이 아닌가?
4. `startPoint`가 `Lever/CablePoint`, `endPoint`가 `Crane/CablePoint`로 연결되어 있는가?
5. LineRenderer Material이 비어 있다면 `Tools > _Project > Crane > Repair PSD And Lever References`로 기본 `CraneCableLine` Material을 다시 만들었는가?
6. Sorting Order가 배경 뒤에 있지 않은가?
7. Crane이 움직일 때 `Crane/CablePoint`가 따라 움직이는가?
8. `HangingWireVisual`과 `CarryPlatform`이 Crane의 child인가?

## Crane_Set 설치

1. `Assets/_Project/Prefabs/Objects/Crane/Crane_Set.prefab`을 씬에 배치한다.
2. `Crane_Rail/Point_A`, `Point_B`를 원하는 레일 끝으로 이동한다.
3. `CraneObject`의 시작 지점과 `Snap To Start Point`를 설정한다.
4. 프로젝트 Layer 정책에 맞게 `Obstacle Layer Mask`와 `Carry Layer Mask`를 직접 설정한다. 코드가 Layer/Tag를 자동 변경하지 않는다.

## Point_A / Point_B

두 Transform만 이동해 왕복 범위를 조정한다. Scene View의 노란 선과 원이 경로를 표시한다. Crane은 `Clamp Position`을 통해 이 선분 밖으로 나가지 않는다. 복잡한 다중 경로는 지원하지 않는다.

## Lever 연결 방법

크레인 스위치는 별도 `CraneSwitch` 프리팹이 아니라 기존 이름인 `Lever`를 사용한다. Lever의 `CraneLeverSwitch.targetCrane`에 씬의 `Crane_Body/CraneObject`를 연결한다. 기본 Trigger Collider는 기존 `PlayerInteraction3D`가 F키로 `IInteractable3D`를 찾을 수 있게 하며, Trigger 안에서는 `Use Fallback Input`을 켰을 때 자체 F키 fallback도 사용할 수 있다. 같은 프레임의 중복 입력은 한 번만 처리한다.

`Can Use While Crane Moving`이 꺼져 있으면 이동 중 입력을 무시한다. Animator와 `Activate` Trigger는 선택 사항이며 비어 있거나 해당 파라미터가 없어도 오류 없이 동작한다. Lever를 Crane_Set 밖에 따로 배치했다면 씬 인스턴스에서 `targetCrane`을 다시 연결해야 한다.

Lever와 Crane 연결선은 `Crane_CableVisual`을 사용한다. `CraneCableVisual3D.startPoint`는 `Lever/CablePoint`, `endPoint`는 `Crane_Body/CablePoint`로 연결한다. `Update Every Frame`이 켜져 있어야 Crane 이동 중 선 끝점이 계속 따라간다.

## CarryZone 설정

`Crane/CraneCarryZone3D`에서 다음을 조정한다.

- `Carry Layer Mask`: Player와 운반 가능한 Box Layer
- `Zone Center Offset`: 발판 위 운반 판정 중심
- `Zone Size`: Player와 여러 오브젝트를 동시에 포함할 크기
- `Trigger Interaction`: Trigger 포함 여부

청록색 Gizmo 안에 들어온 대상은 Crane의 프레임 이동량만큼 함께 이동한다. PlayerController는 수정하지 않는다.

하단 와이어와 발판은 `Crane_Body/HangingWireVisual`, `Crane_Body/CarryPlatform`처럼 반드시 `Crane_Body` 자식으로 둔다. Transform 상속으로 Crane 몸체와 같이 움직이므로 별도 로프 물리나 PlayerController 수정이 필요 없다.

## 장애물 감지

`CraneObject`의 `Obstacle Layer Mask`, `Obstacle Check Center Offset`, `Obstacle Check Size`, `Obstacle Check Padding`을 설정한다. 이동 방향으로 BoxCast하여 해당 Layer의 장애물을 만나면 목표 이동을 정지하고 `IsBlocked` 상태가 된다. LayerMask가 0이면 장애물 검사를 하지 않는다.

## 셔터 정지/재개

Crane은 `IShutterFreezable3D.ApplyShutterFreeze`를 구현하므로 기존 셔터 탐색 구조에서 직접 정지할 수 있다. 외부 이벤트에서는 다음 public API를 사용할 수 있다.

- `PauseByShutter()` / `ResumeByShutter()`
- `SetShutterPaused(bool)`
- `StopMovement()`

`Can Pause By Shutter`와 `Resume Target After Pause`를 켜면 정지 시간이 끝난 뒤 기존 A/B 목표를 향해 계속 이동한다.

## 테스트

1. rail/crane PSD가 `Assets/_Project/Art/Objects/Dynamic/Crane`에 있는지 확인한다.
2. `Crane_Rail/Rail_Visual`과 `Crane/Visual`에서 각각 rail/crane 이미지가 보이는지 확인한다.
3. `Crane_RailPath`를 테스트 씬에 배치하고 Point_A/B를 조정한다.
4. `Crane_RailVisual`을 배치하고 같은 Point_A/B를 연결한 뒤 `Rebuild Rail Visual`을 실행한다.
5. `Crane_Body`를 배치하고 `CraneObject.railPath`를 연결한다.
6. `CraneObject > Test Move To B`, `Test Move To A`를 차례로 실행한다.
7. `Lever`를 배치하고 `CraneLeverSwitch.targetCrane` 연결을 확인한다.
8. `CraneLeverSwitch > Test Interact`를 두 번 실행해 A→B, B→A 왕복을 확인한다.
9. `Crane_CableVisual`을 배치하고 `Lever/CablePoint`, `Crane_Body/CablePoint`를 연결한 뒤 `Refresh Cable`을 실행한다.
10. Crane 이동 중 CableVisual 끝점과 하단 와이어/발판이 함께 움직이는지 확인한다.
11. Player와 Box Layer를 `Carry Layer Mask`에 넣고 발판 위에 동시에 배치해 함께 이동하는지 확인한다.
12. 장애물 Layer를 `Obstacle Layer Mask`에 넣고 경로 앞에 두어 정지하는지 확인한다.
13. 이동 중 셔터 또는 `PauseByShutter`를 적용하고, 해제 후 같은 목표로 재개하는지 확인한다.

모든 물리 구성은 `Rigidbody`, `Collider`, `BoxCollider`, `Physics`를 사용한다. Rigidbody2D/Collider2D는 사용하지 않는다.
# 기능 테스트 모드

현재 Crane은 이미지 없이 기능 확인용 불투명 오브젝트로 테스트한다.

- Crane_Body는 불투명 Cube로 표시한다.
- RailPath는 Point_A / Point_B와 Rail_Debug_Visual로 표시한다.
- Lever는 기존 Lever 또는 불투명 Debug Visual로 표시한다.
- CarryPlatform은 반드시 눈에 보이는 불투명 발판으로 표시한다.
- 투명 Collider만 있는 오브젝트는 사용하지 않는다.

## 이미지 적용은 이후 단계

- rail.psd / crane.psd 연결은 기능 검증 이후 진행한다.
- 기능 테스트 단계에서는 SpriteRenderer 이미지 연결을 하지 않는다.
- 먼저 이동, 상호작용, 운반 기능이 작동하는지 확인한다.

## 상부 레일과 캐빈 위치 관계

- `Rail_Point_A`와 `Rail_Point_B`는 위쪽 레일의 이동 기준점이다.
- 실제 `Crane_Cabin` 목표 위치는 Rail Point에 `(0, cabinYOffset, 0)`을 더해 계산한다.
- `cabinYOffset`은 레일에서 캐빈까지의 수직 거리이며 일반적으로 음수로 설정한다.
- Rail 목표점과 Cabin 목표점은 X 좌표를 공유하고 Y 좌표만 `cabinYOffset`만큼 다르다.
- 캐빈 위치를 상부 Rail 선분에 직접 Clamp하지 않는다.

## Crane 이미지 적용 방식

기능이 완성된 뒤 이미지는 기능 오브젝트의 자식으로 연결한다.

- Rail 이미지는 `CraneRailVisualBuilder3D`가 기존 `Point_A` / `Point_B` 사이에 자동 생성한다.
- Rail Left End는 `leftEndSprite`, Middle은 `middleSprite`, Right End는 `rightEndSprite`에 연결한다.
- Crane Cabin 이미지는 `Crane/CabinVisual`의 `SpriteRenderer`에 연결한다.
- Cable/Wire는 `CraneCableVisual3D`의 `LineRenderer`가 레일 Y와 움직이는 캐빈 X를 연결한다.

## 레일 길이 조정 방법

1. `Crane_RailPath`의 `Point_A` / `Point_B` 위치를 조정한다.
2. `CraneRailVisualBuilder3D`의 pointA / pointB에 같은 Transform을 연결한다.
3. rail PSD의 `rail_start`, `rail_middle_01`, `rail_end`를 각각 left/middle/right에 연결한다.
4. `Rebuild Rail Visual`을 실행한다.
5. 생성된 조각이 Point_A와 Point_B 사이에 이어졌는지 확인한다.

## 줄/와이어 연결 방법

1. 캐빈 아래에 `CabinCablePoint`를 둔다.
2. `Crane_CableVisual`에 `CraneCableVisual3D`와 `LineRenderer`를 둔다.
3. cabinTransform, cabinCablePoint, railPath를 연결한다.
4. `Refresh Cable`을 실행한다.
5. Play 중 캐빈 이동에 맞춰 케이블의 X 좌표가 갱신되는지 확인한다.

레일 이미지와 케이블은 장식이며 이동 범위나 물리에 관여하지 않는다. 이미지 참조가 없어도 Crane 기능은 계속 작동한다.

## Crane 시각 구조

- `RailVisualRoot`: Point_A / Point_B 사이에 고정되는 레일 이미지
- `TrolleyVisual`: 캐빈 X를 따라가면서 레일 Y에 붙는 상단 이동 장치
- `UpperConnectorVisual`: Trolley 아래에서 케이블로 연결되는 후크/연결부
- `CableVisual`: TrolleyCablePoint와 CabinCablePoint를 잇는 LineRenderer
- `CabinVisual`: 플레이어가 타는 캐빈 이미지

레일은 Crane_Cabin의 자식이 아니다. TrolleyVisual, UpperConnectorVisual, CableVisual, CabinVisual은 캐빈의 시각 자식이며 `CraneMovingVisualAligner3D`가 위치만 정렬한다. 이 스크립트는 CraneObject 위치를 변경하지 않는다.

### 시각이 안 보일 때 확인

1. TrolleyVisual과 UpperConnectorVisual이 active인지 확인한다.
2. SpriteRenderer가 enabled이고 Scale이 0이 아닌지 확인한다.
3. 기존 PSD의 `Hoist`, `hook`, `cable car` Sprite가 연결됐는지 확인한다.
4. Sorting Order가 Rail보다 높은지 확인한다.
5. Aligner의 railPath, TrolleyCablePoint, CabinCablePoint 연결을 확인한다.

## 최종 단순 시각 구조

레일 이미지는 기능 좌표와 분리된 `Crane_Rail_Static` 장식 오브젝트로 수동 배치한다. `Point_A` / `Point_B`는 계속 이동 범위만 담당한다. `CraneRailVisualBuilder3D`와 `RailVisualRoot`는 이전 자동 생성 작업을 위해 남겨 두지만 기본값은 비활성화하며 Rebuild 흐름을 사용하지 않는다.

움직이는 Crane 자식은 다음 SpriteRenderer로 구성한다.

- `TrolleyVisual`: crane.psd의 `Hoist`
- `RopeOrCableVisual`: crane.psd의 `wire`
- `HookVisual`: crane.psd의 `hook`
- `CabinVisual`: crane.psd의 `cable car`

모두 Crane 자식이므로 별도 위치 갱신 없이 캐빈과 같은 X 속도로 움직인다. LineRenderer는 기본 비활성화하고 최종 시각에는 사용하지 않는다. 기획자는 `Crane_Rail_Static`의 위치/Scale과 네 시각 자식의 Local Position/Scale을 씬 이미지 비율에 맞춰 수동 조정한다.
