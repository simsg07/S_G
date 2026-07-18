# Data Driven Block Guide

## 목적

- 플레이어가 타일 경계나 부서지는 블록 사이에 끼는 문제를 줄이기 위해 충돌 구조를 분리한다.
- 일반 바닥/벽은 긴 `BoxCollider` 프리팹으로 관리한다.
- 부서지는 구간은 긴 바닥/벽 콜라이더에 포함하지 않고 `Block_Breakable` 단독 콜라이더로 배치한다.
- 블록의 기본 동작 값은 `ObjectData`에서 조정한다.

## 생성된 블록 프리팹

| 프리팹 | 용도 | 연결 데이터 |
| --- | --- | --- |
| `Assets/_Project/Prefabs/Objects/Blocks/Floor_Collision_Long.prefab` | 긴 일반 바닥 충돌 | `NormalFloorBlockData.asset` |
| `Assets/_Project/Prefabs/Objects/Blocks/Wall_Collision_Long.prefab` | 긴 일반 벽 충돌 | `NormalWallBlockData.asset` |
| `Assets/_Project/Prefabs/Objects/Blocks/Block_Breakable.prefab` | 부서지는 독립 블록 | `BreakableBlockData.asset` |
| `Assets/_Project/Prefabs/Objects/Blocks/Block_SightBlock.prefab` | 시야 차단 블록 | `SightBlockData.asset` |

## 충돌 배치 규칙

좋은 구조:

```text
[Floor_Left] [Block_Breakable] [Floor_Right]
```

피해야 하는 구조:

```text
[Floor_Long 전체]
        [Block_Breakable]
```

부서지는 블록 아래나 뒤에 긴 바닥/벽 콜라이더가 겹쳐 있으면, `Block_Breakable`이 깨져도 남아 있는 긴 콜라이더 때문에 길이 열리지 않는다.

## 부서지는 블록 끼임 방지

`Block_Breakable`은 `BlockObject.BreakBlock()` 순서로 처리된다.

1. 이미 부서졌으면 중복 실행하지 않는다.
2. `isBroken`을 켠다.
3. `playerLayerMask`에 해당하는 대상이 블록 콜라이더와 겹치는지 확인한다.
4. 겹친 대상은 블록 중심 기준으로 가까운 안전 방향으로 `safePushDistance`만큼 밀어낸다.
5. `mainCollider`를 즉시 끈다.
6. `BreakableObject3D.BreakObject()`와 `OpenPathOnBreak.OpenPath()`를 호출한다.
7. 필요하면 Visual을 즉시 또는 지연 후 숨긴다.

중요한 점은 Collider를 애니메이션보다 먼저 끄는 것이다. 그래야 겉보기 파괴 연출이 남아 있어도 물리적으로는 바로 길이 열린다.

## 공격 반응

`Block_Breakable`은 `HitReceiver` 규칙상 `BoomberExplosion`과 `BoomberContact`를 허용한다.
EyeballFly 공격은 기본적으로 허용하지 않으므로, EyeballFly가 모든 블록을 깨는 상황을 막는다.

Boomber가 블록을 깨려면 `BoomberExplosion.breakableLayerMask`에 `Block_Breakable`이 배치된 Layer가 포함되어야 한다.

## ObjectData로 조정 가능한 값

`ObjectData`의 Block 섹션에서 아래 값을 조정할 수 있다.

- `blockType`
- `canBlockPlayer`
- `canBlockMonster`
- `canBlockSight`
- `canBlockLight`
- `removeColliderOnBreak`
- `hideVisualOnBreak`
- `delayHideVisual`
- `visualHideDelay`
- `clearPlayerOverlapOnBreak`
- `safePushDistance`

`DataDrivenObjectController`는 `BlockObject`가 있으면 `Apply Object Data` 실행 시 위 값을 전달한다.

## 기획자 배치 방법

1. 보이는 타일은 기존 Tile/Visual 방식으로 칠한다.
2. 일반 바닥은 `Floor_Collision_Long`을 길게 배치한다.
3. 일반 벽은 `Wall_Collision_Long`을 길게 배치한다.
4. 부서지는 구간은 긴 Floor/Wall 콜라이더에서 반드시 제외한다.
5. 제외한 자리에 `Block_Breakable`을 배치한다.
6. 필요하면 `BreakableBlockData` 또는 `UnstableTileData` 값을 조정한다.
7. 프리팹에서 `DataDrivenObjectController > Apply Object Data`를 누른다.
8. `BlockObject > Test Break Block`으로 Collider가 즉시 꺼지는지 확인한다.

## Unity에서 직접 설정해야 하는 것

- Layer/Tag는 코드에서 자동 변경하지 않는다.
- `Block_SightBlock`은 프로젝트의 시야 차단 규칙에 맞게 `TileObstacle`, `MapObstacle`, `SightBlock` 등 적절한 Layer로 직접 설정한다.
- `BlockObject.playerLayerMask`는 현재 기본값으로 Default Layer를 포함한다. 프로젝트에서 Player 전용 Layer를 쓰게 되면 Inspector에서 해당 Layer로 바꾼다.

## 테스트 방법

### 일반 바닥/벽

1. `Floor_Collision_Long` 또는 `Wall_Collision_Long`을 씬에 배치한다.
2. Player가 타일 사이 경계에서 걸리지 않고 지나가는지 확인한다.
3. 긴 콜라이더가 부서지는 블록 구간과 겹치지 않는지 Scene View에서 확인한다.

### 부서지는 블록

1. `Block_Breakable`을 배치한다.
2. `Validate Block Setup`을 실행한다.
3. `Test Break Block`을 실행한다.
4. `BoxCollider`가 즉시 꺼지는지 확인한다.
5. Player가 블록과 겹친 상태에서 깨졌을 때 살짝 밀려나는지 확인한다.

### 시야 차단 블록

1. `Block_SightBlock`을 배치한다.
2. Layer를 프로젝트 시야 차단 레이어로 직접 맞춘다.
3. 몬스터 Line of Sight 또는 빛 감지 차단이 의도대로 동작하는지 확인한다.

## 주의

- 부서지는 블록과 긴 콜라이더가 겹치면 안 된다.
- 끼임 방지는 보조 안전장치다. 가장 좋은 해결은 통로 폭을 Player Collider보다 충분히 넓게 잡는 것이다.
- 이번 구조는 `Rigidbody2D` / `Collider2D`를 사용하지 않는다.
- PlayerController, MonsterAI, Camera, World 전환 시스템은 직접 수정하지 않았다.
