# 학습 워커 — JEV 판단을 모으고 증류한다

근무를 돌려 JEV 가 내린 판단을 모으고(수집), 그 판단을 작은 모델이 재현할 수 있는지 본다(증류).
잘 재현되면 그 판단은 규칙으로 대체할 수 있다는 뜻이고, 안 되면 기록하지 않은 맥락이 있다는 뜻이다.
둘 다 쓸모가 있다.

---

## 먼저 읽을 것 — 지금 바뀌는 중이다

PR #259 가 사건 합성 방식을 바꾼다.
**머지 전에 모은 데이터와 머지 후 데이터는 라벨이 다르다.** 섞어서 학습하면 안 된다.

| | #259 전 | #259 후 |
|---|---|---|
| 사건 판단 | 후보 하나를 `Choice` 로 고름 | 후보마다 임박도를 `Score` 단계로 |
| 판단 주기 | 10~16초 | **1초마다 + 상황 변화 즉시** |
| 후보 수 | 최대 12개 | 상한 없음 (실측 29~31) |
| 키가 없으면 | 로컬 가중치가 대신 고름 | **비상상황이 아예 안 생김** |

수집을 시작하기 전에 `git log` 로 #259 가 머지됐는지 확인하고, 그 사실을 데이터 폴더 이름에 적어 둔다
(예: `D:\cg-data\post259-2026-10-05\`).

---

## 1. 준비

| | |
|---|---|
| Unity | **6000.3.23f1** (Hub 에서 같은 버전) |
| 저장소 | `develop` 최신 + 하네스가 든 브랜치 |
| Python | 3.9 이상 |
| 디스크 | 20분 근무 1회당 JSONL 약 1~2 MB |

```sh
pip install -r workers/learning/requirements-core.txt
```

### JEV 키 — 이제 필수다

키가 없으면 비상상황이 **만들어지지 않는다**. 예전에는 로컬 규칙이 대신 채워줘서 티가 안 났지만
지금은 조용한 근무만 쌓인다. 그래서 하네스가 시작 전에 키를 확인하고 없으면 멈춘다.

둘 중 하나에 둔다. 환경변수가 우선이다.

- 환경변수 `TYPESAFE_API_KEY` (값이 `off` 면 꺼진 것으로 본다)
- 파일 `%USERPROFILE%\.chooguard\typesafe.key`

**키를 저장소에 커밋하지 않는다.** 스크립트에도 박지 않는다.

---

## 2. 수집 — 근무를 돌린다

> 데스크톱에서 1배속 20분 근무를 돌릴 거면 **[DESKTOP_SETUP.md](DESKTOP_SETUP.md)** 를 쓴다.
> 확인·검증 단계까지 짜여 있어 Claude Code 에게 그대로 줄 수 있다. 아래는 명령 요약이다.

에디터를 **닫은 채로** 배치모드로 실행한다. 같은 프로젝트를 두 인스턴스가 못 연다.

```sh
CG_SHIFT_COUNT=10 CG_SHIFT_SECONDS=1200 CG_SHIFT_SCALE=1 CG_SHIFT_SEED=1000 \
CG_SHIFT_OUT="D:\cg-data" \
"<Unity>/Editor/Unity.exe" -batchmode -nographics -runTests \
  -projectPath "<저장소>" -testPlatform PlayMode \
  -testFilter "ChooGuard.Tests.PlayMode.ShiftSampleTests.근무를_돌려_JEV_판단을_모은다" \
  -testResults "D:\cg-data\shift.xml" -logFile "D:\cg-data\shift.log"
```

| 환경변수 | 뜻 | 기본 |
|---|---|---|
| `CG_SHIFT_COUNT` | 회차 수 | 1 |
| `CG_SHIFT_SECONDS` | 회차당 게임 시간(초) | **1200** |
| `CG_SHIFT_SCALE` | 시간 압축 배수 | 1 |
| `CG_SHIFT_REALCAP` | 회차당 실시간 상한(초) | 1800 |
| `CG_SHIFT_SEED` | 첫 회차 시드(0=무작위). 회차마다 +1 | 0 |
| `CG_SHIFT_OUT` | 회차 요약 JSON 을 쓸 폴더 | 안 씀 |
| `CG_SHIFT_ZONES` | 역무원이 순찰할 구역 id, 쉼표 구분 | 2층 실내 3구역 |
| `CG_SHIFT_REQUIRE` | `Hazard.Label` 에 이 문자열이 들면 채택 | 없음 |
| `CG_SHIFT_SEEK` | 그 사건을 기다리는 게임 시간(초) | 300 |
| `CG_SHIFT_ATTEMPTS` | 회차당 재시도 상한 | 8 |

시험은 `[Explicit]` 이라 `-testFilter` 로 직접 지정해야 돈다. 일반 회귀에는 섞이지 않는다.

### 시간 압축을 쓰지 않는다

`CG_SHIFT_SCALE>1` 은 **수집에 쓰면 안 된다.** 분당 90건 상한이 실시간 기준인데 게임만 빨라지면
같은 실시간에 몇 배의 판단이 몰려 상한에 걸리고 버려진다. 노트북에서 4배속으로 240초를 돌린
결과가 이렇다 — **516건 중 `compose` 는 5건, 나머지 511건이 군중 판단이었다.**
#259 이후에는 판단이 1초마다 일어나므로 더 심해진다.

압축은 '총량이 얼마나 쌓이는가' 만 볼 때 쓴다.

### 근무는 짧게 잡지 않는다

근무 첫 30초는 조용하고 첫 사건은 평균 약 3분 뒤에 난다. 이후 새 사건은 드물어
15~20분에 1~2건이다. **240초짜리는 사건을 한 건도 못 보고 끝날 수 있다.**
300초 미만이면 하네스가 경고한다.

### 역무원이 실제로 대응하게 한다

하네스는 역무원을 조작한다. 조작하지 않으면 JEV 가 보는 staff 상태가 근무 내내
`reported=0 · public_announcement=false · cordons=0` 으로 고정되어, **대응 뒤의 전개를 한 건도
배우지 못한다.**

- 사건을 모르는 동안: 2층 실내를 NavMesh 경로로 순찰한다
- 사건을 인지한 뒤: 6초마다 판정해서, 지금 열린 무전 선택지 중 균등하게 하나를 고르고
  ("아무것도 안 함" 확률 0.2) 그 무전을 실제로 보낸다

순서는 코드가 강제한다 — 보고 전에는 방송 선택지가 열리지 않는다.
순찰 구역을 넓히려면 `CG_SHIFT_ZONES` 를 쓴다(기본은 대합실이 있는 2층 실내: `hall2f,main2f,eastexit`).

---

## 3. 나오는 것

**정본은 JSONL 이다.** 회차 요약이 아니라 이쪽을 학습에 쓴다.

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\CHOOGuard\jev-runs\jev-<UTC>.jsonl
```

한 줄이 요청 하나다. `purpose` 로 갈린다 — `crowd` 는 승객 일상 판단, 그 외가 사건 합성 계열이다.
(#259 가 `purpose` 이름을 바꿀 수 있다. 무엇이 있는지는 실제 파일에서 세는 것이 맞다:
`distil_crowd.py` 가 첫 출력에 `건너뜀:` 으로 목적별 개수를 찍는다.)

회차 요약(`CG_SHIFT_OUT`)은 회차당 한 줄이다.

```json
{"shift":0,"seed":1000,"gameSeconds":1200,"realSeconds":1200,
 "jevLogLines":418,"keyPresent":true,"require":"","attempts":1,"accepted":true,
 "model":"jev-1.13.0",
 "staff":{"patrolPoints":996,"arrivals":31,"unstucks":4,"walkedMetres":1480,
          "laps":0,"partialPaths":0,"invalidPaths":6,"noops":3,
          "radio":{"역무실 · 화재 보고":1,"역무실 · 대피 안내방송 요청":1}}}
```

`jevLogLines` 는 그 회차에 JSONL 이 몇 줄 늘었는지다. **요약 카운터를 믿지 않고 실제로 쓰인
줄을 센다** — 제품 API 가 어떻게 바뀌어도 이 값은 맞는다.

`staff` 로 수집 품질을 본다. `arrivals` 가 0이면 역무원이 못 걸은 것이고, `radio` 가 비어 있으면
대응이 한 건도 없어 대응 뒤 전개를 못 배운다.

---

## 4. 학습 — 증류해 본다

```sh
python workers/learning/distil_crowd.py --show-tree
python workers/learning/distil_crowd.py --logs "D:\cg-data\jev-runs" --out "D:\cg-data\evidence"
```

지금 붙어 있는 과제는 **승객의 다음 행동 예측**이다. 왜 이것인지, 무엇을 하지 않는지는
`distil_crowd.py` 의 맨 위 설명에 적혀 있다 — 읽고 시작한다.

나오는 것:

- 표본 수·클래스 분포·다수 클래스 기준선
- 근무 파일 단위로 가른 교차검증 점수(같은 근무가 학습·시험에 함께 들어가지 않게 한다)
- 기여도 상위 특징, `--show-tree` 면 학습한 규칙
- `evidence/crowd-<UTC>.json` 에 같은 내용을 기록

**기준선을 넘는지가 전부다.** 정확도 숫자만 보면 안 된다 — 다수 클래스만 찍어도
`route`(층 이동) 과제는 97.4% 가 나온다. 그래서 그 과제는 아예 뺐다.

---

## 5. 하지 말 것

- **에디터를 열어 둔 채 배치모드 실행.** 프로젝트 락이 걸려 즉시 실패한다.
- **이 시험을 일반 회귀에 넣기.** 매 실행마다 과금된다. `[Explicit]` 을 떼지 않는다.
- **키를 저장소에 커밋.** `~/.chooguard/` 는 저장소 밖이다.
- **`CG_SHIFT_SCALE>1` 로 수집.** 위 참조.
- **#259 전후 데이터를 섞어 학습.** 라벨의 뜻이 다르다.
