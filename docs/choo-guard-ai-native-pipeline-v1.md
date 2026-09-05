# choo guard AI-native 개발 파이프라인 v1.2

- 작성일: 2026-09-05
- 상태: 설계 기준선
- 중심 코딩 에이전트: [earendil-works/pi](https://github.com/earendil-works/pi)
- 적용 원칙: Pi 중심 허브 + 사람별 전문 에이전트 + 결정론적 검증 + 독립 검토

## 1. 목표

이 파이프라인은 “AI가 코드를 많이 작성한다”는 의미의 AI-native가 아니다. 요구사항, 조사, 설계, 구현, Unity 조작, 테스트, 검토와 증거 수집이 하나의 추적 가능한 작업 흐름으로 연결되어야 한다.

핵심 원칙은 다음과 같다.

1. Pi가 중심 허브와 주 코딩 에이전트를 맡는다.
2. 팀원마다 담당 Work Package와 전용 에이전트 세션을 갖는다.
3. 한 Unity 직렬화 자산에는 동시에 한 에이전트만 쓰기 권한을 갖는다.
4. 에이전트의 완료 주장은 자동 테스트와 독립 검토를 대체하지 못한다.
5. 독립 검토 에이전트의 반대가 해소되기 전에는 완료 처리하지 않는다.
6. 모델은 기본값을 유지하고, 모델을 변경할 때 사용자가 선택한다.
7. SOTA 도구는 매주 바꾸지 않는다. 현재 도구가 실패·병목을 만들 때만 종합 게이트로 재평가한다.
8. 정제된 전체 실행 기록을 공개 저장소에 남긴다.

## 2. SOTA 종합평가 기준

초기 후보와 교체 후보는 다음 가중치로 5점 척도 평가한다.

| 항목 | 가중치 | 확인 내용 |
|---|---:|---|
| 작업 성능 | 25 | 실제 choo guard 대표 과제 성공률, 수정 횟수, 소요 시간 |
| Unity·3D 통합 | 25 | Unity Editor, C#, 씬·프리팹, XR, Blender·맵 도구 연결성 |
| 재현·검증 | 20 | 세션·도구 기록, 테스트 영수증, 되돌리기, 독립 검증 용이성 |
| 보안·라이선스 | 20 | 권한 제한, 샌드박스, 민감자료 처리, OSS·모델·에셋 라이선스 |
| 활동성·비용 | 10 | 유지보수 상태, 릴리스 안정성, 사용 비용, 팀 학습비용 |

교체 조건은 다음 모두를 만족해야 한다.

- choo guard 대표 작업에서 현 도구보다 유의미하게 우수
- 유지보수·보안·라이선스 검토 통과
- 기존 세션·도구·로그 형식의 마이그레이션 가능
- 재현 가능한 비교 기록 보존
- 제한 라이선스 또는 유료 변경이면 사전 승인

## 3. 선택한 단계별 스택

| 단계 | 주 도구·에이전트 | 역할 | 채택 상태 |
|---|---|---|---|
| 요구사항 발견 | [BMAD Method](https://github.com/bmad-code-org/bmad-method) + 소크라테스 인터뷰 | 가정 노출, 이해관계자·목표·비목표·미결정 분리 | 채택 |
| 요구사항 품질 | [GitHub Spec Kit](https://github.com/github/spec-kit) | 명확성·완전성·일관성 체크리스트, 변경 단위 명세 | 채택 |
| 웹·기술 조사 | [Pydantic AI Harness](https://github.com/pydantic/pydantic-ai-harness) Researcher, 필요 시 [GPT-Researcher](https://github.com/assafelovic/gpt-researcher) 교차검증 | 구조화된 주장·근거·URL·한계 수집 | 조건부 채택 |
| 아키텍처 대안 검토 | [LangGraph](https://github.com/langchain-ai/langgraph) + [DeepAgents](https://github.com/langchain-ai/deepagents) 또는 Pi의 독립 에이전트 체인 | 제약 추출, 대안·위협·비용 검토, ADR 작성 | 복잡한 결정에만 사용 |
| 주 코딩·통합 | [Pi](https://github.com/earendil-works/pi) | C#, 도구 코드, 테스트, 문서, Git 작업, 모델 전환 | 채택 |
| Pi-MCP 연결 | [pi-mcp-adapter](https://pi.dev/packages/pi-mcp-adapter?name=earendil-works) | Pi가 MCP 도구를 지연 탐색·호출하도록 연결 | 공급망 검토 후 채택 |
| Unity 저작 | [CoplayDev/unity-mcp v10.2.0](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.2.0) | 씬·오브젝트·스크립트·프리팹·테스트·프로파일링 | 고정 버전 채택 |
| 3D 보조 저작 | [Blender MCP](https://projects.blender.org/lab/blender_mcp) + glTF/glTFast | Flat Art 메시·LOD·피벗·내보내기 | 격리 작업공간에서만 사용 |
| 결정론적 검증 | Unity Test Framework + [GameCI test runner v4](https://game.ci/docs/github/test-runner) | EditMode·PlayMode·Standalone 테스트와 XML 결과 | Unity 프로젝트 생성 후 활성화 |
| 독립 코드 검토 | Codex CLI 또는 Gemini CLI의 읽기 전용 세션, Pi와 다른 모델 제공자 | 요구사항 위반·테스트 공백·보안·회귀 검토 | 프로젝트 대표 과제로 비교 후 1개 선정 |
| 에이전트 평가 | [Inspect AI](https://inspect.aisi.org.uk/agents.html) | 실행 한도, 궤적·모델·결과 기록, 회귀 평가 | 에이전트 평가셋 생성 후 도입 |
| 보안 평가 | Promptfoo·garak | 제품에 LLM 런타임이 생길 때 프롬프트·도구 공격 점검 | 현재 제품 런타임에는 LLM이 없어 보류 |

### 선정 해석

- Pi는 세션·모델·확장성은 강하지만 기본 MCP, 기본 서브에이전트, 기본 승인창과 내장 샌드박스가 없다.
- 따라서 Pi를 그대로 무제한 실행하지 않고 MCP 어댑터, 권한 게이트, 보호 경로, 샌드박스와 외부 검증기를 조합한다.
- Unity MCP는 저작 인터페이스다. 합격 판정은 Unity Test Framework와 독립 검증 명령이 담당한다.
- 특정 단계에 다른 도구가 더 적합해도 Pi가 작업 계약, 입력·출력, 검증 결과를 연결하는 중심 허브로 남는다.

## 4. 사람·에이전트 매핑

PM 중심 통합 원칙에 따라 핵심 제품 계약과 병합 책임은 엄윤상이 맡고, 나머지 구성원은 독립 가능한 패키지를 책임진다.

| 사람 책임자 | 전문 에이전트 | 사람의 최종 책임 | 에이전트 허용 작업 |
|---|---|---|---|
| 엄윤상 | Pi Orchestrator / Core Agent | 요구사항, 아키텍처, 공통 Quest Core, 통합, 최종 데모 | 계획 분해, C#·테스트 작성, 작업 계약 발행, 결과 통합 |
| 박효원 | XR & Capture Agent | 승인 촬영 계획, VR 입력·상호작용, HMD 실기 검증 | 촬영 체크리스트, XRI 구성, PlayMode 테스트, 기기 로그 정리 |
| 박지민 | Map Pipeline Agent | DA3·Open3D 처리, 맵 산출물, 공간 QA | 처리 스크립트, 메타데이터·해시, 메시 변환, 품질 리포트 |
| 이지현 | Flat Art & UI Agent | Flat Art 환경, 역할·피드백 UI, Desktop 조작 | Unity MCP·Blender 작업, 프리팹·UI·스크린샷·시각 회귀 |
| 고소현 | Scenario & Verification Agent | 역사 대피 흐름, 임시 5직무 행동, 사용자 검증 | Scenario 데이터, 테스트 케이스, 피드백 문구, 요구사항 추적 |

### 통합 규칙

- C# 독립 모듈은 Git worktree로 병렬 작업할 수 있다.
- 씬, 프리팹, 애니메이터, ProjectSettings는 자산별 단일 Writer Lease를 사용한다.
- 다른 에이전트는 동일 자산을 읽고 리뷰할 수 있으나 동시에 쓰지 않는다.
- 각 인계는 `입력 계약 / 출력 파일 / 테스트 / 알려진 제한 / 책임자`를 갖는다.
- PM이 핵심 통합을 맡지만, 각 팀원은 자신의 산출물과 검증 결과를 설명할 수 있어야 한다.

## 5. 실행 파이프라인

```text
요구 입력
  ↓
Socratic/BMAD 발견
  ↓
Spec Kit 명확성·충돌 검사
  ↓ G0: 요구사항 기준선
근거 조사 + 대안 비교
  ↓ G1: 출처·라이선스 검토
아키텍처/ADR + 독립 비판
  ↓ G2: 구현 계약
사람별 Pi 전문 세션 + worktree
  ↓
Unity MCP / CLI / DA3 / Blender 도구 실행
  ↓
작업별 검증 후 PM 통합
  ↓
통합 커밋 대상 Unity compile + EditMode + PlayMode + 시각·성능 증거
  ↓
정제된 공개 후보 파일 + 불변 target manifest 고정
  ↓
다른 제공자·모델의 읽기 전용 독립 리뷰
  ↓ G3: 반대 해소 및 target manifest 동일 해시 확인
  ↓ G4: 병합·배포 승인
승인된 공개 후보 + 별도 검토·승인 영수증 공개
```

### 단계별 필수 산출물

| 단계 | 필수 산출물 |
|---|---|
| 발견 | 확정 요구, 가정, 미결정, 비목표, 용어 정의 |
| 조사 | 주장, 근거 URL, 조회일, 제한, 상충 자료, 라이선스 |
| 설계 | ADR, 대안, 선택 이유, 롤백, 위협, 검증 계획 |
| 구현 | 작업 ID, 담당자, 모델, 브랜치, 변경 파일, 테스트 |
| Unity 저작 | 씬·프리팹 변경 목록, 스크린샷, Console 로그 |
| 맵 | 승인 ID, 입력 해시, 도구·모델 버전, 출력 해시, 라이선스 |
| 검증 | 테스트 XML, 실행 로그, 성능 측정, 영상·화면, 독립 리뷰 |
| 릴리스 | 승인자, 커밋, 빌드 해시, 알려진 제한, 자료 공개 범위 |

## 6. Pi 필수 확장 계층

초기 통제 구축은 PM이 비민감·합성 자료만 있는 제한 환경에서 수동으로 수행한다. M0-03에서 실제 선택한 실행 프로파일·버전·실행 계정·정책 해시와 확장/도구별 실행 위치를 고정한다. 해당 프로파일을 M1-02~04에서 검증하기 전에는 실제 역사 촬영물·KORAIL 문서·민감 화면을 Pi, 자식 에이전트, MCP 또는 외부 모델에 연결하지 않는다. 프로파일·버전·계정·정책 해시가 바뀌면 이전 검증 결과를 재사용하지 않는다.

### 6.1 강제 실행 경계

| 실행 주체 | 파일 권한 | 네트워크·모델 전송 | 강제 방법 |
|---|---|---|---|
| Pi 컨트롤러 | 프로젝트 정책·세션 메타데이터 읽기, 보호 정책 쓰기 금지 | 사용자가 선택한 모델 제공자만 | 정책 파일 읽기 전용·해시 검증 |
| Pi 파일·셸·자식 에이전트 | 전용 worktree만 읽기·쓰기, 그 밖의 사용자 홈과 KORAIL 경로는 마운트하지 않음 | 기본 차단, 승인된 개발 도메인만 허용 | M0-03에서 고정한 Gondolin/OpenShell 등 실제 프로파일과 경로 allowlist |
| Pi 호스트 확장 | 확장별 실제 실행 위치에서 허용 경로만 접근 | 허용 도메인·모델 전송만 | 호스트 직접 파일·네트워크 접근 시험. 샌드박스 위임 여부를 확장별로 manifest에 기록 |
| pi-mcp-adapter·Unity MCP | 합성·공개 Unity 작업공간만 접근, 도구 그룹 allowlist | 화면·이미지·도구 결과의 외부 모델 전달은 데이터 반출로 분류 | 고정 버전, 직접·프록시 호출 공통 승인 게이트 |
| Unity·Blender 호스트 실행자 | 지정 프로젝트·DCC staging만 쓰기, KORAIL 원본 경로 미연결 | 기본 외부 전송 금지 | 별도 실행 프로필·호스트 방화벽·MCP allowlist |
| DA3·Open3D 민감자료 처리자 | 승인된 오프라인 작업영역에서만 원본·중간 결과 접근 | 외부 모델·API 전송 금지 | 별도 작업영역과 승인된 반입·반출 절차 |
| Independent Verifier | 불변 통합 소스는 읽기 전용, Unity 실행 사본의 생성 폴더·빌드·임시 로그만 쓰기, 확정 증거는 append-only | 외부 업로드 금지 | 별도 실행 계정·컨테이너, 격리된 쓰기 가능 실행 사본, 보호된 증거 경로 |
| 독립 리뷰 에이전트 | 정제된 통합 diff·테스트 증거 읽기 전용 | 승인된 정제본만 다른 제공자에 전송 | 검토 manifest와 입력 해시 |

자료 등급은 최소 `PUBLIC_SYNTHETIC / TEAM_INTERNAL / KORAIL_RESTRICTED`로 나눈다. 외부 모델에는 `PUBLIC_SYNTHETIC`만 기본 허용하며, 그 밖의 자료·스크린샷·이미지·도구 출력은 명시적 자료 반출 승인 없이는 전송하지 않는다. 공개 정제와 모델 전송 승인은 서로 다른 게이트다.

Pi를 주 에이전트로 사용하기 전에 다음을 갖춰야 한다.

1. **Permission Gate**
   - 민감자료 접근·이동
   - 권한·보안 설정 변경
   - 제한 라이선스 도입
   - 병합·배포·삭제

2. **Protected Paths**
   - `.env`, 키·자격증명, KORAIL 자료 경로: 에이전트 작업영역에 마운트하지 않으며 읽기·쓰기를 모두 금지한다.
   - Unity `Library/`, `Temp/`, `Obj/`: 에이전트의 직접 쓰기를 금지하고 Unity 프로세스만 생성한다.
   - Unity `Logs/`: 작성 에이전트와 검증기가 읽을 수 있으나 기존 로그 수정·삭제는 금지한다.
   - 정책 파일·Writer Lease registry·검증 결과: PM/검증기만 쓰고 에이전트는 읽기 전용으로 사용하며 시작 시 해시를 확인한다.
   - `ProjectSettings/`와 릴리스 설정: 명시된 작업 계약 범위 밖의 쓰기를 차단한다.

3. **MCP Adapter**
   - `pi-mcp-adapter`의 소스·의존성·권한을 검토하고 버전을 고정한다.
   - Unity MCP 도구를 필요한 그룹만 노출한다.
   - 임의 C# 실행, 외부 생성 서비스, 패키지 설치 도구는 기본 비활성화한다.

4. **Agent Team Extension**
   - 각 에이전트는 별도 Pi 세션과 worktree를 사용한다.
   - 세션 시작 계약에는 `작업 ID / 기준 커밋 / 허용 경로 / 도구 allowlist / 모델·제공자 / 수용 기준 / Writer Lease`를 포함한다.
   - 세션 종료 시 patch·diff·테스트·실패·재시도·취소 결과를 회수한다. 실패·취소도 기록에서 제거하지 않는다.
   - Writer Lease는 `asset path / Unity project instance / writer / acquired_at / expires_at / base hash`를 기록한다. 비정상 종료 시 PM이 기존 hash를 확인한 뒤에만 회수·재발급한다.

5. **Independent Verifier**
   - 첫 Unity 작업을 Done으로 처리하기 전에 PM이 별도 실행 계정·컨테이너로 구축한다.
   - 검토 대상 통합 소스와 기준 해시는 불변으로 보존한다.
   - Unity 검증은 해당 입력으로 만든 격리된 실행 사본에서 수행하고, `Library/`, `Temp/`, `Obj/`, 빌드 출력과 임시 로그에 필요한 쓰기만 허용한다.
   - 실행 종료 후 결과를 보호된 append-only 증거 경로에 보존한다. 작성 에이전트는 확정 결과를 읽을 수 있지만 수정·삭제할 수 없다.
   - 기준 입력의 전후 해시가 같고 실제 Unity 검증이 성공했는지 확인한다.
   - 검토 대상 커밋·빌드·맵·테스트 결과의 해시를 불변 target manifest로 묶는다.
   - 검토 후 통합 산출물 또는 target manifest가 바뀌면 영향을 받은 테스트와 독립 검토를 자동 무효화한다.

6. **Trace Sanitizer**
   - 공개 전 토큰·키·이메일·로컬 절대경로·민감 파일명·촬영 메타데이터를 제거한다.
   - 제거 규칙과 검사 결과를 기록한다.

## 7. 승인 정책

### 자동 수행 가능

- 허용된 worktree 안의 코드·문서·테스트 작성
- 빌드·테스트·프로파일링·스크린샷
- 로컬 브랜치와 로컬 커밋
- 정제 전 실행 기록 생성
- 허용목록 라이선스의 기존 의존성 사용

### 사전 승인 필수

- KORAIL 자료 접근·이동·업로드·삭제
- 권한·보안·네트워크 설정 변경
- 비상업·출처불명·제한적 라이선스 모델·패키지·에셋 도입
- 보호 브랜치 병합, 릴리스, 외부 배포·게시
- 데이터 삭제·마이그레이션

### 검토 차단 규칙

독립 검토 에이전트가 반대하면 다음 중 하나가 충족될 때까지 완료할 수 없다.

- 지적 사항 수정 후 독립 재검토 통과
- 요구사항·테스트·근거로 반박하고 독립 재검토 통과
- 합의하지 못하면 사람 책임자가 수정 방향·요구사항 해석·변경안을 결정하고 근거를 기록한다. 그 결정이 반영된 산출물이 독립 재검토를 통과하기 전에는 Done으로 전환하지 않는다.

재검토 없이 사람의 위험 수용이나 사유 기록만으로 완료하는 예외는 두지 않는다.

## 8. 모델 선택 정책

- 기본 모델은 프로젝트 설정에 고정한다.
- 같은 모델을 계속 사용하는 동안 작업마다 재확인하지 않는다.
- 모델을 바꿀 때만 사용자가 제공자·모델·추론 수준을 선택한다.
- 변경 사유, 이전·새 모델, 비용·성능 차이를 기록한다.
- 독립 검토는 작성 에이전트와 다른 모델 제공자·모델을 반드시 사용하며, 실제 제공자·모델 ID를 검토 manifest에 기록한다.

## 9. 공개 실행 기록

공개 저장소에는 민감정보를 제거한 전체 기록을 남긴다.

```yaml
run_id:
parent_run_id:
work_item:
human_owner:
agent_role:
source_commit:
integrated_commit:
harness_version:
model_provider:
model_id:
reasoning_level:
prompt_hash:
policy_hash:
sanitation_policy_version:
events:
  - sequence:
    timestamp:
    type: prompt|tool_input|tool_output|edit|test|failure|retry|cancel|review|approval|redaction
    input_ref:
    output_ref:
    content_or_redaction_marker:
    status:
changed_files:
artifact_hashes:
test_receipts:
target_manifest_hash:
review_run_id:
review_provider:
review_model:
review_status:
review_receipt_hash:
approvals:
approval_receipt_hash:
public_release_manifest:
known_limits:
```

공개 전 검사 항목:

- API 키·토큰·쿠키·인증 헤더
- 이메일·전화번호·개인정보
- 로컬 사용자명과 절대경로
- KORAIL 자료명·위치·메타데이터
- 모델 입력에 포함된 비공개 원문
- 스크린샷의 알림·계정·파일 경로

정제로 제거한 사건은 흔적 자체를 삭제하지 않고 `redaction` 이벤트, 제거 사유와 원본 보관 정책을 남긴다. ‘정제 검사 통과’는 ‘외부 공개 승인’을 뜻하지 않는다. 독립 검토 전에는 실제 공개 후보 기록·맵·이미지·영상의 정확한 파일 목록과 해시를 불변 `target manifest`로 만든다. 독립 검토와 사람 승인은 이 target manifest의 해시를 참조하는 별도 append-only 영수증으로 보존하며, 영수증을 target manifest에 소급 삽입해 대상 해시를 바꾸지 않는다. 승인 후 게시하는 `public_release_manifest`는 target manifest, 검토 영수증, 승인 영수증의 해시를 참조한다. 각 영수증과 공개 manifest도 별도의 정제·공개 승인 범위를 따른다.

## 10. 근거와 주의사항

- Pi는 MIT 라이선스의 다중 모델·세션·확장형 harness지만, 기본적으로 실행 사용자 권한을 상속하며 MCP·서브에이전트·승인창을 기본 제공하지 않는다: [Pi README](https://github.com/earendil-works/pi), [Pi coding-agent README](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/README.md)
- Pi MCP 연결은 커뮤니티 확장을 사용할 수 있으나 Pi 패키지는 코드를 실행하므로 설치 전 소스 검토가 필요하다: [pi-mcp-adapter](https://pi.dev/packages/pi-mcp-adapter?name=earendil-works)
- CoplayDev Unity MCP v10.2.0은 MIT이며 Unity 2021.3 LTS~6.x에서 씬·에셋·스크립트·테스트·프로파일링 도구를 제공한다: [release](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.2.0), [tool catalog](https://coplaydev.github.io/unity-mcp/reference/tools)
- BMAD는 암묵적 가정을 명시적 결정으로 보존하는 AI-driven workflow를 제공한다: [BMAD Method](https://github.com/bmad-code-org/bmad-method)
- Spec Kit은 요구사항의 완전성·명확성·일관성을 검사하는 체크리스트를 지원한다: [Spec Kit](https://github.com/github/spec-kit)
- GameCI는 Unity EditMode·PlayMode·Standalone 테스트를 구분하며 Standalone은 명시적으로 실행해야 한다: [GameCI test runner](https://game.ci/docs/github/test-runner)
- Inspect AI는 에이전트 실행 한도, 개입, 체크포인트와 평가 로그를 제공한다: [Inspect AI agents](https://inspect.aisi.org.uk/agents.html)
