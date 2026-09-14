# Foundation 공동 훈련 기준 v2 — 20/100/2

상태: **사용자 합의, 구현·수용 진행 중**. 2026-09-08 제시한 구축 계획 및 기존 GitHub 보드를 업데이트한 뒤 개발하라는 지시가 출처다. 요구 도출 보류와 과거 싱글플레이 전용 제외 범위를 대체한다. [상위 작업 #54](https://github.com/xrlab-dau/CHOOGuard/issues/54), [개발 실행 보드](https://github.com/orgs/xrlab-dau/projects/1), [준비·검증 보드](https://github.com/orgs/xrlab-dau/projects/2).

## 완료 범위

| ID | 이번 Foundation의 확정 요구 |
|---|---|
| F-MAP | 기존13구역 전체의 실제 이동·상호작용. 철도/도시철도 차량·선로·승강장·역사·매표·광장·유라시아플랫폼·지하 연결통로. 구역별 scene, 다층 경로·승하차·늦은 참가·로딩 중 상태 유지 |
| F-LOAD | 교관 포함20클라이언트·NPC100·동시사건2건. 후속100/500/3은 현재 완료 조건이 아님 |
| F-AUTH | 전용 서버가 세계를 결정. 접속신원·직무·거리·가림·대상 상태·중복CommandId를 검증하고 참가자별 관측 정보만 배포 |
| F-FLOW | 평상시 운영→불시 발견→대응→복구. 기본 경험에 사건 선택기·발생 카운트다운·미발견 해결 위치를 노출하지 않음 |
| F-TEAM | 실제 여러 직무 팀의 공유설비·자원 경합·정형 보고·수신확인·NPC 인솔 경합/인계·교관 운영·개인/팀 복기 |
| F-FIRE | 문/환기에 반응하는 구획 열·연기 보존·교환, 긴 복도/차량 국소흐름. 계산 상태를 렌더링 |
| F-CROWD | 경로탐색·보행속도·벽/사람 상호작용·접촉·밀집·병목이 실제 이동에 영향. 동일한 얼굴 없는 대피자, 감정·직무 대행 없음 |
| F-TRAIN | 역/접근선로/터널 자동운행, 거리·속도·가감속·제동·편성 전체 길이 점유, 고장/비상정차/차내 화재. 탑승자·설비·화재의 차량 좌표계 일관성 |
| F-NAV | 다층/구역 경로·관측/보고된 팀 상황. 연습은 요청형 근거 안내, 평가는 절차 힌트 은닉. 같은 서버 세계 사용 |
| F-VOICE | 자체호스팅 팀/지휘 PTT, 발언/구독 권한. 해제·포커스 상실·입력 비활성·연결 해제 시 송신 중단. 음성 녹음 미제공 |
| F-RECOVERY | 참가자 재접속, 버전 있는 체크포인트+순서 기록, 승인행동 중복 실행 방지, 서버 재시작 후 교관 확인까지 정지 |
| F-RUN | Windows Desktop·Mac 개발 실행·Windows/Linux 전용 서버. 개발 월고정비0원·보유 장비. 유료 단기 인터넷 시험은 비용 산정 후 판단 |
| F-SOURCE | 운영기관·근거문서·개정판·적용구역/차량/직무·검증상태를 데이터로 분리. 라이선스·재배포 조건과 출처 등급을 확인한 공개 자료를 코어 재작성 없이 반영하며, 확정되지 않는 치수·배치·직원 SOP는 `공개 자료로 확정 불가`로 유지 |
| F-ART-NATIVE | 네이티브 Blender 33종 자산 및 Unity 기본 저작 기반으로 3D 모델·13구역 맵·오브젝트를 제작/고도화하고 Unity에 연결한다. 외부 AI 생성 서비스(Higgsfield/VARCO 등)는 배제. |
| F-AAA | 2026-09-09 추가: 워커 팀과 독립 harsh critic, 동일 조건 익명 A/B 비교·판정 잠금·합격까지 자동 수정. [검수 절차](art/aaa-review-workflow.md), [동결 루브릭](art/aaa-review-rubric.md). 순수 네이티브 제작과 기존 기능·성능 수용을 유지 |


2026-09-12 최신 결정상 코레일은 개발 요구·도면·평면·매뉴얼·자료를 제공하지 않으며 실제 역사 촬영과 현장 모의훈련도 수행하지 않는다. 자료 제공이나 촬영 승인을 기다리지 않고 공개 자료의 근거 범위에서 개발한다. 실제 부산역 정밀 디지털 트윈·기관 직원SOP·현장 훈련효과는 미검증 경계를 유지하며 일반 기능 구현으로 수용하지 않는다. VR·기관 상시운영 역시 별도 후속 범위다. 공개 자료의 근거 공백은 일반 기능 개발의 전역 장애가 아니다. LLM이 실제 안전절차나 공식 점수를 결정하지 않는다.

## 서버 구조와 공통 계약

기존 `Runtime/TrainingContracts.cs`와 `TrainingSession.cs` 및 `Demo/`는 보존할 legacy 시제품이다. 새 권위 세계 코어를 엔진 없는 `Runtime/Multiplayer/`에, NGO 전송·Unity 물리/표현·Editor builder·플레이 테스트를 별도 `Multiplayer/` assembly에 둔다. 시제품 `StationWorldSession.Act`만 RPC로 감싸지 않는다. 현재 거기에는 인증/거리/가림 검증과 복구 가능한 세계 checkpoint가 없다.

- `WorldId`, `ShiftId`, `ParticipantId`, `TeamId`, `CommandId`는 명시적인 계약 필드다. 송신 클라이언트 ID에서 서버가 참가자를 찾고 요청의 참가자 주장과 대조한다. 신원/직무 배정은 서버 승인 입장 자료에서 읽는다.
- 세계 상태에는 seed/profile·사건·설비·군중·열차·시각·승인 원장과 재현에 필요한 상태를 포함한다. `ObservedState`는 인근 객체·해당 참가자의 직접 관측·권한 있는 팀 보고로 투영하며 전체 세계 DTO를 전송하지 않는다.
- 승인 영수증은 world/shift/participant/command와 정규화 payload를 결속한다. 재전송은 원래 결과를 반환하고 같은 ID의 다른 payload는 거부한다. 대상별 revision으로 설비 경합을 처리한다.
- 개인 Esc와 포커스 상실은 로컬 입력/송신을 끈다. 교관만 세계 정지/재개·복구 확인을 수행한다. 기존 싱글플레이의 전역 pause 회귀와 새 멀티 동작을 구분한다.
- 내구성 실패 시 승인/세계 변경을 확정하지 않는다. 체크포인트와 순서 기록은 완전성·버전·hash를 검사하며 부분 저장/중복/누락을 검출한다. 복구 후 재입장과 교관 확인을 요구한다. legacy schema2 복기 내보내기를 서버 복구 파일로 오인하지 않는다.

| 계층 | 고정 기술 / 경계 |
|---|---|
| 저작/실행 | Unity6000.3.23f1, Blender·기존33종 자산·개별 참조 기준. 외부 AI 생성 서비스 배제, 순수 네이티브 툴체인 적용 |
| 게임 전송 | NGO2.13.2 + UTP2.7.4,20Hz. 인터넷 암호화·인증서 및 hostname 검증 |
| 음성 | LiveKit server1.13.6 + UnitySDK2.0.0. 서버 발급 room/participant 제한 토큰, 팀/지휘 채널 |
| 저장 | 서버 전용 checkpoint·승인행동 journal; 클라이언트 관측/복기 payload와 분리 |
| 자료 | 합성/기관/이용객/제조사 자료 구분, 출처·개정·적용범위·검수 상태를 data contract에 결속 |

버전 존재는 실제 조합 PASS가 아니다. [UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.Transports.UTP.UnityTransport.html), [LiveKit SDK2.0.0](https://github.com/livekit/client-sdk-unity/tree/v2.0.0), [서버1.13.6](https://github.com/livekit/livekit/releases/tag/v1.13.6)을 대조한다. 자체호스팅 LiveKit은 참여자 제거만으로 이미 발급한 토큰이 즉시 무효화되지 않는다. 팀 변경 시 channel epoch/room 교체와 만료·발급 차단을 검증하며 클라이언트 unsubscribe를 접근 통제로 취급하지 않는다. [공식 토큰 제약](https://docs.livekit.io/frontends/reference/tokens-grants/).

## 구현·시험 추적

| 단계 | 작업 | 산출물 | 현재 수용 |
|---|---|---|---|
| 1 | [#10](https://github.com/xrlab-dau/CHOOGuard/issues/10) | 요구사항·공통 계약·수용 근거 정합화 | 구현/검증 전 |
| 2 | [#27](https://github.com/xrlab-dau/CHOOGuard/issues/27) | 전용 서버·실제 두 클라이언트 공유 행동 경로 | 구현/검증 전 |
| 2 | [#99](https://github.com/xrlab-dau/CHOOGuard/issues/99) | 재접속·서버 체크포인트·순서 기록 복구 | 구현/검증 전 |
| 2 | [#100](https://github.com/xrlab-dau/CHOOGuard/issues/100) | LiveKit 자체 호스팅 음성 무전·PTT | 구현/검증 전 |
| 3 | [#82](https://github.com/xrlab-dau/CHOOGuard/issues/82) | 13구역 장면 전환·다층 이동·공유 상태 통합 | 구현/검증 전 |
| 4 | [#101](https://github.com/xrlab-dau/CHOOGuard/issues/101) | 구획·복도·차량 열·연기 모델과 검증 | 구현/검증 전 |
| 4 | [#59](https://github.com/xrlab-dau/CHOOGuard/issues/59) | NPC100 보행·밀집·병목·접촉 모델 | 구현/검증 전 |
| 4 | [#102](https://github.com/xrlab-dau/CHOOGuard/issues/102) | 역·접근 선로·터널 열차 운행·이동 좌표계 | 구현/검증 전 |
| 4 | [#60](https://github.com/xrlab-dau/CHOOGuard/issues/60) | 동시사건2건·불시 발견·대응·복구 데이터 | 구현/검증 전 |
| 5 | [#35](https://github.com/xrlab-dau/CHOOGuard/issues/35) | 다직무 팀 협업·보고·수신확인·인계·교관 운영 | 구현/검증 전 |
| 5 | [#88](https://github.com/xrlab-dau/CHOOGuard/issues/88) | 다층 네비게이터·연습/평가·요청형 근거 안내 | 구현/검증 전 |
| 6 | [#91](https://github.com/xrlab-dau/CHOOGuard/issues/91) | 20/100/2 통합·60분 부하·인터넷·음성 실측 | 구현/검증 전 |
| 6 | [#92](https://github.com/xrlab-dau/CHOOGuard/issues/92) | 클라이언트·전용서버·음성 실행본 및 독립 재현 전달 | 구현/검증 전 |

추가 작업: [FMP-14 #106](https://github.com/xrlab-dau/CHOOGuard/issues/106)은 외부 생성 서비스 폐기 결정에 따라 Native-only 후속으로 보존한다. [FMP-15 #107](https://github.com/xrlab-dau/CHOOGuard/issues/107)은 독립 AAA 블라인드 반복으로 유지한다.

상태는 GitHub 실조회와 실행 영수증으로 갱신한다. 이 표와 context hash를 바꾸는 행위가 PASS를 생성하지 않는다.

## WU3 REWORK 현재 순서와 중단점

WU3 REWORK는 부하·증거 수용을 다시 확인하는 P1 세 건이며, 독립 실행 8/8과 Python 26/26은 부하 수용이 아니다.

1. **#140 / FMP-12a** — 2~4 클라이언트 축소 리허설로 metric collection pipeline을 검증한다. 현재 `run_foundation_load.py`의 준비 경로는 `PREPARED_NOT_RUN`이며 headless protocol rehearsal일 뿐, 물리·음성·WAN·desktop renderer 수용이 아니다.
2. **#141 / FMP-12b** — 실제 프로토콜 클라이언트 20개·NPC100·동시사건2건을 물리와 음성을 켠 상태로 분산 순회 60분과 한 집결지 60분 각각 실행하고 정제 receipt를 남긴다. 두 시험은 아직 실행되지 않았다.
3. **#142 / FMP-12c** — RTT100ms·손실1% 조건에서 원격 행동 p95 250ms와 음성 p95 300ms, 게임/음성 송수신량·재전송·지터를 분리 측정한다. 실제 microphone/speaker와 서로 다른 네트워크 수용은 아직 없다.
4. **#143 / FMP-13a → #144 / FMP-13b → #92** — Windows/Linux 전용서버·Windows Desktop 실행본을 만들고, 새 checkout의 다른 개발자 재현과 source/build manifest를 연결한 뒤 전달한다.

`full407=332/0/75skip`, `isolate70/70`, 독립 8/8, Python 26/26은 당시 관측·진단 기록이다. 새 PASS나 20/100/2 부하 receipt로 승격하지 않는다. 열 계산 p95 68.42ms와 120-body motion 약 493~513ms도 서버 50ms 예산 미달 관측으로 유지하며, 원인은 미확정이다.

## 검증 계약

기능은 동일설비 동시조작·중복요청·거리/가림/직무 거부·NPC 경합/인계·팀간 보고·미발견 payload 차단·연습/평가 차이·구역 이동/승하차/늦은입장을 포함한다. 연결 끊김·교관 이탈·서버 강제종료·저장 중단 후 팀/직무/사건/열차/승인기록을 비교한다.

물리는 질량/에너지 보존·음수농도 방지·시간간격 수렴, 공개 화재 실험의 온도/연기층/개구부 유동, 군중 밀도–속도/출구폭별 유량/대향통행/병목, 열차 정지거리/제동지연/편성 점유해제/탑승자 좌표를 검사한다. 보정 사례와 검증 사례를 분리하고 오차/적용한계를 기록한다. [NIST CFAST](https://pages.nist.gov/cfast/), [NIST 피난모델 V&V](https://www.nist.gov/publications/process-verification-and-validation-building-fire-evacuation-models).

| 시험 | 설계 기본값 / 수용에 필요한 증거 |
|---|---|
| 부하 | 교관 포함20개 실제 프로토콜 클라이언트+NPC100+동시사건2, 물리/음성 켜기. 분산순회와 한 집결지 각각60분 |
| 서버 |20Hz 틱 처리시간과 stall/메모리 측정 |
| Windows |실제 사양·1080p frame interval p95≤33.3ms 초기 목표 |
| 네트워크 |RTT100ms·손실1%,원격행동 반영 p95≤250ms 초기 목표 |
| 음성 |음성 종단지연 p95≤300ms 초기 목표, 실제 마이크/PTT/수신 검증 |
| 계측 |게임/음성 송수신량·재전송·지터 별도. 로컬FPS는 네트워크 수용 근거가 아님 |
| 인터넷 |서로 다른 네트워크 실접속.20프로토콜 부하와 여러PC 사람 조작/PTT를 따로 기록 |
| 전달 |실행본·서버/음성설정·자료작성계약·부하도구·정제기록·source/build hash·다른 개발자 재현 |

이 수치는 목표이며 현재 통과 결과가 아니다. 실패는 수정하거나 근거와 함께 목표 변경을 논의한다. 실제 자료는 Exa로 발견한 원문을 조건/행동/보고/인계에 연결하며 코레일 공개자료·부산교통공사 이용객 안내·[SR 직원 매뉴얼](https://www.srail.or.kr/cms/attach/download.do?pageId=KR0701300007&atchNo=47)의 적용 대상을 구분한다. SR 자료를 코레일 직원 SOP로 전용하지 않는다.
