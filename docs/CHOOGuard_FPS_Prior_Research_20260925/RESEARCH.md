# CHOOGuard FPS 선행조사 — 통합 보고서

조사 기준: **2026-09-25**. 범위는 자료조사, 공개 코드·매뉴얼·사진/영상 검토, 사용자 역질의, JEV 실제 API 실험이다. **게임 코드·씬·모델링·패키지는 변경하지 않았다.** 다른 세션의 내부 공간 모델링과 작업 경계를 유지했다.

## 1. 결론과 현재 방향

**기존 Unity의 명시적 세계를 유지하면서, 세밀한 1인칭 작업과 JEV 기반 NPC 판단을 결합하는 방향이 현재 근거와 사용자 선택에 맞는다.** 생성영상 모델로 게임 전체를 교체하거나 온라인 인간 멀티플레이를 먼저 도입할 이유는 이번 요구에서 확인되지 않았다. Unity 유지 자체는 연구 권고이며 새 엔진 교체를 사용자가 승인한 것은 아니다.

| 항목 | 이번 대화에서 정리된 내용 | 지위 |
|---|---|---|
| 사람 | 플레이어 **1명** | 사용자 확정 |
| NPC | AI 동료·시민 **수백 명** 목표 | 사용자 확정; 정확 인원/성능 실측 아님 |
| FPS 참고 | 이동·시점·조작감, 물체·도구 직접 조작, 협동·핑·역할 분담 | 사용자 확정 |
| 전투/배틀로얄 경쟁 | 총격전·최후 생존 경쟁은 선택하지 않음 | 이번 범위에서 제외 |
| 튜토리얼 | 본게임과 분리, 직무별 역할 전환, 세부 작업까지 직접 조작 | 사용자 확정 |
| 본게임 | AI/NPC가 활동하는 세계에 테러·사고·자연재해가 발생하고 사람이 대응 | 사용자 직접 설명 |
| JEV | 동료·시민의 판단기로 사용하고 적합성 검토 | 사용자 요청; 실험 결과는 조건부 적합 |
| 대화 | JEV + 별도 생성 대화 모델 | 사용자 확정; 생성 모델 제공자는 미정 |
| 학습 | 경험·기억·관계 변화와 별도 정책 학습 연구를 모두 포함 | 사용자 확정 |
| 연결 | 온라인 API 연결 허용 | 사용자 확정 |
| 정확성과 재미 | 작업·권한·완료 상태의 정합성 유지, 표현·진행 속도 조정 | **사용자 요청에 따른 JEV 권고**, 실증된 교육효과 아님 |
| 권리 조건 | 상용·유료·권리 제한 자료도 검색에 포함 | 사용자 지시; 실제 배포 권리는 별도 메타데이터 |

튜토리얼의 실수·정비 상태가 본게임 사건을 자동 유발하도록 연결하지 않는다. 자연재해가 반드시 NPC의 행위로 발생한다는 뜻도 아니다. **주체가 살아 있는 세계에서 사건과 영향이 발생한다**는 요구로 정리했다.

세 차례 제품 역질의의 답과 개발 구조는 [DECISIONS_AND_DEVELOPMENT_STRUCTURE.md](DECISIONS_AND_DEVELOPMENT_STRUCTURE.md)에 분리했다. 과거 계획의 AI 평가 결과나 `authority:0` 기록을 최신 사용자 승인으로 재사용하지 않았다.

이 조사를 바탕으로 한 후속 산출물: [정밀 설계와 OSS 적용 계획](../CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/DESIGN.md). 조사 당시 확인한 사실과 새 설계 선택을 구별하며, 게임 구현·설치 완료를 뜻하지 않는다.

## 2. 상세 조사 문서 안내

| 문서 | 내용 |
|---|---|
| [FPS_TECH_SOURCES.md](FPS_TECH_SOURCES.md) | Unity/Unreal/Godot, 상호작용·네트워크 공개 코드, 물리 계층, OpenBVE/Open Rails, 실제 파일·라이선스 |
| [RAIL_MAINTENANCE_MANUALS.md](RAIL_MAINTENANCE_MANUALS.md) | KORAIL 유지보수, 국토부 기준, 자격·운전 교재, NCS 30개 모듈 목록, 실제 확보와 공백 |
| [RAIL_SERVICE_SAFETY_MANUALS.md](RAIL_SERVICE_SAFETY_MANUALS.md) | 객실 청소·정리·승무·역무·교통약자·비상대응, 정확한 문서명과 기관별 역할 |
| [RAIL_VISUAL_REFERENCES.md](RAIL_VISUAL_REFERENCES.md) | 철도 현장 사진·방송·VR·영상 **25개 자료군**, 동작/소품 관찰점과 실제 열람 수준 |
| [GAMEPLAY_VISUAL_REFERENCES.md](GAMEPLAY_VISUAL_REFERENCES.md) | **12개 게임·24개 자료군**, 정비·청소·검표·HUD·명령·협동 비교 |
| [NPC_RANDOM_SCENARIOS.md](NPC_RANDOM_SCENARIOS.md) | 시민 인지·사회행동·이동의 분리, 제약 있는 사건 생성, 실제 사고·수요·기상 데이터 |
| [AGORA_WORLD_MODELS.md](AGORA_WORLD_MODELS.md) | Agora-2 기술보고서·실제 프리뷰, MIRA/Solaris/MASS 및 생성 월드 대안의 공개 상태 |
| [JEV_MODEL_SOURCES.md](JEV_MODEL_SOURCES.md) | JEV 공식 사양·약점·SDK·가격·서비스 경로·학습/기억/대화 한계 |
| [JEV_NPC_FEASIBILITY.md](JEV_NPC_FEASIBILITY.md) | 현재 로컬 연동 + **실제 JEV 호출** + 수백 NPC의 지연·비용·책임 분리 판정 |

문서군 숫자는 원문 전권 확보 수나 서로 독립된 승인 매뉴얼 수가 아니다. 교차 인용과 첨부 목록이 포함되므로 단순 합산하지 않는다.

## 3. 현재 프로젝트에서 확인한 출발점

- `ProjectSettings/ProjectVersion.txt`: Unity **6000.3.23f1**.
- `Packages/manifest.json`: URP **17.3.0**, Input System **1.20.0**. `multiplayer.center`가 있다는 사실은 게임 멀티플레이가 구현됐다는 증거가 아니다.
- `Assets/ChooGuard/App/Fps/FirstPersonResponder.cs`: CharacterController 이동·시선·점프 옵션·raycast와 `IFpsInteraction` 기반 상호작용.
- `Assets/ChooGuard/App/Fps/Tutorial/TutorialSession.cs`: 소화기 관찰·번호/판정 기록·정비 요청·표찰·증거 기반 완료의 구체적인 출발점. 코레일 교육과정 전체 구현이라는 뜻은 아니다.
- `workers/physics/worker.py`: JuPedSim·FDS 결과 연동이 있으나 **30×20×4m reference hall, 0~120초 화재장** 등 범위가 명시되어 있다. 부산역 전체의 임의 재난을 검증한 solver가 아니다.
- `graphify-out/GRAPH_REPORT.md`: **2026-09-21** 보고서의 FPS·튜토리얼·세계 상태·worker 경계를 참고했다. 이후 변경을 모두 반영한 실시간 그래프로 취급하지 않았다.

위는 관련 소스 구간을 읽은 결과다. 다른 세션의 동시 작업을 포함한 저장소 전체 실행 감사나 현재 씬 검증은 수행하지 않았다.

## 4. 재사용할 코드: 큰 FPS 게임보다 작은 상호작용 단위

아래는 **원문/공식 문서에서 확인한 재료**이지 설치·호환 검증이 끝난 패키지 선정표가 아니다.

| 필요 기능 | 실제 후보·파일 | 가져올 점 | 주의 |
|---|---|---|---|
| 문·스위치 상태 | [Boss Room `SwitchedDoor.cs`](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Assets/Scripts/Gameplay/GameplayObjects/SwitchedDoor.cs) | 명령·상태·표현 분리 | 해당 샘플은 연속 문짝 역학이 아니라 충돌 오브젝트 활성 전환 예 |
| 물체 잡기·점유 | [Boss Room `PickUpAction.cs`](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Assets/Scripts/Gameplay/Action/ConcreteActions/PickUpAction.cs) | raycast, 이미 점유된 물체 거부, 손 소켓 결합 | 싱글플레이에서도 사람/AI 간 중복 점유는 필요. NGO 전체 도입이 필수는 아님 |
| 힘 기반 문·서랍·키/소켓 | [XRI Physics Interactables](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/PhysicsInteractables.md) | joint·힘·축 제한·손잡이 | VR 입력을 마우스 FPS에 맞게 해석해야 함 |
| 부품 위치와 체결 구분 | [MscModApi Part](https://github.com/MarvinBeym/MscModApi/blob/master/docs/class-documentation/Parts/Part.md), [Screw](https://github.com/MarvinBeym/MscModApi/blob/master/docs/class-documentation/Parts/Screw.md) | `IsInstalled`와 `IsFixed`, 부모 부품 조건, 공구 크기 | 커뮤니티 모드 API이지 My Summer Car 원본 소스나 독립 Unity SDK가 아님 |
| 철도 문 상태 | [OpenBVE `Doors.cs`](https://github.com/leezer3/OpenBVE/blob/master/source/TrainManager/Train/Doors.cs), [Door.cs](https://github.com/leezer3/OpenBVE/blob/master/source/TrainManager/Car/Door.cs) | 좌우·인터록·개방 정도·재개방 상태 | 한국 차량의 실제 수치·절차를 그대로 보증하지 않음 |
| 차량 제동·물성 모델 | [Open Rails](https://github.com/openrails/openrails), [Physics manual](https://open-rails.readthedocs.io/en/latest/physics.html) | 압력·밸브·저항·차량별 파라미터의 분리 | 오락용 시뮬레이터를 실제 차량 검증모델로 전용하지 않음 |
| AI 과업 단계 | [GOAP HaulItemAction](https://github.com/crashkonijn/GOAP/blob/master/Demo/Assets/CrashKonijn/GOAP/Demos/Complex/Actions/HaulItemAction.cs) | claim→접근→집기→운반→놓기 | demo 완료 타이머를 실제 검수 근거로 사용하지 않음 |
| 군중 이동 | [JuPedSim](https://github.com/PedestrianDynamics/jupedsim), [RVO2](https://github.com/snape/RVO2) | 이동·회피·waypoint·queue | 사회적 인지, 압사 접촉력, 도움 동의, 인계 완료는 별개 |
| 제약 있는 사건 | [Scenic](https://github.com/Scenic-Foundation/Scenic) | 공간 제약·시간 조건·행동·불변조건 | 공개 Unity 완성형 어댑터 확보를 뜻하지 않음 |

**엔진 판단 [INFERENCE]:** 현재 GameObject/C#·URP 자산과 병행 모델링을 유지하는 Unity가 기본 후보다. Unreal Lyra의 interaction option/Experience, Godot/Jolt의 공개 관절 구현도 조사했지만 엔진 전환의 필수성이 확인되지는 않았다. DOTS 샘플을 기존 MonoBehaviour에 바로 붙일 수 있다고 가정하지 않는다.

**피할 함정:** Unity FPS Sample은 공식적으로 Unity 2018.3/HDRP·유지보수 중단 자료다. 구 Starter Assets FirstPerson 상품도 deprecated 표기를 확인했다. 이름만 보고 신규 기반으로 채택하지 않는다. 최신 상품과 XRI의 호환 표기가 있어도 현 프로젝트 조합에서 실행해 본 것은 아니다.

권리 메타데이터는 상세 보고서에 있다. Unity Companion, MIT/Apache/BSD, GPL/LGPL, 상용 런타임을 함께 포함했다. **라이선스 때문에 조사 후보를 제외하지 않았고**, 실제 코드/자산 배포 여부를 결정할 때 해당 파일·모델·콘텐츠별로 구분한다.

## 5. 물리는 역할별로 구분한다

| 계층 | 후보 | 이 조사에서 인정할 수 있는 범위 |
|---|---|---|
| 일인칭 이동·충돌·도구·관절 | 기존 Unity/PhysX, Rigidbody joints | 현장 상호작용의 가까운 출발점 |
| 호스·천·입자 유체 | Obi | 시각·물체 상호작용 후보. 유독가스/열 피해의 정답 엔진 아님 |
| 산업 기계·케이블·하중 | AGX Dynamics/AGXUnity | 별도 상용·플랫폼·모델 정확도 검토 대상 |
| ECS 대규모 강체 | Unity Physics/Havok | 전체 데이터/시스템 전환과 실제 병목을 먼저 확인 |
| 연기·열 해석 | NIST FDS/Smokeview | 적용 공간·경계조건·시간·검증 범위가 있는 별도 해석 |
| 피난 이동 | JuPedSim 등 | 계층화된 시민 인지/목표/도움 행동과 결합해야 함 |

[FDS 공식 자료](https://pages.nist.gov/fds-smv/)는 화재의 저속 유동·연기·열 해석을 설명한다. **FDS+Evac 지원은 6.7.8부터 종료**됐으므로 오래된 결합 예제를 유지보수되는 표준 대안으로 권장하지 않았다.

세부 나사산까지 강체 접촉으로 계산해야만 직접 정비가 되는 것은 아니다. 공구 선택·체결 상태·선행조건·측정·검수의 의미 모델과 실제 손맛을 함께 설계해야 한다. 세밀한 조작 목표를 단순 체크리스트로 축소하지 않는다.

## 6. 철도 매뉴얼: 실제 확보와 공백

| 자료 | 판본·확인 수준 | 사용할 수 있는 근거 |
|---|---|---|
| [KORAIL 철도차량 유지보수 세칙](https://www.kric.go.kr/KricFileDownload.do?file=M01020299071) | **2026.01.29 개정, 21쪽 PDF 관련 본문·별표 확인** | 정비 종류·주기, 문서 개정이력, 일일점검, 공기호스 표기 등. 차종별 상세 작업은 별도 기준으로 위임 |
| [국토부 철도차량정비 기술기준](https://law.go.kr/LSW/admRulLsInfoP.do?admRulId=67900&efYd=0) | **2025-530호, 2025.09.19 시행**, 관련 조문 확인 | 수행자·책임관리자·확인자, 정비 후 승인, 교정된 계측기·이력 |
| [제2종 운전면허 기능 교재](https://www.kric.go.kr/KricFileDownload.do?file=asdasdq532) | **2020.12.07 구판**, KORAIL 4호선 교육모델, 관련 구간 확인 | 준비 점검→실제 반응→지적확인·통신. KTX 전체 현행 SOP로 확대 불가 |
| [NCS 학습모듈 검색](https://www.ncs.go.kr/unity/th03/ncsModuleFileSearch.do) | 고속/전기/디젤 차량 유지보수 **30개 모듈 명칭·버전·PDF 버튼 확인**, PDF 본문 미확보 | 교육 단위 분류. 사원교육 승인본·법정 자격·차종별 작업서와 구분 |
| [코레일테크 청소 FAQ](https://www.korailtech.com/faq/list.do?boardType=Y0230&subMenuId=10010400) | 원시 HTML의 실제 답변 확인 | 「철도차량 청소작업 기준」 제17조/별표1·2, 「역사 등 청소 업무 위탁 용역 설계서」 제4조/붙임5의 정확한 이름과 요약 |
| [2025 해랑 청소 용역](http://co.korailtravel.com/community/bidding_read.asp?seq=744) | 공식 HWP 다운로드 성공, **본문 미독해** | 정식 과업설명서 확보 경로. 별도 상용 공개 미리보기와 동일판으로 단정하지 않음 |
| [철도 교통약자서비스 교육교재](https://dtis.kotsa.or.kr/sdm_pts/pmm/notice/18) | **2025.08.29 배포**, 철도 PDF 정상 다운로드·관련 장 확인 | 도움 필요 확인, 구체적 위치 안내, 의사소통 수단, 비상 안내·정보 전달 |
| [KTX·일반열차 승무원 직무자료](http://recruit.korailtravel.com/file/2024002/KTX열차승무원_1_1.PDF) | 공개 채용 PDF 본문 확인 | 출무·영접·순회·비품·유실물·청결 업무 및 「승무업무 매뉴얼」「비상대응 매뉴얼」의 존재. 상세 매뉴얼 원문은 아님 |
| [2025 한국 심폐소생술 가이드라인](https://www.kdca.go.kr/bbs/kdca/49/305205/download.do) | 2026.01.29 공지·02.09 수정과 정오표 경로 확인 | 일반인·전문가 범위 구분. 게임 구현/현장 적용은 전문 검토 필요 |

### 가장 중요한 판본 오류 두 가지

- KORAIL `preview/202604`의 유지보수 문서에는 **2020.00.00·제2000-00호**가 남아 있었다. 업로드 경로의 202604를 현행 개정일로 읽지 않았다.
- 비상대응 문서의 `202606` 경로에도 **2013.09.00·제2013-000호** 같은 미완성 표기가 있었다. 2017 안전관리체계도 구판으로 분류했다.

### 아직 확보하지 못한 자료

‘코레일 사원 매뉴얼을 모두 확보’했다고 주장할 수 없다. 특히 사용자가 선택한 세부 직접조작에는 다음 자료가 중요하다.

1. 차종·장치별 최신 작업지시서, 분해/조립 순서, 체결값·허용 한도·측정 도구·판정표.
2. 사업장별 작업허가·전원 격리·잠금표지·검전·접지·복전 승인과 교대/공동 작업 절차.
3. 청소 기준 전문, 오염물/소재별 도구·약품·소모품·분실물 처리 및 품질검수 기준.
4. 승무업무·비상대응 매뉴얼 최신 승인본, 직무별 장비 조작 권한과 인수인계 서식.
5. 사원교육 교안·강사 평가표·실습 시나리오·개정 이력.

공개 접근 실패·로그인 이동·본문 미해독을 모두 ‘비공개’로 단정하지 않았다. 상세 보고서에 기관요청 대상과 정상 획득 경로를 적었다. **모르는 수치를 게임 규칙으로 작성할 수는 있어도 그것을 공식 철도 기준으로 표시해서는 안 된다.**

## 7. 이미지·사진·영상: 바로 비교할 묶음

공식 자료와 상용 게임·방송·커뮤니티 실사 자료를 함께 포함했다. 영상의 링크/자막을 얻은 것과 실제 프레임을 본 것을 구분한다.

| 관찰 목적 | 직접 자료 | 확인한 것 / 한계 |
|---|---|---|
| 공구·부품·체결점 | [My Summer Car 작업대 사진](https://images.steamusercontent.com/ugc/394455950196896301/60CD4D0D4FE6516252C747F75AA9465B625F5223/) · [가이드 맥락](https://steamcommunity.com/sharedfiles/filedetails/?id=787506162) | 사진 직접 열람. 작업대·공구 크기·분리 부품. 실제 차량 정비 기준 아님 |
| 평시 승무 업무 | [TSW5 검표 화면](https://media-cdn.dovetailgames.com/2024/082024/08/Conductor-Mode-WCML.png) · [공식 설명](https://live.trainsimworld.com/news/tsw5-wcml-conductor-mode) | 사진·본문 확인. AI 운전 + 사람이 출입문/검표/통로 업무. 해외 규칙을 국내 SOP로 전용하지 않음 |
| 청소 대상과 개인 물건 | [House Flipper 2 청소 사진](https://houseflipper2.com/res/img/scr_11.webp) | 직접 열람. 도구 작용점, 부스러기와 보존할 물건 구분 |
| 요청·응답·핑 | [PUBG 라디오 휠](https://wstatic-prod-boc.krafton.com/common/news/20241008/c7l5dG0X.jpg) · [공식 패치](https://pubg.com/en/news/7810) | 직접 열람. 위치 표시·요청·확인 응답을 분리하는 참고 |
| 차량 인양·대차·작업자 위치 | [코레일 제공 현장 사진](https://pds.joongang.co.kr/news/component/htmlphoto_mmdata/201903/22/9d5af866-cdcf-448f-bb3e-bd80e0c3118f.jpg) · [기사](https://www.joongang.co.kr/article/23418402) | 직접 열람. 작업 자세·장비 규모. 공정 전체 순서 증거 아님 |
| 객실 위생 검수 | [화장실 점검 사진](https://cdn.econovill.com/news/photo/202602/729778_698105_4236.jpg) · [기사](https://www.econovill.com/news/articleView.html?idxno=729778) | 직접 열람. 청소 품질 확인 장면이지 세제 사용법 촬영 아님 |
| 청소팀 도구 운반 | [방송 스틸](https://cdn.newsworks.co.kr/news/photo/201909/396308_292328_217.jpg) · [tvN KTX 청소 영상](https://www.youtube.com/watch?v=4GHUw-uBaXg) | 스틸 직접 열람. 영상은 링크/설명 확인, 연속 동작 미시청 |
| 장기 정비 현장 | [KBS 정비단 72시간](https://www.youtube.com/watch?v=yoaNiPDZWNY) | 채널·2017.08.27 원방송 맥락·썸네일 확인. 현재 재생 가능/전체 시청을 주장하지 않음 |
| 차종별 내부 설비 비교 | [KORAIL KTX VR](https://info.korail.com/info/contents.do?key=1512) | 공개 VR 경로. VR 실제 조작과 치수 측정은 미수행 |

정비 애니메이션에는 **접근 자세→도구 선택→접촉점→상태 변화→확인→정리/인계**를, 청소에는 **오염·쓰레기·소지품 구분→작업→소모품 확인→품질검수**를 관찰 축으로 삼을 수 있다. 이는 자료에서 도출한 비교 틀이며 전 직무의 승인된 작업순서를 완성했다는 뜻은 아니다.

## 8. NPC와 무작위 사건은 같은 인과적 세계에 놓는다

[상세 근거](NPC_RANDOM_SCENARIOS.md).

- **시민은 이동 점이 아니다.** 목적지·일정·동행자·이동 지원·관측 정보·믿음·요청·현재 행동을 구분한다. [NIST Evacuation Decision Model](https://www.nist.gov/publications/evacuation-decision-model)은 이동 전 판단에 물리적·사회적 단서가 작용하는 근거다.
- **군중 회피와 사회행동을 분리한다.** JuPedSim/RVO2는 유용하지만 협조·오해·안내 수용·동행 유지·도움 동의가 자동 생기는 것은 아니다.
- **사건은 무작위 문자열이 아니다.** 위치·설비·환경·주체·전파 조건·대응 자원에 제약을 두고 허용된 원인과 결과를 조합한다. Scenic의 제약·시간 조건이 참고가 된다.
- **연출과 물리 규칙을 분리한다.** [Left 4 Dead AI Director 발표](https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf)의 긴장/완화는 pacing 참고다. 화면 밖 위협 생성 규칙을 실제 철도 재해의 물리 법칙으로 옮기지 않는다.
- **상태의 끝을 정확히 정의한다.** 군중 solver에서 exit로 삭제됨 ≠ 확인된 안전, 무전 수신 ≠ 인계 완료, 대사 ‘처리 완료’ ≠ 실제 작업 완료.
- **실제 데이터는 분류·범위 확인용이다.** [TS 철도사고·운행장애 공개자료](https://www.data.go.kr/data/15079935/fileData.do?recommendDataYn=Y)는 2021~2025년 범위·978행이라는 포털 메타데이터를 확인했다. 원시 CSV 분석은 안 했으며 운행 노출량 없이 게임 사건 확률을 산출하지 않았다.

NPC가 항상 최적 행동을 해야 한다는 결정은 하지 않았다. 오해·지연·실수는 게임/연구 대상으로 설계할 수 있지만, 모델 발화가 개발 권한·물리 법칙·확인된 사실을 임의로 바꾸는 것과는 구분한다.

## 9. Agora-2: 의미 있는 참고지만 그대로 들여올 엔진은 아님

[공식 발표](https://odyssey.systems/introducing-agora-2), [기술보고서](https://agora-2.odyssey.systems/agora-2.pdf), [상세 조사](AGORA_WORLD_MODELS.md).

- 공개 문구는 최대 20 humans and agents. 실제 보고서 배포 구성은 **인간 조작자 최대 4명 + 자율 엔터티 16개**다. 20명의 인간이 각자 플레이하는 구성으로 확대 해석하지 않는다.
- shared state + 학습 상태전이 + 서버 통합/생명주기 + 시점별 생성 렌더러다. 맵·스폰·시나리오 일부는 native-engine scaffolding에 남는다.
- 640×480, 목표 30 표시 FPS, 4인 세션에 RTX PRO 4500 4장 구성이 서술된다. 실제 sustained FPS·input-to-display latency와 장기/시점간 일관성의 정량 검증은 부족하다고 저자가 명시한다.
- 실제 웹 프리뷰의 시작·캐릭터 선택 화면과 공식 도해/영상 표본 프레임을 확인했다. 이름 제출·세션 플레이·성능 측정은 하지 않았다.
- Agora-2 전용 공개 SDK/API·weights·철도 도메인 전이·Unity object API 확보는 미확인이다. Odyssey 일반 스트리밍 API와 혼동하지 않는다.

**공개 구현 연구 후보:** [MIRA](https://github.com/mira-wm/mira), [Solaris](https://github.com/solaris-wm/solaris), [MASS 연구](https://alaya-lab.github.io/MASS/). MIRA의 동기화된 다중 시점 데이터와 Solaris의 모델/평가 코드는 실제 공개 경로가 있다. 이것도 특정 게임으로 학습한 연구 시스템이지 철도 정비용 완성 플러그인이 아니다.

현재 사용자가 원하는 것은 **생성영상 자체보다, 인간과 AI가 같은 세계에서 행동하는 것**이다. 이 목적에는 Unity의 명시적 상태와 NPC 판단/실행 분리가 더 직접적인 출발점이다.

## 10. JEV 정밀검토의 핵심

[전체 판정·실측](JEV_NPC_FEASIBILITY.md).

- **조건부 적합:** 시민/동료의 의미 판단과 제한된 행동 선택. 자유대화는 별도 모델, 기억·실행·학습은 별도 시스템.
- 실제 `jev-1.13.0` 기본 실험 **12/12 선택 일치**, 순차 중앙값 **576ms**. 명확한 합성 상황이며 실제 NPC 정확도 100%를 뜻하지 않는다.
- 정보 확인 선택지를 빼면 confidence 0이어도 A/B 중 하나가 반환됐다. 두 직원은 같은 카트를 동시에 요청했다. **보류 선택지·권한·자원 예약·최신 상태 확인을 코드가 소유해야 한다.**
- 공식 direct 한도 **1,200 RPM**. 300명 전원 매초 판단은 **18,000 RPM**으로 초과한다. 수백 시민이 개별 상태를 갖는 것과 전원 고빈도 API 호출은 별개다.
- 요청당 2,000 tokens 가정 시 ‘활성 50명/5초 + 일상 250명/60초’는 약 **850 RPM, $4.284/시간**이다. 이는 규모 감각용 계산이지 채택된 주기·성능 보증이 아니다. 별도 대화 모델/서버 비용은 제외한다.
- 현재 `/turnaround` 어댑터는 작업 복귀 workload 분류용이다. 원격 호출이 이미 있다는 사실만으로 NPC 행동 시스템이 준비됐다고 볼 수 없다.

## 11. 검증 범위와 남은 결정

### 실제 수행

공식 웹/논문/PDF/공개 코드/LICENSE·일부 사진의 직접 열람, 공개 교육교재 다운로드·관련 장 독해, Agora 프리뷰 입구/표본 영상 관찰, 사용자 제품 역질의 3회, JEV 모델 목록과 **추론 18회**를 수행했다. 연구 산출물에 원문 URL·열람 수준·실패·미확인을 남겼다.

### 수행하지 않음

게임 실행·씬 변경·코드 구현·패키지 설치·Unity 빌드/테스트, 패키지 호환성 시험, 전체 영상 재생, VR 조작 검증, 실제 철도 설비 안전 검증, 수백 NPC 부하 시험, 교육효과 시험, 법정 교육 인증은 하지 않았다. 조사 작업이므로 게임 테스트를 실행한 것으로 보고하지 않는다.

### 다음 개발 계획에서 구체화할 항목

정확한 차량 형식/장치별 작업서, 최종 NPC 수·목표 하드웨어·목표 FPS, 생성 대화 모델과 음성 포함 여부, 세션 길이/저장 범위, 사건별 검증 가능한 물리 범위가 남는다. 과거 문서의 20~30분이나 특정 차종 범위를 이번 대화에서 새로 확정한 것으로 표시하지 않았다. 이것들은 [개발 구조 문서](DECISIONS_AND_DEVELOPMENT_STRUCTURE.md)의 명시적 미정 항목이다.

### 조사 자기평가

정확성 **4/5**, 범위 충족 **4/5**, 명료성 **4/5**, 실행 가능성 **4/5**, 간결성 **4/5**. 실제 원문·코드·API 결과를 확보했으나 모든 내부 SOP·모든 영상·수백 NPC 동작은 검증하지 않았다. 통합 문서는 읽는 경로를 제공하고 상세 증거는 분야별 문서에 남겼다.
