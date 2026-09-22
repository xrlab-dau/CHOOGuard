# 전체 설계도 인덱스

각 도면은 다른 질문을 다룬다. DOT가 렌더 정본이며 SVG/PNG는 그 출력이다. Mermaid는 연결 공유용 소스다. 모두 설계이고 현재 구현 완료의 지도는 아니다.

## A01-overview · 전체 시스템 — 운영 실험에서 훈련 대본까지

누가 어떤 경계를 통해 운영하고, 근거가 어디에서 생성되는가?

[SVG](A01-overview.svg) · [PNG](A01-overview.png) · [DOT](A01-overview.dot) · [Mermaid](A01-overview.mmd)

실선은 데이터·요청 흐름이다. AI와 워커에는 기관 승인 또는 운영 DB 직접 수정 권한이 없다.

## A02-deployment · 배포와 장애 격리 — 한 PC 우선

어떤 실행 프로세스·스레드가 어느 상태를 소유하는가?

[SVG](A02-deployment.svg) · [PNG](A02-deployment.png) · [DOT](A02-deployment.dot) · [Mermaid](A02-deployment.mmd)

프로세스 격리는 장애 범위를 줄이는 설계다. 완전한 OS 보안 sandbox를 자동 제공한다는 뜻은 아니다. 같은 run에 두 host를 붙이지 않는다.

## A03-dependencies · 컴파일 경계 — 기술 세부에서 도메인으로

의존성은 어느 방향으로 흐르는가?

[SVG](A03-dependencies.svg) · [PNG](A03-dependencies.png) · [DOT](A03-dependencies.dot) · [Mermaid](A03-dependencies.mmd)

반복되는 참조를 묶은 요약도다. BOOT의 전체 직접 참조와 15개 모듈의 정확한 dependency는 contracts/architecture.json에 있다. runtime 호출의 양방향성과 다르다.

## A04-command · 명령과 내구성 — 승인 전에 무엇이 확정되는가?

중복 요청·동시 예약·저장 실패에도 성공을 거짓으로 게시하지 않는가?

[SVG](A04-command.svg) · [PNG](A04-command.png) · [DOT](A04-command.dot) · [Mermaid](A04-command.mmd)

외부 worker 효과는 at-least-once + job/result dedup으로 처리한다. 네트워크 전체 exactly-once를 보장한다고 쓰지 않는다.

## A05-cosimulation · 다중 계산 — 일관된 경계만 게시

서로 다른 시간 간격의 모델을 어떤 순서로 결합하는가?

[SVG](A05-cosimulation.svg) · [PNG](A05-cosimulation.png) · [DOT](A05-cosimulation.dot) · [Mermaid](A05-cosimulation.mmd)

FDS는 기본 batch reference다. live step·임의 rollback을 가정하지 않는다. UI 60fps와 physics timestep은 다른 시간축이다.

## A06-branch · 분기와 비교 — 원본을 보존한 재실험

위치·시드뿐 아니라 실제 실행 상태 전체가 이어지는가?

[SVG](A06-branch.svg) · [PNG](A06-branch.png) · [DOT](A06-branch.dot) · [Mermaid](A06-branch.mmd)

EXACT_STATE와 bitwise 재현은 같은 개념이 아니다. 결과 동등성은 별도 numerical/semantic/statistical profile로 검사한다.

## A07-data · 데이터 관계 — 현실·가상 계획·실행·대본

어떤 사실이 어떤 revision과 근거에 연결되는가?

[SVG](A07-data.svg) · [PNG](A07-data.png) · [DOT](A07-data.dot) · [Mermaid](A07-data.mmd)

관계 개념도다. SQL 테이블/키의 상세는 database/schema.sql에 있다. Unity instanceID나 파일명은 영속 business ID가 아니다.

## A08-authoring · 매뉴얼·AI·대본 — 제안과 승인을 분리

LLM이 권한이나 실제 결과를 창작하지 않게 만드는 경계는?

[SVG](A08-authoring.svg) · [PNG](A08-authoring.png) · [DOT](A08-authoring.dot) · [Mermaid](A08-authoring.mmd)

기관 승인 필드는 AI output schema에 없다. 대본의 의미 수정은 관련 검토를 만료시키지만 원 실행 로그를 변경하지 않는다.

## A09-map · 현실 자료에서 Unity 맵까지

자료의 확인 수준과 계산·표현 형상은 어떻게 함께 관리되는가?

[SVG](A09-map.svg) · [PNG](A09-map.png) · [DOT](A09-map.dot) · [Mermaid](A09-map.mmd)

시각 LOD·지붕 숨김은 계산 상태를 바꾸지 않는다. 무료 모델의 이름·외형으로 실제 정원·물성·업무 능력을 생성하지 않는다.

## A10-delivery · 구현·검증 경로 — 하나의 PASS로 합치지 않는다

기존 EP를 어떤 순서로 연결하고 무엇을 별도로 검증하는가?

[SVG](A10-delivery.svg) · [PNG](A10-delivery.png) · [DOT](A10-delivery.dot) · [Mermaid](A10-delivery.mmd)

화살표는 산출물 소비 관계다. 생산 EP 전체 종료를 자동 선행으로 만들지 않는다. 문서·도구 시험은 실제 제품/현장 수용이 아니다.

