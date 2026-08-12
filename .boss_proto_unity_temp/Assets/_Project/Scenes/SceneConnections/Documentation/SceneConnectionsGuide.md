# SceneConnections 사용법

## 기본 배치

1. `Prefabs/SceneConnections.prefab`을 씬에 배치합니다.
2. Edit Mode에서 `LeftEntrance`, `RightEntrance`를 실제 플레이어 도착 위치로 이동합니다.
3. `LeftExit`, `RightExit`를 출구 위치로 옮기고 Box Collider 크기를 조절합니다.
4. Exit Inspector에서 **이동할 씬**과 **도착 위치**를 선택합니다.
5. **연결 검사**를 누른 뒤 씬을 저장합니다.

## 분기 출구

기존 `PlayerSpawnPoint` 또는 `StageExitTrigger` 프리팹 인스턴스를 각각 `SpawnPoints`, `Exits` 아래에 추가하고 `UpperRightEntrance`, `UpperRightExit`처럼 이름과 ID를 지정합니다. Transform은 씬 인스턴스 Override로 유지합니다.

## 검사

Exit Inspector의 **연결 검사**는 대상 씬, Entrance ID, Build Profile 등록과 Trigger 설정을 검사하며 Transform을 변경하지 않습니다.

## 위치 편집

Play Mode에서 바꾼 Transform은 Unity 기본 동작으로 종료 시 복구됩니다. 실제 배치는 Edit Mode에서 수정하고 `Ctrl+S`로 씬을 저장하세요.
