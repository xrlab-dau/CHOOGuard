# 국소 기준 공간 물리 워커

실행: `workers/physics/.venv/bin/python workers/physics/worker.py`

이 워커는 부산역 또는 부산 시가지의 검증된 디지털 트윈이 아니다. 30×20×4 m 단층 기준 공간과 2 m 병목을 계산한다. 게임 시가지에 표시할 때도 이 계산 범위를 명시해야 한다. 시나리오 문서 생성·내보내기는 없다.

## 실제 계산

- JuPedSim 1.4.2 `CollisionFreeSpeedModelV3`, ARM64, dt=0.05 s. 자동 경로와 충돌 회피로 출구 영역에 도달해 엔진이 제거한 인원만 대피 인원이다.
- NIST FDS 6.11.1 공식 x86_64 `fds_openmp`, Rosetta, 2 OpenMP threads. 실제 성공한 0–120 s batch reference output을 사용한다. 0.5 m mesh, 19,200 cells. 계산 덱과 로그·원시 slice·수치 CSV·해시는 `cases/reference-hall/`에 있다.
- FDS x/y → 워커 x/z, FDS z는 높이. 1.5 m 단면의 extinction [1/m], 온도 [°C], soot density [kg/m³], CO/CO2/O2 체적분율을 샘플링한다. 시각 범위 밖에서는 오류를 반환하고 마지막 필드를 재사용하지 않는다.
- [pyFDS-Evac](https://github.com/PedestrianDynamics/pyFDS-Evac) 원본 세 모듈의 `SliceFieldSampler`, Lund smoke speed, toxic/convective-heat FED 함수를 재사용한다. 핀은 `upstream-lock.json`. 전체 프로젝트 GUI/runner를 설치하거나 실행한 것은 아니다. `.tools/physics/pyfds-evac` checkout의 원본 모듈을 별도 namespace로 로드한다.
- 연기는 실제 지역별 FDS extinction을 통해 보행 희망속도를 낮춘다. 독성/대류열 누적 FED는 각각 적분한다. 서로 더하지 않으며 실제 사망·임상 결과로 해석하지 않는다. 복사열 인체영향과 HCN/자극가스는 미지원이다. 이 워커는 열/FED 임계치에 의한 무력화도 구현하지 않았다.
- FDS batch → JuPedSim의 단방향 연결이다. 경보·대피 명령은 보행 상태에만 작용한다. 문·환기·소화 변경을 FDS에 다시 계산해 보내지 않는다.

## 상태·단위

`HELLO {protocolVersion:1}` 이후 `SUBMIT {runId,generation,seed,population,scenario,action,stepSeconds}`. `scenario`: `fire_smoke` 또는 `crowd_medical`. `action`: `start`, `advance`, `warn`, `evacuate`, `medical`. `stepSeconds`: 0–1 s, 0.05 s 배수. Unity에서는 1 simulated second마다 요청한다. 최대 인원 200. 새 generation은 새 start가 필요하다. CANCEL은 해당 generation을 fence한다. 한 워커는 한 active session만 소유한다.

시작은 경보 전 정지 상태이다. warn은 기준 희망속도의 80%, evacuate는 100%로 이동을 개시한다. 해당 비율은 운영 실험 설정이며 보정된 군중 행동 비율이 아니다. medical은 지원 요청 상태를 기록하며 임상 효과를 생성하지 않는다.

`density`: 반경 2 m 이내 인원 / 원 면적의 최대값 [person/m²]. 경계 절단 면적 보정은 하지 않는다. `pressureIndicator`: 같은 이웃의 밀도 × 1초 구간 보행속도 분산 [1/s²]. 압착 접촉력·압력 Pa가 아니다. `visibility`: 활동 인원의 최소 가시거리 `3/K`, 화면 표시는 30 m 상한. 온도·soot·extinction은 활동 인원 위치의 최대값이다. 의료 요청, FED, units/frame/fieldTime/inputDigest가 추가 필드로 전달된다. crowd-only의 온도20°C와 가시거리30m는 명시적 무화재 기준값이며 FDS 계산값이 아니다.

JSONL UTF-8 stdout만 프로토콜로 사용한다. stderr는 진단. 1 MiB line read cap, 64 KiB inline cap, depth32, duplicate key/NaN/unknown kind 거부. 동기 1초 이하 작업 경계에서 CANCEL을 처리한다. 별도 heartbeat scheduler나 durable checkpoint/replay는 아직 없다. 후속 계산에 실패하면 physicsReady=false ERROR이고 Unity는 기존 값으로 성공 처리하면 안 된다.

## 실행 근거와 재실행

`evidence/reproduction-summary.json`은 실제 두 세션 기록이다. 인구72 무화재 대피와 인구24 화재(25 s 지연 후 대피)를 실행했다. 원시 request/result JSONL도 보존한다. 이것은 실행·연결 증거이며 격자수렴, 모델 검증, 실제 역사 보정 또는 전문 안전평가가 아니다.

재실행:

```sh
workers/physics/.venv/bin/python workers/physics/run_reference.py
workers/physics/.venv/bin/python workers/physics/reproduce.py
```

설치 범위는 `.tools/physics`와 `workers/physics/.venv`뿐이다. FDS self-extract script는 실행하지 않았다. `__TARFILE_FOLLOWS__` 이후 tar payload를 경로 검증 후 해당 폴더 안에만 추출했다. 전역 설치·환경설정·셸 startup 변경은 없다. 선택한 Python core deps는 `requirements-core.txt`에 고정했으며 `uv pip install --no-deps`로 GUI의 PySide/VTK 설치를 제외한다. 원본 installer URL/해시 및 solver binary 해시는 receipt에 있다.
