# 게임플레이 씬 연결 가이드

새 게임플레이 씬에는 **Player 프리팹을 직접 배치하지 않습니다.**  
씬 연결과 `PlayerSpawnPoint` 설정만 완료하면 Play Mode에서 Player가 자동으로 생성되거나 기존 Player가 유지됩니다.

## 1. 새 씬 준비

1. 새 씬을 생성합니다.
2. 새 씬을 Unity Build Settings의 Scene List에 등록합니다.
3. 다음 프리팹을 씬에 배치합니다.

   `Assets/_Project/Prefabs/Scene/PlayerSpawnPoint.prefab`

## 2. 기본 시작 위치 설정

씬을 Unity Editor에서 직접 Play했을 때 사용할 `PlayerSpawnPoint`를 설정합니다.

- `Spawn Point ID`: `Default`
- `Is Default Spawn`: 켜기
- `Can Use As Respawn Point`: 필요하면 켜기
- 위치: Player가 실제로 시작할 안전한 바닥 위

Default SpawnPoint는 한 씬에 하나만 배치합니다.

## 3. 포털 도착 위치 설정

포털로 들어오는 위치마다 별도의 `PlayerSpawnPoint`를 배치합니다.

예시:

- 왼쪽 입구: `LeftEntrance`
- 오른쪽 입구: `RightEntrance`
- 보스방 입구: `BossEntrance`

포털용 SpawnPoint는 일반적으로 `Is Default Spawn`을 끕니다.  
한 씬 안에서 같은 Spawn Point ID를 두 번 사용하지 않습니다.

## 4. StageExitTrigger 연결

출구 오브젝트의 `StageExitTrigger`에서 다음 값을 설정합니다.

- `Next Scene Name`: 이동할 씬의 정확한 이름
- `Target Spawn Point ID`: 목적지 씬에 배치한 Spawn Point ID

예시:

```text
Scene_A의 StageExitTrigger
Next Scene Name: Scene_B
Target Spawn Point ID: LeftEntrance

Scene_B의 PlayerSpawnPoint
Spawn Point ID: LeftEntrance
```

Spawn Point ID는 대소문자까지 정확히 같아야 합니다.

## 5. 양방향 연결

Scene_A와 Scene_B를 양방향으로 연결하려면 양쪽 씬에 각각 출구와 도착점을 설정합니다.

```text
Scene_A → Scene_B
Target Spawn Point ID: LeftEntrance

Scene_B → Scene_A
Target Spawn Point ID: RightEntrance
```

## 6. 작업 완료 체크리스트

- [ ] 새 씬을 Build Settings에 등록했다.
- [ ] Default PlayerSpawnPoint를 정확히 하나 배치했다.
- [ ] 포털 도착 위치마다 고유한 Spawn Point ID를 설정했다.
- [ ] StageExitTrigger의 씬 이름과 목적지 Spawn ID가 정확하다.
- [ ] 씬에 Player 프리팹을 직접 배치하지 않았다.
- [ ] 새 씬을 직접 Play했을 때 Default 위치에서 시작한다.
- [ ] 기존 씬에서 들어왔을 때 지정한 Spawn ID 위치에서 시작한다.
- [ ] 반대 방향으로 돌아가는 포털도 정상 작동한다.

## 주의사항

- 게임플레이 씬에는 `PlayerSpawnPoint`가 반드시 하나 이상 있어야 합니다.
- `PlayerSpawnPoint`가 없는 씬에는 Player가 자동 생성되지 않습니다.
- MainMenu처럼 Player가 필요 없는 씬에는 `PlayerSpawnPoint`를 배치하지 않습니다.
- Player가 보이지 않는다고 씬 Hierarchy에 Player 프리팹을 추가하지 마세요. Player는 Play Mode에서 자동으로 생성됩니다.
- SpawnPoint는 벽이나 바닥 내부가 아닌 안전한 위치에 배치하세요.
