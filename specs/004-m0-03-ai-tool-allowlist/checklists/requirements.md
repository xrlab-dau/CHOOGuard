# Requirements Quality Checklist: M0-03 AI 도구 허용목록

- 날짜: 2026-09-06.
- 대상: [명세](../spec.md).
- 상태: 1차 changes_required 반영 후 자체 서술 점검. 독립 재검토 미완료. 후보 도구의 실행·설치 승인 기록이 아니다.

## 명확성·완전성

- [x] CHK001 직접 의존성 pydantic-ai-slim, 미채택 pi-mcp-adapter, Node·uv를 포함한다. FR-001.
- [x] CHK002 정확 버전과 호환 조건·실측·미정값을 구분한다. 미판정 항목은 비허용이다.
- [x] CHK003 라이선스 원문·고지·vendored 재배포·전이 그래프·배포물 무결성을 요구한다. FR-001·004.
- [x] CHK004 리뷰어와 reducer 스키마, verifier의 격리 쓰기를 실제 계약과 구분했다. FR-003.
- [x] CHK005 실제 제공자·override·상속·fallback·Python 하네스도 검사 대상이다. FR-002.
- [x] CHK006 DA3/Open3D·Independent Verifier·순수 reviewer를 포함한 실행 주체와 목적지 세부 열을 요구한다. FR-005.
- [x] CHK007 보호 경로·승인 유형·대체자와 실제 계정의 비공개 매핑을 구분한다. FR-006~007.

## 측정·일관성

- [x] CHK008 보호 대상·확장자·자료등급·우회 실행 경로별 fixture, test ID/argv, 기대 deny, 비적용 근거를 요구한다. FR-008.
- [x] CHK009 명세 001과 동일한 POLICY_GLOBS 및 추적 npm manifest/전체 lock·integrity를 요구한다. FR-009.
- [x] CHK010 조기 차단 성공 금지와 변경 후 새 독립 재검토·판정 저장 실패 차단을 요구한다. FR-010~011.
- [ ] CHK011 머신별 정확 Node·uv와 학교 PC 프로파일, 라이선스·의존성 승인이 확정됐다.
- [ ] CHK012 실제 허용목록·BT 매트릭스와 해시 생성기·누락/변경 fixture 시험이 완료됐다.
- [ ] CHK013 필수 관점 오류를 승인하지 않는 YAML·실제 제공자·파일·네트워크 경계가 시험됐다.
- [ ] CHK014 최신 불변 manifest의 독립 재검토와 PM 승인이 완료됐다.

명세의 열거는 허용목록 구현이나 환경 승인 완료가 아니다. 미정 프로파일은 M1 실행 비허용으로 유지한다.
