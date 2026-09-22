# 유사 게임 "종료 정산/디브리프 화면" 조사 (5문 x 6종)

## PowerWash Simulator
1. gameuidatabase 사이트에 개별 스크린 목록 미확인(검색으로 직접 못 찾음) — `unverified`
2. `unverified`
3. `unverified`
4. `unverified`
5. `unverified`
근거: gameuidatabase.com game id=1313, "Results Screen" 카테고리(scrn=53) 일반적으로 순위/시간/점수 표시하나 이 타이틀 전용 항목은 확인 못함. https://gameuidatabase.com/index.php?scrn=53

## Viscera Cleanup Detail
1. 전통적 팝업 정산화면 없음. 사무실(The Office)에 있는 대형 스크린 "Report Screen"(제목 "Work-Notice and Conduct Report; WNCR2")이 완료된 사고보고서 목록을 표시. 신문 스크랩·상사/검사관 쪽지·Employee of the Month 명패로 피드백이 세계 안에 분산됨.
2. 완벽 수행 시 전용 축하 화면 없음 — 대신 Employee of the Month 명패 + 특별 기사가 세계 안에 생성됨. `unverified`(정확한 트리거 조건)
3. Report Screen 항목은 시간순(완료된 사고보고 나열)으로 보이며, 심각도별 정렬 근거는 확인 안 됨. `unverified`
4. 마우스로 Hands 툴(기본 "1") 좌클릭, 항목별 "X"로 개별 삭제 — 자동 닫힘 없음, 키 입력이 아니라 클릭으로 조작.
5. 부분 화면 — 사무실 안 물리적 모니터이며 배경 월드(사무실 내부)가 항상 보임.
출처: https://viscera-cleanup-detail.fandom.com/wiki/Report_Screen , https://viscera-cleanup-detail.fandom.com/wiki/Ending_Messages , https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/VisceraCleanupDetail

## Hardspace: Shipbreaker
1. Shift Summary 화면: 총 매각액(입금액), 부위별 매각 내역(수량/질량) 전체 breakdown, 파손된 매각물 수량/질량.
2. `unverified`(완벽 수행 전용 화면 확인 못함 — 파손 0건이면 해당 항목이 생략/0으로 표기될 가능성이 높으나 스크린샷 미확인)
3. 카테고리별(부위 종류) 나열로 추정되나 정렬 기준(심각도/발생순) 명시 자료 없음 — `unverified`
4. 우하단 "Continue" 버튼 클릭으로 닫힘(자동/키 아님). Steam 버그 리포트 다수가 이 버튼이 멈춰 Alt+F4가 필요했다고 보고 — 명시적 버튼 클로징이라는 근거.
5. `inferred`(버튼이 멈추면 게임이 진행 불가하다는 보고로 보아 풀스크린/블로킹으로 추정, 배경 월드 가시 여부는 미확인) — `unverified`
출처: https://hardspaceshipbreaker.fandom.com/wiki/Shifts , Steam 커뮤니티 버그 스레드(검색 결과, URL 미특정)

## My Summer Car
1. 전통적 "디브리프 화면" 자체가 없음. 대신 검사소(Lindell inspection shop) 카운터에 놓이는 물리적 "영수증"(katsastustodistus) 오브젝트 — 문제 항목마다 체크박스에 X 표기, 상단에 HYLÄTTY(불합격)/HYVÄKSYTTY(합격) 도장.
2. 완벽 수행 시 모든 체크박스 비어있고 HYVÄKSYTTY 도장 + 번호판 지급 — 별도 화면 전환 없이 같은 영수증 오브젝트 형식 유지.
3. 항목은 체크리스트 순서(브레이크/서스펜션/차체/전조등/타이어/배기가스 등 카테고리별) 고정 나열로, 각 X 항목 옆에 사유 텍스트 필드가 동반됨(예: 브레이크 오일 누출) — 발생순/심각도 정렬 아님, 카테고리 고정순.
4. 플레이어가 직접 영수증을 집어 확인 후 인벤토리에 넣거나 놓는 방식 — 자동/버튼 클로징 개념 자체가 없는 다이어제틱 오브젝트. `unverified`(정확한 조작 입력)
5. 풀스크린 아님 — 월드 내 물리 오브젝트를 들고 보는 것이라 배경 전체가 항상 보임.
출처: https://my-summer-car.fandom.com/wiki/Inspection_receipt , https://my-summer-car.fandom.com/wiki/Lindell_inspection_shop

## Lethal Company
1. 전통적 종료-런 모달 없음. 배 안 상시 모니터(녹색 텍스트 화면)가 할당량(quota) 진행률과 남은 일수를 계속 표시. 고철 판매는 화면이 아니라 물리적 상호작용(카운터에 놓고 벨 누름 → 문 열림 → 크리처가 획득 금액 표시)으로 이뤄짐.
2. 해당 없음/`unverified` — "완벽 수행" 개념이 정산 화면과 분리돼 있어 대응하는 화면 상태 없음.
3. 해당 없음 — 나열형 위반 리스트 자체가 존재하지 않음(할당량 미달 시 게임오버로 직결).
4. 상시 HUD라 닫는 개념 없음.
5. 부분 화면(선내 모니터), 배경 월드 항상 보임.
초과 보너스 공식: `overtimeBonus = (quotaFulfilled - profitQuota) / 5 + 15 * daysUntilDeadline` (0 미만 시 0으로 캡).
출처: https://lethal.miraheze.org/wiki/Ship , https://lethal.fandom.com/wiki/Profit_Quota , https://lethal.miraheze.org/wiki/Quota

## Train Sim World (서비스 종료 점수 요약)
1. Action Points 기반 디브리프 화면: 획득 액션포인트 breakdown, 노선 속도제한 대비 퍼포먼스 그래프, 정시성/정차 정확도 breakdown, 최종 브론즈/실버/골드 메달.
2. `unverified`(만점/무위반 전용 표시 형태 확인 못함 — 골드 메달 + 감점 0건으로 추정되나 스크린 문구 미확인)
3. 항목은 카테고리별(속도/정시성/정차정확도) breakdown이며 심각도 기반 개별 위반 나열은 아님. TSW4 이후 버전은 개별 감점 사유(예: "driving over speed: 15 AP")가 주석처럼 붙는 사례 보고됨 — 각 항목에 근거 텍스트 동반. `unverified`(정확한 정렬 순서)
4. `unverified`(버튼/자동 여부 확인 못함)
5. `unverified`(풀스크린 여부·배경 가시성 확인 못함) — 단, Ctrl+6 은 진행 중 HUD 점수 표시만 끄고 최종 디브리프와는 별개.
출처: https://live.dovetailgames.com/live/train-sim-world/articles/article/tsw-20-detail , https://forums.dovetailgames.com/threads/scoring-in-tsw4.73572/ , https://steamcommunity.com/app/530070/discussions/0/3223871682629759587/

## 요약 시사점
- 확실한 "종료 후 전용 정산 화면" 패턴: Hardspace(버튼 클로징, 부분 정보 breakdown), Train Sim World(메달+breakdown).
- 세계 안에 분산된 디제틱 피드백 패턴(전용 모달 없음): Viscera Cleanup Detail(사무실 스크린+환경 오브젝트), My Summer Car(물리 영수증), Lethal Company(상시 모니터+물리 상호작용).
- 6종 중 PowerWash Simulator만 자료 부족으로 5문 전부 `unverified`.
- 영상 미시청 — 스크린샷/텍스트 위키·포럼 근거만 사용, 화면 배치·정렬 관련 세부는 대부분 `unverified`/`inferred` 표기.
