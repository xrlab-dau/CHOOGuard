# Frontier 공식 가이드 벤치마크와 CHOOGuard 이식

2026-09-21 · CS-EXEC.01.01 · Unity 유지 · 메인 판단 / Jev 보조 / Astra low 구현

핵심 포지셔닝은 실제 도시의 기관을 운영 거점으로 삼는 비상대응 게임이다. **공식 자료로 확인한 기관 위치 → 게임 내 업무·지원 요청 → 해당 거점의 인력·차량 출동 → 도로 이동 → 현장 작업·인계 → 복귀·재출동 준비**가 중심이다. 기존 역사 내부의 소규모 팀 조작은 이 흐름의 마지막 구간이다. 시민 표현은 이동·대기·작업 상태를 읽는 간단한 수준으로 두고 추가 동작 제작보다 운영 연결을 우선한다.

사용자 요구는 방향과 우선순위다. 가이드의 관찰된 행동, 실제 기관·도로 자료, 코드와 실행 결과가 각각 게임 메커니즘·공간·구현의 증거가 된다. 이 세 가지를 혼동하지 않는다. Jev 판정도 최종 승인이나 실측 근거가 아니다.

## 조사 범위

현재 공식 플레이어 가이드 허브의 15개 가이드와 52개 How-to 링크를 목록화했다. 14개 가이드의 본문, 입문 영상 페이지의 노출 텍스트, 도움말/FAQ, 조작표와 Toolbox 글 2개를 조사했다. 입문 영상 전체와 자막은 미검토이므로 영상 내용까지 모두 분석했다고 표시하지 않는다. 사이트맵에서 빠진 Dinosaurs/Rebirth도 현재 허브 링크로 보완했다.

공식 FAQ 일부에는 출시·DLC 초기 문구가 남아 있지만 현재 가이드는 확장팩을 다룬다. 최신 기능은 개별 현행 가이드와 버전으로 확인했다. Frontier는 [자체 Cobra 개발 도구와 외부 도구](https://www.frontier.co.uk/games)를 함께 사용한다고 설명한다. 제작사 전체가 Unreal이라는 전제는 사용하지 않는다.

## 기능별 정밀 비교

| 공식 자료 | 확인한 작동 원리 | CHOOGuard 적용 및 현재 상태 |
|---|---|---|
| [Quickstart](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/quickstart-guide) | 캠페인에서 점진적으로 도구를 익히며 프리셋과 세부 건설 중 선택 | 기관 선택→지원 요청→출동 확인→현장 인계의 첫 흐름부터 안내. 직원 매뉴얼 튜토리얼은 별도 후속 범위 |
| [Welcome](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/welcome-to-jurassic-world-evolution-3) | 모드·도구를 소개하는 영상 페이지 | 페이지 개요만 확인. 영상 세부 조작은 근거로 사용하지 않음 |
| [Construction](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/construction-guide) | 건물의 연결 경로, 경로 폭/용량, 그리드·그룹·축·스냅, 필수 모듈, 청사진, 자원까지 되돌리는 Undo/Redo | 미터 모듈·footprint·연결 소켓·통로 여유를 가진 배치로 이식. 현재 실내 키트는 편집기 생성형이며 런타임 건설은 미구현. 실제 역사 복원과 임시 대응 배치를 구분 |
| [Operations](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/operations-guide) | 응답/의료/정비 거점, 팀 배정, 보급, 범위별 자동 업무와 직접 지시 | 실제 119 거점에서 출동하는 기반을 구현 중. 시설 상태·서비스 범위·재고·자동 배정은 다음 연결. 임의 전력·의료 수치를 철도 절차로 옮기지 않음 |
| [Vehicles](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/vehicles-guide) | 차량 직접 조작/지시, 감지 범위에 배정된 팀의 자동 반응, 손상 차량 기지 복귀 | 도시 도로 출동/복귀와 현장 팀 가용성을 연결. 직접 운전이나 전투 도구를 먼저 만들지 않음 |
| [Calamities](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/calamities-and-sabotage-guide) | 위험 근처 대피시설, 감지/출동, 고장 후 수리·재부팅, 영향 지속 | 사건 종료가 세계 초기화로 이어지지 않는 복구 운영. 현재 피난 뒤 운영 지속은 native 확인; 설비 수리·제한 재개는 미구현 |
| [Park Management](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/park-management-guide) | 운영 상태를 분해해 조회하고 시설 업그레이드/연구 선행조건을 관리 | 평점 대신 기관 가용성·현장 수요·접근 경로·처리 중 작업·남은 영향 표시 |
| [Scientists](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/scientists-guide) | 자격/능력·가용 인력·작업 점유·휴식에 따른 선택 제약 | 팀 작업량·동시 수행 한계·교대/보급 연결 후보. 실제 인력 자격·피로 계수는 별도 근거 필요 |
| [Finances](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/finances-guide) | 반복 유지비와 일회성 비용, 제한된 계약과 선택의 결과 | 출동 가용 자원과 보급을 먼저 다룸. 테마파크 재정·수익·해고 시스템을 중심 게임으로 옮기지 않음 |
| [Guests](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/guests-guide) | 방문 수요·편의·접근·혼잡·교통 연결을 구분 | 시민 수요, 연결된 이용 동선과 지원 필요를 별도 상태로 설계. 방문자 수를 점수 장식으로만 쓰지 않음 |
| [Attractions](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/attractions-guide) | 시설의 가시 범위와 수요 집중, 종류별 연결 조건, 순환 경로 | 감지·대피·대응 범위의 빈곳을 관리 뷰로 확인하는 방식. 관광 흥미 수치를 안전 지표로 쓰지 않음 |
| [Enclosures](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/enclosures-guide) | 경계, 팀 접근 게이트, 소켓 확장, 감시 범위와 배정 조건 | 출입·연결·서비스 접근성을 구분. 시각적 문/벽 변경만으로 계산 기하가 바뀌었다고 처리하지 않음 |
| [Dinosaurs](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/dinosaurs-guide) | 영속 개체·조건별 상태, 필요한 원인 진단, 선행 작업이 있는 복합 지시/수송 | 인물 ID·누적 상태·단계별 작업 원리만 활용. 생물학·유전·공격성·치료 규칙은 인간에게 전용하지 않음 |
| [Tips & Tricks](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/tips-and-tricks) | 직원 위임, 감시·보안 대비, 원인 수리 뒤 복귀 | 수동 지시와 자동 담당 구역의 역할 분리, 복귀 전 미완료 작업 가시화 |
| [Rebirth](https://www.jurassicworldevolution.com/en-US/3/help/player-guides/rebirth-expansion-guide) | 고정 공급점·연결망·분배 용량, 단계별 필수/선택 과제, 도구 유효 범위 | 고정된 실제 기관 위에서 연결·용량·선행조건을 갖는 운영을 설계. DLC 전용 장비·기술 자체는 복제 대상 아님 |
| [Controls](https://www.jurassicworldevolution.com/en-US/3/help/controls-keyboard-shortcuts) | 모드별 입력과 재매핑, Tab 알림 초점, Space 정지, 차량 순환, 건설 스냅/회전 | 일시정지 중 지원 요청을 계획하고 시간 재개 후 출동. 알림 초점은 발주와 분리. 현재 팀 선택/속도 키 충돌을 피하면서 문맥 조작을 정리 |
| [Help/FAQ](https://www.jurassicworldevolution.com/en-US/3/help) | 시간 압박 없는 입력, 색 대안·카메라 편의, 입력 장치 선택, 저장 접근성 | 정지 중 판단·한국어 설명·상태와 색의 병행 표현. 중간 저장 등 미구현 기능은 지원한다고 표시하지 않음 |
| [Park Toolbox](https://www.jurassicworldevolution.com/en-US/3/news/park-managers-toolbox-park-build-ideas) | 구역 분리·지형 경계·현장 시점 점검·청사진 기반 빠른 구축 | 공공기관/역사/도시 연결과 재사용 모듈 제작 방식 참고. 환경의 모양만으로 이동 가능성을 판정하지 않음 |
| [Attraction Toolbox](https://www.jurassicworldevolution.com/en-US/3/news/park-managers-toolbox-attraction-build-ideas) | 경계·높이·지지 구조를 고려하는 모듈 조합 | 실제 자료가 확인된 외피·수직 동선·지지 구조에 모듈을 맞춤 |

## 실제 이식 우선순위

1. **기관 출동 왕복:** 확인된 기관과 도로를 이용해 요청·출동·도착·현장 가용·복귀를 연결한다. 현재 중앙119 소방/의료 훈련 단위를 통합했고, 소방차의 실제 Unity 왕복·현장 작업·귀환을 확인했다. 실제 보유 대수·실시간 가용성으로 표시하지 않는다.
2. **현장 접근:** 설치한 DotRecast로 실제 현재 2층 메시의 장애물을 피하는 경로를 사용한다. 이미 native 경로 질의와 다른 층 거절을 확인했으며, 도시 차량 인계 뒤 실제 현장 작업 ACK와 별도 50명 대피·복구도 확인했다.
3. **운영 선택:** 실제 기관/시설의 역할과 확인된 조건 위에 고장·요청·재고·작업 점유·담당 범위·수동 개입을 연결한다.
4. **공간 복원과 배치:** 도면/사진/공개 3D 자료가 확인되는대로 현 추정 구조를 교체한다. 건물의 정확한 복원과 사용자의 임시 시설 배치 기능은 별개로 다룬다.

Jev040은 실내 경로 기반을 먼저 권했으나 신뢰도0.67의 검토 판정이었다. 이후 사용자의 도시 기관 중심 의도를 재확인하고 실제 누락된 기관 상태와 공식 좌표를 확인해 메인이 우선순위를 기관 출동으로 바로잡았다. Jev041도 이 후보를 지지했다. 설치된 엔진이나 가이드의 유명세만으로 정합성·완료를 판정하지 않는다.

근거와 상세 입력: `.planning/2026-09-21-official-guide-benchmark/{coverage,root-guide-graph,build-guides-graph,operations-guides-graph,agency-source-graph,oss-candidates-graph}.json`. 원문 스냅샷과 해시는 `asset-library/research-public/2026-09-21/jwe-official/`에 보존한다. 현재 전체 제품은 IN_PROGRESS / NOT_ACCEPTED다.
