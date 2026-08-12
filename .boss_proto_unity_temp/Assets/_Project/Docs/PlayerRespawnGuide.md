# Player Respawn Guide

## 필수 구성

- Player에는 `PlayerDamageReceiver`가 필요하다.
- 씬에는 `PlayerSpawnPoint`가 붙은 리스폰 지점이 필요하다.
- `PlayerSpawnPoint.canUseAsRespawnPoint`를 켠다.
- Player의 `PlayerDamageReceiver.respawnPoint`에 지점 Transform을 직접 연결하는 방식을 권장한다.
- 직접 연결하지 않으면 `respawnPointId`가 같은 지점을 찾고, 없으면 `isDefaultSpawn` 지점을 사용한다.

`RespawnPoint.prefab`은 `Assets/_Project/Prefabs/Player/RespawnPoint.prefab`에 있다. Scene View에서는 청록색 구·십자와 ID 라벨로 표시된다.

## 중력 오브젝트 충돌

낙하 중인 Stone 또는 FallingBox의 `GravityObjectDamageDealer`가 Player를 확인하면 Player 또는 부모/자식에서 `PlayerDamageReceiver`를 찾아 `KillAndRespawn()`을 호출한다. PlayerController를 직접 참조하지 않는다.

확인 로그:

- `[GravityObjectDamageDealer] Player collision detected.`
- `[GravityObjectDamageDealer] Falling state valid.`
- `[PlayerDamageReceiver] KillAndRespawn called.`
- `[PlayerDamageReceiver] RespawnPoint found: Respawn_Default`
- `[PlayerDamageReceiver] Player respawned.`

## AbilityTestScene 테스트

1. `Respawn/Respawn_Default`의 청록색 Gizmo와 `Default` 라벨을 확인한다.
2. Player의 `PlayerDamageReceiver.respawnPoint`가 `Respawn_Default`에 연결됐는지 확인한다.
3. Player 컴포넌트 메뉴에서 `Validate Respawn Setup`을 실행한다.
4. `Test Kill And Respawn`을 실행해 Player가 지점으로 이동하는지 확인한다.
5. Stone 감지 박스 아래로 Player를 이동시킨다.
6. Stone이 낙하 중 Player와 충돌하게 한다.
7. Console 로그 순서와 Player 위치 이동을 확인한다.

수동 테스트 중에는 `T` 키로 즉시 `Respawn()`을 실행할 수도 있다.
