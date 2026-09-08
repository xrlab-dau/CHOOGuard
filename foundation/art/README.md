# 오브젝트별 레퍼런스 기반 역사 자산

2026-09-07 후속 사용자 요구에 따라, 모든 자산을 개별 원본 이미지와 관찰한 형상에 연결한다. 전체 맞이방 사진이나 FBX 개수만으로 재현 완료를 주장하지 않는다. [자산별 감사](../../docs/art/object-reference-audit.md), [참조 레지스트리](object-references.json), [전체 맞이방 참고](../../docs/art/public-station-references.md)를 함께 읽는다.

## 구성과 범위

- 33종: 장치 12종, 가구·장갑·동일 익명 NPC 6종, 건축·가상 유도 15종. 실제 장면의 사용 여부는 Editor inventory와 대조한다.
- 부산역 사진에서 확인한 목재 무등받이 벤치·곡면 안내대·기둥·천장 형상과, 제조사 장치의 대체 외형을 구분한다. 등록된 `station_specific`은 사진의 위치 식별이며 모델의 실측 일치를 뜻하지 않는다.
- 수동 발신기·방송 콘솔·터치 단말·기울어진 경로 단말·태블릿 거치대는 서로 다른 형상이다. 임시 설치대와 게임 동작은 실제 KORAIL 장치/절차로 검증되지 않았다.
- WallModule/FloorModule은 물리·구조를 연결하는 unit adapter다. 패널·지붕재·보·격자 천장·점자블록은 별도 Blender 모델을 사용한다. UI·가상 유도·배경 폐쇄면의 범위는 레지스트리 `sceneFamilies`에 기록한다.
- 실제 도면·시설 치수·현재 배치·직원 매뉴얼·현장 효과는 미검증이다. 합성 평면을 실제 부산역 전체 평면으로 표시하지 않는다. 장치 화면의 내용과 조작 의미는 현재 게임의 임시 인터페이스다.

## 제작과 임포트

Blender 4.5.9 LTS에서 아래 실행기를 사용한다. [장치](../../scripts/art/station_equipment.py), [가구·인물](../../scripts/art/station_furniture.py), [건축](../../scripts/art/station_architecture.py) 모듈과 공통 드라이버가 재생성 원본이다.

```sh
blender --background --threads 2 --python-exit-code 1 --python scripts/art/build_station_assets.py
python3 scripts/art/validate_reference_assets.py
python3 scripts/art/validate_reference_assets.py --asset RadioConsole
```

`station-kit.blend`는 카탈로그/자산별 편집 장면, FBX는 Unity 전달물이다. 33 FBX와 .blend 총 34개 파일은 Git LFS로 받는다. 사진은 형태 관찰에만 사용하며 게임 텍스처·원본 이미지·로고를 복제하지 않는다. 절차적 작은 표면 텍스처, 미터 형상, 피벗, 광택/금속성/투명도를 사용하며 무거운 로컬 모델링·렌더·베이크는 수행하지 않는다.

좌표는 Unity +Y 위, +Z 이동 전방이다. 장치 앞면은 -Z, NPC 앞면은 +Z다. 현재 고정 도구 체인의 FBX X축 반전을 보상해 `Unity(x,y,z) → Blender(-x,-z,y)`를 사용한다. 역변환·열린 면의 winding·바닥 UV 방향·게이트 열린/닫힌 Renderer와 Collider·좌우 신발/엄지 위치를 실제 임포트 시험으로 확인한다. 이 어댑터는 근거 없이 다른 importer 설정에 재사용하지 않는다.

Unity 6000.3.23f1에서 `CHOOguard > Foundation > Build Playable Demo`로 생성한다. 생성기 v6는 이전 v1~v5 소유권을 확인하고 새 슬롯의 팀 파일을 덮어쓰지 않는다. Collider는 작은 장치의 실제 주요 부품마다 생성하며, 같은 이름의 여러 부품을 합쳐 빈 공간을 막지 않는다. NPC 시각 모델만 바닥에 정렬하고 항법 Root·사건 규칙을 바꾸지 않는다.

## 실제 출력 검수

전용 개발 검수 플레이어는 각 자산의 전체 및 확대 정면/측면을 실시간으로 표시한다. 훈련 프로그램의 플레이 흐름에 들어가지 않는다.

```text
Unity batch method: ChooGuard.Foundation.Demo.Editor.FoundationArtAudit.BuildMacReviewBatch
Output: Builds/ArtReview/ChooGuardArtReview.app
Capture argument: --choo-art-captures <task-local-output-directory>
Inventory method: ChooGuard.Foundation.Demo.Editor.FoundationArtAudit.ExportSceneInventoryBatch
Inventory argument: --choo-art-inventory <output.json>
```

캡처와 원본을 비교한 뒤 해당 FBX 해시·두 시점의 캡처 해시·관찰 범위를 `visualReview`에 기록한다. 참고 자료가 있다는 것, 시각 검수를 했다는 것, 실제 시설과 일치한다는 것은 서로 다른 판단이다. 검증 도구는 실제 시설/절차 수용을 PASS로 만들지 않는다. 새 데이터·코드·자산 변경은 [context graph](../../docs/context/README.md)에도 함께 연결한다.
