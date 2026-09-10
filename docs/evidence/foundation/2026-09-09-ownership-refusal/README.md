# 소유권 거부 경로 Unity 실검증 — 2026-09-09

기준 SHA: `8090f8a8732d76873e622e4c66347ee17e9dcc8c` (브랜치 `bugfix/110-ownership-test-teardown`)
기기: `laptop-A` · Unity 6000.3.23f1 · 실행 2026-09-10 10:47:01~10:53:18 (UTC+09:00)

[PR #108 적대적 리뷰](https://github.com/xrlab-dau/CHOOGuard/pull/108)와
[PR #110 증거 요청](https://github.com/xrlab-dau/CHOOGuard/pull/110#issuecomment-5599068869)이
수용 조건으로 열거한 항목을 실제 Unity 배치 실행으로 채운 기록이다.
판정과 집계는 [`receipt.md`](receipt.md), 단계별 명령·종료 코드·시각·해시는
[`receipt.json`](receipt.json)에 있다.

**이 기록은 승인이 아니다.** 한 기기의 1회 실행 결과이며, 작성자 자체 보고는 독립 검토를
대신하지 않는다. 아래 미실행 항목은 통과로 계산하지 않는다.

## 미실행으로 남은 항목

`WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting`의 **Windows 지원 모듈이 없는
조건**은 실행하지 않았다. `laptop-A`는 해당 모듈이 설치된 상태이고, 조건을 만들려면 별도 기기
또는 모듈 미설치 Editor가 필요하다. Editor 설치를 변형해 조건을 위조하지 않았다.

#110이 고친 검사 순서 회귀의 핵심 조건이 이쪽이므로, 해당 항목은 여전히 열려 있다.

## 이 디렉터리에 있는 것

| 파일 | 내용 |
|---|---|
| `receipt.json` | 단계별 명령·종료 코드·시각·산출물 해시·정제 내역 |
| `receipt.md` | 사람이 읽는 판정 요약 |
| `editmode-results.xml` | EditMode NUnit3 결과 119건 |
| `playmode-results.xml` | PlayMode NUnit3 결과 16건 |
| `ownership-results.xml` | 소유권 거부 시험 단독 실행 결과 |
| `SHA256SUMS.txt` | 정제된 산출물 **12개**의 SHA256. manifest 자신과 이 README는 목록에 없다 |

manifest 12개는 **여기 커밋된 5개**(`receipt.json`, `receipt.md`, XML 3개)와 **커밋하지 않은
7개**(로그 6개 + diff 1개)로 나뉜다. 따라서 이 디렉터리에서 `sha256sum -c SHA256SUMS.txt`를
실행하면 커밋된 5개가 `OK`로 나오고 나머지 7개는 `could not be read`로 보고된다. 전체 성공을
주장하지 않는다.

## 이 디렉터리에 없는 것

Editor 로그 6종(`compile`, `editmode`, `playmode`, `ownership`, `build-negative`,
`build-positive`)과 재생성 diff는 크기 때문에 Git에 넣지 않았다(`editmode.log` 약 1.2 MB,
`regenerated.diff` 약 4.1 MB). 해시는 `SHA256SUMS.txt`에 그대로 있으므로, 같은 SHA에서
[`scripts/dev/verify_unity_receipt.ps1`](../../../../scripts/dev/verify_unity_receipt.ps1)을
다시 실행해 대조할 수 있다. 산출물은 모두 게시 전 정제를 거쳤다. 결과 XML은 텍스트 치환이 아니라 XML 트리를 통해 정제하고,
기록 전에 다시 파싱해 집계·case 이름·제외 사유가 보존되는지 확인한다.

## 재현

```powershell
powershell -File scripts/dev/verify_unity_receipt.ps1 -Sha 8090f8a8732d76873e622e4c66347ee17e9dcc8c
```

씬 생성기가 바이트 단위로 재현되지 않으므로 재생성 diff의 해시는 실행마다 달라진다
(`FoundationDemo.unity`의 `fileID` 변경). 판정에 쓰는 값은 종료 코드와 NUnit 집계다.
