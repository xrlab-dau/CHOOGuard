# 데스크톱 수집 절차 — 1배속 10분 근무 × 여러 회

> 이 문서는 **데스크톱에서 Claude Code 에게 그대로 주면 되는 실행 지시서**다.
> 목표: 1배속(`CG_SHIFT_SCALE=1`) 600 게임초(약 10 게임분) 근무를 여러 회 돌려 JEV 판단 기록을 모은다.
> 왜 1배속인가 — JEV 예산(분당 요청 수·시간당 비용)이 **실시간** 기준이라, 게임만 빠르게 돌리면 판단이
> 상한에 걸려 버려진다(#259 이전 측정: 노트북 4배속 516건 중 사건 합성 5건).
> 600 게임초 근무는 Unity 테스트 러너의 기본 제한(180초)보다 길다. 하네스는 `[Timeout(int.MaxValue)]` 로 그 제한을 풀어 두었고
> 실시간 상한은 `CG_SHIFT_REALCAP` 이 회차(재시도가 있으면 시도)마다 맡는다.

각 단계는 **확인 → 실행 → 검증** 순이다. 검증에서 걸리면 다음으로 넘어가지 말 것.

---

## 0. 먼저 물어볼 것 (사람에게)

- 수집 결과를 어디에 둘 것인가 (예: `D:\cg-data`) — 이 문서에서는 `<DATA>` 로 쓴다.
- 몇 회차를 돌릴 것인가 — 아래 **시간·비용 실측**을 먼저 읽고 정한다.
- 이 PC 를 그동안 다른 일에 쓸 것인가 — 배치모드가 프로젝트를 잠그므로 Unity 에디터를 못 연다.

### 시간·비용 실측 (2026-10-01~02)

**짧은 근무는 실시간과 같은 속도로 가는데, 긴 근무에서는 느려진다.** 원인은 아직 모른다.

| 커밋 | 게임초/실시간초 | 요청 | 비용 | 후보 수 |
|---|---|---|---|---|
| `6d8a7d32` | 180 / 180 = **1.00배** | 462 | $0.027 | 53 |
| `6d8a7d32` | 589 / 1800 = **0.33배** | 937 | $0.080 | 52~54 |
| `90f503c1` (#265) | 180 / 180 = **1.00배** | 210 | **$0.145** | **3,813** |
| `90f503c1` (데스크톱) | 566 / 4800 = **0.12배** | 688 | $0.439 | 2,700~3,879 |

#265 가 후보를 **72배**로 늘려 비용이 5배가 되었고, 긴 근무에서 게임 진행이 0.12배까지
떨어졌다. 두 값이 같이 커졌지만 **인과는 측정하지 않았다.**

범인이 아닌 것은 확인했다 — 디렉터 프레임은 0.354~0.405ms(p50)이고, JEV 요청 소요 시간
합계는 실시간의 14% 다. 다만 디렉터 heartbeat 전체는 1.99ms → 26.09ms(p50), heartbeat 당
GC 는 212KB → 5,012KB 로 커졌다.

**추정하지 말고 5단계 스모크 1회로 이 PC 의 `gameSeconds`/`realSeconds` 비율과
`peak_dollars_per_hour` 를 재서 회차 수를 정한다.**

600 게임초 근무를 권한다 — 589 게임초 근무에서 사건 1건과 전개 4건이 났고 첫 사건이
113초였다. 같은 실시간이면 **회차를 길게 끌기보다 서로 다른 시드를 늘리는 쪽이 학습에
낫다.** 같은 근무 안의 후보들은 상태가 거의 같아 표본이 서로 닮는다(그래서 교차검증도
근무 파일 단위로 가른다).

**회차 수와 비용은 #266 판단 뒤에 정한다.** 후보 3,800개가 유지되면 비용 상한에 막혀
회차를 늘려도 표본이 비례해 늘지 않는다.

### 지금은 본 수집을 시작하지 않는다 — 이슈 #266 판단 대기

**디렉터 판단의 절반 넘게가 예산에 막혀 버려진다.** 데스크톱 실측(2026-10-02, `90f503c1`):

```
  rounds 370 · unanswered_rounds 377   <- 판단의 50.5% 가 답을 못 받았다
  director lane 피크 $2.9787/h         <- 상한 $3 을 거의 정확히 채운다
  분당 요청 피크 48                     <- 상한 1,000 의 5%. 요청 수는 문제가 아니다
```

**막는 것은 분당 요청이 아니라 시간당 비용이다.** 상한이 둘인데(분당 1,000건 · **시간당 $3**)
비용 쪽이 먼저 찬다. 이 문서의 앞선 판은 분당 요청만 보고 "상한은 걱정하지 않아도 된다" 고
적었는데 **틀렸다.**

원인은 후보 수다. 같은 시드·길이·PC 로 비교하면 #265 가 후보를 **53 → 3,813(72배)** 로 늘렸고,
`judge` 요청당 입력 토큰이 28,477(중앙값)이 되었다. 요청 수는 오히려 줄었는데 요청 하나가
커져서 비용이 찬다.

이대로 돌리면 **`judge` 표본이 조용히 절반 빠진다.** 예산에 막힌 요청은 JSONL 에 줄을 남기지
않으므로 `answeredLines`(688/688) 만 보면 정상처럼 보인다.

**→ [#266](https://github.com/xrlab-dau/CHOOGuard/issues/266) 에서 후보 3,800개가 의도된
규모인지 판단이 나온 뒤에 본 수집을 시작한다.** 그전까지는 5단계 스모크까지만 돌린다.

### 매 회차 확인할 값

| 값 | 기준 | 못 지키면 |
|---|---|---|
| `director.unanswered_rounds` | **0** | 판단이 버려졌다. 그만큼 표본이 빠진다 |
| `jev.lanes.director.peak_dollars_per_hour` | **$3 미만** | 비용 상한에 찬다. 이게 실제 병목이다 |
| `jev.usage.peak_requests_per_minute` | 1,000 미만 | 실측 48~271 로 여유가 많다 |
| `director.rated_candidates` | — | 게임초당으로 나눠 PC·커밋 간 비교 |
| `director.hazard` 의 `candidates` | — | 후보 수 시계열. 53(#265 전) 대 3,813(#265 후) |

### 수준 분포 — 수집이 쓸모 있는지 보는 값

`director.levels` 가 임박도 수준을 scale 별로 센다. 180초 실측은 이랬다.

```
  calm_origin      [504, 340, 2, 0, 0]
  incident_origin  [180, 297, 1, 0, 0]
  development      [  0,   0, 7, 10, 0]
```

**원인과 전개가 완전히 다른 분포다.** 원인은 최저 두 수준에 쏠리고 전개는 상위에 몰린다.
한 scale 안에서 한 수준이 95% 를 넘으면 그 scale 은 학습할 것이 거의 없다.

---

## 1. 전제 확인

```sh
git --version
python --version                         # 3.9 이상
ls "C:/Program Files/Unity/Hub/Editor"   # 6000.3.23f1 이 있어야 한다
```

**검증**: Unity `6000.3.23f1` 이 없으면 Unity Hub 에서 그 버전을 설치한다. 다른 버전으로 열면
프로젝트 파일이 업그레이드되어 되돌리기 어렵다. **임의로 다른 버전을 쓰지 말고 사람에게 묻는다.**

```sh
pip install -r workers/learning/requirements-core.txt
```

---

## 2. 저장소와 브랜치

```sh
git fetch origin
git log --oneline -5 origin/develop
```

**#259 가 머지됐는지 반드시 확인한다.**

```sh
git log origin/develop --oneline | grep -i "jev-all-emergencies\|#259" | head
```

| #259 상태 | 해야 할 일 |
|---|---|
| **머지됨** | `origin/develop` 위에서 수집한다. 데이터 폴더 이름에 `post259` 를 넣는다 |
| **아직 열림** | 사람에게 알리고 **멈춘다.** 지금 모으면 라벨이 옛 형식(`Choice`)이라 버려야 한다 |

수집 하네스는 **이미 develop 에 있다**(PR #261 머지, `651558bd`). 따로 받을 것이 없다.

### 아직 머지되지 않은 변경 하나 — 이 수집에 필요하다

요약에 `timeline` · `compositions` · `jev` · `director` 를 담는 변경은 **아직 develop 에 없다.**
그것이 없으면 수집은 되지만 **아래를 볼 수 없다.**

- 무엇이 언제 일어났는지(`timeline`) · 어떻게 합성됐는지(`compositions`)
- 요청 상한에 막혔는지(`director.unanswered_rounds`)
- 임박도 수준이 한쪽으로 쏠렸는지(`director.levels`)

**이 수집의 목적 중 하나가 그 변경을 PR 로 올릴 근거를 만드는 것이다.** 그래서 적용하고 돌린다.

둘 중 하나로 적용한다.

```sh
# (가) 브랜치가 원격에 올라가 있으면 - 이쪽이 깔끔하다
git fetch origin feat/shift-timeline
git checkout -b collect origin/develop
git merge --no-ff origin/feat/shift-timeline

# (나) 파일 사본을 받았으면 - 키트의 ShiftSampleTests.cs 로 덮어쓴다
#      경로: Assets/ChooGuard/Tests/PlayMode/ShiftSampleTests.cs
#      덮어쓴 뒤 git diff --stat 으로 그 파일 하나만 바뀌었는지 확인한다
```

충돌이 나면 **직접 해결하지 말고 사람에게 보고한다.**

적용됐는지는 4단계 컴파일 확인 뒤 5단계 스모크 요약에서 본다 — `jev` 와 `director` 키가
있으면 적용된 것이다. 없으면 develop 버전이 돌고 있다.

---

## 3. JEV 키

키가 없으면 비상상황이 **아예 만들어지지 않는다**(개정된 하드룰: 로컬 대체 합성 금지).
하네스는 시작 전에 **게임과 같은 함수(`JevKey.Load`)** 로 키를 확인하고, 쓸 수 있는 키가 없으면 이유를 말하고 멈춘다.

둘 중 하나에 둔다. 환경변수가 우선이다.

- 환경변수 `TYPESAFE_API_KEY` (값이 `off` 면 꺼진 것으로 본다)
- 파일 `%USERPROFILE%\.chooguard\typesafe.key`

**환경변수가 공백이 아니면 파일은 보지 않는다.** 그 값이 키 형식(`JevKey.Plausible`: 길이 20~512, 공백 없음)이
아니면 파일로 넘어가지 않고 "키 없음"이다. 잘린 키 파일도 같다.
형식이 맞는다고 서버가 받아 주는 것은 아니다 — 그것은 요청을 보내 봐야 알고, 요약의 `answeredLines` 가 말해 준다.

**Claude 가 직접 하지 말 것**: 키를 파일에 쓰거나 명령 인자로 넘기는 일. 사람이 직접 넣게 한다.
터미널에서 `notepad "%USERPROFILE%\.chooguard\typesafe.key"` 를 안내하면 된다.

**검증** — 키 값도 경로도 출력하지 않고 형식 통과 여부만 확인한다(환경변수로 주는 경우엔 하네스가 같은 검사를 한다):

```sh
python -c "import pathlib;p=pathlib.Path.home()/'.chooguard'/'typesafe.key';t=p.read_text(encoding='utf-8').strip() if p.exists() else '';print('키 파일 형식:', 20<=len(t)<=512 and not any(c.isspace() for c in t))"
```

---

## 4. 컴파일 확인 (수집 전)

```sh
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "<저장소>" -runTests -testPlatform EditMode -testFilter "NoSuchTestZZZ" \
  -logFile "<DATA>/compile.log" -testResults "<DATA>/compile.xml"
```

**검증** — 두 가지를 다 본다. 오류 0건만 보면 *어셈블리를 아예 안 짓고 끝난 경우*를 놓친다.

```sh
grep -c "error CS" "<DATA>/compile.log"                    # 0 이어야 한다
grep "Csc.*PlayModeTests" "<DATA>/compile.log" | head -1   # 이 줄이 있어야 한다
```

---

## 5. 스모크 1회 (예산 측정용)

본 수집 전에 **1회차만** 돌려 실제 요청량과 비용을 잰다. 추정으로 예산을 잡지 않는다.

```sh
CG_SHIFT_COUNT=1 CG_SHIFT_SECONDS=600 CG_SHIFT_SCALE=1 CG_SHIFT_REALCAP=2400 \
CG_SHIFT_SEED=1000 CG_SHIFT_TIMELINE=1 CG_SHIFT_OUT="<DATA>/smoke" \
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "<저장소>" -runTests -testPlatform PlayMode \
  -testFilter "ChooGuard.Tests.PlayMode.ShiftSampleTests.근무를_돌려_JEV_판단을_모은다" \
  -logFile "<DATA>/smoke.log" -testResults "<DATA>/smoke.xml"
```

1.00배면 실시간 약 10분, 0.33배면 약 30분 + 부팅이다. 도중에 터미널을 닫지 않는다.
**이 실행의 `gameSeconds`/`realSeconds` 비율로 본 수집의 회차 수를 정한다.**
끝나면 요약과 사본을 본다:

```sh
cat "<DATA>/smoke"/shifts-*.json
ls "<DATA>/smoke"/jev-*/
grep -E "CG_HARNESS|CG_POLICY|CG_STAFF|CG_PATROL|CG_FLUSH" "<DATA>/smoke.log"
```

**하네스가 JSONL 을 다루는 방식.**

- 시도가 끝날 때마다 **그 세션의 기록 파일 하나**(`JevClient.LogFile`)를 `<DATA>/smoke/jev-<UTC>/jev-shift-00.jsonl` 로 복사한다.
  요약 `shifts-<UTC>.json` 도 회차마다 다시 쓴다. `jev-runs` 폴더 전체를 세거나 복사하지 않는다.
- 읽기 전에 시간을 멈추고, 받아들인 요청이 모두 끝나 기록될 때까지(`JevClient.PendingRequests == 0`) 기다린다.
  요청 제한시간 + 3초 안에 안 끝나면 요약에 `drained:false` 를 쓰고, 모든 회차를 쓴 뒤 시험을 **실패**로 끝낸다.
- 회차에 JEV 가 답한 줄이 한 건도 없으면 요약을 쓴 직후 시험이 실패한다.

**검증 — 아래를 모두 만족해야 본 수집으로 넘어간다.**

| 항목 | 기준 | 안 맞을 때 |
|---|---|---|
| 시험 판정 | `passed=1 failed=0` | 로그의 첫 `Assert` 실패 메시지를 사람에게 보고 |
| `keyPresent` | `true` | 3번으로 돌아간다 |
| `jevRejected` | `false` | 서버가 키를 거부했다(401/403). 3번으로 돌아간다 |
| `answeredLines` | **0보다 충분히 큼** — `http` 200 이고 `answers` 가 비어 있지 않은 줄만 센다 | 키·네트워크·예산 확인 후 사람에게 보고 |
| `jevLogLines` − `answeredLines` | 실패한 요청(401 키 거부·429 한도·시간초과). 작아야 한다 | 로그의 `JEV` 상태 줄을 인용해 사람에게 보고 |
| `drained` · `pendingRequests` | `true` · `0` | 그 회차 사본은 완전하지 않다 |
| `requests` | `jevLogLines + pendingRequests` 이상 | 더 크면 JEV 기록 파일이 상한에 걸려 줄이 버려진 것 |
| `malformedLines` | `0` | 읽지 못한 줄이다. 사본 원문을 확인 |
| `seed` | 세션이 **실제로 쓴** 시드 | 지정했다면 6번의 시드 공식과 맞는지 |
| `errorLogs` | `0` | 기록을 남긴 뒤 시험이 실패한다. `smoke.log` 의 첫 오류 줄을 인용해 보고 |
| `staff.arrivals` | **0보다 큼** | 역무원이 못 걸었다 — `CG_PATROL` 로그의 정지 좌표를 보고 |
| `staff.radio` | 비어 있지 않으면 좋음 | 비어도 진행 가능. 사건이 안 났거나 인지 못 한 것 |

### 사건이 났는가 — 관측만 하고 판정하지 않는다

JSONL 의 `purpose` 로는 사건 발생을 판정할 수 없다. #259 의 `purpose` 는 이렇다.

| purpose | 누가, 언제 |
|---|---|
| `judge` | 디렉터. 사건 후보(지금 세계에서 일어날 수 있는 일)가 있을 때, 새로 생겼거나 상황이 바뀌었거나 앞선 판단이 낡았으면 근무 26초부터 임박도를 묻는다 |
| `magnitude` | 디렉터. 추첨이 후보 하나를 뽑았을 때 '어떻게 전개되는가' 를 묻는다 |
| `crowd-routine` · `crowd-urgent` | 승객. 일상 판단 · 눈앞의 일에 대한 급한 판단 |

**`judge` 요청이 있다는 것은 사건이 났다는 뜻이 아니다.** 후보가 있어서 물었을 뿐이고, 조용한 근무도 `judge` 로 채워진다.
사건이 실제로 있었는지는 요약의 관측값으로 **서술만** 한다. 합격 기준이 아니며 특정 사건을 요구하지 않는다.

- `hazardsSeen` — 이 회차에 위험 레지스트리(`HazardRegistry.Active`)에 한 번이라도 오른 위험의 수(프레임마다 표본). `playerKnew` — 역무원이 사건을 인지한 적이 있는가.
- `answeredByPurpose.magnitude` — 추첨이 후보를 뽑아 JEV 가 전개 크기를 **답한** 횟수. 사건이 난 횟수와 같지 않다:
  크기 수준이 없는 전이는 묻지 않고 바로 일어나고, 일어난 전이가 새 위험을 만들지 않는 전개일 수도 있다.
- 둘 다 0 이어도 수집 실패가 아니다. 그 근무는 '사건이 없던 근무' 라는 분포의 한 점이다. 회차를 더 돌릴지는 사람이 정한다.

목적별로 답한 줄을 세려면 (사본 폴더를 준다):

```sh
python -c "
import json,glob,sys,collections
n=collections.Counter(); bad=0
for f in glob.glob(sys.argv[1]+'/*.jsonl'):
    for l in open(f,encoding='utf-8'):
        if not l.strip(): continue
        r=json.loads(l)
        if r.get('http')==200 and r.get('answers'): n[r.get('purpose')]+=1
        else: bad+=1
print('답한 줄', dict(n), '· 못 받은 줄', bad)
" "<DATA>/smoke/jev-<UTC>"
```

### 비용 산정

JSONL 한 줄이 요청 하나다. 입력 $0.042/1M 토큰, 출력 무료. `input_tokens` 가 없는 줄(시간초과 등)은 0 으로
세므로 실제보다 작을 수 있다.

```sh
python -c "
import json,glob,sys
rows=[json.loads(l) for f in glob.glob(sys.argv[1]+'/*.jsonl') for l in open(f,encoding='utf-8') if l.strip()]
tokens=sum(r.get('input_tokens') or 0 for r in rows)
print('요청 %d건 · 입력 토큰 %d · \$%.4f' % (len(rows), tokens, tokens*0.042/1e6))
" "<DATA>/smoke/jev-<UTC>"
```

1회차 실측값에 회차 수를 곱해 사람에게 보고하고 **승인을 받은 뒤** 본 수집을 시작한다.

---

## 6. 본 수집

```sh
CG_SHIFT_COUNT=10 CG_SHIFT_SECONDS=600 CG_SHIFT_SCALE=1 CG_SHIFT_REALCAP=2400 \
CG_SHIFT_SEED=2000 CG_SHIFT_TIMELINE=1 CG_SHIFT_OUT="<DATA>/run" \
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "<저장소>" -runTests -testPlatform PlayMode \
  -testFilter "ChooGuard.Tests.PlayMode.ShiftSampleTests.근무를_돌려_JEV_판단을_모은다" \
  -logFile "<DATA>/run.log" -testResults "<DATA>/run.xml"
```

| 환경변수 | 이 수집에서 | 왜 |
|---|---|---|
| `CG_SHIFT_SCALE` | **반드시 1** | 압축하면 판단이 실시간 상한에 걸려 버려진다 |
| `CG_SHIFT_SECONDS` | **1200** | 첫 30초는 조용하고 첫 사건이 평균 약 3분 뒤다 |
| `CG_SHIFT_OUT` | **반드시 준다** | 없으면 JSONL 사본을 뜨지 않아 세션이 쌓이며 오래된 기록이 지워진다 |
| `CG_SHIFT_SEED` | 고정값 | 재현하려면 필요하다. 0 이면 매번 무작위 |
| `CG_SHIFT_FILM` | **주지 말 것** | 녹화는 근무에 없던 부하를 더한다 |

**시드.** `CG_SHIFT_SEED` 는 기준값이다. 회차·시도마다 세션(세계) 시드는
`시드 + 회차 × CG_SHIFT_ATTEMPTS + 시도번호`(시도 상한 기본 8)이고 역무원 정책 시드는 `시드 + 회차` 다.
예: `2000` 이면 0번 회차 첫 세션은 2000, 1번 회차 첫 세션은 2008. **요약의 `seed` 는 세션이 실제로 쓴
값(`World.Seed`)이고 `staffSeed` 는 역무원 정책의 시드다** — 회차마다 +1 이 아니다.

**시간.** 600 게임초 회차는 1.00배면 실시간 약 10분, 0.33배면 약 30분 + 부팅이다. 10회차면 1.7~5시간이다. 비율은 5단계 스모크로 먼저 잰다. Unity 테스트 러너의 기본 제한(180초)은
시험의 `[Timeout(int.MaxValue)]` 로 풀려 있고, 회차(재시도가 있으면 시도)마다 `CG_SHIFT_REALCAP` 이 실시간 상한이다.

특정 사건을 더 모으고 싶을 때만 `CG_SHIFT_REQUIRE`(예: `화재`)를 쓴다. 사건을 **만드는 게 아니라
그 사건이 난 회차를 고르는 것**이라 하드룰에 어긋나지 않는다. 다만 **선택 편향**이 생기니
분포를 볼 목적이면 쓰지 않는다. 사건이 안 난 시도는 버리고 새 세션으로 다시 여는데, 그 기록은
`jev-<UTC>/discarded/` 에 따로 남는다(표본이 아니다). 요약의 `attempts` 는 실제로 연 세션 수다.

백그라운드로 돌리고 **진행 중에 같은 프로젝트를 Unity 에디터로 열지 않는다.**

---

## 7. 수집 후

```sh
cat "<DATA>/run"/shifts-*.json               # 회차별 요약 (회차당 한 줄)
ls "<DATA>/run"/jev-*/                        # 회차별 JSONL 사본 (jev-shift-NN.jsonl)
```

회차별로 확인할 것:

- `keyPresent: true` · `jevRejected: false` 가 전 회차에서 유지됐는가
- `answeredLines` 가 회차마다 비슷한가 — 한 회차만 유독 적으면 그 회차는 중간에 막힌 것. `jevLogLines` 가 아니라 이 값이다
- `drained` 가 전 회차에서 `true` 인가 · `errorLogs` · `malformedLines`
- `staff.arrivals` · `staff.radio` — 역무원이 돌고 대응했는가
- `staff.invalidPaths` 가 유난히 크면 순찰이 막힌 구간이 있었다는 뜻
- `hazardsSeen` · `playerKnew` · `answeredByPurpose` — **서술용**이다. 사건이 난 회차와 안 난 회차가 각각 몇 개인지 적을 뿐, 기준이 아니다

**백업은 이미 끝나 있다.** 하네스가 시도가 끝날 때마다 `<DATA>/run/jev-<UTC>/` 에 그 세션의 JSONL 을 떠 뒀다.
이 폴더가 수집물이다 — `jev-runs` 를 다시 통째로 복사하지 않는다(다른 실행의 기록이 섞인다).

예외는 시험이 중간에 실패한 경우다. 실패한 회차의 사본은 없고 원본은 `jev-runs` 에만 있다.
JevClient 는 세션을 열 때마다 30개 파일 / 400 MB 밖의 오래된 것부터 지우므로, 다음 실행 전에 그 파일을 직접 떠 둔다:

```sh
ls -lt "%USERPROFILE%/AppData/LocalLow/DefaultCompany/CHOOGuard/jev-runs/"jev-*.jsonl   # 가장 위가 실패한 회차
mkdir -p "<DATA>/partial"
cp "%USERPROFILE%/AppData/LocalLow/DefaultCompany/CHOOGuard/jev-runs/<위에서 확인한 파일>" "<DATA>/partial/"
```

군중 워커는 `--logs` 폴더의 `jev-*.jsonl`, 임박도 라벨러는 `*.jsonl` 을 읽는다. 하위 폴더 `discarded/` 는 읽지 않는다.
사본은 두 워커가 읽는 `jev-shift-NN.jsonl` 이다. 파이프라인이 동작하는지 본다:

```sh
python workers/learning/label_causes.py --logs "<DATA>/run/jev-<UTC>" --out "<DATA>/labels"
python workers/learning/train_imminence.py --labels "<DATA>/labels" --out "<DATA>/evidence"
python workers/learning/distil_crowd.py --logs "<DATA>/run/jev-<UTC>" --out "<DATA>/evidence" --show-tree
```

**기준선을 얼마나 넘었는지**를 본다. 정확도 숫자만 보면 안 된다 — 다수 클래스만 찍어도 높게
나오는 과제가 있다.

---

## 8. 사람에게 보고할 것

- 회차 수 · 총 실시간 · 받아들여진 요청 수(`requests`)와 **답한 줄**(`answeredLines`) · 추정 비용
- 목적별 분포 (`answeredByPurpose`: `judge` · `magnitude` · `crowd-routine` · `crowd-urgent`)
- 사건 관측 — `hazardsSeen > 0` 이거나 `playerKnew` 인 회차가 몇 개인지. **서술이지 합격 기준이 아니다**
- `staff` 지표 요약 (도착·무전·막힌 경로)
- 증류 결과 (기준선 대비)
- 실패하거나 이상했던 회차(`drained:false` · `errorLogs>0` · `jevRejected`)와 그 로그 줄

### PR 근거로 쓸 값 — 이 네 개는 꼭 적어 보낸다

요약에 timeline·jev·director 를 담는 변경(2단계)을 PR 로 올릴 때 쓴다.
노트북 실측과 나란히 두어, 그 변경이 실제로 쓸모 있었음을 수치로 보인다.

| 값 | 노트북 실측 | 데스크톱 |
|---|---|---|
| `gameSeconds` / `realSeconds` | 180/180 = 1.00배 · 589/1800 = 0.33배 | |
| `jev.usage.peak_requests_per_minute` (상한 1000) | 271 | |
| `director.unanswered_rounds` | 0 | |
| `timeline` 줄 수 · `compositions` 건수 | 14줄 · 5건 (589초 근무) | |

**타임라인이 실제로 쓸모 있었는지**도 한 줄로 적는다 — 사건이 시간순으로 읽혔는가,
사건 없이 끝난 회차가 몇 건인가. 이것이 그 변경의 PR 에 들어갈 `테스트 결과` 다.

---

## 하지 말 것

- **`CG_SHIFT_SCALE>1` 로 수집.** 이 수집의 존재 이유가 1배속이다.
- **에디터를 열어 둔 채 배치모드 실행.** 프로젝트 락으로 즉시 실패한다.
- **`[Explicit]` 떼기.** 일반 회귀에 섞이면 매 실행 과금된다.
- **`[Timeout]` 떼기.** 없으면 Unity 기본 180초에 1배속 20분 근무가 잘린다.
- **키를 저장소에 커밋하거나 명령 인자·로그에 남기기.** 키 값도 키 파일 경로도 출력하지 않는다.
- **#259 머지 전 데이터와 머지 후 데이터를 섞어 학습.** 라벨의 뜻이 다르다.
- **`judge` 요청이 있다는 이유로 사건이 났다고 보고하기.** 후보가 있어서 물었을 뿐이다. 사건은 `hazardsSeen` 같은 관측값으로만 서술한다.
- **`jevLogLines` 만 보고 수집이 됐다고 판단하기.** 401·429·시간초과도 줄이다. `answeredLines` 를 본다.
- **`jev-runs` 폴더를 통째로 세거나 복사하기.** 이전 실행의 기록과 `crowd-*.jsonl` 성능 기록이 섞인다. 하네스가 뜬 `jev-<UTC>/` 를 쓴다.
- **백업을 끝에 한 번만 하면 된다고 안내하기.** 세션을 열 때마다 오래된 파일이 지워진다. 하네스가 시도마다 뜨는 사본이 수집물이다.
- **Unity 버전을 임의로 바꾸기.** 프로젝트가 업그레이드되면 되돌리기 어렵다.
- **결과가 기대와 다를 때 원인을 추측해서 보고하기.** 로그 줄을 인용해 사실만 전한다.
