# 현재 역사 플레이와 튜토리얼

2026-10-16 현재 역사 씬에는 provider 기반 초기 조건 생성·NPC 행동 런타임을 연결했다. 현재 오브젝트8075개를 입력 목록으로 사용하며, 기존 승객 모델13종과 한국어 폰트를 참조한다. 기존 공식 자원과 승인된 건축 형상을 새 prefab으로 대체하지 않는다.

## 역사 근무

편집은 `Assets/ChooGuard/Scenes/FpsStation.unity`에서 계속한다. 타이틀의 모드 선택은 `Bootstrap.unity`를 직접 열어 Play한다. `플레이 시작` → `랜덤 비상상황 대응`을 고르면 현재 역사 근무 준비 화면을 연다. `AI 계정과 모델 · F8`에서 계정 로그인, 모델 선택, 구독 사용과 초기 생성1회 허용을 설정한다. `근무 시작` 한 번으로 선택한 AI의 비상상황 초기 조건을 생성하고 완료 후 자동으로 근무에 진입한다. 이미 생성한 조건이 있으면 이를 재사용하며 중복 요청을 보내지 않는다. `초기 조건만 미리 생성`은 플레이 전에 생성 결과를 준비하는 선택 동작이다. 선택한 동일 계정·모델을 초기 조건과 NPC 계획에 고정한다. 실패 시 JEV나 API 키 과금으로 바꾸지 않는다. 모델 응답이 비어 있어도 정상적인 조건 생성 결과다.

WASD로 이동하고 Shift로 빠르게 이동한다. E는 대상 조작, Esc는 포인터 해제, F8은 계정·모델 메뉴다. 랜덤 비상상황의 시작 버튼은 AI 준비와 초기 생성 허용을 확인하며, 연결이 준비되지 않았으면 계정 메뉴로 안내한다. 계정·모델 변경은 근무를 종료한 뒤 가능하다.

## 승무원 열차점검

타이틀 → 튜토리얼 → 승무원 열차점검, 또는 `CrewTrainTutorial` 씬에서 직접 Play한다. 이 교육은13단계의 오프라인 장비 실습이며 provider 로그인이나 JEV 키를 요구하지 않는다. 역사 씬 강제 시작 설정으로 튜토리얼 씬을 덮어쓰지 않는다.

## 구현 근거와 상태 경계

원래 미설치였던 `workers/simulator/Runtime/{Core,Planning,Unity}`와 `workers/simulator/docs/native-provider-ui.md`, `RESEARCH.md`의 구현을 `Assets/ChooGuard/Seeded/`에 설치했다. `StationWorldAdapter`는 현재 씬의 참조를 사용하고 원본을 재배치하거나 파괴하지 않는다. 이동 표면은 현재 충돌 형상에서 생성하며,1·2층 MODEL 높이0/7의 후보는 실제 바닥 hit·capsule clearance·완전한 NavMesh 경로가 있어야 채택한다. 새 바닥이나 층간 연결을 만들지 않는다. 연결되지 않은 구역은 분리된 상태로 남긴다. NavMeshAgent만 NPC의 물리 위치를 소유하고 모델 계획은 관측·캐시·예산 계약을 거친다.

현재 씬 적용/저장과 실제 플레이 검증, 현장 절차/효과 검증을 구분한다. 최초 설치는 추론 없이 수행했다. 이후 사용자가 GPT-6-Luna와 구독 사용을 명시적으로 선택했다. 구버전 helper를 앱 소유권 확인 후 갱신했고 기존 로그인 파일을 byte-exact 보존했다. 같은 계정·모델·허용을 복원해 현행 추론 계약을 확인했다. 승인된 시드/가정은 현장 측정이나 군중 행동의 효과 증거가 아니다.

건축 편집 후에는 새 현재 씬 inventory와 native object correspondence를 갱신해야 한다. 기존 frozen snapshot으로 되돌리지 않는다. 원본 runtime의 설치본은 `Assets/ChooGuard/Seeded`, 별도 simulator 연구 소스는 `workers/simulator`이며, 변경을 자동 양방향 동기화하지 않는다.

2026-10-16 PR289 확인: 로컬에 머지 커밋이 포함돼 있으며, 실제 PlayMode에서 전체13단계 이동/조작·메뉴 복귀·인터폰·일시정지·재시작·무전 등8개 개별 항목이 통과했다. 묶음 실행 중단 후 남은 항목은 분리 실행했다. 근거: `docs/game-provider-handoff/TUTORIAL-VERIFICATION.json`. 현재 역사 전체의 NPC 이동/추론 실효성 검증과는 구분한다.

2026-10-16 최신 진입 UX: 사용자는 Play 시작 뒤 튜토리얼/랜덤 비상상황 대응을 선택한다. 다른 PC에서는 Bootstrap을 직접 열어 타이틀 플레이를 시작한다. 이는 앞선 직접 튜토리얼 진입용 설정보다 우선하는 최신 사용자 요청이다. 타이틀의 계정 메뉴는 역사 형상을 불러오지 않고 계정·모델 설정만 처리한다. ChatGPT GPT-6-Luna 선택, 구독 사용 허용, 초기 생성1회 허용 적용 완료; 동의 자체는 추론을 실행하지 않는다.

현재 전체9558개 상태의 모델 입력은 UTF8 약766KB다. 과거192000/256000 입력 예약 한도를 초과해 근무 시작이 차단된 문제를 수정했다. 초기 생성1회에 한해 최대1048576 입력 예약 단위를 허용하고 실제 요청 출력은512토큰으로 제한한다. 이는 공급사 토큰 수가 아닌 보수적 앱 예약 단위다. 객체·상태를 제거하지 않으며 일반 NPC 예산12요청/12000입력/3000출력은 그대로다. helper HTTP 입력도 전체 payload를 수용하며 계정 설정 본문 한도는 유지한다.

실제 근무 시작 확인 완료: GPT-6-Luna 초기 응답 accepted, Running=true, NPC116명 중66명 실제 NavMesh 속도 확인, tick26진행, 입력 캡처 정상. 초기 공급사 실제 사용량377813입력/307출력 토큰과 후속NPC계획2764입력/126출력을 확인했다. 전체 객체를 포함한 현재 역1회 실행의 증거이며 모든 구역/상황의 효과 검증은 아니다. `docs/game-provider-handoff/PLAY-START-FIXED.json`에 기록했다.

## 다른 PC에서 이어가기

Unity6000.3.23f1과 Git LFS가 필요하다. 머지 후 `git switch develop`, `git pull --ff-only origin develop`, `git lfs pull`을 순서대로 실행한다. 편집은 FpsStation, 타이틀 플레이는 Bootstrap을 직접 연다. runtime은 씬에 이미 저장돼 있으므로 날짜가 붙은 설치 메뉴를 다시 실행하지 않는다. 다른 씬을 테스트했다면 작업 후 FpsStation 편집 씬으로 돌아온다.

현재 저장 씬의 기본 시드는20261016이다. 준비 화면에서 수동 변경할 수 있고 빈 시드의 새 난수 생성 코드도 있으나 매 근무마다 새 시드를 자동 생성하는 기본 정책은 아직 연결하지 않았다. seed만으로 새 외부 LLM 응답이 재현되지는 않는다. PR300의 `workers/simulator/docs/approved-play/`가 이어 구현할 계획·아키텍처·의사코드다.

macOS 새 환경의 ChatGPT helper 준비:

```sh
python3 -m venv ~/.venvs/chooguard-provider
~/.venvs/chooguard-provider/bin/python -m pip install -r workers/simulator/provider_auth/requirements.txt
~/.venvs/chooguard-provider/bin/python workers/simulator/provider_runtime/session.py start
```

Unity AI 계정 메뉴에서 본인 계정 로그인→모델 목록→GPT-6-Luna 선택→구독 사용·초기 생성1회 허용을 새 PC에서 다시 진행한다. 모델은 해당 계정의 실제 목록에 있어야 한다. helper 재시작 시 타이틀 재진입 또는 Play 재시작으로 연결 descriptor를 다시 읽는다. 기존 계정 파일·토큰·connection descriptor와 원시 Runs는 이관하지 않는다.

현재 helper는 macOS/Linux의 POSIX 파일 잠금·권한·프로세스 확인을 사용한다. native Windows helper 이식은 미완료이며 WSL 설치만으로 Windows Unity의 descriptor 경로가 자동 연결되지는 않는다. Windows에서 프로젝트 편집과 오프라인 튜토리얼을 이어갈 수 있으나 구독 생성 플레이의 Windows 지원은 별도 작업이다.

현재 저장 씬이 참조하는 미추적 mesh/material/texture와 meta를 함께 넣어 다른 PC에서도 같은 형상을 열 수 있게 했다. 이는 기존 로컬 형상의 필수 의존성 이관이다. 다른 CAD 검토/주석·과거 계획·캐시·로그는 이 PR에 포함하지 않는다. 새 건축 편집 후에는 inventory/현재 객체 대응을 갱신한다.
