# 아키텍처 근거와 확인 범위

기준일 2026-09-19. WEB_PRIMARY_READ는 공식 문서의 열람이며 다운로드·설치·성능 검증이 아니다. 현재 기술 후보의 정확 버전은 구현 lock에서 정한다. 제공된 에셋/매뉴얼 조사 이력은 이 문서에서 재검증한 사실로 승격하지 않는다.

## SRC-PRD — PRD v11

[PRD v11](basis/PRD_v11.md)

확인: PROVIDED_DOCUMENT. 적용: 91개 요구·EP00~11의 제품 기준.

## SRC-UX — Unity Native UX v2

[Unity Native UX v2](basis/UNITY_UX_v2.md)

확인: PROVIDED_DOCUMENT. 적용: uGUI+TMP, S01~12, 입력 우선순위.

## SRC-CORE — Foundation README

[Foundation README](https://github.com/xrlab-dau/CHOOGuard/blob/aae4867c71c96f34f8fe8a252cddd9aa0412394f/Packages/com.xrlab.chooguard.foundation/README.md)

확인: CONNECTOR_READ. 적용: 기초 코어 설명; 전체 현행 구현 범위는 별도 소스와 대조.

## SRC-WORLD — WorldContracts.cs

[WorldContracts.cs](https://github.com/xrlab-dau/CHOOGuard/blob/aae4867c71c96f34f8fe8a252cddd9aa0412394f/Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/WorldContracts.cs)

확인: CONNECTOR_READ. 적용: WorldCommand, ObservedState, ICommitSink.

## SRC-AUTH — AuthoritativeShift.cs

[AuthoritativeShift.cs](https://github.com/xrlab-dau/CHOOGuard/blob/aae4867c71c96f34f8fe8a252cddd9aa0412394f/Packages/com.xrlab.chooguard.foundation/Runtime/Multiplayer/AuthoritativeShift.cs)

확인: CONNECTOR_READ_PARTIAL. 적용: 1~155행; 단일 소유 스레드, 거리 제약, 물리 게시.

## SRC-MANIFEST — Unity manifest

[Unity manifest](https://github.com/xrlab-dau/CHOOGuard/blob/aae4867c71c96f34f8fe8a252cddd9aa0412394f/Packages/manifest.json)

확인: CONNECTOR_READ. 적용: uGUI/InputSystem/Addressables 직접 선언 미발견; 설치 여부 전역 추론 금지.

## SRC-UNITYVER — Unity version

[Unity version](https://github.com/xrlab-dau/CHOOGuard/blob/aae4867c71c96f34f8fe8a252cddd9aa0412394f/ProjectSettings/ProjectVersion.txt)

확인: CONNECTOR_READ. 적용: 6000.3.23f1; 실제 컴파일 증거 아님.

## SRC-UUI — Unity UI comparison

[Unity UI comparison](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)

확인: WEB_PRIMARY_READ. 적용: native runtime framework 선택의 근거.

## SRC-UINPUT — Unity Input System UI support

[Unity Input System UI support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html)

확인: WEB_PRIMARY_READ. 적용: UI/game 입력 구분; 문서 버전은 설치 버전 아님.

## SRC-DOTNET — Unity .NET profile support

[Unity .NET profile support](https://docs.unity3d.com/kr/6000.0/Manual/dotnet-profile-support.html)

확인: WEB_PRIMARY_READ. 적용: Unity 관리 코드와 외부 .NET 프로세스 구분.

## SRC-C4 — C4 model diagrams

[C4 model diagrams](https://c4model.com/diagrams)

확인: WEB_PRIMARY_READ. 적용: context/container/component와 동작 뷰; 성숙한 표기법.

## SRC-EVENT — Event Sourcing pattern

[Event Sourcing pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)

확인: WEB_PRIMARY_READ. 적용: 원시 이벤트와 투영·복원, 적용 비용.

## SRC-SQLITE — SQLite WAL

[SQLite WAL](https://sqlite.org/wal.html)

확인: WEB_PRIMARY_READ. 적용: 동일 호스트·단일 writer·WAL 파일 보존·내구성.

## SRC-SQLITE-FIX — SQLite 3.51.3 release

[SQLite 3.51.3 release](https://sqlite.org/releaselog/3_51_3.html)

확인: WEB_PRIMARY_READ. 적용: 2026-03-13 WAL-reset 수정; 검증된 수정판 이상 필요.

## SRC-TAPAAL — TAPAAL features

[TAPAAL features](https://www.tapaal.net/features/)

확인: WEB_PRIMARY_READ. 적용: timed/colored/stochastic nets, 독립 검증 엔진.

## SRC-JPS — JuPedSim models

[JuPedSim models](https://www.jupedsim.org/stable/pedestrian_models/)

확인: WEB_PRIMARY_READ. 적용: 보행 모델 비교; 층간/운영 모델은 별도.

## SRC-SUMO — SUMO emergency

[SUMO emergency](https://sumo.dlr.de/docs/Simulation/Emergency.html)

확인: WEB_PRIMARY_READ. 적용: silent teleport 경고; 한국 현장 보정 필요.

## SRC-FDS — NIST FDS

[NIST FDS](https://pages.nist.gov/fds-smv/)

확인: WEB_PRIMARY_READ. 적용: 화재 열·연기 기준 계산, Evac 중단 범위.

## SRC-PHYSICS — NVIDIA PhysicsNeMo

[NVIDIA PhysicsNeMo](https://docs.nvidia.com/physicsnemo/latest/index.html)

확인: WEB_PRIMARY_READ. 적용: 신경 대체모델 훈련 프레임워크; 현장 모델 아님.

## SRC-MAP — MapAnything

[MapAnything](https://github.com/facebookresearch/map-anything)

확인: WEB_PRIMARY_READ. 적용: 학습 기반 metric 복원; 독립 정합 별도.

## SRC-FMI — FMI 3.0.2

[FMI 3.0.2](https://fmi-standard.org/docs/3.0.2/)

확인: WEB_PRIMARY_READ. 적용: 2024-11-27 표준; state restore 등 capability별.

## SRC-PROV — W3C PROV-O

[W3C PROV-O](https://www.w3.org/TR/prov-o/)

확인: WEB_PRIMARY_READ. 적용: 출처·실행·생성 결과 관계.

## SRC-SHACL — W3C SHACL

[W3C SHACL](https://www.w3.org/TR/shacl/)

확인: WEB_PRIMARY_READ. 적용: RDF 그래프 제약 검사; ontology 자체가 런타임 아님.

## SRC-OTEL — OpenTelemetry specification

[OpenTelemetry specification](https://opentelemetry.io/docs/specs/)

확인: WEB_PRIMARY_READ. 적용: 진단용 trace/metrics. 제품 증거 ledger와 분리.

## SRC-OWASP — OWASP LLM01:2025

[OWASP LLM01:2025](https://genai.owasp.org/llmrisk/llm01-prompt-injection/)

확인: WEB_PRIMARY_READ. 적용: 문서·도구출력 간접지시와 권한 격리.
