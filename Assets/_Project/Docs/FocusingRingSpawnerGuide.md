# Focusing Ring / Spawner 설정 가이드

## 런타임 규칙

- 포커싱 링은 플레이어의 휠 입력 또는 지정된 `InputActionReference`로만 실행됩니다.
- 초기화 시 플레이어, 능력, 세이브 데이터와 씬은 유지됩니다.
- 대상 Spawner의 현재 인스턴스와 등록된 임시 오브젝트를 제거한 뒤 Prefab을 새로 생성합니다.
- 재생성된 Prefab이 자체 `Awake`/`OnEnable`을 실행하므로 FSM, Rigidbody, Animator를 수동 복구하지 않습니다.
- Spawner 상태는 `Alive`, `Defeated`, `Disabled`로 구분합니다. 사망한 인스턴스는 즉시 재생성하지 않고 `Defeated`에서 다음 포커싱 링 입력을 기다립니다.

## Spawner 배치

1. 빈 GameObject를 생성하고 생성 위치에 배치합니다.
2. `FocusingSpawner3D`를 추가합니다.
3. `Prefab`에 몬스터 또는 퍼즐 전체 Root Prefab을 연결합니다.
4. 필요하면 `Spawn Point`, `Default Parent`, `Temporary Objects Root`를 지정합니다.
5. 자동 등록은 컴포넌트의 활성화 등록을 사용합니다. `Initialization` Tag도 초기화 시 한 번만 검색합니다.

기존 씬 오브젝트 자체를 Prefab처럼 되감지 않습니다. 반드시 원본을 Prefab으로 만든 뒤 Spawner가 생성하게 구성해야 합니다.

`Initialization` Tag가 기존 프로젝트에 정의되어 있다면 최초 캐시에 함께 사용할 수 있습니다. 현재 Tag 구성을 자동 변경하지 않으므로, Tag가 없을 때는 컴포넌트의 명시적 런타임 등록을 사용합니다.

## 임시 오브젝트

투사체, 공격 판정, 파편처럼 Spawner 인스턴스 밖에 생성되는 오브젝트에는 `FocusingTemporaryObject3D`를 추가하거나 생성 직후 `RegisterTemporaryObject`를 호출합니다. Spawner의 `Temporary Objects Root` 아래에 생성하는 방식도 지원합니다.

## 영구 완료 퍼즐

- `Permanently Disable After Puzzle Completion`과 고유한 `Persistent Completion Key`를 설정합니다.
- 퍼즐 완료 시 `MarkPuzzlePermanentlyCompleted()`를 호출합니다.
- 완료 정보는 기존 `GameProgressSave3D` 데이터에 기록되며 포커싱 링으로 삭제되지 않습니다.

## 컷신과 메뉴 차단

컷신 또는 별도 메뉴 Root에 `FocusingRingBlocker3D`를 추가하면 활성화된 동안 링 사용이 차단됩니다. 일시정지는 `Time.timeScale == 0`, 씬 전환은 기존 두 Scene Loader의 상태로 자동 차단됩니다.

## 플레이어 겹침 보호

새 인스턴스의 고체 Collider가 플레이어와 겹치면 플레이어 위치는 변경하지 않습니다. 새 인스턴스의 Collider를 먼저 비활성화하고 플레이어가 최초 생성 Bounds 밖으로 빠져나간 뒤 원래 활성 상태였던 Collider만 복구합니다.
