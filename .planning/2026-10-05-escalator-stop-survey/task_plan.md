# Goal
사용자(2026-10-05): "실측을 진행해야해". 에스컬레이터 비상정지 버튼 34개(에스컬레이터 17대 × 승강장 2곳)는 코드가 '오른쪽 난간 0.9 m'라는 일반 위치에 만들어 왔다(`EscalatorStopButton.Build`, "the twin has no survey of the button positions"). 5c783468의 디지털 트윈 원칙(대상·장소를 잇는 관찰 근거가 있는 물체만 둔다, `docs/CHOOGuard_Story_Plan_v5/WORLD_OBJECT_PROVENANCE.md`)에 맞게 실측한다.

## 방법(저장소의 기존 근거 체계)
- 근거로 인정: 원본 공간 모델, 실제 도면, 사진·영상 관찰 기록. 법규 간격·가장 가까운 벽·모델 라이선스는 근거가 아니다.
- 이 저장소의 '관찰'은 지금까지 공개 영상(유튜브 보행 영상)·공개 사진(블로그)·네이버/카카오 지도·공식 SketchUp 모델과 그 정합이었다. 팀이 찍은 현장 사진·측정은 아직 없다(현장 실측 양식도 없음).
- 존재가 관찰된 것과 좌표까지 확인된 것을 구분해 기록한다.

## Phases
1. 공개 자료 원격 조사(병렬) — 승강장 우물 에스컬레이터 11대 / 1·2·3층 연결 6대 / 승강기 공개 등록 정보·안전기준·제조사 자료 — done (`findings-*.md`)
2. 버튼 배치를 근거 기반으로: 관찰 기록 파일(Resources) → 기록이 있는 승강장에만 버튼, 없는 곳은 만들지 않음. 버튼이 없는 에스컬레이터는 '승객이 비상정지 버튼을 누름' 전개 후보도 열지 않음 — done
3. 현장 실측 양식: 공개 자료로 못 정한 승강장과 정밀 좌표(높이·거리), 이후 빠진 설비 묶음에 쓸 기록 양식·촬영 목록 — done (`field-sheet.md`, 절차 요약은 WORLD_OBJECT_PROVENANCE.md). 현장 방문은 팀 일정에 달림
4. 검증(컴파일·PlayMode·Play에서 버튼 위치 화면 대조)·문서·graphify — done

## 결과
- 레코드 4개(`Assets/ChooGuard/Art/Emergency/Resources/EscalatorStops.json`): esc-well-s-56 위(탄 사람 오른쪽), esc-2f3f-up 아래(오른쪽)·위(왼쪽), esc-2f3f-down 아래(왼쪽). 네 곳 모두 관찰된 것은 유리 끝단의 빨간 비상정지 표지와 승강장·쪽이다. 버튼 본체·덮개·키 스위치는 보이지 않았다. 높이 0.3 m와 위치는 [INFERENCE]이며 트윈 형상(난간 끝·입구 패널)에 맞춰 두었다(`positionBasis`).
- 2F↔3F 줄 방향: hCyroXn0Jhg 850.6–858 s에 2층에서 볼 때 오른쪽 줄을 타고 올라간다. 트윈도 하행 줄이 상행 탄 사람의 왼쪽 2.1 m에 있어 일치한다. 원격 조사 에이전트의 '오른쪽 줄 = 하행' 판단은 이 영상과 맞지 않아 쓰지 않았다.
- 영수증: `receipts.json`(URL·업로드일·시점·sha256·보이는 것, 미디어는 로컬 전용). Play 화면: `smoke/`(로컬, png는 gitignore).
- 검증: 컴파일 오류 0. `EscalatorStopSurveyTests` 2/2. Play에서 에스컬레이터 17대 중 3대에 버튼 4개, 레코드 수와 같다. 버튼 없는 에스컬레이터(esc-well-n-1, esc-1f-south-0)에서 넘어짐을 일으키면 `estop_` 후보가 없고, 있는 곳(esc-2f3f-up·down)에는 있다. 크게 넘어져도 버튼 없는 esc-well-n-34는 계속 돌고, 버튼 있는 esc-well-s-56은 '비상정지 버튼(승객)'으로 멈춘다.

## 2차(사용자 "계속진행해", 2026-10-05)
- 병렬 조사: `findings-inventory.md`(등록 대수·실내지도·우물 대조), `findings-landings-2.md`(미관찰 승강장 새 출처), 통합 담당 `findings-official-model.md`(공식 모델 경사·단면 스캔, 트윈 수정).
- 바로잡음: esc-well-s-56 위 레코드는 북쪽 가장자리 5·6 우물(가운데 쌍의 내려가는 줄) 표지였다. zkOH0nK4nIY 발치 표지 '← 6 … 5 →'(−z로 내려감), 내려가는 사람 기준 에스컬레이터 왼쪽·다른 줄 오른쪽, 홀의 PASCUCCI·KAKAO FRIENDS가 근거다.
- 트윈 수정(`StationSurvey` → Build station points → Bake world navmesh → Bake route graph):
  - 가운데 5·6·8·9 에스컬레이터 쌍을 더했다.
  - s-56 차선을 계단에서 실제 에스컬레이터로 옮겼다.
  - 중앙 뱅크 두 차선의 방향을 킷 사양·실제대로 바꿨다.
  - 에스컬레이터는 17 → 21대다.
- 버튼 기준점: 계단참 점(navmesh로 옆으로 비킴) → 벨트 끝과 벨트 방향. 기존 2F↔3F 레코드의 along은 +1.0 바뀌었다(같은 자리).
- 레코드 8개:
  - 2F↔3F 셋.
  - esc-well-m-56-1 위(오른쪽).
  - esc-well-n-34 아래(오른쪽).
  - 중앙 뱅크 올라가는 줄 아래(오른쪽)·위(왼쪽), 내려가는 줄 위(오른쪽).

## Constraints
- 하드룰 jev-emergency-composition: 원인 후보는 실제 물체에서만. 버튼이 없는 곳에서 버튼을 누르는 전개를 만들지 않는다.
- 타 게임·외부 영상 미디어는 커밋하지 않는다. `asset-library/research-public/`는 로컬 전용(.git/info/exclude). 추적되는 영수증(URL·시점·sha256·보이는 것)은 이 폴더에 둔다.
- 추정은 [INFERENCE]로 표시한다.
