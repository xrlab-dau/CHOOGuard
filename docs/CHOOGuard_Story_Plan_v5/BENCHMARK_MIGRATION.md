# 프로덕션 벤치마크와 실제 이식

2026-09-21 · CS-EXEC.01.01 · 전체 제품 IN_PROGRESS / NOT_ACCEPTED

메인은 레퍼런스의 공개 자료·공개 구현과 현재 파일·실행 결과를 대조해 이식 대상을 선택한다. Jev는 후보 비교·중복·보고 범위를 짧게 분류하며, 구현은 Astra low가 맡는다. 제품 코드를 복사했다고 주장하지 않는다. 아래 게임 메커니즘은 현재 프로젝트의 계약과 C#·Python에 맞춰 적용한 것이다.

| 대상과 근거 | 선택한 원리 | 실제 연결 | 현재 증거/남은 범위 |
|---|---|---|---|
| [JWE3 디렉터 인터뷰](https://store.epicgames.com/en-US/news/jurassic-world-evolution-3-interview-baby-dinosaurs-terraforming-atvs-more): 직접 ATV 점검·수리, 거점 범위 대응, 카메라 경보 | 선택한 팀에 지시하고 이동·작업·결과를 현장에서 관찰 | `MvpWorkspace`, `MvpTeamTaskController`, `MvpTrainingDirector`: 3개 주 행동, 전체 명령 접기, 작업 큐, 도착·자기 명령 ACK 알림, 알림으로 현장 초점 | 실제 워커 50명 실행과 알림 비발주 확인. 차량 직접 운전·자동 대응 거점은 미구현 |
| [OpenTTD 공개 구현](https://github.com/OpenTTD/OpenTTD/blob/master/src/network/network_command.cpp), [OpenRCT2 공개 구현](https://github.com/OpenRCT2/OpenRCT2/blob/develop/src/openrct2/GameState.cpp) | 지속되는 개체와 순서가 있는 시뮬레이션 상태 | 승인된 시뮬레이션 시간으로 팀 이동/준비, 요청ID·revision·ACK, 평시→사건 동일 시민, 결과 이후 복구 운영 | 원자 전환/일시정지/복구 지속 native PASS. 완전한 tick ledger 재생과 시설 수리·제한 재개는 후속 |
| [NIST FDS/Smokeview 문서](https://pages.nist.gov/fds/manuals.html)와 원본 `.sf` 출력 | 단위·좌표·시각·출처를 보존하는 물리장 시각화 | 원본 61×41 node, 0.5m, 높이1.5m 소광계수 float32 → worker → 검증 bridge → Unity 30×20m 분석 평면. 6종 샘플러 좌표도 원본 node로 수정 | 원본 4시점/216점 일치·6개 Python 반례 PASS. C# 반례6개·실제 화면·120초 경계 숨김·복기 스크롤 확인. 이전 화재 결과는 legacy 좌표 버전이며 재사용하지 않음 |
| [JuPedSim 공개 구현](https://github.com/PedestrianDynamics/jupedsim) | 실제 경로·보행 계산에 영속 인물 상태 연결 | 1.4.2 CFSV3의 이동·대기와 사건 경로 전환, 식별자/노출 누적 보존 | 실제 50명 평시→사건→출구→Recovery. 부산역 실측 보정·접촉 압착·임상 결과는 미검증 |
| [Blender LTS](https://www.blender.org/download/lts/), 코레일 외관·공식 시설도·공개 지도/DEM | 미터 단위의 원본 지형/건물과 반복 가능한 실내 모듈 제작 | Blender 모델17종, 1m 단위 통일, 3개 층 바닥0.3m 슬래브·미터 타일, 출입/발매/운영/대기/식음/수직동선 모듈, Unity 메시 실제 업로드 | 3개 층 native 바닥·인물 치수/월드 텍스트0 확인. 층간5m 등 미실측 치수는 설계값. 사진만으로 BIM·정밀 사진측량 완료를 주장하지 않음 |

현재는 검증된 기법과 사용 가능한 입력을 우선한다. 모든 분야의 최신 논문을 나열하거나 근거 없이 SOTA라고 이름 붙이는 것이 선택 기준은 아니다. 상용 게임 품질의 미술·애니메이션·조작성, 20~30분 운영 밀도와 자원 제약, 도시 전역 사건·시설 복구는 남은 제품 작업이다.

화재 원본은 사건 후0~120초만 유효하다. 분석 화면은 그 시각의 1.5m 단면이며 입체 연기나 부산역 전체 예측이 아니다. 계수 자료를 임의로 늘이거나 게임 진행 때문에 안전 판정을 생성하지 않는다.

근거 그래프: `.planning/2026-09-20-integrated-build/production-benchmark-graph.json`, `actual-method-benchmark-graph.json`, `hazard-field-contract.json`. 수용 근거는 실제 native receipt에만 연결한다.
