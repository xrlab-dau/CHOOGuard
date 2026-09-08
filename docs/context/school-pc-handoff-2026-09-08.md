# 학교 PC에서 이어 할 Foundation 작업

기준일: 2026-09-08. 현재 선택은 **VARCO 폐기 → CV/SOTA 연구 기반 파이프라인 재개**다. 최신 사용자 추가 설명에 따라 **CV-01~06은 기존 로컬 세션의 작업이고, 학교 PC에는 독립적인 고용량·고부하 작업을 할당**한다. 학교 PC에서 다시 조회한 [PR61](https://github.com/xrlab-dau/CHOOGuard/pull/61)은 06:15:45 UTC에 `develop`으로 병합됐으며 커밋은 `f2011ac2cd4510a09c78f0de111771fc5aa433e8`이다. 아래의 과거 미실행·Mac 검증 기록은 당시 이력이다. 후속 학교 결과는 이 병합 기준에서 별도 작업 브랜치로 준비하고 PM이 검토한다. 이 링크와 날짜는 이후 원격 상태나 게시 권한을 보장하지 않는다.

이번 학교 세션의 설치·Windows bootstrap/Unity 시험·실행본 결과와 남은 수용 범위는 [학교 PC 검증 기록](../../school/windows-validation/2026-09-08-pm-setup.md)에 있다. 아래 Mac 실행 수치와 원본 자료 한계는 덮어쓰지 않는다.

## 1. checkout과 시작 문맥

기존 작업이 있으면 먼저 보존한다. `reset --hard`, `clean`, 임의 stash/pop으로 작업을 지우지 않는다. 깨끗한 checkout에서:

```sh
git fetch origin
git switch develop
git pull --ff-only origin develop
git switch -c chore/school-pc-work
git lfs install --local
git lfs pull
git lfs fsck
python3 scripts/context/context_graph.py validate
python3 scripts/context/context_graph.py brief --topic handoff --machine school-pc
python3 scripts/dev/check_foundation.py
```

Windows에서는 설치된 Python에 맞춰 `python3`를 `python` 또는 `py -3`로 바꾼다. Git LFS의 기존33 FBX+Blender 원본34개를 확인한다. JSON/HTML 그래프의 current source와 historical evidence를 구분하며, 과거 PASS를 현재 Windows/HMD 결과로 옮기지 않는다.

다음 파일은 순서대로 읽는다:

1. [현재 결정](accepted-decisions.md), `AGENTS.md`
2. [기존 sequence spec](../../_bmad-output/implementation-artifacts/spec-foundation-sequence-reconstruction.md) — 로컬 소유 범위를 확인하는 읽기 자료. 학교 작성자가 CV-01~06을 대신 구현하지 않는다.
3. [CV 연구](../../_bmad-output/planning-artifacts/research/academic-lit-foundation-cv-pipeline-2026-09-08/research.md)
4. [엔진 실행 증거](../evidence/foundation/2026-09-08-sequence-resume.json), [최초 실패 감사](../evidence/foundation/2026-09-08-sequence-parent-audit.json)
5. [복원 실행 설명](../../reconstruction/README.md), [엔진 비교](../reconstruction/engine-selection.md)

## 2. 어디까지 실제로 했는가

| 영역 | 현재 상태 |
|---|---|
| 게임 | 기존 Desktop 상시 운영·사건·대응·복구 게임과33종 참조 자산이 있음. 새 CV 장면이 전체 게임을 대체한 것은 아님. |
| 자료 | 보유 영상4개, 누락 사진13개 기록 복구, 공식 문서/자료28파일의 취득 이력과 한계를 보존. 원본은 Git 밖. |
| 엔진 | 동일 연속4장×2구간에서 DA3 SMALL/BASE MPS 및 MapAnything Apache CPU를 실제 실행. 독립 SfM은 두 구간0/4 미등록. |
| 최신 연구 | LIST3R/MonoEM-GS/SimRecon 원문과 DA3-Streaming/MapAnything/SAM3.1 공개 구현·조건 조사. 문헌을 읽었다고 전체 알고리즘을 구현한 것은 아님. |
| 로컬 다음 구현 | spec의 CV-01~06: 다중 시점 지원/가림/모순 검사, 엔진 독립 export, 관측/instance 표현, 재실행 가능한 Blender·Unity 전달. 이 세션에서 계속하며 학교 결과를 선행조건으로 삼지 않는다. |
| VARCO | 이미지1+벤치3D1 생성, GLB/FBX 검사,220크레딧. 사용자가 비용으로 폐기. Unity 교체·상업 이용 승인·Sound 생성0. [이력](../art/varco-production.md) |
| 학교 PC | Unity 개발 장비로 이미 사용자 확정. 이번 실행·GPU 메모리·HMD 결과는 아직 없음. 현재 전달 세션은 Mac arm64였다. |

학교 PC 적합성을 다시 승인받기 위해 사양 제출을 요구하지 않는다. 설치된 실행 파일·OS·GPU/메모리는 실제 세션에서 **실행 프로파일을 고르기 위해** 확인한다. 역할 분담과 실행 조건은 [ADR0005](../adr/0005-school-pc-unity-workstation.md)를 따른다.

## 3. Git에 없는 자료

`private-data/`, `reconstruction/output/`, `.local/`의 영상·프레임·원시 예측·가중치·venv, `.planning/`의 로그/캡처/일회성 작업 자료, 연구 `imports/` 원문, 계정/쿠키/서명 URL은 푸시하지 않는다. **git pull만으로 원본이나 설치 환경까지 전달되지 않는다.**

자료 등급과 기관 규정상 허용된 비공개 전달 수단으로 필요한 입력만 이동하거나 공개 원 출처에서 다시 취득한다. 원본/프레임 hash·순서·PTS를 검증한다. 다른 영상 encode나 새 다운로드의 hash가 달라지면 기존 실험의 동일 입력으로 간주하지 않는다. 개인 계정·인증 상태를 복사하지 않는다.

기존 receipt에 기기 절대 경로가 있다면 덮어써서 과거 PASS를 갱신하지 않는다. 새 기기의 경로 매핑/재실행은 별도 receipt와 새 출력 디렉터리에 기록하고 원본 hash는 보존한다. 사용할 최종 비교 receipt:

- `reconstruction/output/sequence-resume-connector-final/sequence-pilot-comparison.json`
- `reconstruction/output/sequence-resume-ry-final/sequence-pilot-comparison.json`

원시 영상·카메라·깊이·mask·변환은 따로 보존한다. Blender/Unity로 넘기기 위해 재학습이나 기존 두 구간 추론 전체를 불필요하게 반복하지 않는다.

## 4. 독립 학교 작업 큐

기존 로컬 세션은 `reconstruction/src`, 현재 runner/receipt/export, `ReconstructionReview*` 및 해당 회귀시험을 소유한다. 학교 작업은 아래 경계만 사용하고 로컬 핵심 파일을 동시에 수정하지 않는다. 공통 기준은 PR61이 병합된 **불변 커밋**이며, 작업별 별도 브랜치/worktree에서 수행한다. 완료 후 PM이 결과·diff와 후속 통합 경로를 검토한다. 이미 병합된 PR61에 후속 작업을 추가하지 않으며, 자동 merge/push나 새 PR 생성 권한은 부여하지 않는다.

| 단위 | 독립 입력과 작업 | 출력/편집 경계 | 독립성 |
|---|---|---|---|
| S-ART-01 | 이미 존재하는33종 station kit와 재질을 기준으로4K 텍스처 베이크 후보 및 자산별 정면/측면 고해상도 검수 렌더. 무거운 렌더/베이크를 학교에 배치한다. | 작업 스크립트는 `school/art-review/`, 큰 산출물은 ignored `private-data/school-runs/art/`. 원 FBX/Blender·훈련 Collider·좌표·형상을 덮어쓰지 않는다. 원본 hash와 렌더/베이크 설정·결과를 보고. | 새 로컬 CV 출력 없이 기존 Git LFS kit로 시작. 형상/재질 변경이 필요하면 별도 diff로 제출하며 배치를 직접 바꾸지 않는다. |
| S-BENCH-01 | 라이선스가 확인된 공개 실내 benchmark의 데이터/고정 모델 캐시 준비와 upstream DA3-Streaming 또는 MapAnything GPU 비교. 큰 저장소/VRAM 소모를 학교에 배치한다. | `school/benchmarks/`의 독립 실험 설정·명령·정제 결과만 공유. 원본/가중치/예측은 ignored `private-data/school-runs/bench/`. 입력 순서/hash, 모델 revision, 실제 메모리·시간·GT 존재/평가 범위 기록. | 로컬 adapter를 수정하거나 완성을 기다리지 않는다. 상류 native 출력과 별도 manifest를 반환한다. gated SAM3.1은 접근 조건이 충족된 경우에만 별도 후보로 평가. |
| S-WIN-01 | 기존 Foundation 장면으로 Windows standalone 빌드·기본 회귀·장시간 프레임/메모리 측정. 빌드/Library/Profiler 저장 부하를 학교에 배치한다. | 기존 build entry 사용, `school/windows-validation/`의 실행 명세/정제 보고. 바이너리·raw profiler/captures는 ignored `Builds/`/`private-data/school-runs/windows/`. 게임 규칙·layout·CV core 변경 없음. | 로컬 CV 장면과 무관한 기존 플레이어 baseline 검증. VR 기기가 미정이면 Desktop만 측정하고 HMD PASS를 주장하지 않는다. |

세 단위는 **논리적으로 독립**이지만 한 학교 PC의 GPU/디스크를 무조건 동시에 점유시키지 않는다. 실제 자원에 맞춰 순차/제한 병렬로 실행하며 Unity writer는1개다. 각 단위의 초기 자원 probe로 입력량·scratch/VRAM/시간 예산을 정하고, 원본이나 기존 결과를 자동 정리하지 않는다. 초기 Mac 인계 시에는 계약 준비만 완료였다. 실제 학교 실행과 미실행 단위는 상단의 날짜별 검증 기록으로 구분한다.

학교 결과는 선택 가능한 성능/자산 증거로 반환한다. 로컬 CV 개발은 그 결과 없이 기존 연속4장 자료로 계속한다. 학교 benchmark 결과를 받은 뒤 채택 여부를 결정하며 모델 교체·결과 융합을 자동 수행하지 않는다.

현재 runner는 Mac에서 검증했으며 POSIX `resource`/process-group/`bin/python` 가정이 있다. **순수 Windows나 CUDA 전용 runner가 완성됐다고 하지 않는다.** 이미 허용·설치된 Linux/WSL 환경이면 작은 재현부터 확인하고, 순수 Windows가 필요하면 해당 portability 테스트/수정부터 분리한다. WSL/드라이버/관리자 정책 변경을 자동 실행하거나 우회하지 않는다. 로컬4장 파일럿의30분/RSS8GiB 제한은 당시 실행 범위이며 학교의 대규모 프로파일은 실제 자원을 확인해 별도 기록한다.

실제 미터 축척·중력/북쪽·제어점·기관 절차가 없는 모델은 relative review다. 훈련 Collider와 metric SceneBundle 수용으로 바꾸지 않는다. VR 기기와 대체 사운드 제작 방법은 아직 확정하지 않았다.

## 5. 이번 전달 검증의 읽는 법

현재 소스 재검증: reconstruction92, reference19, context15, art11, foundation31개 Python 시험 및 Ruff, LFS 검사 통과. Unity EditMode117통과/0실패/기존 Mac 빌드 보존1skip, 구조 합성fixture 별도12통과, PlayMode16통과, compilation/console 오류표식0을 확인했다. 이는 Mac의 기존/구조 검토 경로이며 새 시퀀스 전달이나 학교/HMD PASS가 아니다.

최초 Unity 검사에서는 기본 `busan-concourse-pilot`의 오래된 receipt에 `colorEncoding`이 없어1개가 실패했다. 이는 HEAD부터 있던 정상 거부 조건이다. 원 receipt/실패를 보존하고, 실제 유효한 `busan-concourse-base-504-patched` 입력으로 다시 검사했다. 기본 폴더의 과거 파일을 새 형식으로 조용히 덮어쓰지 않는다.

Native 검증자 프로필의 flow-list `tools: [read, …, ls]`가 `[read`/`ls]`로 파싱되어 첫 게시 검토가 실패했다. 선언 권한은 유지하고 지원 표기로 정리했다. 설치된 pi-subagents0.65.1 parser를 사용하는 `node --test scripts/dev/test_agent_profiles.mjs`는7실패→7통과했다. 실패한 agent 결과를 검토 PASS로 쓰지 않으며 재시도 결과는 별도 증거에 기록한다.

Pi0.85.1 및 프로젝트의 고정 패키지, BMAD6.12.0/SpecKit1.0.4 소스는 [도구 전달 기록](../tooling/agent-resources.md)을 따른다. 개인 모델/인증 설정과 전역 설치물은 Git으로 옮기지 않는다. 현재 승인된 spec/Step3 범위를 유지하고 새 연구/계획 게이트부터 반복하지 않는다.
