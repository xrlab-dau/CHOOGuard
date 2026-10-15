# 부산역 복원 방법 적용 근거와 실행 경계

공식 원논문·구현체를 기준으로 검토했다. **사진처럼 보이는 정도, 메시의 실제 위치, 조작 가능한 부품의 경계는 서로 다른 검증 대상이다.** 선택한 주 경로는 기존 COLMAP의 카메라/점군 정합 → OpenMVS의 표면 메시 → 현장 실측 정합·독립 검증 → Blender 부품 정리다. 특정 알고리즘을 선택했다는 이유로 2cm 위치 오차나 전수 완전성을 인정하지 않는다.

## 방법별 역할

| 방법·원자료 | 확인한 능력 | 부산역 적용과 한계 |
|---|---|---|
| Schönberger & Frahm, **Structure-from-Motion Revisited**, CVPR 2016. [원논문](https://openaccess.thecvf.com/content_cvpr_2016/html/Schonberger_Structure-From-Motion_Revisited_CVPR_2016_paper.html), [COLMAP](https://colmap.github.io/) | 이미지 집합으로 카메라와 희소 3D 구조를 추정한다. | 실제 이동과 겹치는 다시점 사진을 입력한다. 반복 천장·동일 간판의 잘못된 정합을 검사하고 고유 고정점·바닥 법선·루프 경로를 교차 확인한다. 축척은 현장 측정으로 정한다. |
| Schönberger et al., **Pixelwise View Selection for Unstructured Multi-View Stereo**, ECCV 2016. [COLMAP 공식 인용](https://colmap.github.io/), [OpenMVS 구현체](https://github.com/cdcseacave/openMVS) | 카메라·희소 점에서 조밀한 표면을 복원한다. OpenMVS는 메시 생성·정리·텍스처 단계를 제공한다. | 정합된 카메라와 마스크를 재사용해 고정 표면을 복원한다. 관측되지 않은 뒤쪽·유리 반사·가림은 임의로 완성하지 않는다. 메시 텍스처와 충돌은 각각 검증한다. |
| Mildenhall et al., **NeRF: Representing Scenes as Neural Radiance Fields for View Synthesis**, ECCV 2020. [프로젝트](https://www.matthewtancik.com/nerf), [원논문](https://arxiv.org/abs/2003.08934) | 알려진 카메라 시점의 사진으로 연속 방사장과 새 시점의 영상을 학습한다. | 외관 비교와 다시점 시각화의 연구 대안이다. 메시 추출 가능성이 있어도 버튼·문짝의 독립 객체 ID, 피벗, 물리, 절대 실측 정확도는 별도 작업이다. |
| Kerbl et al., **3D Gaussian Splatting for Real-Time Radiance Field Rendering**, SIGGRAPH/TOG 2023. [프로젝트](https://repo-sam.inria.fr/fungraph/3d-gaussian-splatting/), [공식 구현체](https://github.com/graphdeco-inria/gaussian-splatting) | 희소 점에서 3D Gaussian을 최적화해 새 시점을 빠르게 렌더링한다. | 외관 재현을 비교할 수 있다. 원 구현체의 학습은 CUDA 경로이며 현재 M1 로컬에서 실행했다고 주장하지 않는다. Gaussian 집합이 곧 개별 조작 물체나 검증된 충돌 메시인 것은 아니다. |
| Huang et al., **2D Gaussian Splatting for Geometrically Accurate Radiance Fields**, SIGGRAPH 2024. [프로젝트](https://surfsplatting.github.io/), [원논문](https://arxiv.org/abs/2403.17888), [공식 구현체](https://github.com/hbb1/2d-gaussian-splatting) | 표면 지향의 2D Gaussian 표현과 기하 정규화로 표면 복원을 개선한다. | 평면·모서리와 메시 표면의 대안 실험에 사용한다. 부산역 현장 측정에 대한 2cm 검증을 별도로 수행하며 원 논문의 평가값을 이 현장에 이전하지 않는다. |
| Guédon & Lepetit, **SuGaR: Surface-Aligned Gaussian Splatting for Efficient 3D Mesh Reconstruction and High-Quality Mesh Rendering**, CVPR 2024. [원논문](https://arxiv.org/abs/2311.12775), [공식 구현체](https://github.com/Anttwo/SuGaR) | Gaussian을 표면에 맞춰 메시를 추출하고 메시 기반 편집·렌더링으로 이어간다. | 메시 추출과 Blender 편집의 연구 대안이다. 문·버튼·가동부 분류와 피벗·설비 연결·현장 위치 검증을 자동으로 제공한다고 간주하지 않는다. |

## 현재 로컬 상태와 재사용

2026-10-08 직접 확인한 하드웨어는 **Apple M1, 메모리 16GB**다. Blender, yt-dlp, ffmpeg, pdftoppm 실행 파일은 PATH에서 확인했다. COLMAP/OpenMVS는 PATH에 없지만 실제 사용 가능한 로컬 경로를 확인했다. `.tools/video-reconstruction/bin/python`의 **pycolmap 4.2.0**과 `.tools/openmvs-v2.4.0/`의 **arm64 OpenMVS 2.4.0** 실행 파일을 재사용했다. 기존 프로젝트에는 SfM 산출물·OpenMVS 복원 기록·원본 SKP·실내 메시가 남아 있다. 설치 여부와 과거 실행 기록은 새 데이터의 복원 성공 여부와 구분한다.

기존 `.planning/2026-09-23-video-twin/reconstruction-method-receipt.json`은 **모델 단위가 인증된 미터가 아니고, 같은 영상의 holdout은 독립 물리 측량이 아님**을 명시한다. 또 잘못된 26.566도 바닥 기울기·큰 카메라 이동, OpenMVS 마스크 극성 문제를 기록한다. 이를 새 입력의 검사 항목으로 재사용하고, 오래된 `SfM-measured` 문자열을 현장 실측 인증으로 승격하지 않는다.

## 새 공개 자료로 실행한 소구획 시험

새로 내려받은 RY2qvE0Tugk 영상의 **컷 로컬 15–27초**에서 720px·2fps·24프레임을 추출해 COLMAP→OpenMVS를 실제 실행했다. **24/24 카메라 등록**, 희소점 2,954개, 평균 재투영 오차 **0.4867px**, 조밀점 19,372개, 메시 **11,429 정점 / 22,830 면**과 텍스처 OBJ를 생성했다. [실행 receipt](../../workers/twin/evidence/reference-pilot-final.json), [텍스처 메시](../../workers/twin/output/public-reference-pilot-20261008-v3/reference_textured.obj)

이 시험은 단일 과거 영상의 작은 상부 정적 영역 복원이다. 촬영일·절대 축척·현장 기준점·독립 측량은 없으므로 receipt가 `reference_only`, `metric_verified: false`, `placement_export_allowed: false`를 명시한다. **0.4867px는 이미지 재투영 오차이며 2cm 현장 정확도가 아니다.**

## 실행과 검증 순서

1. 출처·촬영 시점·원본 해시·파생 관계를 intake로 검사한다. 과거 공개 참조는 `reference_only`로 유지한다. 한 영상의 여러 컷을 여러 독립 촬영으로 세지 않는다.
2. 동일 시점의 작은 블록으로 먼저 SfM을 수행한다. 반복 무늬 정합, 여러 끊어진 모델, 바닥 법선, 층간 연결, 카메라 이동을 확인한다. 알려진 현장 기준점으로 축척·회전·이동을 맞춘다.
3. 사람·움직이는 설비·반사를 선별하고 MVS 메시를 만든다. 이미지의 일치도와 별도로 고정점의 3D 위치·핵심 치수를 독립 측량과 비교한다. 가림·누락·측정 불확실성을 보고한다.
4. Blender에서 원본 SKP와 복원 표면을 대조해 구조와 실제 객체를 나눈다. 문틀/문짝, 버튼 패널/누름부, 객실/문, 발판/손잡이를 개별 편집 가능하게 만든다. 재질별 분리나 연결 성분만으로 개별 물체를 확정하지 않는다.
5. 검증된 위치·방향·크기에 부품 피벗·충돌·실제 동작을 연결한다. 같은 상태가 애니메이션·물리·NPC·경로·JEV에 반영되는지 실장면에서 확인한다.

## 결과의 판정

- **영상:** 학습/정합에 사용하지 않은 시점의 형태·표지·재질·가림을 비교한다. 이 값만으로 위치 합격을 주지 않는다.
- **기하:** 분리한 독립 검증점의 3차원 잔차·최대 오차와 실측 치수 차이를 측정 불확실성과 함께 보고한다. 0.02m는 프로젝트 목표이며 현재 달성값이 아니다.
- **완전성:** 구역·객체 목록 대비 확인된 개수, 미확인·가림·추정·자료 충돌을 따로 센다. 파일 수와 삼각형 수를 현장 완전성으로 쓰지 않는다.
- **조작:** 실제 부품 경계·설비 연결에 대해 조작 중단/재개, 정전/잠금/고장, 탑승자·NPC·길 안내를 검사한다. 외관이 맞아도 조작 검증이 빠진 부품은 완료로 집계하지 않는다.

취득한 논문·공식 문서는 [receipts.json](../../asset-library/research-public/2026-10-08/measured-twin/receipts.json)과 연결한다. 다운로드 실패는 실패 기록으로 보존한다. 구현체 설치·GPU 실행·유료 작업은 이 자료 수집 레인에서 수행하지 않았다.
