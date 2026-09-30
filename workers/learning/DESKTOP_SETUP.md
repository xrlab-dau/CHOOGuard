# 데스크톱 수집 절차 — 1배속 20분 근무

> 이 문서는 **데스크톱에서 Claude Code 에게 그대로 주면 되는 실행 지시서**다.
> 목표: 1배속(`CG_SHIFT_SCALE=1`) 20분 근무를 여러 회 돌려 JEV 판단 기록을 모은다.
> 왜 1배속인가 — 분당 90건 상한이 **실시간** 기준이라, 게임만 빠르게 돌리면 판단이 상한에
> 걸려 버려진다. 노트북에서 4배속으로 재 보니 516건 중 사건 합성은 5건뿐이었다.

각 단계는 **확인 → 실행 → 검증** 순이다. 검증에서 걸리면 다음으로 넘어가지 말 것.

---

## 0. 먼저 물어볼 것 (사람에게)

- 수집 결과를 어디에 둘 것인가 (예: `D:\cg-data`) — 이 문서에서는 `<DATA>` 로 쓴다.
- 몇 회차를 돌릴 것인가 — 1회차가 실시간 20분 + 부팅 1~2분이다. 10회차면 **3.5시간 이상**.
- 이 PC 를 그동안 다른 일에 쓸 것인가 — 배치모드가 프로젝트를 잠그므로 Unity 에디터를 못 연다.

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

수집 하네스는 `feat/jev-shift-sampling` 에 있다(PR #261). develop 에 아직 없으면:

```sh
git checkout -b collect origin/develop
git merge --no-ff origin/feat/jev-shift-sampling
```

충돌이 나면 **직접 해결하지 말고 사람에게 보고한다** — 하네스와 #259 가 같은 파일을 건드린다.

---

## 3. JEV 키

키가 없으면 비상상황이 **아예 만들어지지 않는다**(개정된 하드룰: 로컬 대체 합성 금지).
하네스가 시작 전에 확인하고 멈춘다.

둘 중 하나에 둔다. 환경변수가 우선이다.

- 환경변수 `TYPESAFE_API_KEY` (값이 `off` 면 꺼진 것으로 본다)
- 파일 `%USERPROFILE%\.chooguard\typesafe.key`

**Claude 가 직접 하지 말 것**: 키를 파일에 쓰거나 명령 인자로 넘기는 일. 사람이 직접 넣게 한다.
터미널에서 `notepad "%USERPROFILE%\.chooguard\typesafe.key"` 를 안내하면 된다.

**검증** — 키 값을 출력하지 않고 존재만 확인한다:

```sh
python -c "import pathlib;p=pathlib.Path.home()/'.chooguard'/'typesafe.key';print('키 파일:', p.exists() and p.stat().st_size>0)"
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
CG_SHIFT_COUNT=1 CG_SHIFT_SECONDS=1200 CG_SHIFT_SCALE=1 CG_SHIFT_REALCAP=1800 \
CG_SHIFT_SEED=1000 CG_SHIFT_OUT="<DATA>/smoke" \
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "<저장소>" -runTests -testPlatform PlayMode \
  -testFilter "ChooGuard.Tests.PlayMode.ShiftSampleTests.근무를_돌려_JEV_판단을_모은다" \
  -logFile "<DATA>/smoke.log" -testResults "<DATA>/smoke.xml"
```

실시간 약 20분 걸린다. 끝나면 요약을 본다:

```sh
cat "<DATA>/smoke"/*.json
grep -E "CG_HARNESS|CG_POLICY|CG_STAFF|CG_PATROL" "<DATA>/smoke.log"
```

**검증 — 아래를 모두 만족해야 본 수집으로 넘어간다.**

| 항목 | 기준 | 안 맞을 때 |
|---|---|---|
| 시험 판정 | `passed=1 failed=0` | 로그의 첫 `Assert` 실패 메시지를 사람에게 보고 |
| `keyPresent` | `true` | 3번으로 돌아간다 |
| `jevLogLines` | **0보다 충분히 큼** | 키·네트워크·분당 예산 확인 |
| `staff.arrivals` | **0보다 큼** | 역무원이 못 걸었다 — `CG_PATROL` 로그의 정지 좌표를 보고 |
| `staff.radio` | 비어 있지 않으면 좋음 | 비어도 진행 가능. 사건이 안 났거나 인지 못 한 것 |
| 사건 발생 | JSONL 에 `crowd` 아닌 `purpose` 가 있음 | 아래 명령으로 센다 |

```sh
python workers/learning/distil_crowd.py --logs "%USERPROFILE%/AppData/LocalLow/DefaultCompany/CHOOGuard/jev-runs"
```

첫 줄의 `건너뜀:` 에 목적별 개수가 찍힌다. `crowd` 외의 목적이 **한 건도 없으면** 사건이 안 난
것이다. 회차를 늘리거나 사람에게 보고한다.

### 비용 산정

JSONL 한 줄이 요청 하나다. 입력 $0.042/1M 토큰, 출력 무료.

```sh
python -c "
import json,glob,sys
rows=[json.loads(l) for f in glob.glob(sys.argv[1]+'/*.jsonl') for l in open(f,encoding='utf-8') if l.strip()]
print('요청 %d건' % len(rows))
" "%USERPROFILE%/AppData/LocalLow/DefaultCompany/CHOOGuard/jev-runs"
```

1회차 실측값에 회차 수를 곱해 사람에게 보고하고 **승인을 받은 뒤** 본 수집을 시작한다.

---

## 6. 본 수집

```sh
CG_SHIFT_COUNT=10 CG_SHIFT_SECONDS=1200 CG_SHIFT_SCALE=1 CG_SHIFT_REALCAP=1800 \
CG_SHIFT_SEED=2000 CG_SHIFT_OUT="<DATA>/run" \
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "<저장소>" -runTests -testPlatform PlayMode \
  -testFilter "ChooGuard.Tests.PlayMode.ShiftSampleTests.근무를_돌려_JEV_판단을_모은다" \
  -logFile "<DATA>/run.log" -testResults "<DATA>/run.xml"
```

| 환경변수 | 이 수집에서 | 왜 |
|---|---|---|
| `CG_SHIFT_SCALE` | **반드시 1** | 압축하면 판단이 실시간 상한에 걸려 버려진다 |
| `CG_SHIFT_SECONDS` | **1200** | 첫 30초는 조용하고 첫 사건이 평균 약 3분 뒤다 |
| `CG_SHIFT_SEED` | 고정값 | 재현하려면 필요하다. 0 이면 매번 무작위 |
| `CG_SHIFT_FILM` | **주지 말 것** | 녹화는 근무에 없던 부하를 더한다 |

특정 사건을 더 모으고 싶을 때만 `CG_SHIFT_REQUIRE`(예: `화재`)를 쓴다. 사건을 **만드는 게 아니라
그 사건이 난 회차를 고르는 것**이라 하드룰에 어긋나지 않는다. 다만 **선택 편향**이 생기니
분포를 볼 목적이면 쓰지 않는다.

백그라운드로 돌리고 **진행 중에 같은 프로젝트를 Unity 에디터로 열지 않는다.**

---

## 7. 수집 후

```sh
cat "<DATA>/run"/*.json                      # 회차별 요약
ls "%USERPROFILE%/AppData/LocalLow/DefaultCompany/CHOOGuard/jev-runs"
```

회차별로 확인할 것:

- `keyPresent: true` 가 전 회차에서 유지됐는가
- `jevLogLines` 가 회차마다 비슷한가 — 한 회차만 유독 적으면 그 회차는 중간에 막힌 것
- `staff.arrivals` · `staff.radio` — 역무원이 돌고 대응했는가
- `staff.invalidPaths` 가 유난히 크면 순찰이 막힌 구간이 있었다는 뜻

**JSONL 을 백업한다.** `jev-runs` 는 30개 파일 / 400 MB 를 넘으면 오래된 것부터 지워진다.

```sh
mkdir -p "<DATA>/jsonl-backup"
cp "%USERPROFILE%/AppData/LocalLow/DefaultCompany/CHOOGuard/jev-runs/"*.jsonl "<DATA>/jsonl-backup/"
```

증류를 돌려 파이프라인이 동작하는지 본다:

```sh
python workers/learning/distil_crowd.py --logs "<DATA>/jsonl-backup" --out "<DATA>/evidence" --show-tree
```

**기준선을 얼마나 넘었는지**를 본다. 정확도 숫자만 보면 안 된다 — 다수 클래스만 찍어도 높게
나오는 과제가 있다(노트북 실측: 기준선 0.385 → 결정나무 0.880).

---

## 8. 사람에게 보고할 것

- 회차 수 · 총 실시간 · 총 요청 건수 · 추정 비용
- 목적별 분포 (`crowd` 대 사건 합성 계열의 비율) — **이 비율이 이번 수집의 핵심 결과다**
- `staff` 지표 요약 (도착·무전·막힌 경로)
- 증류 결과 (기준선 대비)
- 실패하거나 이상했던 회차와 그 로그 줄

---

## 하지 말 것

- **`CG_SHIFT_SCALE>1` 로 수집.** 이 수집의 존재 이유가 1배속이다.
- **에디터를 열어 둔 채 배치모드 실행.** 프로젝트 락으로 즉시 실패한다.
- **`[Explicit]` 떼기.** 일반 회귀에 섞이면 매 실행 과금된다.
- **키를 저장소에 커밋하거나 명령 인자·로그에 남기기.**
- **#259 머지 전 데이터와 머지 후 데이터를 섞어 학습.** 라벨의 뜻이 다르다.
- **Unity 버전을 임의로 바꾸기.** 프로젝트가 업그레이드되면 되돌리기 어렵다.
- **결과가 기대와 다를 때 원인을 추측해서 보고하기.** 로그 줄을 인용해 사실만 전한다.
