# LLM 작업 문맥 레지스트리

현재 지시: [PM 오케스트레이션 계약](../orchestration-contract.md). 이 레지스트리는 계획·지시·실제 실행·증거를 구별합니다.

- `NNN.json`: 해당 이슈의 실행 지시와 최소 문맥
- `ontology.jsonld`: 프로젝트 온톨로지와 관계 방향
- `index.jsonld`: Goal/WorkItem/Phase/Artifact/Source/Resource/Role 등록
- 상세 입력·쓰기·검증은 `task_context.mjs --section`으로 해당 이슈/단계만 읽습니다.

| 이슈 | PM 작업 지시 | 문맥 |
|---|---|---|
| [#10](https://github.com/xrlab-dau/CHOOGuard/issues/10) | [FMP-01] Foundation 목표·세계 계약·근거 추적 기준선 확정 | [packet](010.json) |
| [#13](https://github.com/xrlab-dau/CHOOGuard/issues/13) | [M0-00] AI 작업 권한·안전 기준선 정리 | [packet](013.json) |
| [#14](https://github.com/xrlab-dau/CHOOGuard/issues/14) | [M0-02a] 공개 자료 출처 등급·주장 범위 규약 | [packet](014.json) |
| [#15](https://github.com/xrlab-dau/CHOOGuard/issues/15) | [M0-02b] 공개 자료 사용 조건·비공개 경계 확정 | [packet](015.json) |
| [#16](https://github.com/xrlab-dau/CHOOGuard/issues/16) | [M0-03] AI 도구·검토·승인 허용목록 | [packet](016.json) |
| [#17](https://github.com/xrlab-dau/CHOOGuard/issues/17) | [M1-01] LLM 세션 입력·Writer Lease·인계 규칙 구현 | [packet](017.json) |
| [#18](https://github.com/xrlab-dau/CHOOGuard/issues/18) | [M1-02] 파일·네트워크·도구 권한 경계 검증 | [packet](018.json) |
| [#19](https://github.com/xrlab-dau/CHOOGuard/issues/19) | [M1-03] 공식 Unity CLI·Pipeline 공급망 검토 | [packet](019.json) |
| [#20](https://github.com/xrlab-dau/CHOOGuard/issues/20) | [M1-04] 공식 Unity Editor MCP 연결·허용 작업 검증 | [packet](020.json) |
| [#21](https://github.com/xrlab-dau/CHOOGuard/issues/21) | [M1-05] 실행 기록·정제·공개 manifest | [packet](021.json) |
| [#22](https://github.com/xrlab-dau/CHOOGuard/issues/22) | [M1-06] Agent Team 세션·Writer Lease 검증 | [packet](022.json) |
| [#23](https://github.com/xrlab-dau/CHOOGuard/issues/23) | [M1-07] 외부 검증기·독립 리뷰 경로 | [packet](023.json) |
| [#24](https://github.com/xrlab-dau/CHOOGuard/issues/24) | [M2-01] Unity·네이티브 의존성 재현 기준선 | [packet](024.json) |
| [#25](https://github.com/xrlab-dau/CHOOGuard/issues/25) | [M2-02] TrainingAction 입력·거부·중복 계약 고정 | [packet](025.json) |
| [#26](https://github.com/xrlab-dau/CHOOGuard/issues/26) | [M2-03] Quest 상태·설명형 피드백 역할 연동 회귀 | [packet](026.json) |
| [#27](https://github.com/xrlab-dau/CHOOGuard/issues/27) | [FMP-02] 서버 권위·두 클라이언트 공유 행동 검증 | [packet](027.json) |
| [#28](https://github.com/xrlab-dau/CHOOGuard/issues/28) | [M3-01] 공개 자료 취득·검증 체크리스트 | [packet](028.json) |
| [#29](https://github.com/xrlab-dau/CHOOGuard/issues/29) | [M3-02] 공개 영상·사진 자료 취득·정합 | [packet](029.json) |
| [#31](https://github.com/xrlab-dau/CHOOGuard/issues/31) | [M3-04] 공개 자료 기반 시설 플레이 맵 경계 | [packet](031.json) |
| [#32](https://github.com/xrlab-dau/CHOOGuard/issues/32) | [M4-01] 임시 5직무·판정 fixture 정의 | [packet](032.json) |
| [#33](https://github.com/xrlab-dau/CHOOGuard/issues/33) | [M4-02] 대표 직무 Desktop 전체 흐름 | [packet](033.json) |
| [#34](https://github.com/xrlab-dau/CHOOGuard/issues/34) | [M4-03] 나머지 네 직무 핵심 행동 | [packet](034.json) |
| [#35](https://github.com/xrlab-dau/CHOOGuard/issues/35) | [FMP-10] 다직무 협업·보고·수신확인·인계·교관 운영 | [packet](035.json) |
| [#36](https://github.com/xrlab-dau/CHOOGuard/issues/36) | [M5-01] 자동 테스트 세트·실행 manifest | [packet](036.json) |
| [#37](https://github.com/xrlab-dau/CHOOGuard/issues/37) | [M5-02] 실제 HMD·성능 검증 | [packet](037.json) |
| [#38](https://github.com/xrlab-dau/CHOOGuard/issues/38) | [M5-03] 독립 에이전트 검토 | [packet](038.json) |
| [#39](https://github.com/xrlab-dau/CHOOGuard/issues/39) | [M5-04] 시연 증거·공개 후보 manifest | [packet](039.json) |
| [#40](https://github.com/xrlab-dau/CHOOGuard/issues/40) | [M5-05] 최종 빌드·발표 PM 승인 | [packet](040.json) |
| [#41](https://github.com/xrlab-dau/CHOOGuard/issues/41) | [R-01] 문서 선행관계·검토 지적 정합성 재검토 | [packet](041.json) |
| [#42](https://github.com/xrlab-dau/CHOOGuard/issues/42) | [R-02] Windows·HMD 시험의 대상·필수 기능 결정 | [packet](042.json) |
| [#43](https://github.com/xrlab-dau/CHOOGuard/issues/43) | [R-03] 보호 경로·GitHub 거버넌스 변경안 검토 | [packet](043.json) |
| [#44](https://github.com/xrlab-dau/CHOOGuard/issues/44) | [R-04] 부트스트랩 정상·실패·경로 보호 회귀 검증 | [packet](044.json) |
| [#45](https://github.com/xrlab-dau/CHOOGuard/issues/45) | [R-05] 리뷰 실패 중단·라운드·명령 통제 | [packet](045.json) |
| [#46](https://github.com/xrlab-dau/CHOOGuard/issues/46) | [R-06] 조사 하네스 입력·외부 전송·출력 통제 | [packet](046.json) |
| [#52](https://github.com/xrlab-dau/CHOOGuard/issues/52) | [R-07] 검증기 정책 기준선·증거 결속 보강 | [packet](052.json) |
| [#54](https://github.com/xrlab-dau/CHOOGuard/issues/54) | [FMP-00] Foundation 13구역·20클라이언트·NPC100·동시사건2 수용 집계 | [packet](054.json) |
| [#55](https://github.com/xrlab-dau/CHOOGuard/issues/55) | [FND-01] 팀이 재현할 Foundation 기준 SHA·계약·시작 경로 | [packet](055.json) |
| [#56](https://github.com/xrlab-dau/CHOOGuard/issues/56) | [FND-02] 33종 자산의 플레이 가능한 임시 역사 시제품 | [packet](056.json) |
| [#57](https://github.com/xrlab-dau/CHOOGuard/issues/57) | [FND-03] Desktop 입력·가림·복귀와 XR 의미 계약 | [packet](057.json) |
| [#58](https://github.com/xrlab-dau/CHOOGuard/issues/58) | [FND-04] 합성 metadata의 자산 교체·출처 계약 | [packet](058.json) |
| [#59](https://github.com/xrlab-dau/CHOOGuard/issues/59) | [FMP-07] NPC100 보행·밀집·병목·접촉 수용 집계 | [packet](059.json) |
| [#60](https://github.com/xrlab-dau/CHOOGuard/issues/60) | [FMP-09] 동시2사건·불시발견·대응·복구 수용 집계 | [packet](060.json) |
| [#62](https://github.com/xrlab-dau/CHOOGuard/issues/62) | [VAL-01] 공개 절차 자료의 직무·사건 매핑 | [packet](062.json) |
| [#63](https://github.com/xrlab-dau/CHOOGuard/issues/63) | [VAL-02] 공개 자료 기반 미학습 사건 평가 설계 | [packet](063.json) |
| [#64](https://github.com/xrlab-dau/CHOOGuard/issues/64) | [FOUND-CTX] 이슈별 최소 문맥·실행 그래프 구현 | [packet](064.json) |
| [#65](https://github.com/xrlab-dau/CHOOGuard/issues/65) | [FND-ART-REF] 객체 참조·컴포넌트·FBX 검수 결속 | [packet](065.json) |
| [#67](https://github.com/xrlab-dau/CHOOGuard/issues/67) | [FND-WORLD] 13구역 월드 제작·연결 수용 집계 | [packet](067.json) |
| [#68](https://github.com/xrlab-dau/CHOOGuard/issues/68) | [PLAN-TEAM] PM 실행·병렬·통합 계획 허브 | [packet](068.json) |
| [#70](https://github.com/xrlab-dau/CHOOGuard/issues/70) | [TEAM-02] 13구역·번들·portal 경계 명세 인계 | [packet](070.json) |
| [#71](https://github.com/xrlab-dau/CHOOGuard/issues/71) | [TEAM-03] 소재·사용처·권리·파생본 명세 | [packet](071.json) |
| [#72](https://github.com/xrlab-dau/CHOOGuard/issues/72) | [TEAM-04] 운영·사건·직무 사용자 흐름 수용 fixture | [packet](072.json) |
| [#73](https://github.com/xrlab-dau/CHOOGuard/issues/73) | [TEAM-05] 소재 처리 job의 재현·재개·중복 실행 검증 | [packet](073.json) |
| [#74](https://github.com/xrlab-dau/CHOOGuard/issues/74) | [TEAM-06] 한 모듈 Blender→Unity round-trip 계약 | [packet](074.json) |
| [#75](https://github.com/xrlab-dau/CHOOGuard/issues/75) | [TEAM-07] 소재 취득·권리 확인·preview/runtime 파생본 생성 | [packet](075.json) |
| [#76](https://github.com/xrlab-dau/CHOOGuard/issues/76) | [TEAM-08] 역사 구조·가구 모듈 제작 | [packet](076.json) |
| [#77](https://github.com/xrlab-dau/CHOOGuard/issues/77) | [TEAM-09] 설비·익명 NPC 외형 모듈 제작 | [packet](077.json) |
| [#78](https://github.com/xrlab-dau/CHOOGuard/issues/78) | [TEAM-10] 열차·선로·승강장 모듈 제작 | [packet](078.json) |
| [#79](https://github.com/xrlab-dau/CHOOGuard/issues/79) | [TEAM-11] 역사·광장 번들 플레이 조립 | [packet](079.json) |
| [#80](https://github.com/xrlab-dau/CHOOGuard/issues/80) | [TEAM-12] 열차·철도 승강장 번들 플레이 조립 | [packet](080.json) |
| [#81](https://github.com/xrlab-dau/CHOOGuard/issues/81) | [TEAM-13] 환승·지하철 번들 플레이 조립 | [packet](081.json) |
| [#82](https://github.com/xrlab-dau/CHOOGuard/issues/82) | [FMP-05] 13구역 이동·로딩·공유 상태와 제작 결과 통합 | [packet](082.json) |
| [#85](https://github.com/xrlab-dau/CHOOGuard/issues/85) | [TEAM-17] 기존 33종 4K 후보·동일 조건 검수 렌더 | [packet](085.json) |
| [#88](https://github.com/xrlab-dau/CHOOGuard/issues/88) | [FMP-11] 다층 네비게이터·연습/평가·요청형 근거 안내 | [packet](088.json) |
| [#89](https://github.com/xrlab-dau/CHOOGuard/issues/89) | [TEAM-21] 선택 XR 입력·월드 UI 어댑터 | [packet](089.json) |
| [#90](https://github.com/xrlab-dau/CHOOGuard/issues/90) | [TEAM-22] 기존 Foundation Windows 재현·자원 측정 | [packet](090.json) |
| [#91](https://github.com/xrlab-dau/CHOOGuard/issues/91) | [FMP-12] 20/100/2 통합·60분 부하·인터넷·음성 실측 | [packet](091.json) |
| [#92](https://github.com/xrlab-dau/CHOOGuard/issues/92) | [FMP-13] 클라이언트·전용서버·음성 실행본 및 독립 재현 전달 | [packet](092.json) |
| [#93](https://github.com/xrlab-dau/CHOOGuard/issues/93) | [TEAM-25] 공개 절차→직무·사건 매핑 양식 | [packet](093.json) |
| [#94](https://github.com/xrlab-dau/CHOOGuard/issues/94) | [TEAM-26] 평가 질문·익명 기록 양식 | [packet](094.json) |
| [#99](https://github.com/xrlab-dau/CHOOGuard/issues/99) | [FMP-03] 참가자 재접속·세계 체크포인트·순서 기록 복구 | [packet](099.json) |
| [#100](https://github.com/xrlab-dau/CHOOGuard/issues/100) | [FMP-04] 자체호스팅 PTT·팀/지휘 채널 권한 검증 | [packet](100.json) |
| [#101](https://github.com/xrlab-dau/CHOOGuard/issues/101) | [FMP-06] 구획·복도·차량 열·연기 모델 수용 집계 | [packet](101.json) |
| [#102](https://github.com/xrlab-dau/CHOOGuard/issues/102) | [FMP-08] 열차운행·제동·점유·이동 좌표계 수용 집계 | [packet](102.json) |
| [#106](https://github.com/xrlab-dau/CHOOGuard/issues/106) | [FMP-14] 핵심 자산 제작 경로 권위 정합 및 정제 | [packet](106.json) |
| [#107](https://github.com/xrlab-dau/CHOOGuard/issues/107) | [FMP-15] 자산 품질 비교·결함 환류·재작업 | [packet](107.json) |
| [#119](https://github.com/xrlab-dau/CHOOGuard/issues/119) | [FND-05] 공개 층별 평면도→구역 모델링 파일럿 | [packet](119.json) |
| [#120](https://github.com/xrlab-dau/CHOOGuard/issues/120) | [R-08] 팀이 접근할 수 있는 Native 소스 기준선 게시 | [packet](120.json) |
| [#121](https://github.com/xrlab-dau/CHOOGuard/issues/121) | [R-09] 공개 자료 전제의 전수 정합·재발 검사 | [packet](121.json) |
| [#122](https://github.com/xrlab-dau/CHOOGuard/issues/122) | [FMP-06a] 147셀·275개구부 열·연기 비용 최적화와 정확도 보존 | [packet](122.json) |
| [#123](https://github.com/xrlab-dau/CHOOGuard/issues/123) | [FMP-06b] 문·환기·경사 구획의 열·연기 경계 회귀 | [packet](123.json) |
| [#125](https://github.com/xrlab-dau/CHOOGuard/issues/125) | [FMP-07a] 13구역 실제 기하 기반 NPC100 군중 fixture | [packet](125.json) |
| [#126](https://github.com/xrlab-dau/CHOOGuard/issues/126) | [FMP-07b] NPC100 밀도·유량·대향·병목 공개비교 계측 | [packet](126.json) |
| [#127](https://github.com/xrlab-dau/CHOOGuard/issues/127) | [FMP-07c] 접촉·밀집·정지·군중 복원 불변 회귀 | [packet](127.json) |
| [#128](https://github.com/xrlab-dau/CHOOGuard/issues/128) | [FMP-08a] 선로 정렬·이동 차량 프레임·승하차 연속성 | [packet](128.json) |
| [#129](https://github.com/xrlab-dau/CHOOGuard/issues/129) | [FMP-08b] 해석 정지거리·제동 지연·편성 점유 검증 | [packet](129.json) |
| [#130](https://github.com/xrlab-dau/CHOOGuard/issues/130) | [FMP-08c] 차내 화재·고장·비상정차와 동일 프레임 물리 연동 | [packet](130.json) |
| [#131](https://github.com/xrlab-dau/CHOOGuard/issues/131) | [FMP-09a] 사건 데이터 스키마·검토 변형·공개근거 목록 | [packet](131.json) |
| [#132](https://github.com/xrlab-dau/CHOOGuard/issues/132) | [FMP-09b] 동시2사건의 공유자원·독립 복구 통합 시험 | [packet](132.json) |
| [#133](https://github.com/xrlab-dau/CHOOGuard/issues/133) | [FMP-09c] 검토 변형 seed 결정성과 응답·상태·UI 비노출 | [packet](133.json) |
| [#134](https://github.com/xrlab-dau/CHOOGuard/issues/134) | [FMP-10a] 보고·수신확인·인계 클라이언트 화면 | [packet](134.json) |
| [#135](https://github.com/xrlab-dau/CHOOGuard/issues/135) | [FMP-10b] NPC 인솔 경합과 명시적 인계 시험 | [packet](135.json) |
| [#136](https://github.com/xrlab-dau/CHOOGuard/issues/136) | [FMP-10c] 교관 제어 화면과 권한 초과 시험 | [packet](136.json) |
| [#137](https://github.com/xrlab-dau/CHOOGuard/issues/137) | [FMP-11a] 다층 경로 안내 클라이언트 | [packet](137.json) |
| [#138](https://github.com/xrlab-dau/CHOOGuard/issues/138) | [FMP-11b] 연습/평가 모드 분리 | [packet](138.json) |
| [#139](https://github.com/xrlab-dau/CHOOGuard/issues/139) | [FMP-11c] 관측 권한과 경로 안내 일관성 시험 | [packet](139.json) |
| [#140](https://github.com/xrlab-dau/CHOOGuard/issues/140) | [FMP-12a] 2~4 클라이언트 축소 리허설 | [packet](140.json) |
| [#141](https://github.com/xrlab-dau/CHOOGuard/issues/141) | [FMP-12b] 20클라이언트·60분 부하 실행과 서버 20Hz 예산 판정 | [packet](141.json) |
| [#142](https://github.com/xrlab-dau/CHOOGuard/issues/142) | [FMP-12c] RTT 100ms·손실 1% 열화 시험 | [packet](142.json) |
| [#143](https://github.com/xrlab-dau/CHOOGuard/issues/143) | [FMP-13a] Windows/Linux 전용서버·클라이언트 후보 build 스크립트 | [packet](143.json) |
| [#144](https://github.com/xrlab-dau/CHOOGuard/issues/144) | [FMP-13b] 독립 개발자 재현 전달 | [packet](144.json) |
| [#146](https://github.com/xrlab-dau/CHOOGuard/issues/146) | [BRAND-01] 제공 로고의 README·조직 프로필 적용 | [packet](146.json) |
