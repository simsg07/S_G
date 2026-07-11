# S_G 2.5D 개발 규칙

이 프로젝트는 3D 오브젝트를 사용하지만, 플레이 방식은 2D처럼 제한하는 2.5D 메트로베니아 / 퍼즐 플랫폼 게임으로 개발한다.

## 기본 규칙

1. 카메라는 Orthographic 기반의 고정 사이드뷰를 사용한다.
2. 플레이어는 X축 좌우 이동과 Y축 점프/낙하만 가능하다.
3. 플레이어와 주요 오브젝트의 Z축 이동은 금지한다.
4. Rigidbody를 사용할 경우 Freeze Position Z와 Freeze Rotation을 적용한다.
5. 2D 물리와 3D 물리를 섞지 않는다.
6. 3D 오브젝트를 사용한다면 Rigidbody, BoxCollider, CapsuleCollider 같은 3D 물리 컴포넌트를 기준으로 통일한다.
7. 게임 로직은 코드에서 처리하고, Animator는 시각적 연출만 담당한다.
8. 월드 A/B 전환은 씬 이동이 아니라 World_A_Current와 World_B_Past 오브젝트 묶음을 SetActive로 전환하는 방식으로 처리한다.
9. Player, Camera, UI, GameManager는 월드 전환 시 꺼지지 않도록 Shared 구조에 둔다.
10. 나중에 3D 에셋을 교체하기 쉽도록 모든 주요 오브젝트는 Prefab으로 관리한다.

## Unity Inspector 체크 기준

- Player Rigidbody: Freeze Position Z 켜기, Freeze Rotation X/Y/Z 켜기
- 주요 이동 오브젝트 Rigidbody: Freeze Position Z 켜기, 필요한 Rotation Freeze 켜기
- Camera: Projection을 Orthographic으로 설정
- Physics: Rigidbody, BoxCollider, CapsuleCollider 등 3D 물리 컴포넌트만 사용
- Animator: Apply Root Motion 끄기
- 월드 구조: Shared, World_A_Current, World_B_Past를 한 씬 안에 유지

## 작업 기준

- 판정, 이동, 상호작용, 월드 전환 조건은 코드에서 관리한다.
- Animator 상태를 기준으로 게임 판정을 처리하지 않는다.
- 애니메이션은 Idle, Run, Jump, Door Open, Button Press 같은 보이는 연출만 담당한다.
- 새로 배치할 주요 오브젝트는 씬에 직접 만들기보다 Prefab을 먼저 만들고 씬에 배치한다.
