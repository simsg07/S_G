# Shutter System Guide

## 동작

- 셔터는 다회성 기능입니다. Player의 `CameraAbilitySystem3D > Global Cooldown`이 전체 사용 간격을 정합니다.
- 각 대상의 `ShutterTarget3D > Target Cooldown`은 동일 대상이 다음 입력을 받을 때까지의 간격입니다.
- 첫 번째 사용은 Mark와 Pause를 적용합니다. `Mark Duration Infinite`가 켜져 있으면 두 번째 사용까지 유지됩니다.
- 두 번째 사용은 같은 대상의 `WorldSwitchable.ToggleWorld()`를 호출한 뒤 Mark를 지우고 Pause를 해제합니다. `WorldSwitchable`이 없으면 전체 월드를 바꾸지 않고 Warning만 남깁니다.
- `WorldPresence`는 기존 월드 표시/충돌 관리를 위해 참조하지만 private 상태를 직접 변경하지 않습니다.

## Boomber

- Boomber가 달리는 중 첫 셔터를 받으면 이동과 Rigidbody가 정지합니다.
- 폭발 카운트다운 중이면 `BoomberExplosion`의 남은 시간이 감소하지 않습니다.
- 두 번째 셔터 또는 Mark 만료로 Resume되면 남은 폭발 시간부터 계속 진행합니다.
- 이미 폭발했거나 Dead/Destroy 과정인 Boomber는 셔터를 무시합니다.

## IsFinite 오류 확인

`SafeMath3D`가 셔터 대상 위치·스케일, Rigidbody 속도, 카메라 투영점과 Mark Renderer bounds를 검사합니다. 오류가 다시 발생하면 다음을 확인합니다.

1. Rigidbody velocity/angularVelocity
2. Transform position/localScale
3. Destroy 직전 Renderer 및 Collider 참조
4. 폭발 Coroutine 중복 실행
5. 외부 애니메이션이 루트 Transform에 비정상 값을 기록하는지

## 기획자 테스트

1. 대상 루트에 `ShutterTarget3D`를 추가하고 Rigidbody, Animator, WorldSwitchable을 연결합니다.
2. 첫 촬영 후 `Is Marked`, `Is Paused By Shutter`와 정지 상태를 확인합니다.
3. Target Cooldown 뒤 같은 대상을 다시 촬영하고 개별 월드 전환과 Resume을 확인합니다.
4. Global Cooldown과 Target Cooldown을 각각 변경해 독립적으로 동작하는지 확인합니다.
5. Boomber 폭발 직전에 첫 촬영하고 게임 전체는 계속 움직이되 Boomber 타이머만 멈추는지 확인합니다.
6. 두 번째 촬영 후 남은 타이머부터 폭발하는지 확인합니다.
7. Console에서 `IsFinite(distanceForSort)`, `IsFinite(distanceAlongView)`, MissingReferenceException이 없는지 확인합니다.
