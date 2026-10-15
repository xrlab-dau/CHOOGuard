# 부산역 공식 2층 도면 → CAD·3D 모델 연구 및 도구 조사

확인일: 2026-10-09. 사용자가 지정한 `official-100083811-store-plan-page-1-embedded-2.jpeg`를 대상으로 공식 논문, 저자 저장소, 제품 문서를 조사했다. 제품 설명과 논문 결과를 검토한 단계이며, 부산역 도면의 자동 변환·모델 추론·유료 의뢰를 실행한 결과는 아니다.

## 결론

**도면 이미지로 편집 가능한 건축 모델을 만드는 기술과 서비스는 있다.** 이번 입력에는 도면 인식 → 벽·개구부·기둥 등의 벡터 구조 → Blender의 요소별 건축 모델 순서가 가장 직접적이다. Fraunhofer의 **Automatic Reconstruction of Semantic 3D Models from 2D Floor Plans (MVA 2023)**는 스캔 도면에서 벽·문·창을 추출하고 3D·IFC를 만드는 연구로, 요구한 작업에 가장 가까운 참고 사례다. 단, 논문에서도 높이는 가정한다. 검토한 공식 페이지에서는 실행 코드·가중치 배포를 확인하지 못했다. [논문](https://arxiv.org/abs/2306.01642), [연구기관 설명](https://www.hhi.fraunhofer.de/en/departments/vit/research-groups/computer-vision-graphics/research-topics/ai-based-building-digitalization.html)

**권장안은 원본 픽셀 좌표를 보존하는 CV·CAD 작업과 구조 검토를 결합하는 것이다.** 주택용 인식기의 결과만으로 부산역의 계단·승강기·에스컬레이터·빈 공간을 확정하지 않는다. 자동화는 벽과 기호의 후보를 만들고, 원본 대조에서 확인한 구조를 Blender로 생성한다. 실제 도면보다 그럴듯한 새로운 배치를 만드는 생성 모델은 이번 목표에 사용하지 않는다. 이 권장안은 아래 자료를 부산역 원본에 적용해 판단한 것이며, 이미 구현되었다는 뜻이 아니다.

## 실제 입력과 복원 범위

- 원본 JPEG: **631×496**, 54,443bytes. SHA-256: `49695f811421e97ea1b4a0b0c6f6657bf917a90730a0d9e19e152d1e1117caf6`.
- 사용자는 이 원본을 해당 2층의 전체 구조 기준으로 지정했다. 벽, 문, 기둥, 매장 외곽, 수직 이동시설, 난간, 승강장 방향 통로, 본관–맞이방 연결을 모두 반영해야 한다.
- 사용자가 지정한 원본의 **X표시 영역은 공간이 없는 영역으로 마스킹한다.** 그것이 아래층까지 열린 아트리움인지, 외부인지 등 수직 단면의 구체적인 형상은 별도 근거와 구분한다.
- 원본 PDF에 일부 문자·표시용 벡터가 있는 것과, 이 건축 배경 JPEG의 벽선이 이미 CAD 벡터라는 것은 다르다. JPEG를 PDF로 감싸거나 확대해도 새로운 치수·구조 정보가 생기지 않는다. [AutoCAD PDFIMPORT 설명](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-F94D7059-5B4A-4396-97FE-765B1D037D56.htm)
- 이 도면에서 읽은 평면과 사진·다른 층 자료로 확인할 층고, 천장 단면, 층간 관계를 나누어 관리한다. 2층 도면 하나를 다른 층의 실측 도면으로 취급하지 않는다.

[원본 이미지 열기](../workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083811-store-plan-page-1-embedded-2.jpeg)

## 학술 연구와 공개 구현 비교

| 연구·구현 | 실제 입출력 | 이번 도면에서의 용도와 한계 |
|---|---|---|
| **Raster-to-Vector, ICCV 2017** | 래스터 도면 → 접합점·벽·문 등 벡터 구조. 저자 코드에는 3D popup 생성도 포함 | 벽과 문을 기하 구조로 바꾸는 출발점. 주거 도면 기반이며 최신 Mac 실행과 부산역 품질은 미검증. MIT 코드. [논문](https://openaccess.thecvf.com/content_iccv_2017/html/Liu_Raster-To-Vector_Revisiting_Floorplan_ICCV_2017_paper.html), [코드](https://github.com/art-programmer/FloorplanTransformation) |
| **CubiCasa5K, SCIA 2019** | 도면 → 벽·방·아이콘 분할과 접합점, 후처리 polygon | 벽·문·위생기구의 후보 검출. 공개 예제는 방12/아이콘11/접합점21채널이며 승강기·빈 공간 전용 분류가 없다. CC BY-NC 4.0. [논문](https://arxiv.org/abs/1904.01920), [코드](https://github.com/CubiCasa/CubiCasa5k) |
| **Deep Floor Plan Recognition, ICCV 2019** | 도면 → 벽, 문/창, 방 종류의 픽셀 분할 | 다른 모델과 벽 경계를 교차 확인하는 보조 후보. 직접 CAD·3D를 출력하는 모델은 아니다. 공식 구현은 오래된 TensorFlow/CUDA 환경. GPL-3.0. [논문](https://arxiv.org/abs/1908.11025), [코드](https://github.com/zlzeng/DeepFloorplan) |
| **Raster-to-Graph, EG/CGF 2024** | 도면 → 벽 접합점·연결선 그래프 | 국소 벽 구조 검출 후보. 학습에서 연결된 그래프와 접합점10–50개로 제한해 부산역 전체 구조와 차이가 크다. GPL-3.0. [논문](https://doi.org/10.1111/cgf.15007), [코드](https://github.com/SizheHu/Raster-to-Graph) |
| **Automatic Reconstruction of Semantic 3D Models from 2D Floor Plans, MVA 2023** | 스캔 도면 → 벽 polygon·문/창 → 3D·IFC | 목표와 가장 가까운 설계 참고. 기울어진 벽, 개구부 결합, 3D화를 다룬다. §2.5에서 높이를 가정하며 공식 실행 코드 배포는 확인되지 않았다. [논문](https://arxiv.org/abs/2306.01642), [Fraunhofer](https://www.hhi.fraunhofer.de/en/departments/vit/research-groups/computer-vision-graphics/research-topics/ai-based-building-digitalization.html) |
| **FloorplanToBlender3d** | OpenCV 도면 처리 → Blender 프로젝트·메시 객체 | 로컬 변환 시험에 직접적인 공개 도구. 동명 학술 논문은 확인하지 못했다. CUDA 학습 모델이 필수인 구조는 아니지만 Mac 실행은 미검증. 자동 방 닫기가 열린 통로를 막는지 확인 필요. GPL-3.0. [코드](https://github.com/grebtsew/FloorplanToBlender3d) |
| **FloorNet, ECCV 2018** | RGB-D·점군 → 벡터 평면 | 현재 JPEG를 직접 넣는 후보에서 제외. 현장 스캔을 확보한 이후의 다른 입력 경로다. MIT. [논문](https://arxiv.org/abs/1804.00090), [코드](https://github.com/art-programmer/FloorNet) |
| **CADTransformer, CVPR 2022** | 기존 벡터 CAD/SVG 기본요소 → 의미·인스턴스 분류 | 벡터화가 끝난 후 계단·승강기·에스컬레이터·위생기구를 검사하는 조건부 후보. JPEG 자체를 CAD로 복원하는 모델은 아니다. 코드 MIT, 데이터 조건은 별도. [논문](https://openaccess.thecvf.com/content/CVPR2022/papers/Fan_CADTransformer_Panoptic_Symbol_Spotting_Transformer_for_CAD_Drawings_CVPR_2022_paper.pdf), [코드](https://github.com/VITA-Group/CADTransformer) |

중요한 코드 근거가 있다. CubiCasa loader는 `Elevator`, `OpenToBelow`, `StairWell`을 Undefined로 합친다. 따라서 이 모델의 일반 성능이 좋다는 이유로 부산역의 승강기와 빈 공간을 구별했다고 보고할 수 없다. 반대로 CADTransformer의 기호 목록에는 승강기·에스컬레이터·위생기구가 있지만, 이미 벡터 기본요소가 있는 도면을 입력으로 요구한다. [CubiCasa loader](https://github.com/CubiCasa/CubiCasa5k/blob/c34440266665a11f4484eb06cd2e4b7d72ad76c1/floortrans/loaders/house.py), [CADTransformer 기호 설정](https://github.com/VITA-Group/CADTransformer/blob/4cf93756642244e4cea35d99912b2d09b20f7387/config/anno_config.py)

라이선스는 검토한 공식 저장소 표기를 기록했다. 후보별 코드·데이터·가중치 조건과 실행 환경은 [학술 세부 보고서](../.planning/2026-10-09-interior-99-reconstruction/floorplan-conversion-research/academic/academic-floorplan-conversion-report.ko.md)에 구분했다. 설치·이식 또는 부산역 추론 성능을 확인한 것은 아니다.

## CAD·3D 제품과 서비스

| 후보 | 확인된 기능 | 적용 판단 |
|---|---|---|
| **Scan2CAD** | JPEG/PDF 등의 래스터를 DWG/DXF로 벡터화, 기하 인식·OCR·축척 설정. macOS 지원 | 기술도면 벡터화 우선 후보. 선이 벡터가 되는 것과 벽·문·기둥 의미가 자동 확정되는 것은 구분한다. [공식 변환 문서](https://www.scan2cad.com/docs/converting-raster-images-cad/) |
| **QCAD Professional** | macOS CAD 편집, `bmp2dwg` 비트맵 추적으로 DWG/DXF 생성 | 벡터화 비교와 구조 레이어·폐합·개구부 보정 후보. 자동 BIM 의미 복원은 확인되지 않았다. [공식 매뉴얼](https://qcad.org/doc/qcad/latest/reference/en/qcad_reference_manual_en.html#bmp2dwg) |
| **Getfloorplan 맞춤 GLTF/GLB** | 입력 도면에서 벽·파티션·개구부만 포함하는 편집 가능한 건축 장면. Blender·Unity 사용 안내 | 직접 3D 변환 서비스 우선 검토 후보. 상업공간 지원은 공지되어 있으나 부산역 규모·코어·빈 공간 처리 정확도는 미검증. 모델 출력은 맞춤 견적 항목. [공식 출력 설명](https://getfloorplan.com/news/automated-GLTF-scene-export), [상업공간](https://getfloorplan.com/news/commercial-spaces), [가격표](https://getfloorplan.com/pricing) |
| **Planner5D B2B** | 이미지/CAD → 편집 가능한 프로젝트; IFC/DWG/DXF 출력은 beta | 계약/API 경로의 조건부 후보. 일반 PRO의 DWG/DXF는2D이며 이 권한과 다르다. IFC 실제 모델·부재 분리는 샘플 미검증. [B2B 문서](https://support.planner5d.com/en/articles/15189751-planner-5d-b2b-api-technical-overview), [일반 PRO 제한](https://support.planner5d.com/en/articles/9582739-export-projects-in-dwg-or-dxf-formats) |
| **Live Home 3D Pro** | Mac에서 이미지 위 수동 트레이싱 → OBJ/FBX/glTF/GLB | 자동 판독을 기다리지 않는 직접 편집 대안. [Mac 출력 문서](https://www.livehome3d.com/support/help/mac/en/working-with-projects-exporting-a-3d-model) |

Coohom의 전체 모델 출력 제한, RoomSketcher의 외부 편집 파일 제한 때문에 두 제품의 화면상3D를 Unity용 모델 산출물로 추천하지 않는다. Floorplanner Enterprise API에는GLB 메시 출력이 있으나 이번 조사에서 JPEG 자동인식 경로는 확인되지 않았다. [Coohom 출력 제한](https://www.coohom.com/helpcenter/what-can-i-export-download), [RoomSketcher 출력](https://help.roomsketcher.com/hc/en-us/articles/360001709837-How-Do-I-Download-and-Print-My-Floor-Plans-from-Web-Portal), [Floorplanner API](https://floorplanner.readme.io/reference/exportproject)

Aside CLI 브라우저로 Scan2CAD, Planner5D B2B, Getfloorplan, Fraunhofer, ICCV 원문 페이지를 추가 확인했다. Getfloorplan 공식 링크의 공개 데모 폴더에서 전체 장면·최적화 장면·벽 전용 장면의 구분을 확인했다. 이 사실만으로 부산역 변환 품질이 입증되지는 않는다.

## 부산역에 적용할 구체적인 순서

1. **원본 좌표 기준을 통일한다.** JPEG 픽셀을 기준 데이터로 두고 본관, 맞이방, 연결부를 서로 다른 축척으로 임의 보정하지 않는다. 별도로 떨어져 그려진 승강장 도해는 실제 공간에서의 관계를 확인해 등록한다.
2. **도면을 의미별로 분리한다.** 외벽, 내벽·매장 경계, 기둥, 문·개구부, 계단, 에스컬레이터, 승강기, 난간·펜스, 빈 공간, 승강장 통로, 본관–맞이방 연결을 각각 추적한다. 문자·보조선·화장실 기구를 벽 또는 승강기와 혼동하지 않게 한다.
3. **CV와 CAD는 후보를 만든다.** Raster-to-Vector/CubiCasa의 벽·문 후보 또는 Scan2CAD/QCAD의 벡터를 원본에 겹친다. 기울어진 외벽, 반복 기둥, 작은 출입구, 열린 맞이방 경계가 보존되는지 검토한다. 최신 Mac에서 바로 실행된다고 검증하지 않은 연구 코드는 별도 시험 대상으로 둔다.
4. **검토한 구조를 요소별 모델로 만든다.** 벽의 두께·문 개구부·기둥·바닥·빈 공간 마스크를 SVG/DXF/JSON으로 관리하고 Blender의 독립 객체로 생성한다. 이 형식 연결은 프로젝트 통합 작업이며 모든 논문이 기본 제공한다는 뜻이 아니다.
5. **높이와 상하층을 별도 근거로 결합한다.** 사진과 다른 층 자료로 천장·층고·코어축을 확인한다. 승강기 표시는 shaft/car/door와 위생기구를 구별한 뒤 층간 바닥 개구부에 반영한다. 아직 설치가 차단된 승강기 수치 초안을 정답으로 사용하지 않는다.
6. **원본과 결과를 비교한다.** 최상부 정사영 중첩, 벽 외곽/중심선 오차, 문 끝점·폭, 기둥 수와 중심, 연결 통로 그래프, 빈 공간 면적·경계, 층간 코어 편차를 기록한다. 그다음 실제 시점에서 맞이방 연결 시야·발매기 뒤 벽·수직 이동시설을 비교한다. 모델의 논문 benchmark 점수나 전체 이미지의 흰 픽셀 비율을 부산역 일치율로 사용하지 않는다.

이 순서는 기존 개별 사물 배치 중심의 보완을 전체 건축 구조 복원으로 전환하기 위한 권장안이다. 시야가 열려야 하는 연결부에 벽을 추가하거나, 빈 공간을 바닥으로 닫는 식의 오류를 구조 단계에서 잡는 것이 목적이다. 도면이 직접 제공하는 평면 구조의 누락·오배치를 먼저 없애고, 수직 구조와 외관 재질은 해당 근거로 확정한다.

## 이번 조사에서 완료한 것

- 학술·구현8개와 CAD·3D 제품 후보를 공식 출처로 비교했다.
- 논문 PDF2개, 공식 코드·라이선스·requirements·선택 코드와 저장소 commit을 로컬 조사 폴더에 보관했다.
- Aside 브라우저로 주요 공식 본문과 공개 데모 링크를 확인했다.
- **부산역 도면 업로드, 계정 생성, 결제·유료 의뢰, 모델 설치·추론, CAD/3D 변환, Unity Scene 수정은 이번 조사에서 수행하지 않았다.**

세부 자료: [학술 비교](../.planning/2026-10-09-interior-99-reconstruction/floorplan-conversion-research/academic/academic-floorplan-conversion-report.ko.md), [CAD 비교](../.planning/2026-10-09-interior-99-reconstruction/floorplan-conversion-research/cad-tools/comparison.md), [3D 서비스 비교](../.planning/2026-10-09-interior-99-reconstruction/floorplan-conversion-research/3d-services/report.md).
