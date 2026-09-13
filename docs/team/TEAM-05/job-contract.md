# TEAM-05 소재 처리 job 계약

2026-09-13 작성 · 이슈 [#73](https://github.com/xrlab-dau/CHOOGuard/issues/73) · candidate 후보

합성 파일로 job의 입력·출력 경계, 중단·재개, 중복 실행 거부, hash 검증, 원본 보존을 재현한 계약이다. **실제 소재 취득·처리 성능·기기별 용량 수용을 주장하지 않는다.** 완료 판정은 PM이 한다.

구현은 [`scripts/team/TEAM-05/job_fixture.py`](../../../scripts/team/TEAM-05/job_fixture.py), 시험은 [`test_job_fixture.py`](../../../scripts/team/TEAM-05/test_job_fixture.py)다. 표준 라이브러리만 사용하며 네트워크·Unity·Blender를 실행하지 않는다.

## 1. 저장 경계

job은 네 개의 루트를 분리해 사용한다. 한 루트의 역할을 다른 루트가 대신하지 않는다.

| 루트 | 역할 | job의 쓰기 |
|---|---|---|
| `source` | 입력 원본 | **하지 않는다.** 읽기만 한다 |
| `cache` | job별 중간 작업 공간 | 한다 (`cache/<jobId>/`) |
| `work` | job별 영수증과 lock 관측 | 한다 (`work/<jobId>/`) |
| `candidate` | 공개 후보 산출물 | 한다. 단 기존 파일은 절대 바꾸지 않는다 |

입력 이름은 이식 가능한 상대 경로만 허용한다. 절대 경로, `..`, 역슬래시, 드라이브 문자(`:`), `.git`, 심볼릭 링크가 낀 경로는 거부한다. 이 규칙은 `scripts/dev/native_manifest.py`의 `contained_file`과 같은 기준이다.

## 2. job 영수증

`work/<jobId>/receipt.json`에 기록한다. 갱신은 임시 파일에 쓴 뒤 `os.replace`로 교체하므로 기존 영수증이 중간 상태로 남지 않는다.

| 필드 | 의미 |
|---|---|
| `jobId` | job 식별자 |
| `state` | `interrupted` 또는 `completed` |
| `inputs` | 입력별 `sha256`·`bytes` |
| `inputsDigest` | 입력 목록 전체를 정렬·정규화해 계산한 sha256 |
| `outputs` | 산출물별 `sha256`·`bytes` |
| `completedInputs` | 지금까지 완료한 입력 이름 |
| `budget` | `plannedBytes`·`budgetBytes`·`freeBytesBefore`·정책 문구 |
| `limits` | 이 계약이 주장하지 않는 범위 |

## 3. 재개 계약

- 첫 실행은 `state`가 `interrupted` 또는 `completed`인 영수증을 만든다.
- 재개는 `--resume`으로만 한다. 영수증이 없으면 `NO_RESUMABLE_RECEIPT`로 거부한다.
- 재개 시 현재 입력을 다시 해시해 `inputsDigest`와 대조한다. 다르면 `INPUT_DIGEST_CHANGED`로 거부하고 **아무것도 쓰지 않는다.**
- 재개는 `completedInputs`에 없는 입력만 처리한다. 이미 만든 산출물은 다시 만들지 않는다.
- `completed` 상태의 job을 다시 실행하면 `JOB_ALREADY_COMPLETED`로 거부한다.
- `work/<jobId>/job.lock`이 있으면 `JOB_LOCK_HELD`로 거부한다. **fixture는 lock을 만들지도 지우지도 않는다.** lock은 다른 writer가 두는 관측 대상이며, 해제는 사람이 판단한다.

## 4. 거부 코드

모든 거부는 짧은 대문자 코드로만 보고한다. 절대 경로·자격 증명·파일 내용을 메시지에 넣지 않는다.

| 코드 | 조건 |
|---|---|
| `INVALID_PORTABLE_PATH` | 경로 이탈·절대 경로·역슬래시·`:`·`.git` |
| `LINKED_INPUT` | 입력 경로에 심볼릭 링크가 있음 |
| `MISSING_INPUT` / `NO_INPUT` | 선언한 입력이 없거나 목록이 비어 있음 |
| `BUDGET_NOT_SET` / `INVALID_BUDGET` | 예산 미지정 또는 양의 정수가 아님 |
| `BUDGET_EXCEEDED` | 계획 용량이 예산을 초과 |
| `OUTPUT_EXISTS` | 후보 산출물이 이미 있음 |
| `JOB_ALREADY_COMPLETED` | 완료된 job의 중복 실행 |
| `NO_RESUMABLE_RECEIPT` | 선행 실행 없이 재개 시도 |
| `INPUT_DIGEST_CHANGED` | 재개 시 입력 바이트가 바뀜 |
| `JOB_LOCK_HELD` | 다른 writer의 lock이 있음 |
| `MALFORMED_RECEIPT` | 영수증이 JSON이 아니거나 스키마가 다름 |

CLI 종료 코드는 `0` 정상(완료 또는 요청한 중단), `1` 계약 위반으로 거부, `2` 잘못된 호출·입출력 실패다.

## 5. 용량 기록과 삭제 금지

- 실행 전 계획 용량과 남은 디스크 공간을 관측해 영수증에 남긴다.
- 예산을 지정하지 않으면 시작하지 않는다. 계획 용량이 예산을 넘으면 **후보 디렉터리를 만들기 전에** 거부한다.
- **자동 삭제·정리를 하지 않는다.** 거부된 실행도 기존 산출물과 영수증을 그대로 둔다. 정리는 사람이 판단한다. 시험이 모듈 소스에 `shutil.rmtree`·`os.remove`·`unlink(`·`os.rmdir`가 없는지 직접 검사한다.

## 6. 실행

```sh
python scripts/team/TEAM-05/job_fixture.py \
  --source <SOURCE> --cache <CACHE> --work <WORK> --candidate <CANDIDATE> \
  --job-id job-01 --budget-bytes 1048576 --input a.bin nested/b.bin

python -m unittest discover -s scripts/team/TEAM-05 -p 'test_*.py'
```

## 7. 검증 결과 (2026-09-13)

`python -m unittest discover -s scripts/team/TEAM-05 -p 'test_*.py'` → **16개 중 15개 통과, 1개 skip, 실패 0**.

수용 기준별로 정상 경로와 거부 경로를 함께 검사한다. 저장 경계 분리와 원본 불변, 영수증의 입력·출력 digest 결속, 중단→재개→완료, 중복 실행 거부, lock 보유 시 거부, 경로 이탈 5종 거부, 재개 시 입력 변경 거부, 기존 출력 보존, 예산 미지정·초과 거부, 용량·잔여 공간 기록, 거부 후 부분 산출물 보존, CLI 종료 코드 분기, 삭제 경로 부재를 포함한다.

skip 1건은 `test_symlinked_input_is_refused`다. 이 계정에서 Windows 심볼릭 링크를 만들 수 없어(`OSError`) 시험을 건너뛴다. 심볼릭 링크 거부 자체는 구현돼 있으나 **이 환경에서는 실행으로 확인하지 못했다.** 링크 생성이 가능한 환경에서 재확인이 필요하다.

## 8. 한계

- 합성 파일 기반 계약이다. 실제 소재·대용량 자산·CV 모델 캐시를 다루지 않는다.
- 동시 writer 차단은 lock 파일 관측까지다. 분산 잠금이나 프로세스 강제 종료를 구현하지 않는다.
- 측정한 용량·잔여 공간은 실행 시점의 관측값이며 기기별 용량 수용 판정이 아니다.
- 특정 기기·장소를 전제하지 않는다. 어떤 적합한 환경에서도 위 시험을 실행할 수 있다.

## 9. 후행 인계

`#75`(소재 취득·파생본 생성)와 `#85`(33종 렌더 후보)에 실제 입력·출력 경계와 재개·거부 코드를 넘긴다. 두 작업은 이 계약의 루트 분리와 거부 코드를 그대로 사용하고, 실제 용량은 각자 pilot로 관측해 기록한다.
