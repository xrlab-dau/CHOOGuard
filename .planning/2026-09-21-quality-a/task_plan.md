# CS-EXEC.01.01 — A 품질 지속 개선

목표: 사용자 지정 게임성·벤치마크·UIUX·모델링·오픈월드·내부 물리/논리 품질을 고정한 A 기준까지 개선한다. 근거가 부족한 영역을 평균점수나 자기 선언으로 통과시키지 않는다.

- ACTIVE 기준: xhigh 계획 워커가 현재 증거와 요구에서 평가 기준/미달 표를 작성. 루트가 확정한다.
- ACTIVE 게임 운영: Astra low가 시설 자원·점유·준비도·지원 가능 조건을 실제 선택 비용으로 연결한다.
- ACTIVE 공간/가독성: Astra low가 현행 모델·월드 표현의 가장 큰 미달을 실제 소스와 크기로 교정한다. 출처 없는 정밀 복원 주장 금지.
- ACTIVE 루트: 실제 물리 워커의 범위/확장 경계 확인, 공개 공간/검증 자료 조사, Jev 분류·판단 검토와 직렬 Unity 통합.
- PENDING 통합: 필요한 범위의 실플레이/고장경로/성능/수치 검증. TDD·무의미한 전체 반복 없음.
- PENDING 재평가: 기준별 증거와 미달을 갱신하고 다음 미달에 계속 작업. A 미달이면 목표를 완료로 표시하지 않는다.

고정 요구: 부산역~남포동~북항, 실제 기관 위치, 건물 선택→상황별 요청→공동 팀 자동 수행, 한국어, 공개 그래프/예측 패널 없음, Jev 내부 산술, Astra low 구현, 공식 Unity CLI/MCP, computer use 금지, 단일 스토리. 이전 실제 증거: .planning/2026-09-21-production-benchmark/.

## Latest active direction — official-source world replacement

- User explicitly replaces legacy model direction: rebuildactiveopenworld around downloadedofficialStation+KTX, replaceoverlappingoldstation/plaza/context/roads. Retainrawsources/backups; no inactivelegacygeometrypassedoffasnewmap.
- Astra-low visualowner: source layer51chunk model, rigidregistration/sourcecoverage/materialmanifest/fullsiteexports.
- Astra-low binderowner: newOfficialStationBinder stagesmaterials/site; separatesstationenvelopefromcontext; safeactivepublication.
- Astra-low coverageowner: City/WorldSurface exactcoverageclipping+stagedregeneration preservingoutsidegeography.
- Root: evidence/Jevtriage/nativeaxis/metre/texture/seam/functionchecks andintegration. CurrentEditorEDIT.
- Fire1800s officialcompute continuesunderroot ownership; automaticmonitor/sessionhandoff recorded, no GPTpollingloop.
