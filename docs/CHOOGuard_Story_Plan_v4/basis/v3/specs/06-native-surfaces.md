# K06 · Unity 네이티브 화면·입력·통합
**책임:** CS-PLAY, CS-WORLD, CS-LAB, CS-SCRIPT. HTML/DOM/Electron/WebView로 대체하지 않는다.

## 화면 생산자
| 화면 | 신규 구현 소유 |
|---|---|
| S01 런처·두 모드 | CS-PLAY.05 |
| S02 현장 패키지 자격 | CS-MODES.03 |
| S03 상황·범위 설정 | CS-MODES.03 |
| S04 튜토리얼 안내 | CS-MODES.01 |
| S05 실제 맵+HUD | CS-PLAY.03 |
| S06 원인·근거 inspector | CS-PLAY.04 |
| S07 명령 preview | CS-PLAY.02 |
| S08 체크포인트·분기 | CS-PLAY.06 |
| S09 A/B 비교 | CS-PLAY.06 |
| S10 대본 편집·근거 | CS-SCRIPT.03 |
| S11 출력 자격·오류 | CS-SCRIPT.04 |
| S12 환경·접근성 | CS-PLAY.05 |

S08/S09는 API만 존재한다고 끝내지 않는다. 실제 uGUI prefabs·presenter·focus 복귀·취소·loading/error/empty/stale 상태를 구현한다. 모두 same application 내부 Canvas overlay다. PLAYER_INTEGRATION 시험은 CS-PROOF.02에서 실제 서비스를 연결해 수행한다.

## 입력 소유권
modal→IME/text→HUD→world→camera. pointerDown의 소유자를 up/cancel까지 고정한다. UI를 넘어서 드래그해도 world selection으로 변환하지 않는다. ScrollRect 위 wheel은 zoom에 전달하지 않는다. focus loss 시 drag/held keys를 cancel하고 재포커스 뒤 임의 명령을 발행하지 않는다. Input System action map은 context에 따라 enable/disable한다. key remap 충돌·IME 조합 Enter/Esc/Space·clipboard는 실제 Windows Player에서 시험한다.

## scale과 scene
1920×1080 기본, 1280×720/1366×768/2560×1440/ultrawide와 text 100/150/200%를 검토한다. 작은 화면에서 inspector를 접고 넓은 overlay로 이동하며 글자 자동 축소로 접근성 설정을 무력화하지 않는다. CanvasScaler는 기반일 뿐 합격 증거가 아니다.

renderer 층 숨김과 collision/quantity/nav owner를 구분한다. scene visual readiness는 가상 업무 시간을 늦추지 않는다. 전체 논리 geometry가 준비돼 있으면 카메라 로딩은 표시 비용일 뿐이다. 해당 계산 geometry가 없으면 run을 load gate에서 pause/unsupported로 처리하고 renderer delay를 현장 이동 지연으로 통계에 넣지 않는다. 유효 경계 없이 보이지 않는 구역으로 계산을 건너뛰지 않는다.

## 화면 인수 조건
UI click 뒤 world command 0회; IME composition 중 WASD camera 이동 0; modal Esc 후 기존 selection과 focus 복원; stale CommandPreview 제출 시 새 원인 안내; mixed-team batch가 부분 결과를 명시; exact checkpoint unavailable이면 버튼 비활성+이유+원점 재실행; incomparable A/B에 순위 없음; 수정한 문서에서 이전 approval badge 만료; browser test 기록은 Unity acceptance에 사용할 수 없음.
