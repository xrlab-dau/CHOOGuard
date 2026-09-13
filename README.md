# PM 작업 지시와 팀원·LLM 실행

[현재 오케스트레이션 계약](docs/context/orchestration-contract.md) → 해당 이슈의 `work-orders/NNN.json` → 필요한 inputs/writes/checks만 읽습니다.

`node scripts/context/task_context.mjs brief --issue <number>`

PM이 우선순위·선행 산출물·인계를 조율합니다. 개인은 실제 착수 시에만 claim하며 학교 PC·장소·고사양 보유로 배정하지 않습니다. 아래 기존 기술 문서는 필요한 상세와 역사적 근거를 찾는 경로입니다.

---

# CHOOguard



Comprehensive Hazard Operational Optimizer Guard. 철도 비상대응훈련을 위한 XR 플랫폼이다.

**현재 실행 기준:** [PM 실행 계약](docs/context/pm-execution-contract.md)과 [이슈별 실행 그래프](docs/context/work-graph.json)를 먼저 읽는다. 아래 v1/v4 문서는 기존 프로토타입의 요구·아키텍처·운영 이력이며, 현재 멀티 목표나 작업 배정을 제한하지 않는다. v3는 현행 기준이 아니다.

- [요구사항 기준선 v1.1](docs/choo-guard-requirements-baseline-v1.md)
- [플랫폼 아키텍처 v4.2](docs/choo-guard-platform-architecture-v4.md)
- [AI-native 파이프라인 v1.2](docs/choo-guard-ai-native-pipeline-v1.md)
- [실행 백로그 v1.3](docs/choo-guard-execution-backlog-v1.md)

[팀원 소스 전달·시작 절차](docs/choo-guard-foundation-handoff.md)를 제공한다. [당시 코레일 질의서](docs/korail/CHOOGuard_KORAIL_개발질의서_A4_1p.docx)는 역사 자료이며 기관 회신을 공개·합성 기능 개발의 전역 선행조건으로 삼지 않는다.

현재 FPS 시뮬레이션 시제품은 [실행·모델·검증 안내](docs/choo-guard-fps-foundation-progress.md)를 따른다. 저장소 루트를 Unity 6000.3.23f1에서 바로 열 수 있다.

## 개발 준비 상태

현재 Foundation 목표는 **13구역·교관 포함20클라이언트·NPC100·동시사건2**의 공동 훈련이다. 기존 [싱글플레이 시제품](docs/choo-guard-foundation-v1.md)은 회귀 기준으로 보존한다. 2026-09-12 캡처에서 새 Native/Multiplayer 소스 일부는 로컬 미게시 상태였다. 팀은 #120의 실제 접근 가능한 ref와 [소스 가용성 기록](docs/context/source-availability.json)을 확인하며, 파일 존재를 전체 구현·수용으로 해석하지 않는다. 기존 시제품의 Editor 실행 경로는 [Demo 안내](Packages/com.xrlab.chooguard.foundation/Demo/README.md)에 있다.

작업 시작은 `node scripts/context/work_graph.mjs brief --issue <number>`로 이슈별 입력·쓰기 범위·검증을 확인한다. 기존 `python3 scripts/dev/check_foundation.py`는 합성 데이터 검사이며 C# 컴파일·장면·플레이·Windows·HMD 수용이 아니다. 실제 시험 조건을 충족하는 어떤 환경에서도 해당 검증을 수행할 수 있다.

[요구사항 발견](_bmad-output/planning-artifacts/requirements-discovery-v1.md)과 [변경 단위 명세](specs/README.md), [이전 검토 상태](docs/reviews/2026-09-06-workflow-status.json)는 당시 범위의 근거다. 현재 도구 실행·자료·검증 조건은 해당 이슈의 단계별 입력과 현재 승인 범위로 확인하며 과거 미결을 일반 개발의 전역 차단으로 확대하지 않는다.

작업 장소·특정 호스트·고사양 장비 소유로 팀을 배정하거나 차단하지 않는다. 실제 착수 시 자신의 브랜치·scope·lease를 기록하고, 같은 Editor/출력/공유 파일만 배타적으로 보호한다. Unity 연결은 [공식 CLI의 Editor MCP](docs/adr/0006-official-unity-editor-mcp.md)를 사용하며 버전·연결·시험 증거를 남긴다. [이전 학교 환경 절차](docs/choo-guard-school-pc-bootstrap-v1.md)는 OS별 참고 이력이지 현재 장소 제한이 아니다.

## 목표 아키텍처

공개자료와 합성·Native Blender/Unity 모델을 근거 수준에 맞춰 사용하며, 현재 목표는 실제 서버 권위·공유 상태·다층 이동·물리·음성·복구가 연결된 Desktop 공동 훈련이다. CV/SOTA 복원 재개는 현재 작업이 아니다. Higgsfield 활성/폐기 기록의 상충은 SC-01로 분리해 PM의 명시적 결정 전 신규 외부 생성만 보류한다. 실제 시설 정확성·기관 SOP·현장 효과·VR/HMD는 별도 후속 검증 범위다.

이번 MVP는 검수 전 점수·이수 판정 대신 규칙 기반 설명형 피드백을 제공한다. LLM은 개발 보조 수단이며 철도 절차·물리적 안전의 권위가 아니다.

## Git Flow

- feature, bugfix, chore 브랜치는 develop으로 PR을 보낸다. develop은 squash와 팀·CODEOWNER 승인·필수 검사를 요구한다.
- release, hotfix 브랜치는 main의 릴리스 절차를 따른다.
- main/develop 직접 푸시는 하지 않는다. 이슈·PR·공개 변경은 해당 작업의 명시적 승인 범위에서 수행하며, 생성 자체를 실행·자료·병합 승인으로 해석하지 않는다.
- 작업 브랜치 푸시나 문서 clone은 실행·자료 접근·모델 전송·병합 승인을 대신하지 않는다.

[기여 절차](CONTRIBUTING.md), [에이전트 규칙](AGENTS.md), [CI 안내](docs/ci.md)를 함께 읽는다.

## 데이터와 라이선스

철도 촬영 원본·모델 가중치·자격·Unity 라이선스·미승인 자산은 커밋하지 않는다. 외부 코딩 LLM·MCP·검색은 현행 자료 등급과 승인된 PUBLIC_SYNTHETIC 범위를 준수한다. 코딩 LLM의 이미지 입력과 외부 3D 생성 서비스의 자료 입력은 별도 정책이며, 이 문서는 자료 전송 권한을 확대하거나 SC-01을 임의로 해결하지 않는다. 외부 서비스별 권리·개인정보·기관 조건·약관·승인은 따로 확인한다. gitignore나 저장소 밖 경로는 자료 취급 승인·격리 증거가 아니다.

이 저장소는 공개되어 있지만 프로젝트 자체의 오픈소스 라이선스를 아직 부여하지 않는다. [고지](NOTICE.md)를 따른다. 제삼자 도구·vendored 파일은 별도 라이선스·재배포 고지 검토가 필요하다.

현재 Foundation 기본 플레이는 [상시 현장 운영·불시 사건·대응 복기](docs/choo-guard-open-world-training.md) 모드다. 훈련 종류를 고르는 대신 평상시 공간에 입장해 예상하지 못한 변화를 관측하고 대응한다. 현재는 실제 시설/매뉴얼 검증 전의 공개 사진 참고 합성 환경이다.

프로젝트 문맥은 [Context graph](docs/context/README.md)에서 결정·코드·근거·미확인 항목의 관계로 확인한다. PC나 작업자가 바뀌면 해당 시작 명령으로 필요한 문맥과 근거의 변경 여부를 먼저 확인한다.
