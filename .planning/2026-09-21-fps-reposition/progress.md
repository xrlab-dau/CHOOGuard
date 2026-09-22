# Progress

- 사용자 FPS 범위/역무원/싱글플레이 확정, 별도 복기 기능 제외 반영.
- fps-plan-graph.json (xhigh) 및 fps-controller-graph.json (Astra low) 초안 보존, 미게시.
- 병렬 연구: korail_drill_research(Terra high), station_tenant_drawings(Sol high), fps_reality_benchmark(Sol high); 루트 KTX+학습 근거+Jev+제안.
- 이전 렌더 최적화 초안 작업은 방향 전환으로 중단. FPS 지도 범위가 줄었으므로 추후 새 씬에서 실제 성능을 재측정한다.

- 공개근거완료: 코레일훈련8출처/8행동fact, FPS6작품13출처/10전이사슬, 입점내부8출처·34원본이미지. 근거의범위와시점불일치기록.
- Jev054실호출1980ms: 현장협업중심루프/현재역할권한공백/실제월드완료조건 선택. Jev055실호출1103ms: 첫후보화재·대피(모델선호0.93;confidence0.89), 도면모듈정합,현장피드백 선택. 이 수치는모델판정이며정확도·학습효과 증거아님.
- 사용자KTX7z170MB와부산1호선ZIP545MB를제공, source/rail-community로원본이동·SHA검증. 두Astra-low워커가공통BVE파서/차량과별도노선배치작업으로분담. 도시철도부산역환승구간우선 vs1호선전체범위질문대기; 독립자료검사계속.

- 연구제안서작성: FPS_RESEARCH_PROPOSAL.md. 연결범위사용자확정. 원본KTX10량/MetroFPS부분변환완료; 실제FPS씬통합은재질계약을맞춘후진행.

- 첫FPSscene생성후native실패발견: 원MVPframe의worldY=-1000를cloner가그대로복사,열차/Metro/연결로는frame0. 기존spawn합격은작성연결로에만해당하여연결PASS로사용불가. 실패receipt보존/씬백업, 원점정규화AstraLow수정중.

- 프레임0 수정 후 FPS 새 씬 생성. sourcewall에 막힌 지하 통로는 원본을 보존한 파생 메시 절단으로 수정. fps-walk-metro-afterpassage.json 65.65s 정방향 PASS, fps-walk-metro-reverse.json 65.6s 역방향 PASS.
- Native 화면으로 Metro enclosure 결손 확인. Jev057 LIVE1427ms, 입력959/출력169토큰: 분리된 병렬 보완/제한적 연결 주장 선택. Root검수와 Astra저추론 2개 독립 작업 진행.

- Metro14텍스처의 BMP→PNG 알파 손상 재현·원본 픽셀 기준 복구. 공식 importer 재적용3.627s SUCCESS. KTX씬배치가 FBX root 회전270deg/scale100를 제거해 .04m 폭이 된 실패 발견; native FBX원본200.66m 확인. builder수정 게시, 편집기 로드 갱신 후 기존 씬 복구 예정.

- KTX 깊이 보정 native 화면에서 객실 바닥·좌석 관통 오류 해소 확인. 반투명 유리 유지. ktх-vestibule-depth-native.png(파일명은ASCII ktx).
- 원본 MetroRoofCL/CR384메시를 복구했으나 기존 작성 대합실−3.5m가 원본 지붕 상단−3.1m와 겹침. 새 native 보행은 FAIL, 이전 PASS는 복구 전 상태로 한정. 원본지붕을 길게절단하지 않고 작성대합실을−2.8m로 올리는 보완 진행. 실측층높이주장 아님.

- 현재 최종 Metro: 원본 천장 포함, 작성대합실−2.8m/.15m바닥. 지상→승강장65.65s, 역방향65.6s 실제 CharacterController PASS. 원본 천장 부품 복구 후 하늘 노출 해소 화면 확인.
- KTX2호차 안쪽문 원본3메시를 fixed+leaf6메시로 교체. native원본/교체 각4538삼각형 동일(Blender4548면과 구분), 문짝정적+.88m, 나머지차량 보존. 활성 충돌체2057. 실제통로 검수대기.

- 사용자 교정 수용: 기술적 capsule 경로 검증과 실제 역간 환승동선 복원을 혼동한 연결 완료 판단 철회. 추정통로 추가 구현 중단, 실제 연결자료 확보→구간별 근거지도→재구축으로 전환. Editor 사용 중 카메라/플레이어/씬 변경 금지.

- 실제복원자료결과: official-metro/source-graph.json +walk-evidence/source-graph.json +root-public 실제치수CAD와사진 +model/model-graph.json. Astra low 새로컬모델1.8MB FBX/4.6MB blend, 내부·평면렌더 root직접검토. 원본모델연결강행없음. 양끝접속·고저차미확정. canonicalMVP와복원보고서갱신.

- 자동계속 턴 분류: 직전 턴=PROGRESS. 실제운영사CAD확보, 자료기반모듈제작, 잘못된실제연결수용철회로상태/다음행동이변했다. 현재 checkout/계획/산출물재확인. 남은철도역접속·Metro/Mall접합면 조사 계속.
