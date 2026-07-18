# Project File Naming Guide

최신 폴더 구조는 파일명 접두어보다 위치를 우선합니다. 기획자는 Project 창에서 아래 폴더를 기준으로 찾습니다.

## 주요 프리팹

- 몬스터: `Assets/_Project/Prefabs/Enemies/`
- 맵: `Assets/_Project/Prefabs/Map/`
- 오브젝트: `Assets/_Project/Prefabs/Objects/`
- UI: `Assets/_Project/Prefabs/UI/`

## 현재 주요 파일명

- `EyeballFly.prefab`
- `Human_Box.prefab`
- `Boomber.prefab`
- `Floor_Collision.prefab`
- `Tile_Visual.prefab`
- `Wall_Tile.prefab`
- `Block_Tile.prefab`
- `Stone.prefab`
- `StoneTrigger.prefab`
- `Shutter.prefab`
- `Button.prefab`
- `Terminal.prefab`

## 스크립트 rename 기준

C# 스크립트는 public class 이름과 파일명이 같아야 합니다. 이미 씬이나 프리팹에 붙어 있는 스크립트는 class 이름을 바꾸지 않고, 폴더 위치와 문서로 역할을 구분합니다.
