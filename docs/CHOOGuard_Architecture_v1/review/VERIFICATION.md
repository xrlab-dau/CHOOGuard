# 아키텍처 전달물 검증 기록

## 판정 범위

이 패키지는 전체 아키텍처 설계와 구현 인계 계약이다. 구조·예제·설계 SQL의 선택 불변식을 실제 검사했으며, Unity 제품·외부 solver·현장 정확도·사용자 효용의 실행 증거가 아니다.

## 실제 실행

`python -m unittest discover -s tests -v` 최종 결과: **39개 실행, 실패 0, 오류 0, 종료코드 0**. 원시 출력은 `test-final.txt`에 있다.

검사 범위는 15개 모듈의 의존성 DAG와 잘못된 참조, 기존 91개 요구 ID/12개 EP의 연결 보존, 10개 JSON Schema와 10개 합성 예제, 두 모드·native UI 경계, 체크포인트의 일부 교차필드 조건, 오래된 결과 binding, 가짜 승인 필드 및 15-table SQLite 설계 DDL의 예약·receipt·append-only 제약이다.

SQL 테스트는 Python SQLite의 **:memory:** DB에서 실행했다. 실제 Unity native binding, WAL 파일·동시 프로세스·전원상실·디스크 flush는 시험하지 않았다. 테스트 라이브러리 SQLite 3.46.1은 배포 선정 버전이 아니며, 설계에서 요구하는 수정된 SQLite binary의 자격을 입증하지 않는다.

EP00~EP11의 12개 문맥 출력은 JSON으로 파싱 확인했다. 문맥 도구는 읽기 경로와 인계를 출력하는 도구이며 작업 배정·실행·승인 도구가 아니다.

## 실패→수정→재검사

- `test-red.txt`: validator 모듈이 없던 최초 환경/구현 미완성 오류. 행동 결함의 재현으로 계산하지 않는다.
- `test-red-behavior.txt`: no-op 교차검사 함수를 대상으로 39개 시험을 실행했을 때 11개 실패. 손상된 입력을 놓치는 경우를 드러냈다.
- 교차검사·DAG·선택 binding 검사를 구현한 뒤 `test-green.txt`와 최종 `test-final.txt`에서 39개 통과.

이는 실제 게임 버그 39개를 수정했다는 뜻이 아니다. 부정 입력 시험은 명세 도구의 보호 범위를 확인한 것이다.

## 도면·문서

Graphviz DOT에서 10개 SVG/PNG 도면을 렌더했다. 각 도면의 노드·관계 endpoint와 파일 존재를 검사했다. Mermaid는 공유·편집용 연결 소스이며 로컬 Mermaid 파서를 실행하지 않았다. A03은 반복 참조를 묶은 요약도이고 정확한 15개 모듈의 의존성은 `contracts/architecture.json`이 정본이다.

Word는 **23쪽**을 최종 렌더해 모두 확인했다. 12쪽의 공간 배치를 수정한 뒤 전체 최종본을 다시 확인했다. 전체 설계도 PDF는 **10쪽** 모두 시각적으로 확인했다. 한글·레이아웃·표·연결선의 가독성을 검토했으며, Word 자동 접근성 검사에서는 high/medium/low 보고 항목이 모두 0이었다. 스크린리더나 접근성 인증 시험은 아니다.

## 하지 않은 검사

Unity 컴파일·Game View/Player, C# 인터페이스 스케치 컴파일, 실제 SQLite driver·WAL 내구성, native plugin·IPC, FDS/JuPedSim/SUMO/ROM 실행, 현장 관측 비교, 기관 매뉴얼 적용성, 실제 사용자 시험은 **NOT_RUN/NOT_EVALUATED**다. `runtime-test-plan.json`의 12개 실행 시험은 명세만 있고 완료로 표시하지 않았다.

`examples/`의 해시는 전부 SYNTHETIC_FIXTURE 식별 예제다. 해당 이름의 실물 blob이 존재하거나 해시 대조가 끝났다는 뜻이 아니다. `interfaces/ArchitecturePorts.cs`는 구현용 계약 스케치이며 실제 runtime 구현이나 컴파일 결과가 아니다.

GitHub는 고정 SHA의 일부 디렉터리·파일을 읽었다. 전체 clone·기존 CI 재실행·이슈 수정·커밋·배포·담당 변경은 하지 않았다. 원문 자료와 무료 에셋 목록은 이전 확인 수준을 유지했으며 새 모델 원본을 취득한 것으로 계산하지 않았다.

## 재현

```sh
python -m pip install -r requirements.txt
python -m unittest discover -s tests -v
python tools/context.py EP04
```

환경 상세와 개수는 `verification.json`에 있다. 이 검증 기록은 AAA 인증이나 디지털 트윈 현실 정확도 보증을 발급하지 않는다.
