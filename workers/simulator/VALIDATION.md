# 전체 월드 구현·검증 현황 — 2026-10-14

MVP 범위는 저장된 역사·승강장·KTX의 전체 오브젝트와 NPC다. Car04는 개별 구성요소 검사다. 구현 소스는 별도 프로젝트에 있고 원본 건축 씬과 공식 형상은 수정하지 않는다. `output/simulator/acceptance.json`은 이전 승강장/KTX 구성요소의22 PASS·3 PENDING 기록이며 현재 전체 월드 완료 집계가 아니다. 현재 whole-world 실제 실행은 FAIL/PENDING이고 source/API·pure portable 검증은 별도 증거다.

| 범위 | 현재 확인 | 근거 |
|---|---|---|
| 동결 형상 스냅샷 |HISTORICAL_NOT_CURRENT: ec2f epoch의4,115객체,3,844활성,2,748물리후보; 현재 삭제 대상3개 포함 | immutable world-snapshot receipt/integrity-tests; 현재 적용 자격과 구분 |
| 실제 Unity 형상 가져오기 |4,115identity, KTX703MF, 원본 계층·세계행렬·재질 참조 | world-snapshot-import.json |
| 패키지 의존성 |104파일 해시 일치 | world-snapshot-packages.json |
| 원본 전체 등록 | 기존532상태+원본4,115입력 | WorldObjectRegistryTests |
| 역사 의미·공급망 |381개 어셈블리+전원11/급수4 | StationCapabilityInferenceTests/profile ledger |
| 역사 이동 지점 | 동결 epoch의 sampled52곳·25연결그룹; 전체 공간 의미·이동 완료가 아님 | world-spawn-diagnostics.json |
| 승강장 경로 |610수집 형상/16,900정점,7출입문 연결·안전 시작점 | world-spawn-diagnostics.json |
| 부품 조작 | 연속 이동·잠금·공구·끼임·소모·조준·점검·태그·재생 | MechanicalInteractionsTests |
| 대응 절차 | 역할·현장 관측·전달·확인·복창·승인·부분 작업·실제 도착 | ResponseWorkflowTests |
| 전체 월드 FPS·NPC·상세 재생 | 실제 whole-world 검사 FAIL·초기 replay digest FAIL; 일부 source 수정은 실제 Unity 재검증 보류 | wholeworld-playmode-probes.json / geometry-first records |
| 실제 그래픽·육안 검토 | 미완료 | 자동화 호스트 GUI/display 부재 |

원본 객체는 모두 입력과 출처를 유지한다.2,400개 원본의 인과 의미는 미지원 상태이며,52개 역사 문에는 이동 조작을 부여하지 않았다. 가시 메시 수에 비례해 고장 기회를 늘리지 않도록 의미 어셈블리로 묶었다. 전원·급수·재료·마찰·시간·역할·통신 규칙은 사용자 승인 추론에 따른 훈련 가정이다. 대외비 문서가 있어야 구현할 수 있다는 조건은 두지 않았다. 실제 코레일 승인 SOP, 현장 힘·시간의 실측, 현실 조작99%, 훈련 효과를 인증하지 않는다.

이 수치는 동결 scene SHA `ec2f5f3b6097bcc7eae78e01e50ffbbb670583ea355e074ae2f02e04e8bd1e66`에 한정된다. 2026-10-14 08:22 UTC에 read-only로 확인한 원본 SHA `f27783bba9279231685b9aa22a06d42e1e3d2bb61149085cf8bbab0ca8f5ddc4`는 다르며 새 snapshot·staging·Unity 실행을 하지 않았다. 기존 failure packet과 acceptance JSON은 그대로 보존한다. 새 pure5105-state 검증은 과거 기록의 parsed baseline에 대한 exact codec/자료/20틱 인과 재생이지 원래 Unity 메모리 수치 복구나 최신 역사 설치 증거가 아니다.

2026-10-14 22:31 UTC의 saved-only 감사에서는 저장 씬 SHA `2fc961025b1709c0b86ae4e8c34f4956101131113e451c0166594ab481b55436`와4,148객체를 확인했다. 이는 감사 시점의 저장 상태이며 편집기의 미저장 STAFF3객체나 이후 저장을 포함하지 않는다. 과거 ec2f 패킷에는 현재 tombstone인 `MAIN1F_B9_CEIL_HALL`, `MAIN1F_B9_CEIL_SHOPS_273`, `MAIN1F_B9_CEIL_SHOP_A15`가 남아 있으므로 현재 소스로 적용하지 않는다. 새 export는 진행 중인 STAFF 복구의 실제 전체 ACK와 저장공간 여유 확인 전까지 HOLD다. Lease 만료만으로 이미 시작된 복구나 쓰기 권한이 해제되지는 않는다.

`world_inventory/test_snapshot.py`는 매번 현재 저장 씬 SHA와 meta를 확인하고 해당 패킷만 기본 선택한다. 직접 실행 시 `--snapshot-dir <패킷 디렉터리>`로 대상을 지정해도 현재 소스 일치 조건은 유지한다. 과거 패킷 metadata 무결성과 현재 소스 적용 자격은 별개이며, 현재 패킷이 없으면 skip/PASS 대신 오류다. 이 선택기 검증은 새 export·staging·Unity 실행이나 기존 runtime 실패 종료를 뜻하지 않는다.

CURRENT 전체 패킷 검사와 `stage_unity.verify_snapshot`은 같은 freshness 가드를 사용한다. 복사 전에 저장 씬·meta·원본 의존성·현재 authority가 가리키는 tombstone 계약을 대조하고, 검사 끝에 추적 파일을 다시 확인한다. 권위 자료 누락·불일치, 미해결 복구, 삭제 객체 포함, source/meta/dependency 변경은 거부한다. 과거 패킷 무결성 검사는 읽기 전용이며 CURRENT staging 우회 경로가 아니다. 실제 STAFF 복구 전체 ACK와 headroom HOLD는 그대로이며 새 staging·export·Unity 실행은 하지 않았다.

독립 검토에서 발견한 원본 타격과 인과 객체 연결 누락, 즉시 전원 차단 우회, 휴대 무전기 거리 판정, 비활성·미확인 바운드 관측, 경로 중간 위험 누락을 수정하고 해당 경계 검사를 추가했다. Unity 형상 수집 함수가 호출마다 목록을 초기화하는 문제는 실제 엔진에서 확인해 누적 수집으로 수정했다.

이전 승강장/KTX 구성요소에서는22/25검사가 통과했고3개 그래픽 검사는 남았다. 이 기록은 `acceptance-platform-ktx-component.json`에 보존하며 새 전체 월드 구현의 증거로 대체하지 않는다. 과거1,013entity 구성요소 요청에서 실제 사용자 구독 OAuth 로그인과1회 생성은 완료됐고 입력36,145·출력53토큰, 빈 변경안 수락 후40틱 로컬 재생이 통과했다. 이는 현재5,105상태 전체 월드의 AI 생성이나 새 비상상황 발생 증거가 아니다. 새 구현을 위해 추가 구독 사용량을 소비하지 않았다. `provider-live/final`에 그 요청·사용량·원자 적용·재생 근거가 있다.

이전 로컬 Qwen 구성요소 시험에서 동일6개 관측에 대한 호출은6→3, 입력 토큰2,738→1,362였다. 이 작은 시험을 전체 월드 FPS 성능·JEV 청구액·대응 전문성으로 일반화하지 않는다. 전체 입력이 모델 문맥이나 승인 예산을 넘으면 잘라 보내지 않고 거부한다.

```bash
python3 workers/simulator/run_checks.py test
python3 workers/simulator/compile_unity.py
python3 workers/simulator/acceptance.py
```

Unity 재생은 기록한 입력·물리 접촉·가림·경로 관측·모델 응답으로 상태와 전이를 재현한다. 물리 엔진이 독립적으로 같은 궤적을 다시 계산했다는 뜻은 아니다. 소스·스냅샷·카탈로그·NavMesh가 바뀐 테이프는 거부한다. 건축 세션의 이후 저장으로 원본 씬 해시가 바뀌면 기존 스냅샷은 보존하고 새 스냅샷으로 갱신한다.
