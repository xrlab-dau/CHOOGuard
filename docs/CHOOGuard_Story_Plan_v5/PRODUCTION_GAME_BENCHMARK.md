# 프로덕션 게임 벤치마크·OSS 이식·자체평가

2026-09-21 · CS-EXEC.01.01 · 현재 제품 수용: **NOT_ACCEPTED**

## 판단과 조사 범위

JWE3의 시설→팀→업무 구조 일부를 Unity에 재구현했지만, 그것만으로 운영 게임을 완성했다고 볼 수 없다. 시설의 서비스 범위, 가용 자원, 작업 점유, 복구가 다음 선택을 바꾸어야 한다. 세계를 클릭해 행동하는 인터페이스가 중심이며 내부 상태 그래프와 모델 확률은 사용자용 편집 화면으로 노출하지 않는다.

대표 상용 게임 8종과 공개 소스 게임 3종을 대상으로 운영·조작·UI·오브젝트·애니메이션·코드 구조를 비교했다. 추가로 C# 라이브러리 3개를 검토했다. 전 작품을 직접 장시간 플레이한 조사나 매출·시장 규모 조사는 아니다. 공식 가이드의 전체 본문, 공식 검색 발췌, 영상 프레임, 실제 소스 코드를 구분했다. 기존 JWE3 자료는 15개 가이드/52개 How-to 목록과 14개 본문·입문 개요이며, 영상 전체의 조작 인과관계를 검증한 것은 아니다.

이번 운영 구조 비교에서 전체 본문을 재검토한 자료는 JWE3 공식 캐시 3개다. Planet Zoo·Planet Coaster 2·Two Point의 일부 항목은 공식 검색 발췌로 확인했으며 전체 본문 검토로 세지 않았다. 과거 버전의 공식 업데이트는 상호작용 패턴의 근거로 사용하고 현행 화면과 완전히 같다고 주장하지 않는다.

## 게임별 적용 판단

| 대상 | 확인한 운영·게임 구조 | CHOOGuard 적용 | 자료 수준 |
|---|---|---|---|
| JWE3 | 시설에 속한 팀, 작업 지정, 초소 범위의 자동 업무, 공급품·정비·복구 | 실제 기관을 운영 거점으로 사용. 기관/대상 건물을 클릭해 지원을 요청하고 논리 팀이 이동·수행·복귀. 복귀 후 재출동 준비이 가용성을 바꾸도록 연결 | 공식 가이드 본문. [운영](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/operations-guide), [관리](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/park-management-guide) |
| Planet Zoo | 직무별 직원·시설, 업무 구역, 이동·시설 용량과 관리 상태의 관계 | 기관별 지원 역할, 담당 구역, 처리 용량의 설계 근거. 시민을 개별 명령 대상으로 만들지 않음 | 공식 발췌. [직원·방문객](https://www.planetzoogame.com/help-centre/player-guides/staff-and-guests), [건설](https://www.planetzoogame.com/help-centre/player-guides/building-your-zoo) |
| Planet Coaster 2 | 심각도 알림, 관리 오버레이, 직원 스케줄·구역, 전력·수처리·정비 | 문제 발견→시설 선택→원인 확인→업무 요청의 계층. 서비스 공백·응답시간·부하를 선택 가능한 관리 레이어로 발전 | 2024 공식 개발글 발췌. 현행 세부 화면 동일성 미검증. [관리 개발글](https://www.planetcoaster.com/en-US/news/2024-09-25/deep-dive-mastering-management) |
| Two Point Museum | 직원 배정, 외부 파견과 귀환, 전시 유지·보안·운영의 자원 경쟁 | 점검·훈련·현장 대응 때문에 가용 팀이 줄고 귀환 뒤 다음 과업이 달라지는 구조. 탐사 전리품을 비상대응 보상으로 복제하지 않음 | 공식 발췌. [개발사 소개](https://www.twopointstudios.com/games/two-point-museum/) |
| Age of Empires IV | 선택한 건물의 정보·수용 인원을 같은 패널에 제공. 선택 대상에 맞는 행동 메뉴 | 클릭한 건물이 정보창과 호출 버튼의 기준. 선택과 명령, 카메라 초점 이동을 구분 | 공식 과거 업데이트·조작표. Xbox Site Menu의 패턴을 PC 마우스 바인딩으로 오인하지 않음. [건물 패널](https://www.ageofempires.com/news/ageiv_seasonfour_update_60878/), [단축키](https://www.ageofempires.com/news/aoeiv-shortcuts-revealed/) |
| Cities: Skylines II | 건물 정보와 자원 흐름·생산 연결을 함께 설명하는 관리 화면 | 숫자만 나열하지 않고 부족한 서비스와 관련 시설을 연결. 현재 전 도시 자원망은 미구현 | 공식 개발글/영상 소개. [개발글](https://www.paradoxinteractive.com/games/cities-skylines-ii/news/work-in-motion) |
| 112 Operator | 실제 도시 지도에서 신고와 출동 자원을 연결하는 운영 | 실제 기관·도로·요청을 연결하되 현장 행동과 물리 결과를 생략한 숫자 처리로 끝내지 않음 | 공식 개발사·스토어 발췌. [개발사](https://jutsugames.com/index.php/operator-112/), [스토어](https://store.steampowered.com/app/793460/112_Operator/) |
| Rescue HQ | 공동 기지의 직원·차량·장비·교대·준비도 관리 | 출동과 기지 가용성·재출동 준비를 연결하는 인접 장르 기준 | 공식 소개·스토어 발췌. [공식 소개](https://rescue-hq.aerosoft.com/?lang=en), [스토어](https://store.steampowered.com/app/809720/Rescue_HQ__The_Tycoon/) |
| OpenRCT2 | 지속되는 세계 화면과 도구 모음, 커서 줌 등의 설정. 역할에 따른 애니메이션 그룹을 소스에서 분리 | 세계 화면을 유지하는 조작 계층, 상태 중심의 단순한 NPC 표현. 스프라이트 구현을 3D 애니메이션과 동일시하지 않음 | 공식 매뉴얼·실제 C++ 소스. [인터페이스](https://docs.openrct2.io/en/latest/playing/main_interface/), [조작 설정](https://docs.openrct2.io/en/latest/setup/options.html) |
| OpenRA | 작업 활동의 대기·취소를 틱으로 처리하는 실제 C# 코드 | `Wait`의 카운트다운 부분을 실제 이식해 팀 보충 시간을 승인된 시뮬레이션 시간으로 진행 | 고정 리비전 실제 소스. [Wait.cs](https://github.com/OpenRA/OpenRA/blob/f3ec7f8e1593b482f85fd101652deb740c33dee6/OpenRA.Mods.Common/Activities/Wait.cs) |
| OpenTTD | 명령 ID·종류·검증을 연결하는 명령 테이블 | 기존 명령 계약과 그래프의 책임을 검토하는 비교 자료. 동일한 전역 명령 체계를 추가하지 않음 | 고정 리비전 실제 소스. [command.cpp](https://github.com/OpenTTD/OpenTTD/blob/b96f16a478d6eb7287d1a87130f84c2f01cdb319/src/command.cpp) |

## 요소별 결과

| 요소 | 채택하는 원리 | 현재 상태와 한계 |
|---|---|---|
| 게임 진행 | 평시 준비→사건 대응→복구→다음 운영의 가용성 변화 | 50명 참조 홀의 운영·대응·복귀 흐름 확인. 도시 전체 수요·시설 열화·캠페인 진행은 미완료 |
| 조작 | 세계의 건물 선택→해당 건물의 가능한 행동→대상 지정 | 아무것도 선택하지 않으면 정보창을 숨김. 기관/부산역/팀별 카드, 잘못된 대상 클릭 보존, 빈 공간 선택 해제 확인 |
| 카메라 | 원근 시점, 커서 기준 확대, 선택과 초점 이동 분리 | native API에서 커서 앵커 오차 1픽셀 미만, 부드러운 초점·추적 중 줌 유지 확인. OS 마우스와 JWE3 전체 조작 동등성은 미검증 |
| UIUX | 작은 상황별 행동 화면, 운영 상태와 알림, 완료 후 복기 | 상시 기관 목록·공개 그래프·AI 예측 패널 제거. 내부 보기를 선택한 부산역에서만 층 버튼 제공 |
| 오브젝트 | 시설 역할·연결·가용 상태를 읽을 수 있는 재사용 단위 | 실제 기관 좌표와 차량 그룹, 미터 단위 모델. 부산역·중앙119·초량119 외관 3개 교체. 도시 대부분은 여전히 단순 형상 |
| 애니메이션 | 업무 상태와 표현을 분리하고 이동·대기·작업을 읽기 쉽게 유지 | OpenRCT2 역할→애니메이션 그룹 매핑 확인. 현재 3D 인물의 단순 이동 표현 유지. 정지 프레임만으로 타 게임의 애니메이션 부재·완성도를 단정하지 않음 |
| 운영 자원 | 팀 점유와 복구 시간이 다음 요청의 선택 비용이 됨 | 복귀 후 재출동 준비 상태와 재출동 제한 추가. 실제 연료·소모품 재고·시설 유지비·서비스 반경 전체는 미구현 |
| 내부 계산 | Jev 입력을 실제 운영 산술에 사용하고 상태는 실행 증거로 확정 | 보충 시간식에 적용. 유체·열·군중 힘 계산을 Jev가 대체했다는 주장은 하지 않음 |

JWE3 공식 영상의 00:54/01:06 프레임은 원근감과 세계 위의 게임 UI를 보여준다. 정확한 마우스 동작, 카메라 보간 시간, NPC 내부 구조의 증거는 아니다. [공식 영상](https://www.jurassicworldevolution.com/en-US/3/gallery/video/feature-focus-building-your-parks)

## 실제 OSS 코드 이식

원본은 OpenRA `f3ec7f8e1593b482f85fd101652deb740c33dee6`의 `OpenRA.Mods.Common/Activities/Wait.cs`다. 취소 검사와 `remainingTicks-- == 0` 종료식을 `Assets/ChooGuard/App/Mvp/ThirdParty/OpenRaCountdown.cs`로 옮겼다. `Activity`/`Actor` 의존성은 제거하고 승인된 시뮬레이션 시간만 받는 래퍼를 추가했다. 원본의 N+1회 호출 의미는 시작 시 첫 틱을 소비하는 방식으로 보존했다. 원본 저작권 표기와 `COPYING`, 변경 설명은 같은 폴더의 `NOTICE`에 남겼다.

단순 코드 보관이 아니라 현재 실행 경로에 연결했다. `MvpAgencyDispatchController`가 기관에 돌아온 논리 팀을 `Replenishing`으로 전환하고 이 타이머를 진행한다. 그동안 팀은 주차된 상태로 요청 대상에서 제외된다. 완료 이벤트가 내부 그래프의 `replenished` 연결을 통과해야 `Ready`가 된다. 화면의 여러 차량은 한 팀의 같은 상태를 표현한다.

BlueRaja의 MIT 안정 우선순위 큐, Stateless, UniRx도 실제 코드를 확인했다. 현재 필요한 계약 없이 추가하면 이미 있는 큐·규칙 평가·예약 상태와 역할이 중복되어 이번에는 설치하거나 복제하지 않았다. OpenTTD 명령 테이블과 OpenRCT2 금융·애니메이션 코드는 비교 자료로 보존했다. 원본 조사 스냅샷은 약 2.4MB다.

## Jev 내부 산술

Jev는 완료한 과업의 관찰값과 작업 이력에서 보통/추가 보충 조건의 가능성을 반환한다. 클라이언트는 아래 평균 시간 모형을 계산해 팀 보충 타이머에 적용한다.

`보충 시간 = p(보통 조건) × 보통 시간 + (1 − p) × 추가 시간`

현재 15초/45초는 훈련 게임용 운영 계수다. 실제 119 소요시간이나 임상·인체 물리 계수가 아니다. 반환 확률을 특정 시점까지 준비가 끝날 확률로 재해석하지 않는다. 모델 응답은 복귀 이동 중 요청하고 기관 도착 시 고정한다. 응답 누락·불일치·지연 시 명시적 기본 시간만 사용하며 확률을 만들어 넣지 않는다. 늦은 응답이 진행 중인 타이머를 바꾸지 않는다.

실제 Unity 실행에서 `p=0.55`가 `28.5초`로 계산됐고, 1초 단위 승인 갱신에서 29초 뒤 가용 상태가 됐다. 모델 왕복은 이 사례에서 780ms였다. 일시정지 중 시간·위치가 유지되고, 보충 중 재출동이 차단되는 것을 확인했다. 확률이나 내부 그래프는 플레이 화면에 표시되지 않는다. 이 한 사례는 예측 정확도·현장 보정의 증거가 아니다.

## 자체평가

**전체 프로덕션 게임과 실측 디지털 트윈 수용은 미완료다.** 이전에는 엔진 연결·로그·그래프 검증을 게임성 진척으로 지나치게 크게 다뤘다. 건물 선택 중심이라는 기준을 최초 UI 발주와 수용 조건에 강하게 고정하지 못했고, 내부용 그래프·모델 출력을 플레이 화면에 노출했다. 이는 메인 오케스트레이터의 요구 해석·발주·검수 오류다.

현재 확인된 것은 시설/팀/업무 구조, 제한된 참조 홀의 실행, 클릭한 건물의 상황별 UI, 일부 카메라 동작, OpenRA 타이머 이식, Jev 값의 내부 보충 시간 계산이다. 이를 JWE3 전체 UIUX·건설·경제·도시 생활·애니메이션 품질과 동등하다고 평가하지 않는다. 부산 지형·기관 좌표·FDS/JuPedSim/DotRecast 설치는 프로젝트 기반 작업이며 JWE3 게임성 이식 실적으로 합산하지 않는다.

다음 우선순위는 **서비스 범위와 시설 용량 → 실제 소모품·정비·운영 공백 → 반복 가능한 복구·다음 운영의 선택 비용**이다. 런타임 건설·청사진·저장/복원·도시 전체 생활 수요와 장기 플레이 검수도 남아 있다. 우선순위는 같은 단일 스토리에서 다루며 별도 소규모 이야기로 분할하지 않는다.

## 증거 색인

- 시장 운영 비교: `.planning/2026-09-21-production-benchmark/management-benchmark-graph.json`
- 조작·화면·애니메이션 자료: `.planning/2026-09-21-production-benchmark/controls-visual-graph.json`
- OSS 원본/리비전/재사용 검토: `.planning/2026-09-21-production-benchmark/oss-reuse-graph.json`
- 코드 이식과 고지: `.planning/2026-09-21-production-benchmark/oss-turnaround-implementation-graph.json`
- 건물 선택 UI 실행: `.planning/2026-09-21-production-benchmark/context-ui-native-proof.json`
- 내부 계산·보충 상태 실제 실행: `.planning/2026-09-21-production-benchmark/turnaround-native-proof.json`
- 자체평가 세부: `.planning/2026-09-21-production-benchmark/SELF_EVAL_KO.json`
- 과거 실패·후속 교정을 포함한 이전 기록: `.planning/2026-09-21-fleet-building/`

마지막 표시 문구 교정에서 사용자 알림의 내부 GUID를 제거하고 `Replenishing`의 표시를 `재출동 준비 중`으로 정리했다. 시간 계산·상태 ID·그래프 연결은 동일하다. 해당 소규모 표시 변경은 핵심 동작 검증 뒤 컴파일/바인딩으로 확인한다.
