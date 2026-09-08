# 자산별 원본 참고와 모델 대조

2026-09-07. 기존의 맞이방 사진 몇 장을 전체 모델의 공통 근거로 제시한 방식은 **개별 오브젝트의 형태 재현을 입증하지 못했다**. 이번에는 원본·직접 이미지·관찰 특징·출처/권리 상태·제작 부품·치수 근거를 자산별로 연결한다.

## 이전 모델에서 확인된 문제

- 기존 벤치는 등받이가 있는 타공 금속 좌판/등판으로 만들었지만, 선택한 부산역 원본은 주로 등받이 없는 목재 슬랫 좌판과 금속 받침이다. 이 차이는 재질 색만 바꾸는 것으로 해결되지 않는다. [뉴스1/파이낸셜뉴스 원본](https://www.fnnews.com/news/202209081912067122), [KoreaToDo 맞이방 사진 묶음](https://www.koreatodo.com/busan-station).
- 상황 패널·경보 장치·무전 콘솔 등에 같은 큰 콘솔 몸체를 반복한 것은 각 제품의 관찰 형상을 반영하지 못했다. 이제 화면/베젤형, 작은 경보함, 통신 단말, 이동형 표지 등을 개별 출처와 제작 모듈에 연결한다.
- 안내 카운터의 원본은 기둥을 감싸는 호형 전면인데 이전 직사각 박스는 그 특징을 놓쳤다. 현재 모델의 호형 한 구간도 실제 전체 카운터의 실측 복제는 아니다. [원본 안내소 사진의 게시 페이지](https://www.koreatodo.com/busan-station).

## 현재 등록 범위

[자산별 참조 레지스트리](../../foundation/art/object-references.json)와 [export manifest](../../foundation/art/asset-manifest.json)는 같은 **33개 자산 ID**를 연결한다. 원본 사진·관찰 기록이 등록되었다는 것과 모델이 원본을 충분히 재현했다는 것은 별도 판정이다.

| 분류 | 개수 | 해석 |
|---|---:|---|
| 역사 사진 (`station_specific`) | 13 | 특정 역사 사진의 형태 참고. 현재 설치·측량·절차 검증 아님. |
| 제조사·일반형 (`category_proxy`) | 15 | 제조사/일반 제품 참고. KORAIL 설치품으로 확인된 것이 아님. |
| 단위 메시 기반 (`structural_base`) | 2 | Unity mesh-copy 호환을 위한 단일 unit cube. 모든 재사용 물체가 같은 실물 계열은 아님. |
| 가상 안내 (`virtual_guidance`) | 3 | 게임 안내 표시. 실제 시설물의 사진 일치나 표준 적합성을 주장하지 않음. |

`WallModule`·`FloorModule`은 의도된 단위 메시 예외다. 상세 벽 패널·지붕 패널·점자블록은 별도 ID로 관리한다. `NavigationArrow`·`AssemblyRing`과 게임용 표지는 가상 안내 범위를 명시하며 실물 설치 증거로 세지 않는다.

공개 제품 수치는 해당 제조사 모델/옵션의 값이다. 글러브 손바닥 항목처럼 의미가 모호한 값, 폴 길이처럼 확인하지 못한 값은 그대로 미확인으로 남긴다. `authoredBoundsUnity`와 manifest의 bounds는 **현재 제작 모델의 미터 단위 크기**이며, 사진에서 얻은 실측값이 아니다.

## 자산별 원문과 제작 위치

아래 링크는 원래 게시자/제조사 페이지다. 직접 이미지 URL·크레딧·재사용 라이선스 상태·관찰 특징은 레지스트리의 해당 행에서 확인한다. 공개 열람 가능성이 사진 재배포·텍스처 사용 허가를 뜻하지 않는다.

| 자산 | 참고 분류 | 원문 | 제작 모듈 |
|---|---|---|---|
| `AccessGate` | 제조사·일반형 | [원문 1](https://www.gunneboentrancecontrol.com/en_us/products/gates/metro-gates/swing-panel-gate/) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `AlarmSimulator` | 제조사·일반형 | [원문 1](https://apollo-fire.co.uk/products/series-65/mcp/55200-003apo-conventional-manual-call-point-outdoor/) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `AssemblyRegister` | 제조사·일반형 | [원문 1](https://us.bouncepad.com/products/floorstanding) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `AssemblyRing` | 가상 안내 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `AssemblySign` | 가상 안내 | [원문 1](https://www.safetybuyer.com/product/fire-assembly-point-sign-for-post-mounting) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `Bench` | 역사 사진 | [원문 1](https://www.fnnews.com/news/202209081912067122) | [station_furniture.py](../../scripts/art/station_furniture.py) |
| `CeilingLight` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `CeilingPanel` | 역사 사진 | [원문 1](https://themigratorybirds.com/ktx-south-korea-high-speed-train/) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `ClerestoryBay` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `DepartureBoard` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `DirectionSign` | 제조사·일반형 | [원문 1](https://www.tensator.com/event-signage-more-than-one-direction/) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `DoorFrame` | 제조사·일반형 | [원문 1](https://www.dormakaba.com/sg-en/offering/products/entrance-systems/automatic-sliding-doors/st-flex--do_2887) · [원문 2](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `Evacuee` | 제조사·일반형 | [원문 1](https://www.abstractmannequins.com/male.php) | [station_furniture.py](../../scripts/art/station_furniture.py) |
| `FloorModule` | 단위 메시 기반 | [원문 1](https://www.koreatodo.com/busan-station) · [원문 2](https://themigratorybirds.com/ktx-south-korea-high-speed-train/) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `Glove` | 제조사·일반형 | [원문 1](https://www.showaglove.co.jp/asia/product/detail/general_purpose/28) | [station_furniture.py](../../scripts/art/station_furniture.py) |
| `HazardIndicator` | 제조사·일반형 | [원문 1](https://shop.patlite.com/Blue-Rotating-Beacon-p/skh-m1j-b.htm) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `InformationIsland` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_furniture.py](../../scripts/art/station_furniture.py) |
| `InformationKiosk` | 제조사·일반형 | [원문 1](https://themigratorybirds.com/ktx-south-korea-high-speed-train/) | [station_furniture.py](../../scripts/art/station_furniture.py) |
| `Luggage` | 제조사·일반형 | [원문 1](https://www.samsonite.co.uk/proxis-spinner-expandable-length-40cm-55cm-matt-graphite/158191-4804.html) | [station_furniture.py](../../scripts/art/station_furniture.py) |
| `NavigationArrow` | 가상 안내 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `Pillar` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `Purlin` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `RadioConsole` | 제조사·일반형 | [원문 1](https://www.axis.com/en-us/products/axis-c6110) · [원문 2](https://www.axis.com/en-us/products/axis-tc6901-gooseneck-microphone) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `RallyPoint` | 제조사·일반형 | [원문 1](https://www.safetybuyer.com/product/mobile-assembly-point-sign-with-2-4m-collapsible-pole) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `RecessedLight` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) · [원문 2](https://themigratorybirds.com/ktx-south-korea-high-speed-train/) · [원문 3](https://www.signify.com/global/prof/indoor-luminaires/recessed/philips-coreline-panel-gen6/911401800887_EU/product) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `RoofPanel` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `RoofTruss` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `RouteConsole` | 제조사·일반형 | [원문 1](https://eu.peerless-av.com/products/kip522) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `SituationPanel` | 제조사·일반형 | [원문 1](https://www.advantech.com/en-us/products/utc-520-series/sub_64692d29-8576-4534-9b3c-81aad4db8330) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `TactileTile` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) · [원문 2](https://themigratorybirds.com/ktx-south-korea-high-speed-train/) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `TemporaryBarrier` | 제조사·일반형 | [원문 1](https://www.altradgeneration.com/en-gb/products/heavy-duty-hoarding-panel) | [station_equipment.py](../../scripts/art/station_equipment.py) |
| `WallModule` | 단위 메시 기반 | [원문 1](https://www.koreatodo.com/busan-station) · [원문 2](https://themigratorybirds.com/ktx-south-korea-high-speed-train/) | [station_architecture.py](../../scripts/art/station_architecture.py) |
| `WallPanel` | 역사 사진 | [원문 1](https://www.koreatodo.com/busan-station) | [station_architecture.py](../../scripts/art/station_architecture.py) |

## 로컬 비교 화면

비교기는 `.planning/2026-09-07-object-reference-audit/comparison.html`이다. 원본 사진은 ignored planning 안의 검토 사본을 SHA-256으로 찾으며, Git에 복사하거나 게임 텍스처로 만들지 않는다. 원본을 찾지 못하면 링크와 대기 상태를 표시한다.

이미 실행 중인 저장소 루트 서버에서는 [로컬 비교기](http://127.0.0.1:8769/.planning/2026-09-07-object-reference-audit/comparison.html)를 연다. 서버가 없다면 저장소 루트에서 `python3 -m http.server 8769 --bind 127.0.0.1`을 실행할 수 있다. HTML 파일 자체를 열어도 내장 메타데이터와 상대 경로 이미지로 동작한다.

- 검색/분류로 자산을 고르고 원본 이미지 여러 장을 전환한다. 출처·권리 상태와 관찰 특징을 함께 본다.
- 오른쪽은 `detail-{자산ID}-{각도}.png`의 실제 개별 캡처를 우선 표시한다. 정면+상단/측면+상단 두 각도이며, 아직 파일이 없으면 확인된 보조 시트 또는 실제 캡처 대기로 표시한다.
- 개별 캡처의 원본을 열 수 있고, `capture-complete.json`의 자산 순서가 확인된 경우 6개 모델이 담긴 보조 전체 시트도 제공한다. 시트 셀과 개별 이미지는 CSS로만 확대하며 파일을 자르거나 편집하지 않는다.
- 각 모델은 가장 긴 축이 같은 크기로 정규화되어 있다. 스크린샷에서 서로의 실제 치수를 비교하지 말고 아래의 공개 수치·제작 bounds를 사용한다.
- loopback 서버로 열면 현재 레지스트리/manifest와 `capture-complete.json`의 실제 자산 순서를 읽는다. 파일 직접 열기에서는 내장 메타데이터와 ID가 명시된 개별 캡처를 사용하고, 순서를 확인할 수 없는 시트 셀로 자동 대체하지 않는다.
- 캡처가 생성된 뒤 `캡처 다시 확인`을 누른다. 이미지가 로드되었다는 표시가 시각 검수 PASS로 바뀌지는 않는다.

네이티브 시트 생성과 셀 배치는 [FoundationArtAudit.cs](../../Packages/com.xrlab.chooguard.foundation/Demo/Editor/FoundationArtAudit.cs)와 [ArtReviewController.cs](../../Packages/com.xrlab.chooguard.foundation/Demo/Runtime/ArtReviewController.cs)를 따른다. [공유 Blender 생성기](../../scripts/art/build_station_assets.py)가 실제 부품 목록과 bounds를 export manifest에 기록한다.

## 장면의 일반 메시 재사용과 분류

33개 FBX 계열이 장면에 쓰인다는 확인과 모든 장식의 실물 계열이 동일하다는 주장은 구분한다. 현재 [레지스트리의 sceneFamilies](../../foundation/art/object-references.json)는 다음 9개 계열을 분류한다. 이 표는 교체·제외·합성 대체의 처리 상태이며 시설 동일성 판정표가 아니다.

| 장면 계열 | 현재 처리 | 연결 자산 | 범위 |
|---|---|---|---|
| `IncidentRestrictionBarrier` | `replaced` | `TemporaryBarrier` | 줄무늬 cube 대신 불투명 패널형 가설벽을 사용. 숨겨진 기존 경로 제한 Collider는 별도 유지. |
| `DoorHeadsLintels` | `structural_adapter` | `WallModule` | 합성 벽 개구부/문 상부의 구조 체적. 실제 역사 시공 모듈로 식별하지 않음. |
| `WallSkirtingBandsJoints` | `replaced` | `WallPanel` | 기존 장식 띠를 제거하고 패널 앞판/리빌/클립으로 대체. 촘촘한 원본 리브와의 형태 차이는 시각 메모에 남김. |
| `RoofPurlinsAndLuminaireSupportBeams` | `replaced` | `Purlin` | 플랜지/웹/끝 이음의 Purlin 사용. 이전 선형 조명 Beam은 제거하고 조명 지지부를 연결. |
| `GlazingUpperClosureMullionsTransoms` | `explicit_visual_proxy` | `ClerestoryBay` | 투명 판·프로파일·씰과 단순 상부 경계·밝은 배경을 결합. 모델링하지 않은 실제 외부 경관을 재현한 것이 아님. |
| `CeilingSlabs` | `replaced_visible_finish` | `CeilingPanel` | 낮은 천장 가시 마감은 CeilingPanel 격자, 고천장은 RoofPanel. 격자/배치 크기는 합성 공간에 맞춘 값. |
| `FloorJointStrips` | `removed` | `FloorModule` | 이전 줄눈 strip은 제거. 바닥은 미터 단위 UV와 별도 생성한 석재 줄눈을 사용. |
| `HallSignAndRuntimeLabels` | `virtual_guidance` | `AssemblySign` | 표지 모델을 사용하되 문구·TextMesh·조준점·경로 선은 가상 UI. |
| `BenchAndLuggageCollisionBoxes` | `physics_only` | `Bench` | 가시 cube와 충돌 proxy를 구분. 렌더링하지 않는 충돌 도형은 참조 모델 수에 포함하지 않음. |

건축 자산 15종의 실제 개별 캡처 30장을 대조했다. 초기 WallPanel의 넓은 네 판은 원본의 촘촘한 리브와 달라 수정했다. 긴 트러스 미세 접합의 해상도와 단색 배경에서의 유리 구별은 시각 검수의 한계로 남긴다. Purlin·점자 타일의 밝기/요철 대비와 가상 링의 가는 접합 흔적도 검수 메모에 남긴다. 사진 대응과 시각 검수 기록의 존재가 실제 시설 수용을 뜻하지 않는다.

실제 부산역의 현재 배치·설치 제조사·도면 치수·직원 매뉴얼·안전/접근성 적합성·현장 훈련 효과는 미검증이다. 원본에서 보이지 않는 접합부·뒷면·브래킷과 기존 동선에 맞춘 축소/분절은 합성 제작 상세로 남긴다. **참조 등록과 자동 시험을 시설 재현 수용으로 바꾸지 않는다.**

## 검증 상태

<!-- ROOT_VERIFICATION:BEGIN -->
최종 실행: Unity EditMode **106/106**, PlayMode **16/16**, 참조 검증 Python **11개**, context graph **15개** 검사 통과. 메인 Mac 빌드는 Renderer **891개**, 삼각형 **415,894개**다. 실제 장면 inventory에서 **33개 계열 전부 사용**을 확인했고, 네이티브 전체 시트 12장과 개별 확대 정면/측면 **66장**을 원본과 대조했다. 현재 FBX·캡처 해시는 레지스트리에 묶었다.

검수 중 발견한 게이트 좌우/충돌 불일치(0.8m), 열린 장갑 면 법선, 발 접지, 거치대 연결과 카운터 타공 음영을 수정하고 재검증했다. 넓은 판으로 남았던 벽 패널은 촘촘한 세로 리브로 보완했다. 현재 실행본의 눈높이 화면에서도 장치·목재 벤치·구조·NPC 이동을 확인했다. [정제 실행 기록](../evidence/foundation/2026-09-07-object-references.json).

이 결과는 같은 제공자의 형상·표면·임포트·동작 검수다. 실측 시설 일치, 현재 설치 제품, 기관 직원 절차, 생산 수준 수용, HMD 및 현장 학습 효과는 미검증이다. 제조사 대체 외형·합성 거치대/치수/색상과 실제 역사 사진의 관찰 범위를 혼동하지 않는다.
<!-- ROOT_VERIFICATION:END -->
