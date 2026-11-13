# Surface Wet / Snow + Isometric Footprints (Unity URP)

Unity 6.3 LTS / URP 17 계열용 HLSL 셰이더와 C# 스크립트입니다. 기존 젖음·눈 쌓임 효과에 월드 공간에 누적되는 신발 발자국을 추가했습니다.

## 폴더 구성

```text
Assets/02. MyShaders/03_SurfaceWetSnow/
├── SurfaceWetSnow.shader
├── core.hlsl
├── FootprintStamp.shader
├── SurfaceWeatherController.cs
├── SnowFootprintManager.cs
└── SnowFootstepEmitter.cs
```

## 발자국 방식

- 발자국은 월드 XZ 좌표를 사용하는 RenderTexture에 기록됩니다.
- 카메라가 이동해도 발자국은 바닥의 같은 월드 위치에 남습니다.
- 밑창 모양으로 누적되며, 눈 모드이고 눈 덮임 마스크가 있는 곳에서만 보입니다.
- 발자국 내부는 압축된 눈처럼 어두워지고, 경계 노멀을 조정해 아이소메트릭 시점에서 얕게 눌린 인상을 줍니다.
- 기본값은 시간이 지나며 서서히 흐려지는 방식입니다. `Fade Per Second = 0`이면 `Clear()` 또는 씬 언로드 전까지 유지됩니다.

> 실제 메시 정점이나 콜라이더를 변형하는 방식은 아닙니다. 픽셀 셰이딩으로 얕은 움푹함을 표현합니다.

## 설치 순서

1. 이 폴더 전체를 프로젝트의 `Assets/02. MyShaders/03_SurfaceWetSnow/`에 복사합니다.
2. Unity 컴파일이 끝나면 `SurfaceWetSnow.shader`로 Material을 만듭니다.
3. Material의 `_StoneTex`에 바닥 텍스처를 넣고 눈을 덮을 바닥 MeshRenderer에 적용합니다.
4. Material의 `_Mode`를 `Snow`, `_Coverage`를 0보다 큰 값으로 설정합니다.
5. 빈 GameObject를 만들고 `SnowFootprintManager`를 추가합니다.
6. `Stamp Shader` 슬롯에 이 폴더의 `FootprintStamp.shader`를 드래그합니다. 빌드에서 셰이더가 누락되지 않도록 직접 지정하는 것을 권장합니다.
7. `World Size`를 맵 구역의 X/Z 크기에 맞춥니다. 예: 64 x 64 구역이면 `(64, 64)`, `Resolution = 2048`.
8. 플레이어 루트에 `SnowFootstepEmitter`를 추가하고 `Footprint Manager`에 5번의 매니저를 연결합니다.

## 발자국 생성 선택

### 애니메이션 이벤트 방식 (발 위치가 더 정확함)

1. Emitter의 `Left Foot`, `Right Foot`에 캐릭터 리그의 발 본 Transform을 연결합니다.
2. 걷기/달리기 Animation Clip의 발이 지면에 닿는 프레임에 Animation Event를 추가합니다.
3. 왼발 이벤트 함수는 `LeftFootstep`, 오른발 이벤트 함수는 `RightFootstep`으로 지정합니다.
4. 이 방식을 사용할 때는 `Use Distance Fallback`을 끕니다.

### 이동 거리 자동 방식 (빠른 테스트용)

- `Use Distance Fallback`을 켭니다.
- `Step Distance` 기본값은 0.62입니다. 캐릭터 보폭에 맞게 조절합니다.
- 루트 이동 거리마다 좌우 발자국을 번갈아 찍습니다.
- CharacterController가 연결되어 있으면 공중에서는 찍지 않습니다.

## 권장 시작값

| 항목 | 값 |
|---|---:|
| World Size | 64 x 64 |
| Resolution | 2048 |
| Footprint Width | 0.19 |
| Footprint Length | 0.34 |
| Strength | 0.95 |
| Step Distance | 0.62 |
| Fade Per Second | 0.001 |
| Footprint Depth | 0.045 |
| Compressed Snow Darkening | 0.32 |

2048 x 2048 RenderTexture 두 장을 사용하므로 약 32 MiB의 GPU 메모리를 사용할 수 있습니다. 저사양에서는 1024로 낮추세요. 이 구현은 한 번에 하나의 활성 `SnowFootprintManager`가 전역 발자국 텍스처를 제공합니다.

## 눈 쌓임 제어

`SurfaceWeatherController`를 바닥 Renderer에 추가한 뒤 호출합니다.

```csharp
weatherController.SetCoverage(0.85f, 8f); // 8초 동안 눈을 더 덮음
weatherController.Storm();                // 짧은 눈발 이벤트
footprintManager.Clear();                 // 해당 구역 발자국 전체 지우기
```

## 적용 범위와 제한

- MeshRenderer가 있는 불투명 3D 메시 바닥용입니다.
- Unity Terrain 컴포넌트의 Terrain Layer 셰이더에 그대로 적용하는 용도는 아닙니다. Terrain 사용 시 별도 통합이 필요합니다.
- 발자국 지도는 월드 XZ 평면에 기록하므로 완만한 경사에는 쓸 수 있지만, 같은 XZ 좌표에 서로 다른 높이의 바닥이 겹치는 다층 구조에서는 발자국 지도가 공유됩니다.
- 물리 콜라이더나 실제 메시 높이는 변경하지 않습니다.
