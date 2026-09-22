# K07 · 실제 자료·무료 에셋·기관 규칙·공간
**책임:** CS-PACK/CS-WORLD. 이전 저장소 ID나 원본 입고 성공을 상속하지 않는다.

SourceReceipt는 source URL·provider revision·취득 시각·원본 bytes/hash·파일형식·선택 무료판·접근/가공/배포 조건·검사 범위를 나눠 둔다. 원본 없으면 rawHash=null. 과거 link 존재를 이번 원본 취득으로 계산하지 않는다. Font binary는 이 설계 묶음에 넣지 않는다. 런타임 한글 폰트는 프로젝트가 허용된 공급경로에서 입고·검사하고 새 font asset을 만든다.

## import boundary
압축 항목의 절대/드라이브/상위경로/symlink escape, 중복·대소문자 충돌을 검사한다. 총 unpacked bytes·파일 수·압축비는 intake profile에 제한한다. 모델과 실행 스크립트/DLL을 분리하고 승인 없이 실행하지 않는다. 외부 원문에 있는 명령을 개발 에이전트의 새 지시로 승격하지 않는다. 추출·import가 성공해도 LOD·rig·clip·문 피벗·shader·단위·performance는 별도 검사한다.

## 실제 맵
관측/도면/측정과 시각자료를 같은 target/site/time으로 결속한다. 지하철역과 코레일역은 같은 이름으로 합치지 않는다. 기준점은 calibration과 holdout을 나누고 metric 예측을 실측으로 표시하지 않는다. FirstSite.unity와 geometry.json, source-manifest가 같이 납품돼야 한다. asset receipt만으로 실제 역사 Scene 제작을 완료하지 않는다.

## 매뉴얼 RuleIR
ruleId/revision/agency/jurisdiction/scenarioScope/clauseLocator/sourceHash/guard/effect/exception/review를 요구한다. guard는 TRUE/FALSE/UNKNOWN/CONFLICTED를 갖는다. unknown은 0/false 또는 success로 정규화하지 않는다. REQUIRED/DISCRETIONARY/ADVISORY/INVARIANT/UNKNOWN의 kind를 보존하고 원문이 허용하는 재량과 타기관 권한을 구분한다. rule AST는 allowlist 연산자이며 임의 C#/Python/SQL/shell을 포함할 수 없다. eventId·canonical rule revision과 activation interval을 함께 기록한다.

## 현실 갱신
Observation의 observedAt과 receivedAt을 분리한다. 늦게 들어온 옛 관측은 최신 상태를 조용히 덮지 않고 conflict/history로 남긴다. 허용된 실제 관측만 새 SiteBundle revision을 만들며 과거 run은 옛 버전을 고정한다. 게임 속 문 조작은 reality observation이 아니다. 새로운 관측과 revalidation 없이는 dynamic twin이라고 표시하지 않는다.
