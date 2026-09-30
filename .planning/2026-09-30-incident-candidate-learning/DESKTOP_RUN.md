# 데스크톱에서 학습 데이터 모으기

노트북에서는 실험하지 않는다. 이 문서대로 하면 다른 PC 에서 JEV 판단 기록을 모을 수 있다.

## 0. 준비물

| | |
|---|---|
| Unity | **6000.3.23f1** (Hub 에서 같은 버전) |
| 저장소 | `develop` 최신 + 이 하네스가 든 브랜치 |
| JEV 키 | TypeSafe 콘솔에서 발급 |
| 디스크 | 회차당 JSONL 약 0.5 MB (10분 근무 기준) |

### JEV 키 넣기

`console.typesafe.ai/keys` 에서 만든 키를 **둘 중 하나**에 둔다. 환경변수가 우선이다.

- 환경변수 `TYPESAFE_API_KEY`
- 파일 `~/.chooguard/typesafe.key` (Windows: `%USERPROFILE%\.chooguard\typesafe.key`)

조건: 20자 이상, 안에 공백·줄바꿈 없음. 끝의 줄바꿈 하나는 코드가 다듬는다.
확인은 `GET https://api.typesafe.ai/v1/models` 로 한다(HTTP 200 이면 유효, 추론 과금 없음).

## 1. 돌리기

에디터를 **닫은 채로** 배치모드로 실행한다. 같은 프로젝트를 두 인스턴스가 못 연다.

```
CG_SHIFT_COUNT=5 CG_SHIFT_SECONDS=1200 CG_SHIFT_SCALE=1 CG_SHIFT_SEED=1000 \
CG_SHIFT_OUT="D:\cg-data" \
"<Unity>/Editor/Unity.exe" -batchmode -runTests \
  -projectPath "<저장소>" -testPlatform PlayMode \
  -testFilter "ChooGuard.Tests.PlayMode.ShiftSampleTests" \
  -testResults "D:\cg-data\shift.xml" -logFile "D:\cg-data\shift.log"
```

| 환경변수 | 뜻 | 기본 |
|---|---|---|
| `CG_SHIFT_COUNT` | 회차 수 | 1 |
| `CG_SHIFT_SECONDS` | 회차당 게임 시간(초) | 600 |
| `CG_SHIFT_SCALE` | 시간 압축 배수 | 1 |
| `CG_SHIFT_REALCAP` | 회차당 실시간 상한(초) | 1800 |
| `CG_SHIFT_SEED` | 첫 회차 시드(0=무작위). 회차마다 +1 | 0 |
| `CG_SHIFT_OUT` | 회차 요약 JSON 을 쓸 폴더 | 안 씀 |

시험은 `[Explicit]` 이라 `-testFilter` 로 직접 지정해야 돈다. 일반 회귀에는 섞이지 않는다.

## 2. 나오는 것

**정본은 JSONL 이다.** 시험 요약이 아니라 이쪽을 학습에 쓴다.

```
Windows: %USERPROFILE%\AppData\LocalLow\DefaultCompany\CHOOGuard\jev-runs\jev-<UTC>.jsonl
```

한 줄이 요청 하나다.

```json
{"at":"2026-09-30T06:31:02.1234567Z","purpose":"compose","http":200,"seconds":0.83,
 "model":"jev-1.13.0",
 "request":{"model":"jev-latest","state":{...},"questions":{"what_happens":{"type":"choice",
   "instructions":"...","criteria":{"overheat_37":"The power bank in the bag of ...", ...}}}},
 "answers":{"what_happens":{"Choice":"overheat_37","Score":0,"Confidence":0.72,
   "Probabilities":{"overheat_37":0.41, ...}}}}
```

`purpose` 로 갈린다.

| `purpose` | 무엇 |
|---|---|
| `compose` | 사건 합성 — 무엇이 일어날지 고르기 |
| `compose-magnitude` | 크기 점수 |
| `crowd` | 승객 일상 행동 판단 |

회차 요약(`CG_SHIFT_OUT`)은 회차당 한 줄로 `rounds·jevRounds·byJev·byLocal·requests·inputTokens` 등을 담는다.

## 3. 먼저 읽을 것 — 노트북에서 1회 측정한 결과

2026-09-30, 게임 600초 / 실시간 75초(8배속), 시드 1297710125.

```
rounds=25  jevRounds=1  byJev=1  byLocal=24
requests=156  failures=0  inputTokens=187,198  outputTokens=32,817
JSONL 156줄 · 477 KB
목적별: crowd 155건 · compose 1건
```

### 두 가지를 감안해야 한다

**① 시간 압축이 결과를 왜곡한다.** `JevClient` 의 분당 90건 상한은 **실시간** 기준이다.
8배속으로 돌리면 같은 실시간에 8배의 질의가 몰려 상한에 걸리고, 사건 합성이 로컬 규칙으로
떨어진다(위에서 25라운드 중 24회). **비율을 보려면 `CG_SHIFT_SCALE=1`.**
압축은 '총량이 얼마나 쌓이는가' 만 볼 때 쓴다.

**② 예산을 군중과 나눠 쓴다.** `CrowdDirector` 가 같은 `JevClient` 를 쓴다. 위 실행에서
요청의 **99.4%(155/156)가 승객 판단**이었다. 사건 합성 표본이 필요하면 회차를 길게 잡거나,
군중 질의를 줄이는 쪽을 따로 다뤄야 한다.

`JevClient` 의 상한은 코드에 있다 — 실행당 1,500건, 분당 90건, 동시 3건, 응답 대기 8초.
데스크톱에서 늘리려면 그 값을 고쳐야 하고, 그것은 별도 판단이다.

## 4. 비용

입력 $0.042 / 1M 토큰, 출력 무료.

```
10분 근무(8배속)  187k 토큰   $0.008
30분 근무 추정     561k 토큰   $0.024
```

1배속·긴 회차에서는 달라진다. **첫 실행은 `CG_SHIFT_COUNT=1` 로 재고 그 값으로 예산을 잡는다.**

## 5. 하지 말 것

- **에디터를 열어 둔 채 배치모드 실행.** 프로젝트 락이 걸려 즉시 실패한다.
- **이 시험을 일반 회귀에 넣기.** 매 실행마다 과금된다. `[Explicit]` 을 떼지 않는다.
- **키를 저장소에 커밋.** `~/.chooguard/` 는 저장소 밖이다. 환경변수도 스크립트에 박지 않는다.
