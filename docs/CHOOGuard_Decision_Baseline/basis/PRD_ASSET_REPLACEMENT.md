# PRD v10 무료 우선 에셋 대체 제안

이 파일은 PRD 본문이나 #222를 자동 변경하지 않는 제안 부록이다. 핵심 제품 방향, 교육/랜덤 두 모드, 운영·물리 계산, 매뉴얼·현실 적용 검증 요구는 바꾸지 않는다.

| 기존 후보 ID | 무료 우선 대체 | 남는 제작·검사 |
|---|---|---|
| AST-SHRIMPLE | FREE-001 RGS 차량 + FREE-009 CharCrafter + FREE-010/011 인물 + FREE-008 가구 | 통합 팩의 동일 스타일/충돌/기능을 그대로 대체하지 못함 |
| AST-SYNTY | FREE-014 경찰 + FREE-001 차량 + FREE-021 무전기 + FREE-008 상황실 소품 | 한국 제복·기관 표시·상황판·공조 로직 별도 |
| AST-MEDPROPS | FREE-019 AED + FREE-020 들것 + FREE-021 구급상자 | 의료 프로토콜·운반 동작·인계 상태 별도 |
| AST-AMB-BOX / STAREX / HYUNDAI | FREE-001의 구급차를 표현용 베이스로 사용 | 실제 한국 차종·문·실내·좌석·적재·능력 별도 확인 |
| AST-ANIM | FREE-015 Standard 및 FREE-017 Mixamo | 유료 애니메이션 전체를 포함했다고 가정하지 않음 |
| AST-CAR-KIT | FREE-002 유지 | 같은 버전의 실제 원본 검수 |
| AST-KTX | REF-KTX-I 조건부 참고; FREE-003로 기능 시험 분리 | 실측 복원/정확한 객실은 별도 |
| AST-BUSAN | FREE-005/006/007 외부 부품 + FREE-004 선택적 지하 모듈 | 실제 부산역 모델로의 등치 금지. 핵심 공간은 근거로 제작 |

## 개발 AI의 작업 경계

EP03은 선택한 자산 하나씩 입고하고 아래 검사를 수행한다. EP04는 FREE-023/024를 운영 UI 표현 후보로 검토한다. EP02의 능력·권한·자원 계산이나 EP07/08의 물리 모델을 에셋의 외형·파일명에서 생성하지 않는다.

대표 입고 순서: 원문과 무료판 확인 → 원본 취득/해시 → 형식·참조파일 검사 → 축·단위·실제 크기 확인 → 부품/rig/animation 확인 → 격리 씬 임포트 → 프리팹과 operationalAssetId 결속 → 동일 카메라·부하로 표시 비용 계측.

기본 시험 대상을 임의 시간 내 완료하라는 제작 일정은 지정하지 않는다. 원본에 스크립트·플러그인·DLL이 있으면 모델 파일과 분리해 검토하고 승인 없이 실행하지 않는다.

## 반환 계약

`assetId, exactFreeTier, sourceUrl, acquiredAt, originalFileName, rawBytes, rawSHA256, actualFormats, unityVersion, pipeline, importResult, nodeParts, rigAndClipInventory, colliderAndLOD, domainBinding, knownLimits, nextAction`.

취득하지 않은 파일의 해시나 폴리곤 수를 만들어 넣지 않는다. 무료 표시와 권리 조건의 검토는 구분하되, 비용/작업 조건을 기록해서 다음 AI가 다시 유료 팩을 필수 선행으로 넣지 않게 한다.
