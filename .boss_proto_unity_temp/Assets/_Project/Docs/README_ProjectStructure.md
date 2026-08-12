# README Project Structure

`Assets/_Project`는 기획자가 찾기 쉬운 기준으로 정리합니다. 기능 코드는 유지하고, 파일 위치와 문서로 역할을 분리합니다.

## Art

- `Art/Player`: Player 이미지
- `Art/Enemies/EyeballFly`: EyeballFly 스프라이트
- `Art/Enemies/Human_Box`: Human_Box 스프라이트
- `Art/Enemies/Boomber`: Boomber 스프라이트
- `Art/Enemies/Common`: 몬스터 공용 이미지
- `Art/Map/Tiles`: 맵 타일 이미지
- `Art/Map/Backgrounds`: 배경 이미지
- `Art/Map/Props`: 맵 장식 이미지
- `Art/Objects/Stone`: Stone 오브젝트 이미지
- `Art/UI`: UI 이미지

## Animations

- `Animations/Player`: Player Animator / Clip
- `Animations/Enemies/EyeballFly`: EyeballFly Animator / Clip
- `Animations/Enemies/Human_Box`: Human_Box Animator / Clip
- `Animations/Enemies/Boomber`: Boomber Animator / Clip
- `Animations/Objects/Stone`: Stone Animator / Clip
- `Animations/Objects/Door`: Door 관련 Animator
- `Animations/Objects/Shutter`: Shutter/Button/Terminal 계열 Animator

## Prefabs

- `Prefabs/Enemies`: 기획자가 배치하는 몬스터 프리팹
- `Prefabs/Map`: 기획자가 배치하는 맵 타일/충돌 프리팹
- `Prefabs/Objects`: 장치, 함정, 상호작용 오브젝트 프리팹
- `Prefabs/Player`: Player 프리팹 자리. 현재 Player는 씬 오브젝트 중심입니다.
- `Prefabs/UI`: UI 프리팹

## Scripts

- `Scripts/Player`: Player 기능
- `Scripts/Enemies/Common`: 몬스터 공통 기능
- `Scripts/Enemies/EyeballFly`: EyeballFly 전용 기능
- `Scripts/Enemies/Human_Box`: Human_Box 전용 기능
- `Scripts/Enemies/Boomber`: Boomber 전용 기능
- `Scripts/Map`: 맵 배치/타일 기능
- `Scripts/Objects`: 오브젝트 기능
- `Scripts/Camera`: 카메라 관련 기능
- `Scripts/World`: World 전환 기능
- `Scripts/Core`: 공용 인터페이스/유틸리티

## Docs

- `PlayerScriptsGuide.md`: Player 스크립트 역할
- `EnemyGuide.md`: 몬스터 프리팹/애니메이션/스크립트 위치
- `MapTileGuide.md`: 맵 타일 프리팹 배치 기준
- `ObjectGuide.md`: 오브젝트 프리팹 역할
- `DesignerInspectorGuide.md`: Inspector에서 만져도 되는 값

## 주의

- `.meta` 파일은 Unity 참조를 유지하므로 파일 이동 시 반드시 함께 이동합니다.
- C# class 이름은 변경하지 않습니다.
- Animator Controller, Script 참조, Sprite 참조는 Inspector에서 임의로 끊지 않습니다.
