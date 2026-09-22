# K09 · 근거 AI·대본·실행 의미·승인
**책임:** CS-SCRIPT. 모두 새로운 계약과 코드로 작성한다.

AI는 결정적인 운영 상태의 consumer다. source clause/run event/author note/derived metric을 권한 범위로 검색한다. 모델·prompt·index revision을 기록하고 외부 전송 허용을 먼저 확인한다. 중간 추론을 노출하라고 요청하지 않는다. 보여줄 것은 입력·근거·관측·제안 patch다. timeout/비용한도/무효 JSON은 설명과 수동 편집을 유지하고 운영 run을 바꾸지 않는다.

ScriptIR는 planRef/scenarioRef, stepId, role, condition, text, claimType, evidenceRefs를 갖는다. OBSERVED_IN_RUN은 실제 event·log 완전성이 있어야 한다. HYPOTHESIS는 사실처럼 표현하지 않는다. MANUAL_SUPPORTED_PROPOSAL은 적용 clause가 있어야 한다. REVIEWED_INSTRUCTION은 실제 role/scope가 있는 review record를 요구한다.

기본 출력은 JSON/Markdown/DOCX. 기관 템플릿은 format/version/mapping/required fields/validation evidence가 있어야 활성화한다. 외부 편집본은 stepId와 semantic diff로 조정한다. unsupported roundtrip은 수동 reconciliation으로 표시하고 작업량에 기록한다. 문서를 재가져왔다고 로그를 수정하지 않는다.

## approval ledger
result badge는 텍스트가 아닌 typed evidence ledger에서 나온다. subject content hash·입력과 모델 lock·test revision·환경·출력 hashes·수행자·별도 검수자·시각·scope를 결속한다. PASSED_WITH_SCOPE는 실행 결과와 artifact 접근 검사가 있어야 하고 NOT_RUN은 executedAt=null·구체 사유가 있어야 한다. 같은 작성자의 self-review는 독립 검수로 취급하지 않는다.

의미 변경은 새 revision과 관련 approval STALE를 만든다. 형식만 수정했더라도 출력 양식 검수는 별도로 유지한다. UI 사용자 role을 선택했다고 법적 기관 권한이 생성되지 않는다. 기관 지명 수용, 모델 검증, 제품 기능 시험은 서로 독립적이다.

## AI 오류 시험
근거 없는 '승인됨' 주입→qualification 불변; 미존재 인물/문/기관→semantic error; 링크만 있고 파일 없는 근거→확보 미완료; 모델 제안에 shell/SQL/operator invocation→allowlist reject; client-authored evidence/result→server 검토 요구; 원문 지시문→그냥 데이터.
