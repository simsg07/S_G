# 2.5D 적용 메모

이 문서는 기존 작업물에 적용한 2.5D 보정과 앞으로 새 오브젝트를 만들 때 확인할 기준을 정리한다.

## 이미 코드에 반영된 내용

- Player는 `PlatformerPlayer3D`에서 Rigidbody에 Freeze Position Z와 Freeze Rotation을 적용한다.
- Player는 FixedUpdate 마지막에 Z축 위치와 Z축 속도를 다시 0으로 보정한다.
- Camera는 `CameraFollow3D`에서 Orthographic 사이드뷰로 고정한다.
- Camera는 플레이어의 Z축을 따라가지 않고, 지정된 카메라 Z 위치를 유지한다.
- Slime, RangedSlime, ThornSlime, BalloonSlime, SlimeHybe, 투사체는 이동 후 Z축을 0으로 보정한다.
- MonsterRuntime3D로 생성되는 몬스터/박스형 오브젝트는 Rigidbody 3D 제약을 통일한다.
- PlatformSurface3D와 DamageBlock3D는 생성/갱신 시 Z축을 0으로 보정한다.
- WorldStateObject3D는 월드별 위치 오프셋을 적용한 뒤 최종 위치를 2.5D 평면에 고정한다.
- ShutterFreezable3D는 멈췄던 Rigidbody를 다시 풀 때 Z축 속도를 제거하고 2.5D Rigidbody 제약을 유지한다.

## 앞으로 새 오브젝트 만들 때

새로운 주요 오브젝트가 Transform 이동이나 Rigidbody를 사용한다면 다음 중 하나를 적용한다.

1. 해당 스크립트에서 `TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView()`를 호출한다.
2. 이동 후 `TwoPointFiveDUtility3D.ClampTransformToPlane()`을 호출한다.
3. 별도 코드 수정이 어렵다면 오브젝트나 프리팹에 `TwoPointFiveDPlaneLock3D` 컴포넌트를 붙인다.

## Inspector에서 확인할 것

- Rigidbody가 있다면 Freeze Position Z, Freeze Rotation X/Y/Z가 켜져 있어야 한다.
- Player, Camera, UI, GameManager, WorldManager는 Shared 아래에 둔다.
- World_A_Current와 World_B_Past는 씬 안에서 SetActive로만 전환한다.
- Animator Controller는 시각 연출만 담당하고, 이동/충돌/판정은 코드가 담당한다.
