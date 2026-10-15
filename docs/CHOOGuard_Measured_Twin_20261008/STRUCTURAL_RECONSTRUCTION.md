# 부산역 내부 구조의 실제 관측 표면 추출

2026-10-08. 원본 공개 영상에서 실제 복원한 내부 블록 6개를 입력으로, **평면 표면 조각 70개와 관측 삼각형 411,730개를 실제 OBJ·PLY·Blender·FBX로 저장했다.** 실제 원본 카메라 시점 18개에서 구조 조각을 렌더하고 사진 위에 겹쳐 확인했다. Unity 생산 씬에는 이 작업에서 변경을 하지 않았다.

이는 전체 부산역 내부의 완성된 디지털 트윈이 아니다. 촬영된 여섯 구간에서 관측된 구조 증거이며, 물리 축척과 현장 좌표는 아직 검증되지 않았다. `reference_only`, `metric_verified:false`, `placement_export_allowed:false`를 유지한다.

## 실제 산출물

| 블록 | 평면 조각 | 원본에서 유지한 삼각형 | 관측 지지점 기록 수¹ | 입력 관측 표면 중 선택 면적² |
|---|---:|---:|---:|---:|
| retail-corner | 8 | 32,572 | 25,885 | 38.9% |
| food-court | 10 | 30,094 | 16,154 | 53.3% |
| ticketing-wall | 14 | 110,288 | 73,056 | 62.4% |
| retail-corridor | 14 | 117,609 | 79,300 | 62.0% |
| information-corridor-static | 11 | 38,350 | 22,774 | 33.9% |
| corridor-connector | 13 | 82,817 | 58,322 | 51.0% |

¹ 같은 점이 가까운 평면 조각을 지지하거나 중첩 블록에 반복될 수 있어 독립 실측점 개수가 아니다. ² 입력 복원 메시의 면적 중 선택 비율이며, 건물 전체 복원율이 아니다.

분류는 위쪽 천장/소핏 후보 38개, 수직 벽/점포전면/기둥면 후보 32개다. 서로 인접한 조각이 같은 실제 벽의 일부일 수 있으므로 이를 벽 70개나 설치 객체 70개로 세지 않는다. 보행자, 반사, 정합 뒤틀림이 만든 일부 후보가 남는다.

전체 출력: [`workers/twin/output/interior-structure-20261008`](../../workers/twin/output/interior-structure-20261008). 각 블록 안에는 다음 실제 파일이 있다.

- `plane-NN.obj`: 원본에서 선택된 삼각형의 위상과 UV를 유지하고 그 정점을 추정 평면에 투영한 조각. 빈 공간을 둘러싸는 사각형이나 볼록껍질 면을 추가하지 않았다.
- `plane-NN.support.ply`: 원본 OpenMVS 점 중 평면에 가깝고 실제 선택 삼각형의 투영 범위 안에 있는 점. 원래 사진 색을 유지한다.
- `plane-NN.boundary.obj`: 선택된 원본 위상의 경계 선분. 이것을 문이나 개구부로 자동 판정하지 않는다.
- `plane-NN.source-face-ids.json`: 원본 OBJ 해시와 정확한 0 기반 삼각형 번호.
- `structural-planes.relative.json`: 평면 식, 면적, 잔차, 조건부 부트스트랩 불확실성, 원래 시점 지지 목록, 원본 해시.
- `<block>.structure.blend`: 실제 조각 메시, 원본 UV·사진 텍스처, 모든 해당 블록의 관측 카메라를 저장한 Blender 파일.
- `<block>.structure.native-relative.fbx`, `materials.json`, `frame-probes.relative.json`: Unity 검토용 FBX와 `FpsSourceAssetImporter` 호환 재질 명세, 실제 FBX 축 변환을 확인할 기술용 Empty 기준점. 현장 설치 좌표로 해석하지 않는다.

## 추출과 검증

실행 worker는 [`structure_reconstruction.py`](../../workers/twin/structure_reconstruction.py)다. 원본 사진이 배정된 `reference_observed_surfaces.obj`를 직접 읽고 면적 표본 기반 평면 후보를 찾았다. 평면 거리 임계값은 입력 관측 경계 대각선의 0.0015배로 정했으며 미터 값을 가정하지 않았다. 사진상 아래 방향에서 계산한 카메라 상향 평균은 구조 분류의 보조 기준이며 측량된 수직축이 아니다.

지원점은 원본 가변 길이 `view_indices` 목록을 읽었다. 빠른 경계 검사 뒤 실제 선택 삼각형의 barycentric 범위에 들어오는 점만 저장했다. 설치하지 않은 SciPy 대신 기존 OpenCV FLANN으로 가까운 삼각형 16개를 보수적으로 조회한다. 이 접근은 매우 긴 삼각형의 지원점을 놓칠 수 있지만, 확인되지 않은 새 표면을 생성하지 않는다. OpenMVS 시점 번호를 이미지 파일명으로 임의 변환하지 않았다.

완료한 직접 검증:

- 여섯 블록의 원본 메시·점군·카메라·사진 텍스처 SHA-256이 그대로 유지됨.
- 70개 조각의 UV가 대응하는 원본 삼각형과 정확히 일치함. 한 블록에서 원본 삼각형 중복 배정 없음.
- 투영 정점과 평면 식이 유한하며 식 잔차 검사 통과. 새 미관측 채움 삼각형 0개.
- Blender 4.5.9 LTS에서 여섯 파일 실제 저장 및 재오픈 통과.
- 여섯 FBX 실제 새 세션 재수입, 삼각형/재질 이름/UV/기준점/원본 좌표 경계 검사 통과. FBX 좌표의 작은 저장 오차는 상대 단위이며 실측 오차가 아니다.
- 실제 촬영 카메라 세 개씩 총 18개 렌더 및 사진 겹침 검토 완료. 사진 1장당 조각 래스터 점유는 약 22.4–61.8%이며 건물 복원율을 의미하지 않는다.

평면 중심 잔차 RMS는 블록별 약 0.0103–0.0444 **상대 단위**다. 부트스트랩 수치는 선택된 복원 모델 안에서의 조건부 반복성을 나타내며 현장 구조 정확도나 0.02m 달성 증거가 아니다.

원본·구조 비교 그림: [`source-plane-overlay-comparison.jpg`](../../workers/twin/evidence/interior-structure-20261008/source-plane-overlay-comparison.jpg). 왼쪽은 보존된 원본 사진, 가운데는 실제 카메라에서 렌더한 관측 조각을 겹친 이미지, 오른쪽은 선택 조각만 표시한다. 검은 구멍·끊어진 경계·반사 오염이 남으며 임의로 채우지 않았다.

전체 해시/QA: [`artifact-integrity-and-source-camera-qa.json`](../../workers/twin/evidence/interior-structure-20261008/artifact-integrity-and-source-camera-qa.json), [`fbx-structure-integrity-qa.json`](../../workers/twin/evidence/interior-structure-20261008/fbx-structure-integrity-qa.json).

## 확인된 대응과 정합 제한

[`semantic-correspondence-anchors.json`](../../workers/twin/output/interior-structure-20261008/semantic-correspondence-anchors.json)에 정확한 원본 이미지 해시·수동 픽셀 주석·실제 원본 삼각형 ray 교점·기존 권위 파일 해시를 기록했다. 확인된 식별은 CAFE DE CRAFT 물고기 그림 석재 기둥 앞/옆면, 복순도가, 꽃들, PASCUCCI, StoryWay다. 6개 식별 중 5개는 복원 메시와 ray 교점을 가지며, 석재 기둥 앞면 물고기 그림은 해당 원본 시점에서 교점 없음으로 보존했다. **메시 교점은 사진 특징의 실제 표면 위치를 증명하지 않는다.** 아래 후속 검증에서 StoryWay 문자 ray 일부가 간판 메시 공백 뒤의 수평 표면에 닿는 문제를 확인했고, 이를 정합점으로 사용하지 않았다.

기존 `measure-2f-main/observations.json`의 `COL-L-CAFE-STONE`과 새 영상의 석재 기둥은 식별 대응이 성립한다. 그러나 이전 등록은 `positionalHeldOut:null`, 눈높이/평면 가정, 구간 스케일 보정과 위치 tier b를 명시한다. `majibang-plan.json`의 점포 좌표 역시 안내도-기존 모델 정합 잔차 최대 12.6m 등을 명시한다. 점포 영역/기둥 경계 상자는 새 사진에 주석한 문자·그림의 **같은 물리 점 좌표**가 아니다. 따라서 숫자 정합점으로 승격하지 않았다.

[`supported-registration.relative.json`](../../workers/twin/output/interior-structure-20261008/supported-registration.relative.json)은 아래를 분리해 기록한다.

- 실제로 지원되는 상대 정합: 이전에 검증한 retail-corridor 및 information-corridor-static → corridor-connector의 동일 JPEG 해시 16개/12개 기반 카메라 정합. 상대 표면 비교에 사용할 수 있다.
- 부산역 좌표계 등록: `insufficient_numeric_same_feature_controls`, 변환 행렬 `null`, 숫자 현장 정합점 0개, 독립 현장 검증점 0개. 임의 축척·회전·평행이동을 생성하지 않았다.

## 여전히 보이지 않는 구조

기둥의 뒤쪽/깊이, 점포 배면벽과 폐쇄 내부, 보행자에 가린 하단, 샤프트 내부, 여섯 촬영 구간 밖의 대부분 건축 공간, 촬영 사이의 연속 벽·바닥·천장은 이 출력으로 확정할 수 없다. 선택 경계의 구멍은 관측 손실일 수 있어 건축 개구부로 세지 않았다. 구조 전체를 복원 대상으로 유지하되, 이 자료의 공백을 임의 벽으로 메워 완성으로 표시하지 않는다.

이 단계는 전통적 사진측량으로 얻은 실제 구조 증거 추출이다. NeRF/3DGS 학습 결과가 아니며, 별도 동일 자료 비교 실행으로 품질과 한계를 검증해야 한다.

## 후속: 기존 맵 좌표와의 수치 정합을 실제 시험한 결과

`station transform:null`에서 조사를 끝내지 않고 실제 씬 인벤토리 `current-scene-structure.json`과 그 원본 바이너리 정점·UV·renderer 계층 행렬을 확인했다. [`structure_registration.py`](../../workers/twin/structure_registration.py)가 복순도가·꽃들·PASCUCCI·StoryWay의 정확한 모델 소유자를 식별하고, 현재 Unity `.asset` 정점이 실제 씬 추출 바이너리와 동일함을 대조했다. 모델 네이티브 좌표는 `MajibangBuilder.P(u,v,y)=(v,y,u)`이며 실제 renderer 행렬을 적용했다. 사진측량 좌표를 기존 모델 좌표로 곧바로 혼동하지 않았다.

동일한 사진 글자 특징을 **서로 다른 원본 촬영 이미지의 보존된 카메라 2개 이상에서 직접 삼각측량**해 특징점 43개를 만들었다. 복순도가 26개, PASCUCCI 13개, StoryWay 4개다. 모두 진단용 재투영 최대 3px·시차 1° 이상 조건을 통과했지만, StoryWay의 시차는 1.07–1.36°로 작고 문자 범위가 좁아서 특징 평면의 방향은 약한 근거다. 같은 JPEG가 겹친 블록에 반복되면 하나의 촬영 시점으로 세었다.

초기 최근접 메시 ray 방식은 StoryWay 문자 ray 일부가 간판의 빈 복원 부분 뒤 `plane-04` 수평 표면에 닿는 문제를 드러냈다. 이 방식의 실패 기록은 `registration/rejected-nearest-mesh-ray-controls/`에 보존하고 **최종 정합에서는 모든 사진 특징의 mesh ray 교점을 제외했다.** 현재 ray/구조 평면 비교는 배경 표면 오인의 진단 자료이며 실제 문자 표면의 대응 근거로 해석하지 않는다.

석재 기둥 스커팅 하단의 같은 모서리도 원본 카메라 2–3개에서 삼각측량해 3개 후보를 만들었고, 그 중 재투영 오차 최대 5.6px인 한 후보는 수치 정합에서 제외했다. 나머지 하단 후보도 짧은 촬영 기준선 때문에 조건부 불확실성이 커서 현장 측량점으로 승격하지 않았다.

점포 소유자별 균등 가중치를 사용한 유사변환을 두 좌표 parity로 실제 계산하고, 별도 사진 특징점 및 소유자 제외 검증을 수행했다. 아래는 카메라 상향이 기존 모델 위쪽과 맞는 parity -1 후보의 결과다. parity +1도 별도로 시험했지만 통과하지 못했다.

| 조합 | 사용한 모델 대응점 | 맞춤 RMS (기존 모델 단위) | 별도 특징점 재투영 중앙값 | 소유자별 요구 축척비 |
|---|---:|---:|---:|---:|
| PASCUCCI + StoryWay | 17 | 0.323 | 24.6px | 1.256 |
| 복순도가 + 석재 기둥 하단 | 28 | 0.583 | 60.3px | 4.558 |

**네 번의 수치 변환은 모두 거절했다.** 계산한 행렬, 각 점의 오차, 제외한 후보, 평면 방향 불일치와 원본 카메라의 관측점/예측점 겹침 그림을 [`registration/tested-registration-result.json`](../../workers/twin/evidence/interior-structure-20261008/registration/tested-registration-result.json)에 보존했다. 상대 표면을 생산 씬의 벽으로 배치하지 않았다. 실제 점군과 잔차 선분도 `registration/geometry/`에 PLY·OBJ로 저장했으며, 이것은 실패한 정합의 진단 자료다.

사진 특징을 모델의 실제 UV quad로 연결하는 것은 **렌더된 모델 외관의 대응점**이다. 독립적으로 검증된 현장 3D 기준점이 아니다. 원본 사진 원근, 비측정 시각적 rectification, 모델이 정한 quad 크기와 상대 위치, 복원 표면 오차가 모두 정합 실패에 기여할 수 있어 원인을 건물 치수 오류 하나로 단정하지 않는다.

구체적인 생성 경로도 확인했다. [`MajibangBuilder.cs`](../../AgentScripts/MajibangBuilder.cs:1188)의 기존 `PhotoFascia`는 사진 비율을 유지하지만, 이번 실제 소유자들은 `Kit_Lightbox`이며 `KitStorefront`의 4130–4142행에서 `signWidth 또는 전면 길이−2×inset`, `fascia.height−2×inset`으로 정해진 quad에 사진을 매핑한다. 복순도가·꽃들 Storefront는 기본 inset 0.06, fascia 높이 0.6을 사용해 각각 폭 5.51/7.14와 높이 0.48인 quad를 만든다. 이때 현재 모델 이미지의 비율 변형은 각각 3.078/5.327배다. **이 비율만으로 실제 간판 폭·높이 또는 건축 치수가 틀렸다고 확정하거나 구조를 리사이즈할 수 없다.** 사진 영수증도 원근 보정이 실제 물리 비율 측정이 아님을 명시한다. 정확한 코드·파라미터·사진 영수증은 [`builder-photo-control-provenance.json`](../../workers/twin/evidence/interior-structure-20261008/registration/builder-photo-control-provenance.json)에 보존했다.

푸드코트의 실제 기존 모델 소유자 7개와 발권 벽/출발 안내판 소유자도 조사했다. 푸드코트의 일반 방·천장·프레임 배치와 사진 내부의 특정 점이 일대일 대응하는 증거가 없고, 발권 벽은 같은 안내판 사진을 반복한 quad들의 개별 소유자를 특정하지 못했다. 이름이나 한 벽 평면만으로 완전한 3D 변환을 강제하지 않았다. [`unregistered-block-model-extents.json`](../../workers/twin/evidence/interior-structure-20261008/registration/unregistered-block-model-extents.json)에 실제 소유자·바이너리 해시·좌표 범위와 남은 대응 공백을 기록했다.

다음 수치 정합에 필요한 증거는 넓은 촬영 기준선에서 가려지지 않은 같은 석재 기둥 상단/하단 모서리, 같은 시점의 꽃들 문자 또는 출입구 모서리, 반복 안내판 중 유일한 패널의 실제 모서리, 푸드코트의 고정 기둥/카운터 모서리다. 새 폐쇄 벽이나 임의 기준점은 만들지 않았다. 최종 해시/실행 QA는 [`registration-qa.json`](../../workers/twin/evidence/interior-structure-20261008/registration/registration-qa.json)에 있다.
