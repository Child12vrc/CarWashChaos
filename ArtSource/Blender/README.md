# ArtSource/Blender — 3D 원본(.blend) 관리

Unity `Assets/` 밖에 두는 Blender 원본 폴더입니다. 게임은 여기서 내보낸 `.glb`만 사용합니다.
`.blend`는 git에 포함하고, 백업 파일(`*.blend1`, `*.blend2`)은 `.gitignore`로 제외합니다.
Blender 5.0 기준입니다(Steam 설치: `C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe`).

## 폴더 구조
| 폴더 | 내용 | 비고 |
|---|---|---|
| `Vehicles/` | 차종당 1파일: `V01_Hatchback.blend`, `V02_Sedan`, `V03_Pickup`, `V04_Minivan`, `B01_MonsterTruck` | 차량 외관(참조) + 실내 + 도어 카드 |
| `Parts/Parts_Library.blend` | 재사용 부품 라이브러리(Interior / Exterior 콜렉션) | 차량 파일들이 메시를 **Link**해서 사용 |
| `Parts/Interior/`, `Parts/Exterior/` | 부품별 단독 작업 파일이 필요할 때 사용 | 현재는 비어 있음 |
| `Props/CarWash/` | 셀프세차 설비(결제기, 붐 암, 호스 릴 등) | 예정 |
| `Props/Street/`, `Props/Office/` | 거리 소품, 사무실 소품 | 예정 |
| `Buildings/` | 세차 베이, 캐노피, 사무실 | 예정 |
| `Critters/`, `Scenery/` | 동물, 나무·가로등 | 예정 |
| `_scripts/` | 생성 스크립트(`parts_lib.py`, `build_parts_library.py`, `build_interiors.py`) | |
| `export_all.py` | 내보내기 스크립트 | 아래 참고 |

## 네이밍 규칙
- 이름은 영문 PascalCase만 씁니다. `Cube.001` 같은 자동 이름은 남기지 않습니다.
- **오브젝트 이름 = 게임 노드 이름**입니다(예: `Body`, `Door_L`, `Glass`, `Wheel_FL`, `Socket_*`, `SteeringWheel`, `Hand_L`, `Seat_Driver`). Unity 코드가 이 이름으로 노드를 찾으니 바꾸지 마세요.
- 메시 데이터는 `SM_<오브젝트명>`, 재질은 `MAT_<이름>`(예: `MAT_Fabric_Seat`)입니다.
  - 예외: 기존 차량 GLB에서 가져온 외관 재질(`Soap Blue` 등)은 Unity 매칭 때문에 원래 이름을 유지합니다.
- 카메라·조명은 `CAM_Preview`, `LGT_Sun`, 월드는 `WLD_Preview`입니다.
- 콜렉션 이름:
  - `Body`: 차량 외관(현재는 참조용이라 내보내지 않음)
  - `Sockets`: 차량 Socket_* 빈 오브젝트
  - `Interior`: 실내. 하위에 `Interior_Sockets`가 있습니다.
  - `Interior_Doors`: 도어 카드
  - `Exterior_Parts`: 외관 부품
  - `Export`: 내보낼 콜렉션들을 자식으로 연결
  - `Preview`: 카메라·조명

## 파일 정리 규칙
- 스케일은 1(적용된 상태)이고, 단위는 미터입니다. 좌표는 Blender `-Y` = 차 앞(Unity `+Z`), `+X` = 운전석/왼쪽(Unity `-X`), `+Z` = 위입니다.
- 원점: 실내 루트 `Interior_<id>`는 실내 바닥 중앙입니다. 부품은 `parts_lib.py` 각 함수의 주석에 원점을 적어 두었습니다(시트 = 쿠션 아래 바닥, 핸들 = 허브 중심 등).
- 스크립트로 저장할 때 미사용 데이터를 정리(purge)하고 압축 저장합니다. 파일을 열면 `CAM_Preview` 카메라가 바로 잡혀 있습니다.

## 내보내기
`Export` 콜렉션의 자식 콜렉션마다 커스텀 속성이 있습니다(콜렉션 속성 → Custom Properties).
- `export_path`: 프로젝트 루트 기준 .glb 경로(예: `Assets/_Project/Art/Models/Interiors/Interior_V01.glb`)
- `zero_root`: 1이면 내보낼 때만 최상위 오브젝트를 원점으로 옮깁니다. 그래서 차 안에 놓인 실내도 자기 원점 기준으로 나갑니다.

내보내는 방법:
1. **Blender에서 수정한 뒤**: Scripting 탭 → `ArtSource/Blender/export_all.py` 열기 → Run Script. 현재 파일만 내보냅니다.
2. **명령줄에서 전부 내보내기**:
   `blender -b --factory-startup --python ArtSource/Blender/export_all.py -- [이름필터]`
3. **명령줄에서 파일 하나만**:
   `blender -b --factory-startup ArtSource/Blender/Vehicles/V01_Hatchback.blend --python ArtSource/Blender/export_all.py`

라이브러리 부품을 고친 경우:
1. `Parts_Library.blend`에서 `SM_<부품>` 메시를 고치고 저장합니다.
2. 라이브러리와 차량 파일을 다시 내보냅니다. 차량 파일은 메시를 Link하므로 다시 열면 자동 반영됩니다.

주의: 생성 스크립트(`_scripts/build_*.py`)를 다시 돌리면 .blend가 새로 만들어집니다. 손으로 고친 내용이 덮어써지니, 손으로 고친 뒤에는 `export_all.py`만 쓰세요.

## 부품 재사용 방식(모듈식 조립)
- **고정 부품**은 `Parts_Library.blend`의 메시를 Link합니다: SteeringWheel, SteeringColumn, InstrumentCluster, Vent_Round, GearLever, Pedals, FloorMat, AirFreshener, TrashBin_Small.
- **치수가 차마다 다른 부품**은 `parts_lib.py` 함수로 차 치수에 맞춰 차량 파일 안에서 생성합니다: 시트·벤치(등받이 높이), 대시보드 셸, 바닥, 콘솔, 룸미러 스템, 리어 패널, 도어 카드.
- 재질 4종(`MAT_Fabric_Seat` 흰색 = 게임에서 시트 틴트, `MAT_Fabric_Carpet`, `MAT_Plastic_Dark`, `MAT_Rubber_Black`)도 라이브러리에서 Link해서 모든 차가 같은 재질을 씁니다.
- 새 차종 추가: `build_interiors.py`의 `CARS`에 한 줄을 넣고 실행합니다. 차량 GLB의 Glass 박스로 캐빈을 재고, 레이캐스트로 클리핑을 검사하면서 맞춥니다.

## .blend ↔ .glb 대응표
| .blend | Export 콜렉션 | .glb (Assets/_Project/Art/Models/…) |
|---|---|---|
| Parts/Parts_Library.blend | EXP_<부품> ×16 | Parts/Interior/<부품>.glb |
| Vehicles/V01_Hatchback.blend | Interior, Interior_Doors | Interiors/Interior_V01.glb, Interiors/InteriorDoors_V01.glb |
| Vehicles/V02_Sedan.blend | Interior, Interior_Doors | Interiors/Interior_V02.glb, InteriorDoors_V02.glb |
| Vehicles/V03_Pickup.blend | Interior, Interior_Doors | Interiors/Interior_V03.glb, InteriorDoors_V03.glb |
| Vehicles/V04_Minivan.blend | Interior, Interior_Doors | Interiors/Interior_V04.glb, InteriorDoors_V04.glb |
| Vehicles/B01_MonsterTruck.blend | Interior, Interior_Doors | Interiors/Interior_B01.glb, InteriorDoors_B01.glb |

차량 외관 v2(`Vehicles/<id>_v2.glb`)는 같은 차량 파일의 `Body` + `Exterior_Parts`에서 내보낼 예정입니다. 기존 차량 .glb는 덮어쓰지 않습니다.

### 소품 (Props)
| .blend | Export 콜렉션 | .glb |
|---|---|---|
| Props/CarWash/SW_<id>.blend | EXP_SW_<id> | Generated/SelfWash/SW_<id>.glb |

소품 공통 재질은 `MAT_PropAtlas`(아틀라스 `Textures/T_PropAtlas.png`: 아이콘·화면·라벨 + 색 견본 16개), `MAT_PropMetal`, `MAT_PropGlass`입니다.
- 아틀라스를 다시 만들기: `blender -b --factory-startup --python _scripts/build_props.py -- atlas`
- 소품 다시 만들기: `blender -b --factory-startup --python _scripts/build_props.py -- SW_PayTerminal …`
- 정의 파일: `_scripts/props_selfwash.py`. 좌표는 Unity 로컬 기준 `U(x, y, z)`를 씁니다.
