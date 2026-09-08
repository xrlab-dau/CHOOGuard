---
title: '부산역 영상 복원·Blender·Unity 파일럿'
type: 'feature'
created: '2026-09-07'
status: 'in-progress'
resumed_on: '2026-09-08'
prior_cancellation: 'VARCO 전환으로 중단했으나 크레딧 비용으로 해당 방향을 폐기하고 CV 파이프라인을 재개함'
baseline_commit: 'b25413a06ec1943e180219241aad8880c9003f5d'
route: 'dispatch'
review_loop_iteration: 0
context: ['AGENTS.md', 'reconstruction/README.md', 'docs/context/school-pc-handoff-2026-09-08.md', 'docs/evidence/foundation/2026-09-08-sequence-parent-audit.json', 'docs/evidence/foundation/2026-09-08-sequence-resume.json', '_bmad-output/planning-artifacts/research/academic-lit-foundation-cv-pipeline-2026-09-08/research.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**문제:** 실자료와 연속 카메라 복원이 부족하다. 사진 두 장의 표면·합성 역사를 전체 디지털 트윈으로 부를 수 없다.

**접근:** 기존 영상을 활용해 한 구간을 실제 복원·비교하고 Blender 편집본과 Unity 검토 장면까지 전달한다. 전체 13구역의 자료 공백은 유지·갱신하며, 샘플러 정비만으로 끝내지 않는다.

## Boundaries & Constraints

**항상:** 복원 비교는 로컬 추론, 입력 순서·SHA·PTS·전처리·마스크·원시 출력·변환 보존. 설치는 이미 승인됨. 고정 의존성, 새 출력 폴더, 실패 기록, Unity 단일 writer.

**사용자 정책 변경(2026-09-08):** 철도 사진·복원 기하의 외부 3D 생성 서비스 전송을 일괄 금지하던 저장소 정책은 폐기한다. 코딩 LLM의 이미지 첨부 금지는 별도로 유지한다. 외부 서비스 이용은 원본 권리·서비스 약관·실행 환경의 상위 지침을 면제하지 않는다. Blender 디테일 작업에는 GPT-6 Astra max를 우선 고려하며, 모델 선택과 단계 배분은 목표·품질·실행 근거에 따라 오케스트레이터가 판단한다.

**금지:** 무단 개인정보 제공·게시·push, YAML 수동 편집, 미관측 구역 생성으로 실패 은폐. 실측 기준 없는 metric/훈련 Collider 수용. 1F 도면이나 통로 치수를 2F에 전용. 학교 PC/HMD 검증 사칭.

## I/O & Edge-Case Matrix

| 입력/상태 | 결과 |
|---|---|
| 같은 연속 4프레임 | 독립 CPU SfM·DA3 비교, 엔진별 결과와 실패 구분 |
| SHA/순서/PTS 오류·기존 출력 | 추론 전 거부, 원본 보존 |
| 컷·퇴화·미등록·분리 모델 | 불완전 판정; 합쳐 성공 처리 금지 |
| 시간/메모리/디스크 한도 | 중단 영수증; 미실행 학교 작업과 구분 |
| 축척 근거 없음 | 상대 단위 검토만, 실제 거리 오차 주장 금지 |

</frozen-after-approval>

## Code Map

- `scripts/references/{acquire_video,sample_video_frames}.py`: 취득·추출 재사용; 새 폴더 필수.
- `reconstruction/tools/{run_da3,probe_colmap,refine_in_blender}.py`: DA3≤4장, probe≠SfM, Blender 재사용.
- `reconstruction/src/chooguard_reconstruction/{geometry,export}.py`: 표면·마스크·좌표 계약.
- `Packages/com.xrlab.chooguard.foundation/Demo/Editor/ReconstructionReviewBuilder.cs`: 해시/bounds/색 검사.
- `.planning/2026-09-07-foundation-pipeline-research/findings.md`: 고정 후보·설치·제약 근거.

## Tasks & Acceptance

- [x] `reconstruction/tests/test_sequence.py`: I/O 행렬·변환 실패 회귀시험과 후속 adversarial 입력 시험. 현재 재검증에서 reconstruction92개 통과.
- [x] `scripts/references/`: 보유 영상4개 입력 검증, 사진13개 누락 기록 복구. 추가 영상0개; reference19개 재검증 통과.
- [x] `reconstruction/tools/run_sfm_sequence.py`: PyCOLMAP4.2 CPU 정합/등록/BA runner 구현·실행. 실제 두구간0/4 미등록이며 성공 native model/BA 수용은 아님. DA3 보정값 미사용.
- [x] `reconstruction/tools/{setup_mapanything,run_mapanything}.py`: Apache 고정 별도 환경·offline adapter·변환 시험 및 두구간 실제 CPU 추론.
- [x] `reconstruction/tools/run_sequence_pilot.py`: 동일4장×2구간 DA3 SMALL/BASE·SfM·MapAnything 실행 비교. 실측 정확도 순위는 없음; 긴 SfM은 별도 실험.
- [ ] `reconstruction/src/chooguard_reconstruction/export.py`와 시험: 새 audit 사용, 관측 표면→Blender→Unity 전달. 희소 점군 분리.
- [ ] `docs/reconstruction/engine-selection.md`, `foundation/world/facility-coverage.json`, `docs/context/project-context.json`: 실제 결과·권리·공백·증거 반영.

수용: 등록/미등록·track길이·재투영median/p90·시차·시간/peak RAM, 공통 입력SHA, 장치/해상도 차이 기록. 미실행은 순위 제외. FBX·Unity 검사·네이티브 정면/측면 캡처 해시 결속; 실패는 차단 처리.

## Implementation Notes

- 2026-09-07 사용자 CHECKPOINT1 ‘승인하고 계속’ 확인. frozen 의도는 고정했다. baseline은 기존 dirty 작업을 포함한 HEAD이며 해당 기존 변경은 삭제·재분류하지 않는다. 현재 여유디스크 약21GiB, 실행 중인 Unity Editor는 없음을 확인했다.
- 2026-09-08 구현 자식 종료 후 부모 수용 차단. 이미지4개가 외부 모델 세션의 toolResult로 첨부된 금지 위반을 확인했다. 프로젝트 `images.blockImages=true`와 실제 SDK 필터의 합성 검사를 적용했으나 현재 부모 세션 reload·사용자 재개 판단 전 추가 자식 호출을 하지 않는다. 컷이 섞인 입력, MapAnything 자리표시자, provenance 실패 및 Blender/Unity·사진목록 미완료는 [사후 감사](../../docs/evidence/foundation/2026-09-08-sequence-parent-audit.json)를 따른다. 기존 출력/원 영수증은 보존하며 Step4로 넘어가지 않는다.
- 2026-09-08 사용자가 ‘이미지 첨부 없이 계속’으로 재개를 승인했다. 이전 이미지 포함 자식 대화는 fork/resume하지 않고 fresh 텍스트 세션을 사용한다. 새 설정 로드의 `images.blockImages=true`와 SDK의 user/toolResult 이미지 제거를 합성 입력으로 재확인했다. 작성자는 시작 시 실제 작업 cwd의 이미지 차단을 확인하고 false이면 쓰기 전에 중단한다. 이미지 Read/첨부·base64 출력·이미지/기하의 외부 호출은 금지하며, FFmpeg/OpenCV/로컬 추론은 파일을 로컬에서만 처리하고 숫자·텍스트·해시만 보고한다.
- 재개 작업은 사후 감사 SEQ-01~07의 회귀시험을 먼저 추가하고 정상 연속 입력으로 다시 실행한다. MapAnything 추론 자리표시자는 실제 경로로 구현하며, 별도 고정 환경·로컬 backbone·원시 출력 adapter를 검사한다. 설치/추론 차단은 측정한 디스크·메모리·시간/의존성 사유로만 기록하고 가중치가 없다는 사실을 임의의 학교 전용 결정으로 바꾸지 않는다.
- 로컬 실행 파일은 `/Applications/Blender.app/Contents/MacOS/Blender`, `/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/MacOS/Unity`에 있다. Bash를 통한 기존 builder의 실행과 실제 실패 진단을 사용한다. 캡처는 로컬에만 보존하며 이미지 없이 확인하지 못한 시각 검수는 미검수로 남긴다. 이전 실험/사후 감사는 변경하지 말고 새 결과에 적용 범위와 해결 여부를 결속한다.

- 2026-09-08 후속 방향: 사용자 선호를 하드게이트로 확대하지 않고 오케스트레이터가 실행을 판단한다. 이번 다음 작성자는 실제 geometry/export/Blender/Unity 통합의 난도 때문에 Astra max를 선택한다. 선행 엔진 결과는 `docs/evidence/foundation/2026-09-08-sequence-resume.json`과 최종 비교 receipt를 사용한다. 과거 cut-mixed recipe의 마스크를 전용하지 않는다. 이미지 첨부와 실제 외부 전송 없이 로컬 산출물을 전달하고, 확인하지 못한 시각/개인정보 검수는 완료로 표시하지 않는다.

## Spec Change Log

- 2026-09-08 사용자 명시 지시로 frozen 자료 경계의 일괄 외부 3D 생성 전송 금지를 폐기하고 Blender=max 지정을 반영했다. 변경 전 frozen SHA256은 `8b5f999c682ec7241162e750bee9cca48871d82120c78f1792b2be22adbbec45`이며 원본 사본·사후 감사는 보존했다. 이전 Implementation Notes의 blanket 금지 표현은 당시 기록이고 최신 저장소 정책은 위 경계를 따른다. 현재 세션의 상위 실행 지침이 남아 있는 동안에는 실제 외부 사진/기하 전송을 하지 않으며, 현재 작성자는 로컬 복원·텍스트 인계만 마무리한다. 목표·시험 기대값을 실패한 구현에 맞춰 축소한 변경은 아니다.
- 2026-09-08 사용자의 오케스트레이션 위임 명확화에 따라 Blender=max를 영구적 하드게이트가 아닌 선호/현재 배정 판단으로 정리했다. 금지·정답·수용 기준을 임의로 추가하지 않으며 기존 기능 목표와 시험 행렬은 유지한다.

## 2026-09-08 재개한 인계: 최신 CV를 관측 자산 전달에 적용

> **최신 사용자 지시:** VARCO 시제품은 비용 때문에 폐기했고 이전 CV/SOTA 파이프라인을 재개한다. 아래 CV-01~06은 **현재 로컬 세션이 소유하는** 남은 작업이다. 학교 PC에는 별도의 고해상도 베이크/렌더·데이터/모델 및 upstream GPU benchmark·Windows 검증을 독립 배정하며, 로컬 개발의 선행조건으로 두지 않는다. 이미 완료한 두구간 엔진 비교를 재사용한다. 이전 VARCO 전환/취소 이력은 보존하되 현재 실행 지시가 아니다. 새 CV 구현 writer는 아직 실행하지 않았으며 이 문서/커밋만으로 기법 적용 완료를 주장하지 않는다. [학교 인계](../../docs/context/school-pc-handoff-2026-09-08.md)를 먼저 읽는다.

사용자는 오브젝트마다 생성 비용을 지불하는 대신 Astra와 재사용 가능한 파이프라인을 원하며, **현재 날짜의 CV 논문·연구를 확인해 적용하고 Blender·Unity에 연결**하라고 명시했다. 부모가 Exa/alphaXiv/OpenAlex 및 원문·공식 코드·HF metadata를 확인했다. 연구 결론은 frontmatter context의 `research.md`다. 새 연구 주기·BMAD 렌더·엔진 설치/비교를 재시작하지 않고 다음 bounded delivery를 구현한다. 이 범위는 원래 관측 표면 전달 목표를 구체화하며 위 frozen block과 과거 실패 증거를 변경하지 않는다.

### 이번 구현과 수용

- [ ] **CV-01 회귀시험 먼저:** 빈 중첩/단일 뷰/전부 가림·마스크는 다중 관측 지원 PASS가 아님을 검증한다. 양쪽 마스크, 유한한 proper pose/K, optical-z, 뒤쪽/화면 밖 투영, 앞쪽 가림과 free-space 모순, 손상된 입력/receipt/order/grid mapping을 합성 fixture로 시험한다. 기존 입력·좌표·색공간 회귀를 유지한다.
- [ ] **CV-02 실제 기하 처리:** 기존 DA3 BASE와 MapAnything adapter NPZ를 각 엔진의 원 receipt로 검증해 읽는 공통 전달 경로를 만든다. 학습 confidence와 별도로 다중 시점 depth 지원·가림·모순·미관측을 계산하고, 정책이 명시된 keep mask를 표면에 적용한다. raw 예측·원 confidence·원 mask는 불변. 지원 수와 필터 전후 점/면/coverage·median/p90를 보존하며 예측 내부 일치도를 실측 정확도로 표시하지 않는다. 파라미터 완화로 텅 빈 결과를 성공처럼 만들지 않는다.
- [ ] **CV-03 재사용 관측 표현:** 입력/엔진/설정·소스 hash를 가진 관측 패킷을 GLB/Blender/Unity까지 전달한다. 명시적인 instance mask/track ID가 있을 때 해당 관측을 별도 editable 부분으로 내보내는 경계를 실제 합성2객체 fixture로 검증한다. 실제 입력에 instance tracks가 없으면 per-view observed surface라고 기록하며 semantic object extraction이나 SAM 실행으로 가장하지 않는다. 관측만 있는 부분과 추후 canonical authored asset·배치의 참조를 분리한다.
- [ ] **CV-04 재실행 가능한 종단간 명령:** 기존 결과→export→Blender→Unity의 recipe/CLI를 제공한다. 완료 단계만 입력·설정·코드·산출물 hash 검증 후 재사용하며 손상/부분 실패는 캐시 성공으로 취급하지 않는다. 기존 추론/마스크/Blender/Unity 파일럿 출력과 기존 앱을 덮어쓰지 않는다. 새 디렉터리·명시적 실패 receipt를 사용한다. 임의 shell 명령을 recipe로 실행하는 범용 엔진은 만들지 않는다.
- [ ] **CV-05 실제 전달:** `sequence-resume-connector-final` 및 `sequence-resume-ry-final`의 비교 receipt와 양 엔진 출력을 후처리 비교한다(추론 재실행 불필요). 하나의 선택 결과는 실제 `.blend`/FBX와 새 Unity 검토 장면·Desktop native captures까지 연결한다. Blender collection/scene/receipt의 DA3·B/A·upper-concourse 고정 문구를 실제 엔진/뷰/범위에 맞추고, 임의 뷰 ID와 4시점이 사라지지 않는지 검사한다. 필요한 작은 builder/controller 수정만 한다. 지원 기하가 불충분하면 그 사실을 기록하며 합성 대체를 실제 입력 성공으로 보고하지 않는다.
- [ ] **CV-06 검증·근거:** Python 관련 시험·정책/컨텍스트 검사, Unity6000.3.23f1 refresh/compile/console 오류0, 관련 EditMode와 PlayMode를 실제 실행한다. 임포트 삼각형/bounds/색/정상 유한 geometry/physics 없음과 hierarchy/serialized assets를 검사한다. native 정면/측면 캡처와 현재 FBX hash를 묶고, 이미지 없이 못한 시각/개인정보 검수는 `pending`으로 둔다. 적용 기법·원 출처·시험·측정 결과·미적용 SOTA 항목을 `docs/evidence/foundation/2026-09-08-cv-delivery.json` 및 engine-selection/README/context에 구분해 남긴다. 전체 diff 검토와 부모 수용 전 전체 spec을 완료 처리하지 않는다.

### 구현 조사 지점

- `reconstruction/src/chooguard_reconstruction/{geometry,quality,export}.py`: 현재 quality는 진단만 계산하며 가림도 차이에 포함한다. export는 DA3 sibling receipt와 `upper_bound_resize`에 종속되고 raw mask 저장/instance별 전달은 없다.
- `reconstruction/tools/run_mapanything.py`: 실제 adapter NPZ에는 `depth/conf/intrinsics/extrinsics/processed_images/mask`가 있고 `mapanything-receipt.json`은 원시·adapter 출력 및 `fixed_mapping` 전처리를 기록한다. 이것을 DA3 receipt로 꾸미지 않는다. crop/resize 좌표 대응은 고정 upstream의 `utils/image.py`를 읽고 증명한다.
- `.local/reconstruction/mapanything/map-anything/mapanything/utils/multiview_confidence.py`: Apache upstream의 공개 다중 시점 기법을 참고하되, single/no-overlap ones fallback 및 target mask 의미 차이를 그대로 수용하지 않는다. 소스 변경 없이 작은 후처리 구현/어댑터를 선택하고 기법의 부분 적용이라고 기록한다. 상대 단위에 상류 기본 미터 임계값을 무비판적으로 이식하지 않는다.
- `reconstruction/tools/refine_in_blender.py`: 현재 native source/raw/editable 보존과 bounds·색 샘플 receipt가 있다. 새 시퀀스에 실행하고 현재 source metadata에 맞게 일반화한다.
- `Packages/com.xrlab.chooguard.foundation/Demo/{Editor/ReconstructionReviewBuilder.cs,Runtime/ReconstructionReviewController.cs,Tests/Editor/ReconstructionReviewTests.cs}`: 실제 importer가 있고 이미 다른 dirty 변경이 있다. 이를 보존하면서 새 source/generated/build root 선택과 provenance가 통과하게 만든다.

### 현재 실행 경계와 재개

이번 전달에서는 부모가 검증된 기존 변경의 작업 단위 커밋과 기존 PR61 브랜치 푸시를 소유한다. 이는 최신 사용자의 이번 작업 지시이며 후속 에이전트의 커밋/게시 권한이 아니다. 작성자는 아래 기존 무단 commit/push 금지를 유지한다. 학교 PC의 작업 장소 결정은 확정이며 추가 사양 제출로 재승인을 요구하지 않는다. 현재 실행 세션은 Mac이며 CV-01~06을 여기서 계속한다. 학교 실행·HMD 수용을 주장하지 않는다. 학교의 별도 작업은 고정 기준 커밋/별도 브랜치·출력으로 분리하고 실제 환경에 맞춰 설치/실행 프로파일을 점검한다. 현재 Unix runner의 Windows/CUDA 지원 한계는 인계 문서에 명시하며 관리자 정책을 우회하지 않는다.

아래 배정은 VARCO 중단 이전에 마련한 native 인계의 재사용 기준이다. 이번 작성자는 native `worker`, `openai-codex/gpt-6-astra:high`, fresh text context다. 이전 workflow `b47fca6d-e075-41b1-a14b-8b03a1acd730`은 `max`가 설정 ceiling `high`를 초과해 실행 전 실패했다. 이는 network 오류가 아니며, 이번 재시도는 같은 native protocol에서 현재 허용한 reasoning을 명시한 것이다. 설정 ceiling을 수정하거나 외부 CLI로 우회하지 않는다. Astra max를 다음 작성자에게 넘기고 종료하라는 **과거** Notes는 이번 배정으로 대체된다.

시작 시 context validate→brief를 실행하고 실제 cwd의 `images.blockImages=true`를 확인한다. 이미지 Read/첨부/base64·외부 사진/기하 전송은 이번 실행에서 하지 않는다. 자동 기하/보수적 ROI는 사람의 privacy/visual audit를 대체하지 않으며, `mask_proposals`의 존재만으로 수동 검수했다고 쓰지 않는다.

현재 ref는 `feature/foundation-starter`, HEAD는 기존 baseline이다. main/develop·다른 worktree·기존 staged/dirty/untracked 파일을 재설정하지 않는다. 편집 범위는 위 reconstruction/관련 Demo 코드·시험, 해당 README/engine-selection/새 증거·context, 필요 시 작은 새 delivery recipe다. `.agents`, `.claude`, `_bmad` 프레임워크, `.pi` 설정, `.github`, `scripts/ci`, scoring/안전절차/훈련 충돌은 변경하지 않는다. commit/push는 하지 않는다.

로컬은 M1/16GiB, 확인 당시 여유 약13GiB. 기존 Python 환경을 재사용한다. 무거운 재학습·GS 최적화·전체 영상 실행·새 대형 checkpoint 다운로드는 이번 단계가 아니다. 프로세스당30분/RSS8GiB·free disk8GiB를 지킨다. 시작 시 Unity/Blender 프로세스를 다시 읽는다. 열려 있는 Blender UI는 닫거나 덮어쓰지 말고 별도 background factory-startup 인스턴스에서 경량 변환한다. Unity는 하나씩 실행한다. raw·기하·캡처는 ignored 로컬 경로에, 공개 증거에는 필요한 hash·수치·상대 경로만 남긴다. 필요한 의미 결정/실제 실행 차단이 있으면 supervisor에게 질문하고 실패 상태를 보존한다.

## Review Triage Log

## Design Notes

MapAnything4.9GB는 별도 환경. `.[all]`, 가변 Git, 자동 Torch Hub 취득 금지. 실행당30분/RSS8GiB·여유디스크8GiB; 무거운 실행은 학교로 분리. VGGT-Ω는 접근·라이선스·오염 공지로 제외. 보류 샘플러 spec은 재개하지 않는다.

## Verification

- `.local/reconstruction/geometry-venv/bin/python -m pytest reconstruction/tests -q`, `ruff check reconstruction`.
- Unity6000.3.23f1 compile/console오류0, EditMode/PlayMode, hierarchy/serialized 검사.
- `python3 scripts/context/context_graph.py validate`, `git diff --check`, 전체 diff 검토. 과거 PASS 재사용 금지.
