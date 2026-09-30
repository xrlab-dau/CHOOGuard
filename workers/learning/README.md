# 학습 워커 — JEV 판단을 모으고 증류한다

근무를 돌려 JEV 가 내린 판단을 모으고(수집), 그 판단을 작은 모델이 재현할 수 있는지 연구한다(증류).
예측 정확도는 대응의 인과 효과나 사건 발생 시간 분포를 입증하지 않는다.
**이 워커는 제품의 실시간 JEV 판단을 대체하지 않는다.**

---

## 먼저 읽을 것 — #259 이후 형식

PR #259 는 사건 합성 방식을 바꿨다.
**머지 전에 모은 데이터와 머지 후 데이터는 라벨이 다르다.** 섞어서 학습하면 안 된다.

| | #259 전 | #259 후 |
|---|---|---|
| 사건 판단 | 후보 하나를 `Choice` 로 고름 | 후보마다 임박도를 `Score` 단계로 |
| 재평가 확인 | 10~16초 | **1초마다 + 상황 변화 즉시**; 실제 요청은 후보의 갱신 필요와 예산에 따름 |
| 후보 수 | 최대 12개 | 그 순간 세계에서 성립하는 모든 후보 |
| 키가 없으면 | 로컬 가중치가 대신 고름 | **비상상황이 아예 안 생김** |

수집은 최신 `develop` 의 하네스로 한다. 데이터 폴더에 수집한 커밋과 실행 설정을 함께 기록한다
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
| `CG_SHIFT_REALCAP` | 시도당 실시간 상한(초); Unity 기본 180초 제한은 풀려 있음 | 1800 |
| `CG_SHIFT_SEED` | 기준 시드(0=무작위). 세계: `시드 + 회차 × CG_SHIFT_ATTEMPTS + 시도`; 역무원: `시드 + 회차` | 0 |
| `CG_SHIFT_OUT` | 회차 요약 JSON 과 시도별 JSONL 사본을 쓸 폴더 | 안 씀 |
| `CG_SHIFT_ZONES` | 역무원이 순찰할 구역 id, 쉼표 구분 | 2층 실내 3구역 |
| `CG_SHIFT_REQUIRE` | `Hazard.Label` 에 이 문자열이 들면 채택 | 없음 |
| `CG_SHIFT_SEEK` | 그 사건을 기다리는 게임 시간(초) | 300 |
| `CG_SHIFT_ATTEMPTS` | 회차당 재시도 상한 | 8 |

시험은 `[Explicit]` 이라 `-testFilter` 로 직접 지정해야 돈다. 일반 회귀에는 섞이지 않는다.

### 시간 압축을 쓰지 않는다

`CG_SHIFT_SCALE>1` 은 **수집에 쓰면 안 된다.** 요청 수·입력 토큰 비용의 예산은 실시간 기준이다.
게임만 빨라지면 같은 실시간에 더 많은 질의가 몰려 예산에 걸린다. 현재 상한은
`JevBudget` 의 분당 1,000건·시간당 입력 토큰 $3이며, 군중과 디렉터에 우선순위별 몫이 있다.
이전 노트북 4배속 측정 **516건 중 `compose` 5건**은 #259 전 기록이지 현재 합성의 측정값이 아니다.

압축은 '총량이 얼마나 쌓이는가' 만 볼 때 쓴다.

### 근무는 짧게 잡지 않는다

사건 시각·종류·개수는 지금 세계의 후보와 JEV 판단으로 정해진다. 긴 근무도 사건 발생을 보장하지 않는다.
짧은 스모크는 수집기가 동작한다는 증거일 뿐, 긴 근무의 사건 분포를 검증하지 않는다.

### 역무원이 실제로 대응하게 한다

하네스는 역무원을 조작한다. 조작하지 않으면 JEV 가 보는 `staff_response` 의
`station_office_informed`, `station_announcement_made`, `area_cordoned_off` 가 고정되어,
**대응 뒤의 전개를 비교할 표본을 모으지 못한다.**

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

한 줄이 요청 하나다. 현재 군중 판단은 `crowd-routine`·`crowd-urgent`, 후보 임박도는 `judge`,
전개 크기는 `magnitude` 다. `judge` 는 사건이 생기기 전에도 있으므로 요청 존재가 사건 발생의
증거는 아니다. 성공 응답·근무 요약의 실제 위험 관측을 구분해서 본다.

시도가 끝나면 `<CG_SHIFT_OUT>/jev-<UTC>/jev-shift-NN.jsonl` 로 해당 세션만 복사한다.
요구 조건에 맞지 않아 재시도한 기록은 `discarded/` 에 따로 두며 기본 학습에 섞지 않는다.
클라이언트가 오래된 원본을 지우므로 사본을 전체 수집 끝까지 미루지 않는다.
`shifts-<UTC>.json` 은 회차 요약의 JSON 배열이며 매 회차 갱신된다.

| 요약 필드 | 의미 |
|---|---|
| `seed` · `staffSeed` · `attempts` | 실제 세계 시드 · 역무원 정책 시드 · 실제 열린 시도 수 |
| `requests` · `jevLogLines` | 받아들인 요청 수 · 해당 세션의 완결된 기록 줄 수 |
| `answeredLines` · `answeredByPurpose` | HTTP 200이며 답이 있는 줄 수 · 목적별 성공 분포 |
| `drained` · `pendingRequests` | 사본을 뜨기 전 요청·기록 완료 여부 · 미완료 수 |
| `malformedLines` · `errorLogs` · `jevRejected` | 읽지 못한 줄 · 런타임 오류 · 서버의 키 거부 |
| `hazardsSeen` · `playerKnew` | 실제 관측된 위험 · 역무원의 인지 여부; 사건 발생 합격 기준이 아님 |
| `jsonl` | 출력 폴더에 대한 사본 상대 경로 |

답한 줄이 없거나 요청이 끝나지 않았거나 런타임 오류가 있으면 기록을 남긴 뒤 시험이 실패한다.
`staff.arrivals` · `staff.radio` 는 이동·대응의 관측값이다. 대응 없는 표본을 대응 뒤 전개의 증거로 쓰지 않는다.

---

## 4. 파이프라인

```
근무 실행 (ShiftSampleTests)
   └─ jev-<UTC>/jev-shift-*.jsonl             ← 시도마다 보존한 정본 사본
        ├─ distil_crowd.py      → 승객 다음 행동 판단 증류
        └─ label_causes.py      → 후보 단위 임박도 표본 펼치기
             └─ train_imminence.py → 임박도 재현 모델 + 평가
```

`label_causes.py` 와 `train_imminence.py` 는 **#259 의 `judge` 판단이 있어야** 돈다.
머지 전 데이터로 돌리면 이유를 말하고 멈춘다(빈 결과를 내놓지 않는다).

현재 `judge` 로그의 `question_metadata` 에 기록된 실제 후보 `kind`·`scale` 을 사용한다.
개체 ID 에서 종류를 추정하지 않는다. 이 메타데이터가 없는 옛 로그나 옛 `PublicState` 를
현재 라벨과 섞지 않는다. 최신 수집기로 다시 모아 `label_causes.py` 로 펼친다.
라벨 스키마는 `chooguard.imminence-labels.v2`; **크기 판단 `magnitude` 는 임박도 학습에 넣지 않는다.**
상태 특징은 실제 `Focus` 의 사건 설명·부하·최근 새 사건·역무원 대응·기관 도착·시각이다.
`Focus` 에 없는 사람 수나 사건 경과 초는 0으로 지어내지 않는다.

```sh
python workers/learning/label_causes.py --logs "D:\cg-data\jev-<UTC>" --out "D:\cg-data\labels"
python workers/learning/train_imminence.py --labels "D:\cg-data\labels" --out "D:\cg-data\evidence"
python -m unittest discover -s workers/learning
python workers/learning/distil_crowd.py --logs "D:\cg-data\jev-<UTC>" --out "D:\cg-data\evidence"
```

군중 워커는 `jev-*.jsonl` 만 읽고 성능 기록 `crowd-*.jsonl` 은 제외한다. 현재 사람별 상태와 질문을 사용하며,
급한 반응(`p…`)과 다음 일상 활동(`r…`)을 별도 학습한다. 고정 사건 문구 목록으로 표본을 거르지 않는다.
제시된 선택지만 예측하고 근무 파일 단위로 평가를 나눈다. 옛 `purpose=crowd` 는 거부 수를 출력하며 섞지 않는다.

통합 검증(2026-09-30): 실제 저장된 사용자 키로 1배속 190초 근무 2회(세계 시드 1000·1008, 역무원 시드 1000·1001).
성공 응답 290·307줄, 미완료·오류·깨진 줄은 모두 0이었다. 두 사본을 CLI에 직접 넣어 임박도 라벨 3,288건과
일상 판단 359건을 처리했고, 평가 JSON을 비유한수 금지로 읽었다. 두 근무에서 위험 관측은 0이므로
대응 후 전개·급한 승객 반응·장시간 발생 분포의 증거가 아니다. 급한 반응은 별도 기존 실로그 4개에서 2,686건,
일상은 1,075건을 처리했으며 옛 `crowd` 335레코드는 명시적으로 제외했다. 정확도는 연구 결과이지 제품 cutover 조건이 아니다.


### 임박도 모델에서 진짜 질문

단순히 "수준을 맞힐 수 있나"가 아니라, 종류만 본 기준선에 현재 Focus 상태를 더하면
기록된 JEV 판단을 더 잘 재현하는지 묻는다. `train_imminence.py`는 **종류별 평균**과 비교한다 —
그걸 못 넘으면 이 표본과 모델에서 상태의 추가 예측력을 찾지 못한 것이다. 상태가 쓸모없다는
뜻이나 대응의 인과 효과를 입증한 것은 아니다.

평가는 셋이다: **수준 오차(MAE)** · **요청 안 수준 순위 상관** · **최상위 기대 일치**.
동점 후보는 배열 순서가 아니라 균등 선택의 기대 적중률로 센다. 정의되지 않은 상관은 JSON `null`.
scale 별 위험률·경쟁 위험·첫 사건 시간 분포는 별도 검증 대상이다. 이 수치로 제품 JEV 를 대체하지 않는다.

## 5. 학습 — 증류해 본다

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
위 97.4%·기존 `evidence/crowd-20260930-110054.json` 은 #259 이전 데이터의 역사적 결과다.
현재 `crowd-routine`·`crowd-urgent` 에서 같은 점수가 나온다는 근거로 쓰지 않는다.

---

## 6. 하지 말 것

- **에디터를 열어 둔 채 배치모드 실행.** 프로젝트 락이 걸려 즉시 실패한다.
- **이 시험을 일반 회귀에 넣기.** 매 실행마다 과금된다. `[Explicit]` 을 떼지 않는다.
- **키를 저장소에 커밋.** `~/.chooguard/` 는 저장소 밖이다.
- **`CG_SHIFT_SCALE>1` 로 수집.** 위 참조.
- **#259 전후 데이터를 섞어 학습.** 라벨의 뜻이 다르다.
