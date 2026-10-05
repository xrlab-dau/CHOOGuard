# Goal
사용자 플레이 피드백(2026-10-04): "복잡한 조작이나 게임보다는 지시대로 진행하는 수준". 후속 요청: "마이썸머카 같은 시뮬레이션 게임들을 선행조사·정밀분석해서 벤치마킹한 게임 조작법이나 방법을 다시 제안해".
산출물 = 근거(1차 자료·관찰 시점) 있는 벤치마크 정밀분석 + CHOOGuard 조작·진행 방법 재제안 문서. 제안 단계(1–4) 뒤 사용자 승인으로 S1·S2를 구현했다(5–6).

## Phases
1. 현행 진단(코드 근거) 확보 — complete (findings.md §0)
2. 벤치마크 병렬 조사(4개 군) — complete (findings-A~D.md, findings.md §1~3 교차검증·공개 행동요령)
   - A 손 조작 정비 시뮬: My Summer Car, My Winter Car, Car Mechanic Simulator 2021, Pacific Drive
   - B 물리 손·운전실·잔여 피드백: Amnesia(Frictional 물리 상호작용), Train Sim World 5, PowerWash Simulator, Escape from Tarkov
   - C 현장 대응 시뮬: Firefighting Simulator(The Squad/Ignite), Ambulance Life, Police Simulator: Patrol Officers, Arma 3 ACE3 의료
   - D 절차·판단·보고: Ready or Not, SWAT 4, Papers Please, 112 Operator, Hitman/Phasmophobia 안내 설정
3. 종합: 조작 문법·안내 모델·위험/결과·평가를 CHOOGuard 동사·현행 코드에 대응 — complete
4. 제안 문서 작성(docs/CHOOGuard_Story_Plan_v5/SIM_CONTROLS_BENCHMARK.md) — complete (리뷰 반영: 가스 손잡이 출처, 환자 전개를 JEV 입력으로, 요청 출동의 지각 게이트, RoN 근거 삭제, PS:PO 사건 완료도 표기, 7차 인계 종료 유지·D7, E/F 문법 단일화, 평가 배점 합 100, S1에 살펴보기)
5. 2026-10-05 사용자 "진행해" → D1–D7 권장안 채택, S1·S2 구현 — complete (제안서 §13, 결정 원장 9차)
   - S1: 안내 수준(견학·표준·실전)·간편 조작·길 안내 노브, 중앙 이름+상태+글리프, 우클릭 살펴보기, 무전 2단 링(보고·요청 2묶음·방송·응답, 되묻기), 요청 기반 출동(지각 대상 바인딩·Teams.Fits), Tab 수첩, 인계 요청+300 s 인계 실패, 역무실 후속 신고 최소 90 s·응답 시 보류, 조언·정답 토스트 견학 전용
   - S2: IFpsHoldInteraction(E 홀드·마우스·휠) — 가스 밸브·화구 코크, 차단기, 분전반(R 열쇠·문), 여닫이 문, 발신기·비상정지 누름 버튼, 옥내소화전 개폐밸브(소화전함에서, 모형 없음)·노즐 패턴, 소화기 쓸기·압력계·운반 중 달리기 불가. 트윈에 없는 새 물체는 만들지 않는다(5c783468 원칙)
6. 검증(에디터 컴파일·PlayMode·JEV 연결 Play 스모크) — complete (progress.md 2026-10-05)

## Constraints
- 하드룰 jev-emergency-composition: 사건 종류·장소·시각·개수 가정 금지. 새 조작·안내·평가는 Hazard 공통 정보로 동작하거나 모르는 종류에서 안전한 기본 동작. 플레이어가 인지하지 않은 위험 비노출. 부가 기능이 디렉터·세계 난수 소비 금지.
- 역무원 권한: 코레일 역무원이 실제로 하는 조치만(소방·경찰·구급 전문 조작 부여 금지). 공개 행동요령을 내부 SOP로 표시하지 않는다.
- 저장소 공개: 영상·프레임 등 타 게임 미디어는 커밋하지 않는다(media/ 는 .gitignore 대상). 인용은 URL·시점으로.
- 자료 수준 표기: [공식] [2차] [관찰 t=mm:ss] [추론].

## Decisions
- 사용자 ask(지시 제거 범위·실패·평가) 취소 → 결정은 조사 근거와 함께 제안 문서에서 선택지로 제시한다.
- 2026-10-05 "진행해" = §12 권장안 D1–D7 채택. 견학은 이전 동작 그대로 둔다(수용 기준 '견학은 현행과 동일').
- 링 한 겹 8칸 한계로 요청을 '요청 · 기관'(8)과 '요청 · 설비'(5) 두 묶음으로 나눴다.
- 보고 슬롯은 수첩이 채운다(플레이어가 값을 고르는 UI와 정확도 평가는 S4와 함께).
- 기관당 한 팀 출동 구조는 바꾸지 않았다(시설 쪽 두 업체를 동시에 부를 수 없음).

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| 시험 실행 중 스크립트 수정으로 PlayMode 실행이 중단됨(결과 없음, InitTestScene 잔류) | 1 | 시험 중에는 Assets 를 고치지 않고 클래스별로 순차 실행. 잔류 임시 씬 정리(추적 중이던 InitTestScene764e… 는 git 으로 복원) |
| 시험 SetUp 이 남긴 TYPESAFE_API_KEY=off 로 Play 에서 JEV 꺼짐 | 1 | 에디터 프로세스 환경 변수를 지우고 다시 Play |
| capture_game_view 저장 경로가 Assets 기준이라 Assets/Temp 에 PNG 생성 | 1 | 스모크 뒤 .planning/…/media/smoke(git 제외)로 옮기고 Assets/Temp 삭제 |
| 표준 첫 보고가 확인하지 않은 쓰러짐을 '부상자 없음'으로 말함 | 1 | [인원]은 가까이 가서 본 곳만 말하도록 수정 |
