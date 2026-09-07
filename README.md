# CHOOguard



Comprehensive Hazard Operational Optimizer Guard. 철도 비상대응훈련을 위한 XR 플랫폼이다.

**v3 폐기:** 저장소에서 v3 문서는 확인되지 않았다. v3는 현행 개발 기준이 아니며 아래 승인 기준선을 따른다.

- [요구사항 기준선 v1.1](docs/choo-guard-requirements-baseline-v1.md)
- [플랫폼 아키텍처 v4.2](docs/choo-guard-platform-architecture-v4.md)
- [AI-native 파이프라인 v1.2](docs/choo-guard-ai-native-pipeline-v1.md)
- [실행 백로그 v1.3](docs/choo-guard-execution-backlog-v1.md)

[팀원 소스 전달·시작 절차](docs/choo-guard-foundation-handoff.md)와 [코레일 A4 Word 질의서](docs/korail/CHOOGuard_KORAIL_개발질의서_A4_1p.docx)를 제공한다.

현재 FPS 시뮬레이션 시제품은 [실행·모델·검증 안내](docs/choo-guard-fps-foundation-progress.md)를 따른다. 저장소 루트를 Unity 6000.3.23f1에서 바로 열 수 있다.

## 개발 준비 상태

현재 우선순위는 [Foundation: MVP의 MVP](docs/choo-guard-foundation-v1.md)다. 임시 역사형 맵·3D 소품·이동·상호작용·임시 비상대응 시나리오를 연결한 싱글플레이 3D 게임을 만든다. KORAIL 자료 없이 플레이 경로를 구축하며 공통 코드·검사만으로 완료하지 않는다. 로컬 또는 학교 Unity의 `CHOOguard → Foundation → Build Playable Demo` 실행 경로를 [Demo 안내](Packages/com.xrlab.chooguard.foundation/Demo/README.md)에 기록한다.

로컬 시작 명령은 `python3 scripts/dev/check_foundation.py`다. 이 명령은 합성 데이터를 검사한다. C# 컴파일·Unity 장면·플레이 검증은 Editor에서 별도로 실행하고, Windows 실행본·HMD 검증은 학교 PC에서 수행한다.

[요구사항 발견](_bmad-output/planning-artifacts/requirements-discovery-v1.md)과 [변경 단위 명세](specs/README.md)는 검토용 초안이다. [검토 상태](docs/reviews/2026-09-06-workflow-status.json)의 미결을 해결하기 전 Pi·MCP·조사 하네스 운영 실행을 승인하지 않는다.

2026-09-06 사용자 지시로 local의 Unity Hub·Apple Silicon용 안정 LTS Editor 설치와 작은 합성 맵·기본 도형·Desktop 게임 개발 및 테스트를 허용한다. 무거운 모델링·렌더·베이크, Windows 실행본·HMD 검증은 school-pc에서 수행한다. unity-mcp 연결은 기존 공급망·실행 조건을 유지한다. [학교 PC 절차](docs/choo-guard-school-pc-bootstrap-v1.md)는 학교 환경의 전달·사람 검사·설치 및 실행 승인을 구분한다.

## 목표 아키텍처

서면 승인된 촬영물을 내부 DA3·Open3D 처리로 SceneBundle과 충돌 프록시로 변환하고 Unity OpenXR PC VR 및 Desktop 훈련에 사용한다. 촬영 승인 전에는 합성 맵만 사용한다.

이번 MVP는 검수 전 점수·이수 판정 대신 규칙 기반 설명형 피드백을 제공한다. LLM은 개발 보조 수단이며 철도 절차·물리적 안전의 권위가 아니다.

## Git Flow

- feature, bugfix, chore 브랜치는 develop으로 PR을 보낸다. develop은 squash와 팀·CODEOWNER 승인·필수 검사를 요구한다.
- release, hotfix 브랜치는 main의 릴리스 절차를 따른다.
- main/develop 직접 푸시와 에이전트의 자동 이슈·PR 생성은 하지 않는다.
- 작업 브랜치 푸시나 문서 clone은 실행·자료 접근·모델 전송·병합 승인을 대신하지 않는다.

[기여 절차](CONTRIBUTING.md), [에이전트 규칙](AGENTS.md), [CI 안내](docs/ci.md)를 함께 읽는다.

## 데이터와 라이선스

철도 촬영 원본·모델 가중치·자격·Unity 라이선스·미승인 재구성 자산은 커밋하지 않는다. 외부 모델·MCP·검색에는 별도 승인된 PUBLIC_SYNTHETIC만 전송한다. gitignore와 저장소 밖 경로는 자료 취급 승인이나 격리 증거가 아니다.

이 저장소는 공개되어 있지만 프로젝트 자체의 오픈소스 라이선스를 아직 부여하지 않는다. [고지](NOTICE.md)를 따른다. 제삼자 도구·vendored 파일은 별도 라이선스·재배포 고지 검토가 필요하다.

현재 Foundation 기본 플레이는 [상시 현장 운영·불시 사건·대응 복기](docs/choo-guard-open-world-training.md) 모드다. 훈련 종류를 고르는 대신 평상시 공간에 입장해 예상하지 못한 변화를 관측하고 대응한다. 현재는 실제 시설/매뉴얼 검증 전의 flat 합성 환경이다.
