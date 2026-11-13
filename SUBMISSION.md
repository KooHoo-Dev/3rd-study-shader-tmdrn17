# 나만의 셰이더 제작 — 제출 문서

> 과제 안내: [ASSIGNMENT.md](ASSIGNMENT.md)

| 항목 | 내용 |
| --- | --- |
| 엔진 / 파이프라인 | Unity 6000.3.15f1 / URP 17 |
| 셰이더 방식 | HLSL 코드 (`.shader` + `.hlsl`) |
| 데모 씬 | `Assets/00. Scenes/Study Shader.unity` |
| 셰이더 수 | 3개 (디졸브 / 가산 발광 이펙트 / 표면 날씨 + 발자국) |

## 한눈에 보기

| # | 셰이더 | 경로 (`Shader "..."`) | 분야 | 핵심 기법 | 데모 오브젝트 |
| --- | --- | --- | --- | --- | --- |
| 1 | DissolveEdgeGlow | `MyShaders/DissolveEdgeGlow` | 3D 오브젝트 | 3D 값 노이즈, `clip()` 알파 테스트, 경계 발광 | `ShaderBalls/ShaderBall_4` |
| 2 | DescendingLightColumn | `MyShaders/DescendingLightColumn` | 이펙트 | 가산 블렌딩(`Blend One One`), 프레넬 계열 발광, 스크립트로 진행도 애니메이션 | `ShaderBalls/ShaderCylinder` |
| 3 | SurfaceWetSnow | `MyShaders/SurfaceWetSnow` | 3D 바닥 | 법선 기반 덮임 마스크, 젖음 / 눈 2모드, RenderTexture 발자국 누적, 노멀 재계산 | `Floor` |

세 셰이더는 효과와 구현 방식이 서로 달라요.

- **1번**은 불투명 오브젝트를 노이즈로 잘라내요.
- **2번**은 투명 가산 발광으로 빛기둥을 그려요.
- **3번**은 조명을 직접 계산하고, C#이 그린 RenderTexture를 전역 텍스처로 받아서 써요.

## 폴더 구조

```
Assets/
├─ 00. Scenes/
│  └─ Study Shader.unity              ← 세 셰이더 데모 씬
└─ 02. MyShaders/
   ├─ 00_Common/
   │  └─ ShaderGuideUI.cs             ← 게임 화면 우측 상단 조작 가이드 (F1 토글)
   ├─ 01_DissolveEdgeGlow/
   │  ├─ DissolveEdgeGlow.shader
   │  ├─ core.hlsl                    ← 노이즈 + 디졸브 계산
   │  ├─ DissolveEdgeGlow.mat
   │  └─ DissolveController.cs        ← _Threshold 애니메이션, 1/2 키 테스트
   ├─ 02_DescendingLightColumn/
   │  ├─ DescendingLightColumn.shader
   │  ├─ DescendingLightColumn.mat
   │  └─ DescendingLightColumnController.cs ← _Progress/_Flash 애니메이션, 1/2/3 키 테스트
   └─ 03_SurfaceWetSnow/
      ├─ SurfaceWetSnow.shader
      ├─ core.hlsl                    ← 덮임 마스크, 조명, 발자국 노멀 계산
      ├─ FootprintStamp.shader        ← 발자국 RenderTexture에 도장 찍는 Blit 셰이더
      ├─ SurfaceWetSnow.mat
      ├─ _StoneTex.png
      ├─ SnowFootprintManager.cs      ← 발자국 RenderTexture 관리 (핑퐁 버퍼)
      ├─ SnowFootstepEmitter.cs       ← 캐릭터용 발자국 생성기
      ├─ SnowFootprintTester.cs       ← 플레이어 없이 마우스/키보드로 발자국 테스트
      ├─ SurfaceWeatherController.cs  ← 눈 덮임 정도 / 눈보라 런타임 제어
      └─ README_KO.md                 ← 발자국 시스템 상세 설명
```

## 데모 씬 사용법

1. `Assets/00. Scenes/Study Shader.unity`를 열고 Play를 눌러요.
2. Game 뷰 오른쪽 위에 조작 가이드가 나와요. `F1`을 누르면 숨기거나 다시 볼 수 있어요.

| 키 / 입력 | 동작 |
| --- | --- |
| `1` / `2` | 디졸브 + 빛기둥 동시에 나타나기 / 사라지기 |
| `3` | 빛기둥 전체 다시 재생 (시작 시 자동 재생) |
| 좌클릭 / 드래그 | 눈 바닥에 발자국 찍기 / 걸어간 자국 |
| `WASD`·방향키 (+Shift) | 가상 보행자 걷기 (달리기) |
| `Space` | 현재 위치에 발자국 1개 |
| `L` / `O` / `R` | 직선 / 원형 / 자동 걷기 |
| `C` | 발자국 모두 지우기 |

---

## 1. DissolveEdgeGlow — 노이즈 디졸브 + 경계 발광

![DissolveEdgeGlow](Docs/Screenshots/01_DissolveEdgeGlow.png)

### 무엇을 하는 셰이더인가
오브젝트가 불타 없어지듯 구멍이 뚫리며 사라지는 효과예요. 잘려 나가는 경계에는 HDR 색으로 빛나는 띠가 생겨요.

### 동작 원리
1. **3D 값 노이즈 (Object Space)**
   - `dissolveHash()`가 3D 격자 칸마다 고정된 의사 난수를 만들어요.
   - `dissolveValueNoise()`가 주변 8개 꼭짓점 값을 `f*f*(3-2f)` 곡선으로 부드럽게 삼선형 보간해요.
   - 주파수가 다른 노이즈 3개(가중치 0.6 / 0.28 / 0.12)를 더해서, 큰 덩어리와 잔 디테일을 같이 표현해요.
   - UV 대신 **오브젝트 공간 좌표**를 쓰기 때문에, 셰이더볼처럼 UV 경계가 많은 메시에서도 이음새가 보이지 않아요.
2. **세로 쓸어내림 (`_Sweep`)**
   - `lerp(noise, saturate(0.5 - positionOS.y), _Sweep)`으로 노이즈를 높이 그라데이션과 섞어요.
   - 값을 올릴수록 무작위 구멍 대신 위에서부터 차례로 사라지는 형태가 돼요.
3. **잘라내기 (알파 테스트)**
   - `keep = step(_Threshold, noise)`로 계산해서, 노이즈가 임계값보다 작은 픽셀을 `clip(color.a - 0.5)`로 버려요.
   - 반투명 블렌딩이 아니라 픽셀을 버리는 방식이라 `Queue = AlphaTest`로 그리고, 깊이 정렬 문제가 생기지 않아요.
4. **경계 발광**
   - `1 - smoothstep(cut, cut + _EdgeWidth, noise)`는 잘린 지점 바로 위의 얇은 띠에서만 1에 가까운 값이에요.
   - 이 값으로 기본 색과 `_EdgeColor * _EdgeGlow`(HDR)를 보간해요. Bloom이 켜져 있으면 빛나 보여요.
   - `saturate(_Threshold * 20)`을 곱해서, Threshold가 0일 때는 경계가 보이지 않게 했어요.
5. **그림자 / 깊이 일치**
   - `ShadowCaster`, `DepthOnly`, `DepthNormals` 패스에서도 똑같이 `clip()`을 해요.
   - 그래서 뚫린 구멍이 그림자와 깊이 버퍼에도 똑같이 반영돼요.
6. **C# 애니메이션**
   - `DissolveController`가 `Renderer.material`로 인스턴스 머테리얼을 만들고, 코루틴으로 `_Threshold`를 0↔1로 보간해요.
   - 인스턴스를 쓰기 때문에 같은 머테리얼을 쓰는 다른 오브젝트는 영향을 받지 않아요.

### 프로퍼티
| 이름 | 설명 |
| --- | --- |
| Base Color | 남아 있는 표면의 기본 색 |
| Threshold | 0 = 완전히 보임, 1 = 완전히 사라짐 |
| Noise Scale | 구멍 패턴의 크기 (클수록 잘게) |
| Vertical Sweep | 0 = 무작위 구멍, 1 = 위에서 아래로 사라짐 |
| Edge Width | 빛나는 경계 띠 두께 |
| Edge Color (HDR) / Edge Glow | 경계 색과 밝기 배율 |

### 직접 바꾸거나 더한 부분
- 데모용으로 `DissolveController`에 키보드 테스트를 추가했어요. `1` 키는 나타나기, `2` 키는 사라지기예요.
  - 키로 실행할 때는 `Destroy When Done`이 켜져 있어도 오브젝트를 파괴하지 않아서, 반복해서 확인할 수 있어요.
- ✍️ *(작성 필요: shader-gallery 원본에서 직접 수정한 셰이더 코드나 값이 있으면 여기에 적어 주세요.)*

---

## 2. DescendingLightColumn — 위에서 아래로 사라지는 빛기둥

![DescendingLightColumn](Docs/Screenshots/02_DescendingLightColumn.png)

### 무엇을 하는 셰이더인가
아이템 획득이나 소환 연출처럼 쓰는 빛기둥이에요.

- 처음에 한 번 강하게 번쩍여요.
- 그다음 밝은 경계선이 위에서 아래로 내려가요.
- 경계선이 지나간 부분은 사라져요.

### 동작 원리
1. **가산 블렌딩 투명 패스**
   - `Blend One One`, `ZWrite Off`, `Queue = Transparent`로 그려요.
   - 출력 색을 배경에 **더하기만** 하기 때문에 검은색은 투명하게 보이고, 겹칠수록 밝아져요.
   - 알파 채널을 쓰지 않는 발광 이펙트의 전형적인 방식이에요.
2. **원통 단면 발광 (프레넬 응용)**
   - `facing = dot(normal, viewDir)`는 원통의 정면일수록 1, 실루엣 쪽일수록 0이에요.
   - `pow(facing, _CorePower)`는 지수를 크게 써서 가운데 좁은 **코어**를 만들어요.
   - `pow(facing, _HaloPower)`는 지수를 작게 써서 넓은 **후광**을 만들어요.
   - 두 값을 더하면 둥근 빛줄기처럼 보여요.
3. **높이 좌표와 끝부분 흐림**
   - 기본 Cylinder의 오브젝트 공간 Y(-1~1)를 `along`(0~1)으로 바꿔요.
   - `smoothstep`으로 위·아래 끝을 `_EndFade`만큼 부드럽게 흐려요.
   - 윗면 원판은 법선 Y값으로 `clip()`해서 아예 그리지 않아요.
4. **하강 스윕**
   - `headY = 1 - _Progress`가 경계선의 높이예요.
   - `remaining = 1 - smoothstep(headY - soft, headY + soft, along)`이라서, 경계선 아래쪽만 남아요.
   - `headBand`는 경계선 주변 `_HeadWidth` 폭에서만 1이 되는 얇은 띠예요. 이 띠에 `_HeadColor * _HeadIntensity`를 더해서 밝은 스캔 라인을 만들어요.
5. **번쩍임**
   - 기둥 밝기에 `1 + _Flash * _FlashBoost`를 곱해요.
6. **C# 타임라인**
   - `DescendingLightColumnController`가 `MaterialPropertyBlock`으로 `_Flash`(1→0)와 `_Progress`(0→1)를 차례로 애니메이션해요.
   - PropertyBlock을 쓰기 때문에 머테리얼 인스턴스를 만들지 않아요. 같은 머테리얼을 공유해도 오브젝트마다 진행도를 따로 가질 수 있고, SRP Batcher도 유지돼요.

### 프로퍼티
| 이름 | 설명 |
| --- | --- |
| 빛기둥 색 / 하강 경계 색 (HDR) | 기둥 몸통 색, 스캔 라인 색 |
| 기본 밝기 / 초기 섬광 배율 / 하강 경계 밝기 | 각 요소의 세기 |
| 외곽 빛 강도·폭 / 중심 빛 강도·집중도 | 후광과 코어의 강도와 지수 |
| 하강 경계 폭 / 소멸 경계 부드러움 | 스캔 라인 두께, 사라지는 경계의 부드러움 |
| 기둥 위아래 흐림 | 양 끝 페이드 길이 |
| `_Progress`, `_Flash` (숨김) | 스크립트가 제어하는 진행도와 번쩍임 |

### 직접 바꾸거나 더한 부분
- `DescendingLightColumnController`의 연출을 **나타나기(섬광)**와 **사라지기(하강)** 두 단계로 나눠서, 각각 따로 실행할 수 있게 했어요.
  - `1` 키: 나타나서 그대로 유지돼요.
  - `2` 키: 위에서 아래로 쓸어내리며 사라져요.
  - `3` 키: 두 단계를 이어서 전체 재생해요. 원래는 시작할 때 한 번만 재생됐어요.
- ✍️ *(작성 필요: 직접 설계하거나 수정한 부분, 예) 윗면 원판 제거, 2단계 연출 등을 적어 주세요.)*

---

## 3. SurfaceWetSnow — 젖음 / 눈 쌓임 + 월드 발자국

![SurfaceWetSnow](Docs/Screenshots/03_SurfaceWetSnow.png)

### 무엇을 하는 셰이더인가
바닥 표면이 비에 젖거나 눈에 덮이는 효과예요.

- 눈 모드에서는 캐릭터가 지나간 자리에 신발 모양 발자국이 월드 좌표에 남아요.
- 발자국 안쪽은 눌린 것처럼 어둡고, 가장자리에는 그늘과 하이라이트가 생겨요.

### 동작 원리
1. **덮임 마스크**
   - 위를 향하는 면일수록(`normalWS.y`가 클수록) 눈이 먼저 쌓여요.
   - 법선 Y에 3D 노이즈를 더한 값을 `surfaceCoverMask()`의 `smoothstep` 경계와 비교해요.
   - `_Coverage`를 올리면 경계가 아래로 내려가서, 옆면까지 점점 덮여요.
   - 노이즈 덕분에 덮인 경계가 자연스럽게 들쭉날쭉해져요.
   - `_Storm`은 Coverage에 최대 0.4를 더해서 일시적으로 더 덮이게 해요.
2. **세 가지 표면 색을 직접 계산** (커스텀 라이팅)
   - **dry**: 텍스처 × (램버트 + SH 환경광)
   - **wet**:
     - 텍스처를 제곱해서 어둡고 채도 높게 만들어요(젖은 돌).
     - `_Gloss`에 따라 Blinn-Phong 지수와 강도가 큰 스페큘러를 더해요.
     - 프레넬 반사를 더해요.
   - **snow**:
     - 눈 색 × 조명에 약한 스페큘러를 더해요.
     - 반짝이는 결정(**glint**)도 더해요. 오브젝트 공간을 90칸 격자로 나누고, 칸마다 무작위로 흔든 법선 중 일부(20%)만 지수 300짜리 스페큘러로 반짝이게 해요.
   - 최종 색은 `lerp(dry, lerp(wet, snow, _Mode), mask)`예요.
3. **발자국 지도 (RenderTexture)**
   - `SnowFootprintManager`가 월드 XZ 영역(기본 12×12m)을 덮는 RenderTexture 2장을 만들어요.
   - 발을 디딜 때마다 `FootprintStamp.shader`로 Blit해서 **핑퐁 버퍼**에 누적해요.
   - 스탬프 셰이더는 각 텍셀의 월드 좌표를 발 방향 기준 로컬 좌표로 회전시켜요. 그다음 앞꿈치·중간·뒤꿈치 타원 3개의 합집합으로 밑창 모양을 만들고, 기존 값과 `max`를 취해요.
   - `Fade Per Second`가 0보다 크면 주기적으로 `exp(-rate × dt)`를 곱해서, 발자국이 서서히 흐려져요.
   - 결과는 `Shader.SetGlobalTexture`로 모든 SurfaceWetSnow 머테리얼에 전달돼요.
4. **발자국 셰이딩**
   - 표면 셰이더는 `positionWS.xz`를 발자국 지도의 UV로 바꿔서 샘플링해요.
   - 상하좌우 이웃 텍셀과의 차이로 **기울기(gradient)**를 구하고, `(gx·depth, 1, gy·depth)`로 새 법선을 만들어요.
     - 실제 메시는 그대로인데, 조명만 움푹 들어간 것처럼 바뀌어요. 높이맵을 노멀로 바꾸는 원리와 같아요.
   - 발자국 안쪽은 `_FootprintDarkening`만큼 어둡게 해서 눌린 눈처럼 보이게 해요.
   - 기울기가 큰 테두리는 `_FootprintRim`만큼 밝혀서, 눈이 솟아오른 턱을 표현해요.
   - 눈 마스크가 있고(`mask × isSnow`) 위를 향하는 면에서만 적용돼요.

### 프로퍼티
| 이름 | 설명 |
| --- | --- |
| Base Texture / Tiling U·V | 바닥 텍스처와 반복 횟수 |
| Mode | Wet(0) / Snow(1) |
| Coverage / Edge Softness / Noise Scale | 덮이는 양, 경계 부드러움, 경계 노이즈 크기 |
| Wet Darkness / Gloss | 젖은 표면이 어두워지는 정도, 광택 |
| Snow Color / Storm Boost | 눈 색, 일시적인 추가 덮임 |
| Footprint Indent Strength | 발자국 기울기 → 법선 변형 세기 |
| Compressed Snow Darkening | 눌린 눈 어둡게 |
| Raised Snow Rim | 발자국 테두리 밝기 |

### 직접 바꾸거나 더한 부분
- 기존 젖음 / 눈 쌓임 셰이더에 **월드 공간 발자국 시스템**을 추가했어요.
  - `FootprintStamp.shader`
  - `SnowFootprintManager`
  - `surfaceFootprint()` 노멀 재계산
- 플레이어 없이 확인할 수 있도록 `SnowFootprintTester`를 추가했어요. 마우스 클릭·드래그, WASD 가상 보행, 직선 / 원형 / 자동 걷기 프리셋, 화면 버튼, 컨텍스트 메뉴를 지원해요.
- 데모 바닥(10×10m)에 맞춰 `World Size`를 64→12, `Resolution`을 2048→1024로 바꿨어요. 발자국 해상도는 1m당 약 85텍셀로 올라가고, GPU 메모리는 32MiB→8MiB로 줄었어요.
- ✍️ *(작성 필요: shader-gallery 원본에서 직접 바꾼 값이나 코드가 더 있으면 적어 주세요.)*

---

## 공통 — 데모 편의 기능
- `ShaderGuideUI` (`00_Common`): Game 뷰에 셰이더별 조작법을 보여주는 IMGUI 패널이에요. Inspector의 `Entries`에서 문구를 고칠 수 있어요.
- 모든 키 입력은 프로젝트 설정(새 Input System)에 맞춰 `UnityEngine.InputSystem.Keyboard`로 받아요. 예전 Input Manager를 쓰는 환경에서도 동작하도록 처리해 뒀어요.

## 확인 사항 체크리스트
- [x] 셰이더 3개 이상, 서로 다른 방식 / 효과
- [x] `Assets/02. MyShaders/0N_<이름>/` 폴더 규칙, `Shader "MyShaders/<이름>"` 경로
- [x] 데모 씬: `Study Shader.unity`
- [ ] 스크린샷 3장 `Docs/Screenshots/`에 추가
- [ ] Unity에서 열었을 때 컴파일 에러 / 분홍색 머티리얼 없음 최종 확인
