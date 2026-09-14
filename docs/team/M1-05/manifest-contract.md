# M1-05 실행 기록과 공개 후보 계약 v1

이 도구는 입력·출력·도구·편집·시험·실패·재시도·취소·검토·승인·정제 사건을 기록하고, 원본을 보존한 채 정제본과 공개 검토용 요약을 만듭니다. #21의 **candidate 구현**입니다. PM의 최종 수용, #16 정책 수용, #46 하네스 통합·검증은 별도입니다.

## 상태와 보관 위치

| 산출물 | 위치와 상태 | 보존하는 정보 |
|---|---|---|
| 입력 JSON | Git checkout 밖, `raw` | 분류된 원본 사건과 내용. 도구는 수정·삭제하지 않습니다. |
| `redacted.json` | 별도 비공개 디렉터리, `redacted_private` | 원본 전체 SHA-256, 사건 순서·종류·결과, 허용된 텍스트 또는 제거 사유·보관 정책 |
| `events.json` | 새로운 후보 디렉터리, `public_candidate` | 열거형 사건 종류·결과, 순번, 제거 표시만 포함. 자유 텍스트·원본 경로·원본 hash는 포함하지 않습니다. |
| `manifest.json` | 후보 디렉터리, `public_manifest` | `events.json`의 버전·바이트 수·SHA-256 및 파일 목록 digest |
| 외부 승인 기록 | 별도 비공개 기록, `approval_record` | 정확한 manifest 바이트의 SHA-256, `unknown/pending/approved/rejected`, 외부 결정 참조 |

생성과 검증은 모두 `approval: pending`만 반환합니다. `kind: approval`인 원본 사건도 승인을 부여하지 않습니다. 승인 기록의 스키마는 상태 표현을 정의할 뿐, 사람의 신원·승인 권한을 인증하지 않습니다. 공개는 승인된 별도 호출자가 수행합니다. 이 도구는 네트워크 호출이나 게시 기능이 없습니다.

## 입력과 실행

Python 3.12 이상, 표준 라이브러리만 사용합니다. 최소 합성 입력은 다음과 같습니다. 실제 원본은 접근 통제된 checkout 밖 파일에 보관하세요.

```json
{
  "schemaVersion": 1,
  "state": "raw",
  "events": [
    {"kind": "test", "outcome": "failed", "classification": "public", "content": "Synthetic fixture failed", "requiredForEvidence": false},
    {"kind": "tool", "outcome": "recorded", "classification": "secret", "content": "synthetic-secret-fixture", "requiredForEvidence": false}
  ]
}
```

```sh
python scripts/team/M1-05/record_pipeline.py build --raw <private-raw.json> --private-output <new-private-run> --public-output <new-candidate-run>
python scripts/team/M1-05/record_pipeline.py verify --public-output <candidate-run>
python scripts/team/M1-05/test_redaction.py
python scripts/dev/test_native_manifest.py
```

부모 디렉터리는 이미 존재해야 하며 실행자가 독점 사용하는 로컬 디렉터리여야 합니다. 원본·비공개 출력·공개 출력은 서로 포함하지 않는 경로여야 합니다. 원본과 비공개 출력이 이 저장소 안이면 거부합니다. 네트워크 경로·심볼릭 링크·Windows junction은 지원하지 않습니다. 쓰는 동안 다른 프로세스가 부모 경로를 바꾸는 환경의 안전성을 보장하는 파일시스템 샌드박스는 아닙니다.

정상 생성/검증 종료 코드는 0입니다. 분류·스키마·hash·경로·I/O 실패는 nonzero이며 CLI 오류에 원본 내용이나 개인 경로를 출력하지 않습니다. 출력 디렉터리가 있으면 빈 디렉터리도 재사용하지 않습니다. 출력 파일은 배타적 생성 모드로 쓰고 manifest를 마지막에 만듭니다. I/O 중단 시 일부 산출물은 실패 근거로 남기며, 다음 실행에는 새 디렉터리를 사용합니다.

## 제거와 과잉 제거

`classification`은 `public`, `secret`, `personal`, `absolute_path`, `restricted`, `unknown`입니다. 비밀·개인정보·절대경로·제한된 자료로 분류된 내용은 필드 전체를 제거합니다. 흔한 credential·이메일·경로 패턴이 `public` 내용에서 관측되면 `suspected_sensitive`로 제거합니다. 이 패턴은 완전한 민감정보 탐지기가 아니므로 정제본도 비공개로 유지합니다.

모든 제거에는 사건 순번, 이유, `M1-05/raw-retention-v1` 정책 참조가 남습니다. 정책은 **원본을 기존 접근 통제된 보관 위치에 그대로 두고, 개별 값·파일명·경로·제한 자료의 hash를 공개 tombstone에 넣지 않는다**는 뜻입니다. 보관 기간·보관 권한은 자료 보유자의 정책을 따르며 이 도구가 변경하지 않습니다.

공개 후보는 분류가 `public`이어도 자유 텍스트를 모두 제외합니다. 제외 수는 `publicTextOmitted`로 보고하여 의도적인 과잉 제거를 드러냅니다. 삭제 사유와 고정된 열거형 이외에 원본 유래 문자열을 공개 후보에 복사하지 않습니다. 후보의 순번은 원본 사건 ID·개인 식별자가 아닌 0부터의 위치입니다.

분류가 `unknown`이면 파일 생성 전에 멈춥니다. 원본 내용이 핵심 근거라 `requiredForEvidence: true`인 경우에도, 공개 요약이 그 내용을 잃으므로 멈춥니다. 분류를 임의로 낮추거나 필수 근거 플래그를 끄는 것이 해결 방법은 아닙니다. PM과 자료 보유자가 검토한 구조화 요약 계약으로 범위를 조정해야 합니다.

따라서 이 공개 후보는 사건의 구조적 목록입니다. 테스트 상세, 코드 정당성, 철도 절차, 실제 실행 또는 제품 수용을 증명하지 않습니다. 원본의 종류·결과 분류 자체가 정확한지도 생성기가 인증하지 않습니다.

## hash와 검증

비공개 연결은 원본 정확 바이트 → `redacted.json.rawSha256`입니다. 공개 연결은 `events.json` 정확 바이트 → `manifest.json.files`입니다. manifest는 자기 자신을 파일 목록에 넣지 않습니다. `filesDigest`는 UTF-8, 키 정렬, 두 칸 들여쓰기, 마지막 LF로 직렬화한 `files` 객체의 SHA-256입니다. 외부 승인 기록은 완성된 manifest 바이트를 별도로 hash하여 대상에 결속합니다.

검증기는 공개 디렉터리에 `events.json`과 `manifest.json`만 있는지, payload가 고정된 공개 스키마인지, 순서·제거 수·정책·파일 크기·digest가 일치하는지 확인합니다. 추가 파일, 경로 참조, 자기 hash, 승격된 승인 상태, 중복 JSON 키, 알 수 없는 필드를 거부합니다. payload와 manifest를 함께 다시 만든 공격자의 신원을 증명하는 전자서명은 아닙니다.

## 스키마와 인계

[record-schema.json](record-schema.json)은 raw/정제본/공개 후보/manifest/외부 승인 상태를 구분합니다. 스키마만 통과한 것은 semantic 검사나 공개 승인 통과가 아닙니다. 실행기는 순번, 파일 digest, 알 수 없는 분류, 필수 근거 유실 및 경로 조건을 추가로 검사합니다.

후행 #22는 typed 사건 목록을, #46은 분류·정제·승인 분리 계약을 참고할 수 있습니다. 공통 schema나 하네스에 자동 연결하지 않습니다. 입력 근거는 기준 커밋의 #21 packet과 `docs/choo-guard-execution-backlog-v1.md`의 M1-05, `docs/choo-guard-ai-native-pipeline-v1.md`의 추적성 원칙입니다. 원본/비공개 산출물과 로컬 경로는 PR에 첨부하지 않습니다.
