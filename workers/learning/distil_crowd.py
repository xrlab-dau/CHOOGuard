"""JEV 의 군중 판단(PR #259 이후의 군중 질의)을 재현하는 작은 분류기를 학습해 본다.

읽는 것 — JevClient 가 남기는 jev-<UTC>.jsonl (근무 하나 = 파일 하나)
    한 줄이 요청 하나이고, 군중 질의는 purpose 와 lane 이 짝으로 적힌다.
        crowd-urgent   lane CrowdUrgent     급한 줄
        crowd-routine  lane CrowdRoutine    일상 줄
    요청 하나에 사람 여럿의 질문이 든다(같은 관측을 한 사람끼리만 묶는다). 판단의 종류는 줄이
    아니라 **질문 id** 가 말한다 — CrowdMind 가 만든다.
        p<사람>_<번호>      관측이 일으킨 급한 판단        → 과제 emergency
        r<사람>_<번호>      다음 활동을 고르는 일상 판단   → 과제 routine
        route<사람>_<번호>  층 이동 습관                   → 다루지 않는다
    급한 줄에 r 질문이 섞이는 것은 일상 질문의 답이 늦어 사람이 기다리게 되어 급한 줄로 올라온
    경우다. 그래서 줄이 아니라 질문 id 로 과제를 가르고 줄은 개수만 보고한다. 두 과제는 선택지도
    맥락도 달라 한 모델에 섞지 않는다. route 는 사람 설명(성별·고령·짐) 말고 맥락이 없다.
    같은 폴더의 crowd-<UTC>.jsonl 은 CrowdMetrics 의 성능·정지 지표라 읽지 않는다.

    레코드는 purpose 와 lane 이 짝이고, http 200 이며 답이 있어야 쓴다. 질문은 type=choice 이고
    JEV 의 Choice 가 그 질문의 후보(criteria) 안에 있어야 쓴다(JevClient.Parse 가 이미 후보 밖 답을
    버리지만 기록에서 다시 확인한다). 사람 머리 문장(Passenger 설명·하던 일)이나 상태(state)의 모양이
    지금 CrowdMind 가 만드는 것과 다르면 기본값으로 메우지 않고 그 질문을 버려 '건너뜀' 에 센다.
    계기를 일으킨 문장은 모양을 검사하지 않는다 — 아래 '계기 문맥' 처럼 낱말로 읽으므로 새 사건·새
    단서·새 문구도 그대로 특징이 된다.

정답이 아니라 JEV 의 답을 배운다 (그래서 '증류'다)
    라벨은 JEV 의 Choice(최고 선택)다. 게임은 JEV 의 확률분포에서 뽑고, 그 사이 조건이 바뀌면
    다음 선택지를 쓰므로 실제 승객 행동과 같지 않을 수 있다. 잘 맞으면 "이 판단은 규칙으로
    대체할 수 있다", 못 맞으면 "이 판단에는 기록하지 않은 맥락이 들어간다" 는 뜻이다.
    선택지 번호가 붙은 것(shop_0·shop_1·… / leave_0·leave_1·…)은 요청마다 게임이 섞거나 거리순으로
    뽑은 자리 번호라 같은 번호가 같은 곳이 아니다. 한 질문 안에 같은 이름에 번호가 둘 이상 붙은
    경우를 기록에서 찾아 그 이름을 하나의 라벨로 묶는다(목록을 손으로 적지 않는다). 그 밖의
    선택지 이름은 그대로 라벨이다.

특징 — JEV 에게 실제로 보낸 것만, 사람·맥락 단위로
    * 사람: 성별·고령·짐(Passenger 설명), 하던 일(Doing 의 활동 문구).
    * 상태(state): 열차 안내판 문구, 지금 사람이 지각한 것의 개수·지각 방식·설명에 나온 낱말,
      들은 단서, 방송을 들었는지, 역무원이 직접 말했는지. 키가 없으면 '없다'는 뜻이라 0 이다.
    * emergency 계기 문맥: 사람 머리 문장 다음의 자유 문장(무엇을 보았나·단서·지시·막힘·흔들림 등,
      CrowdMind 가 계기마다 다르게 쓴다)에서 위치 구절(“N m away, at …”)과 숫자를 떼고 낱말을
      모아 '맥락:<낱말>' 로 쓴다. 계기 종류를 목록으로 열거하지 않는다. 같은 문장에서 뽑는 수치는
      위험까지 거리, 10 m 안 사람 수·그중 대피 중인 수, 역무원이 눈앞에 있는지·거리, 이미 한 행동
      수(와 그 설명 낱말)이고, 그 문장이 없는 값은 모르는 값이다.
    * routine 문장: 여정 문구, 출발까지 남은 분, 이전 활동 수, 나머지 문장의 낱말(맥락:<낱말>).
    * 제시=<이름>: JEV 가 받은 후보 목록. 코드가 문맥으로 걸러 내놓으므로 후보 목록 자체가 문맥
      정보를 담는다. 그래서 기준선도 '제시된 것 중 최다' 이고, 모델 예측도 제시된 것 안에서만 고른다.
    문장에 없는 값(거리·시간·주변 인원)은 NaN 으로 두고 학습 폴드 안에서만 중앙값으로 메운 뒤
    '없었음' 표시를 붙인다. 0 으로 메우면 '가장 가깝다' 와 '말하지 않았다' 가 섞인다. 0 으로 두는 것은
    그 문장이 '있을 때만 생기는' 개수(이미 한 행동·이전 활동)뿐이다.
    모든 행에 같은 값으로 있는 특징(매 질문에 붙는 고정 안내 문구의 낱말 등)은 정보가 없어 뺀다.
    넣지 않는 것: 승객 번호·질문 id·위험 id 같은 개체 번호, 지각한 위치 이름(where)·지역 이름,
    선택지 설명문(가게·출구·화장실 이름이 든다), 이전 활동 문장(가게 이름이 든다), 상수인 place,
    시각(clock — 근무 경과일 뿐이라 근무 안의 진행 정도를 끌어들인다).

검증
    근무(파일) 단위 GroupKFold 다. 같은 근무의 질문은 세계가 같아 무작위로 나누면 같은 근무가 학습·
    시험에 함께 들어가 정확도가 부풀려진다. 같은 세계(seed)에서 나온 파일들은 독립이 아니므로 그
    경우 점수가 낙관적일 수 있다. 특징 사전과 결측 대체도 학습 폴드에서만 배운다.
    폴드마다 모든 모델이 같은 시험 표본을 본다. 기준선을 넘는지가 전부다.

받지 않는 기록 — 옛 기록과 옛 증거의 한계
    #259 이전 JevClient 는 군중 질의를 purpose=crowd 한 종류로 남겼고 상태도 PublicState
    (people_by_area · visible_situation · staff)였다. 질문 문장·상태·선택지 모양이 지금과 달라
    이 스크립트는 그 기록을 읽지 않고 '건너뜀' 에 옛 형식으로 센다. 섞어 학습하지 않는다.
    evidence/crowd-20260930-110054.json(743표본·5근무·purpose=crowd)은 그 옛 기록에서 나온 것이다.
    라벨도 특징도 스키마도 달라 지금 기록의 성적으로 읽으면 안 되고 이 스크립트로 재현되지도
    않는다. 지금 기록에 대한 증거는 새로 돌려 새 파일로 남긴다.

하지 않는 것
    사건 합성(IncidentDirector 의 후보 임박도)은 다루지 않는다. 그쪽 질문은 Score 이고
    label_causes.py → train_imminence.py 가 맡는다.

실행
    python workers/learning/distil_crowd.py
    python workers/learning/distil_crowd.py --logs "<jev-runs 폴더>" --out workers/learning/evidence
"""

import argparse
import collections
import datetime
import glob
import json
import math
import os
import re
import sys

try:
    import numpy as np
    from sklearn.feature_extraction import DictVectorizer
    from sklearn.impute import SimpleImputer
    from sklearn.linear_model import LogisticRegression
    from sklearn.metrics import accuracy_score, classification_report
    from sklearn.model_selection import GroupKFold
    from sklearn.pipeline import Pipeline
    from sklearn.preprocessing import StandardScaler
    from sklearn.tree import DecisionTreeClassifier, export_text
except ImportError as missing:
    sys.exit("필요한 패키지가 없습니다: %s\n  pip install -r workers/learning/requirements-core.txt" % missing.name)


# Unity 의 Application.persistentDataPath 아래에 JevClient 가 쓴다.
DEFAULT_LOGS = os.path.join(
    os.path.expanduser("~"), "AppData", "LocalLow", "DefaultCompany", "CHOOGuard", "jev-runs")

MIN_ROWS = 50            # 과제 하나에 이보다 적으면 학습하지 않는다
MAX_FOLDS = 5
MEANINGFUL_GAIN = 0.02   # 기준선보다 이만큼은 나아야 '넘었다' 고 적는다
NAN = float("nan")
BASELINE = "제시된 것 중 최다(기준선)"

# ── 지금 JevClient·CrowdMind 가 만드는 것 ────────────────────────────────────
# CrowdMind.Send 가 purpose 를 정하고 JevClient.LogLine 이 lane 을 같이 적는다.
CROWD_LANES = {"crowd-urgent": "CrowdUrgent", "crowd-routine": "CrowdRoutine"}
LEGACY_PURPOSE = "crowd"
# 질문 id: CrowdMind.Raise 는 "p", NewEveryday 는 "r" · "route" 를 앞에 붙이고 사람 번호와 일련번호를 잇는다.
QUESTION_ID = re.compile(r"^(route|p|r)\d+_\d+$")
TASKS = {"p": "emergency", "r": "routine"}
TASK_TITLES = {
    "emergency": "급한 판단 — 관측이 일으킨 승객 반응 (질문 id p…)",
    "routine": "일상 판단 — 다음 활동 (질문 id r…)",
}
DRIFT = "질문: 모양이 지금 CrowdMind 와 다름 — "

# CrowdMind.Profile: "Passenger #N (woman|man[, elderly][, with a large suitcase|, with a bag])"
PROFILE = r"Passenger #\d+ \((woman|man)(, elderly)?(, with a large suitcase|, with a bag)?\)"
# CrowdMind.Situation (급한 질문) 머리: 사람 설명과 하던 일(Doing = 활동 문구 + " (" + 지역 + ")").
URGENT_HEAD = re.compile("^" + PROFILE + r", currently (.+?) \(.*?\)\. ")
# CrowdMind.RoutineQuestion 머리: 사람 설명, 여정(Trip), 열차 안내, 하던 일.
ROUTINE_HEAD = re.compile("^" + PROFILE + r"\. Trip: (.+?)\. Train: .+?\. Now: (.+?) \(.*?\)\. ")
# 머리 뒤의 문장에서 수치를 뽑는 곳. 없으면 모르는 값(NaN)이거나, '있을 때만 생기는 개수' 는 0 이다.
NEARBY = re.compile(r"People within 10 m: (\d+), of them leaving: (\d+)\. ")
HAZARD_AWAY = re.compile(r"(\d+) m away, at ")
STAFF_NEAR = re.compile(r"A uniformed station staff member is about (\d+) m from them\. ")
PRIOR_ACTS = re.compile(r"Since they noticed, they have already: (.+?)\. (?=[A-Z])")
EARLIER = re.compile(r"Earlier: (.+?)\. (?=[A-Z])")
# 위험의 위치 구절. 위치 이름은 개체라 낱말로 쓰지 않는다. 문장 끝(마침표+대문자·문장 끝) 또는 괄호 닫힘(“)), …”) 앞까지 지운다.
PLACE = re.compile(r"\d+ m away, at .*?(?=\)+,|\. [A-Z]|\.?\s*$)")
WORD = re.compile(r"[A-Za-z가-힣]+")      # 숫자는 낱말이 아니다(개체 번호·시각·거리가 섞이지 않게)
LEAVES_IN = re.compile(r"leaves in about (\d+) min")
SLOT = re.compile(r"^(.+)_(\d+)$")

Row = collections.namedtuple("Row", "task source lane features offered choice top")


class Shape(ValueError):
    """질문 문장이나 상태가 지금 CrowdMind 가 만드는 모양이 아니다."""


def digits(text):
    """번호(객차·출발 분)는 '#' 로 접는다. 개체 번호가 범주를 쪼개지 않게."""
    return re.sub(r"\d+", "#", text)


def words(text, prefix):
    """자유 문장의 낱말을 있음(1) 표시로 모은다. 닫힌 목록 없이 문장이 바뀌면 낱말도 따라 바뀐다."""
    return {prefix + word.lower(): 1 for word in WORD.findall(text)}


def person(sex, elderly, luggage):
    return {
        "성별": "여" if sex == "woman" else "남",
        "고령": elderly is not None,
        "짐": (luggage or "").replace(", with ", "") or "없음",
    }


def state_features(state):
    """CrowdMind.State 가 싣는 것. place(상수)·clock(근무 경과)·where(위치 이름)는 쓰지 않는다."""
    board = state.get("train_board")
    seen = state.get("perceiving", [])
    heard = state.get("heard", [])
    if not isinstance(board, str) or not isinstance(seen, list) or not isinstance(heard, list):
        raise Shape("state")
    # perceiving·heard 는 사람이 아는 것이 있을 때만 키가 생긴다. 키가 없다 = 그만큼 모른다.
    bag = {"열차상태": digits(board), "보는것수": len(seen), "들은것수": len(heard)}
    for item in seen:
        if not isinstance(item, dict) or not isinstance(item.get("what"), str) or not isinstance(item.get("how"), str):
            raise Shape("perceiving")
        way = "지각방식=" + item["how"]
        bag[way] = bag.get(way, 0) + 1
        for word in re.findall(r"[0-9A-Za-z가-힣]+", item["what"]):
            bag["본것:" + digits(word)] = 1
    for cue in heard:
        if not isinstance(cue, str):
            raise Shape("heard")
        bag["들음=" + digits(cue)] = 1
    bag["방송들음"] = "announcement" in state
    bag["역무원이직접말함"] = "a_station_staff_member_told_them_directly" in state
    return bag


def emergency_features(text, state):
    head = URGENT_HEAD.match(text)
    if head is None:
        raise Shape("머리 문장")
    bag = state_features(state)
    bag.update(person(head.group(1), head.group(2), head.group(3)))
    bag["하던일"] = digits(head.group(4))
    rest = text[head.end():]
    # '이미 한 행동' 문장은 한 일이 있을 때만 있다. 없으면 정말 0 이다. 설명문(선택지 설명)은 낱말로도 쓴다.
    acts = PRIOR_ACTS.search(rest)
    bag["이미한행동수"] = len(acts.group(1).split("; ")) if acts else 0
    if acts:
        bag.update(words(acts.group(1), "이미한행동:"))
    rest = PRIOR_ACTS.sub("", rest)
    # 주변 인원은 매 질문에 있다. 없으면 0 이 아니라 모르는 값이다.
    nearby = NEARBY.search(rest)
    bag["주변인원10m"] = float(nearby.group(1)) if nearby else NAN
    bag["주변대피중10m"] = float(nearby.group(2)) if nearby else NAN
    rest = NEARBY.sub("", rest)
    # 거리 문장은 위험이 자리를 가졌고 사람이 그것을 지각했을 때만 있다. 없으면 모르는 값이다.
    away = HAZARD_AWAY.search(rest)
    bag["위험까지m"] = float(away.group(1)) if away else NAN
    # 역무원 문장은 같은 층 20 m 안일 때만 있다. 없으면 눈앞에 없다는 뜻이고 거리는 모른다.
    staff = STAFF_NEAR.search(rest)
    bag["역무원보임"] = staff is not None
    bag["역무원거리m"] = float(staff.group(1)) if staff else NAN
    rest = STAFF_NEAR.sub("", rest)
    # 남은 자유 문장이 계기 문맥이다(무엇을 보았나·단서·지시·막힘·기침·이미 지시받음 …).
    bag.update(words(PLACE.sub(" ", rest), "맥락:"))
    return bag


def routine_features(text, state):
    head = ROUTINE_HEAD.match(text)
    if head is None:
        raise Shape("일상 질문 머리 문장")
    sex, elderly, luggage, trip, doing = head.groups()
    bag = state_features(state)
    bag.update(person(sex, elderly, luggage))
    bag["여정"] = digits(trip)
    # 출발 문구는 출발 여정일 때만 있다. 쇼핑객·배웅객은 모르는 값이다.
    minutes = LEAVES_IN.search(trip)
    bag["출발까지분"] = float(minutes.group(1)) if minutes else NAN
    bag["현재행동"] = digits(doing)
    rest = text[head.end():]
    # 'Earlier' 문장은 이전 활동이 있을 때만 있다. 없으면 정말 0 이다. 문장 자체는 가게 이름이 들어 쓰지 않는다.
    earlier = EARLIER.search(rest)
    bag["이전활동수"] = len(earlier.group(1).split("; ")) if earlier else 0
    bag.update(words(EARLIER.sub(" ", rest), "맥락:"))
    return bag


# ── 읽기 ────────────────────────────────────────────────────────────────────

def find_logs(folder):
    files = sorted(glob.glob(os.path.join(folder, "jev-*.jsonl")))
    if not files:
        sys.exit("JEV 요청 기록(jev-*.jsonl)을 찾지 못했습니다: %s" % folder)
    return files, len(glob.glob(os.path.join(folder, "crowd-*.jsonl")))


def read_question(question_id, question, answers, state, lane, source, rows, skipped):
    match = QUESTION_ID.match(question_id)
    if match is None:
        skipped["질문: id 가 CrowdMind 가 만드는 모양(p·r·route)이 아님"] += 1
        return
    if match.group(1) == "route":
        skipped["질문: 층 이동 습관(route) — 사람 설명 말고 맥락이 없어 다루지 않음"] += 1
        return
    task = TASKS[match.group(1)]
    answer = answers.get(question_id)
    if answer is None:
        skipped["질문: JEV 가 답하지 않음"] += 1
        return
    if not isinstance(question, dict) or question.get("type") != "choice" or not isinstance(question.get("instructions"), str):
        skipped["질문: choice 문항이 아님"] += 1
        return
    criteria = question.get("criteria")
    if not isinstance(criteria, dict):
        skipped["질문: 후보(criteria)가 없음"] += 1
        return
    if len(criteria) < 2:
        skipped["질문: 후보가 1개 이하 — 고를 것이 없다"] += 1
        return
    choice = answer.get("Choice") if isinstance(answer, dict) else None
    if not isinstance(choice, str) or choice not in criteria:
        skipped["질문: 답이 후보 밖"] += 1
        return
    probabilities = answer.get("Probabilities")
    if not probabilities:
        top = None
    elif (isinstance(probabilities, dict) and set(probabilities) <= set(criteria)
          and all(isinstance(v, (int, float)) and not isinstance(v, bool) for v in probabilities.values())):
        top = probabilities.get(choice)
    else:
        skipped["질문: 확률이 후보와 맞지 않음"] += 1
        return
    build = emergency_features if task == "emergency" else routine_features
    try:
        features = build(question["instructions"], state)
    except Shape as error:
        skipped[DRIFT + "%s (%s)" % (task, error)] += 1
        return
    rows.append(Row(task, source, lane, features, tuple(criteria), choice, top))


def read_record(record, source, rows, skipped):
    purpose = record.get("purpose")
    if purpose == LEGACY_PURPOSE:
        skipped["레코드: 옛 형식 purpose=crowd (#259 이전 질문·상태 스키마, 섞지 않음)"] += 1
        return
    lane = CROWD_LANES.get(purpose) if isinstance(purpose, str) else None
    if lane is None:
        skipped["레코드: 군중 질의가 아님 purpose=%s" % purpose] += 1
        return
    if record.get("lane") != lane:
        skipped["레코드: purpose 와 lane 이 짝이 아님"] += 1
        return
    answers = record.get("answers")
    if record.get("http") != 200 or not isinstance(answers, dict) or not answers:
        skipped["레코드: 답이 없음 (http=%s)" % record.get("http")] += 1
        return
    request = record.get("request")
    state = request.get("state") if isinstance(request, dict) else None
    questions = request.get("questions") if isinstance(request, dict) else None
    if not isinstance(state, dict) or not isinstance(questions, dict):
        skipped["레코드: request 에 state·questions 가 없음"] += 1
        return
    for question_id, question in questions.items():
        read_question(question_id, question, answers, state, lane, source, rows, skipped)


def collect(files):
    """jev-*.jsonl 을 읽어 (행, 건너뜀 집계, purpose 분포, 레코드 수)를 돌려준다."""
    rows, skipped, purposes = [], collections.Counter(), collections.Counter()
    records = broken = 0
    for path in files:
        source = os.path.basename(path)
        with open(path, encoding="utf-8", errors="replace") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                try:
                    record = json.loads(line)
                except ValueError:
                    broken += 1     # 근무가 중간에 끊기면 마지막 줄이 잘릴 수 있다
                    continue
                if not isinstance(record, dict):
                    broken += 1
                    continue
                records += 1
                purposes[str(record.get("purpose"))] += 1
                read_record(record, source, rows, skipped)
    if broken:
        skipped["레코드: 깨진 줄"] = broken
    return rows, skipped, purposes, records


# ── 표본 만들기 ─────────────────────────────────────────────────────────────

def slot_stems(rows):
    """한 질문 안에서 같은 이름에 번호가 둘 이상 붙은 것의 이름. 그 이름은 자리 번호라 라벨로 묶는다."""
    stems = set()
    for row in rows:
        seen = collections.Counter()
        for key in row.offered:
            match = SLOT.match(key)
            if match:
                seen[match.group(1)] += 1
        stems.update(stem for stem, count in seen.items() if count >= 2)
    return stems


def canonical(key, slots):
    match = SLOT.match(key)
    return match.group(1) if match and match.group(1) in slots else key


def drop_constant(samples):
    """모든 행에 같은 값으로 있는 특징(고정 안내 문구의 낱말 등)은 정보가 없어 뺀다. 문구를 열거하지 않고 값으로 본다."""
    if not samples:
        return samples
    constant = set()
    for key, value in samples[0].items():
        if all(key in other and (other[key] == value or (value != value and other[key] != other[key])) for other in samples):
            constant.add(key)
    return [{k: v for k, v in bag.items() if k not in constant} for bag in samples]


def build(task, rows, slots):
    samples, labels, offered, groups, lanes, tops = [], [], [], [], [], []
    for row in rows:
        if row.task != task:
            continue
        names = {canonical(key, slots) for key in row.offered}
        bag = dict(row.features)
        for name in names:
            bag["제시=" + name] = 1
        samples.append(bag)
        labels.append(canonical(row.choice, slots))
        offered.append(names)
        groups.append(row.source)
        lanes.append(row.lane)
        tops.append(row.top)
    return drop_constant(samples), labels, offered, groups, lanes, tops


# ── 학습·평가 ───────────────────────────────────────────────────────────────

def pipeline(estimator, scale):
    """특징 사전·결측 대체는 폴드마다 학습 표본으로만 배운다(시험 폴드를 미리 보지 않는다)."""
    steps = [
        ("vec", DictVectorizer(sparse=False)),
        ("fill", SimpleImputer(strategy="median", add_indicator=True)),
    ]
    if scale:
        steps.append(("scale", StandardScaler()))
    steps.append(("clf", estimator))
    return Pipeline(steps)


def predict_offered(model, x, mask):
    """행마다 제시된 후보 안에서만 가장 그럴듯한 것을 고른다. 훈련에서 못 본 후보는 확률 0 이다."""
    proba = np.zeros(mask.shape)
    proba[:, model.classes_] = model.predict_proba(x)
    return np.where(mask, proba, -1.0).argmax(axis=1)


def finite(value):
    """JSON 에 NaN·Infinity 를 쓰지 않는다. 유한하지 않으면 null."""
    value = float(value)
    return value if math.isfinite(value) else None


def run_task(task, rows, slots, depth, show_tree):
    samples, labels, offered, groups, lanes, tops = build(task, rows, slots)
    print("\n== %s ==" % TASK_TITLES[task])
    if len(samples) < MIN_ROWS:
        print("표본이 %d건뿐입니다(최소 %d). 근무를 더 돌려 수집하세요." % (len(samples), MIN_ROWS))
        return None

    counts = collections.Counter(labels)
    shifts = sorted(set(groups))
    lane_counts = collections.Counter(lanes)
    print("표본 %d건 · 클래스 %d개 · 근무 파일 %d개 · 줄 %s" % (len(samples), len(counts), len(shifts), dict(lane_counts)))
    for name, count in counts.most_common():
        print("  %-16s %4d  (%4.1f%%)" % (name, count, 100.0 * count / len(labels)))
    if len(counts) < 2:
        print("클래스가 하나뿐이라 배울 것이 없습니다.")
        return None

    # 같은 근무의 질문은 세계가 거의 같다. 무작위로 나누면 같은 근무가 학습·시험에 함께 들어가
    # 정확도가 부풀려진다. 그래서 파일(근무) 단위로 가른다.
    folds = min(len(shifts), MAX_FOLDS)
    if folds < 2:
        print("근무 파일이 %d개뿐입니다. 누수 없는 검증을 못 합니다 — 다른 시드로 더 수집하세요." % len(shifts))
        return None

    classes = sorted(counts)
    index = {name: i for i, name in enumerate(classes)}
    y = np.array([index[name] for name in labels])
    mask = np.zeros((len(labels), len(classes)), dtype=bool)
    for row, names in enumerate(offered):
        for name in names:
            if name in index:
                mask[row, index[name]] = True
    x = np.empty(len(samples), dtype=object)
    for i, bag in enumerate(samples):
        x[i] = bag
    group = np.array(groups)
    splits = list(GroupKFold(n_splits=folds).split(x, y, group))

    models = {
        "결정나무": lambda: pipeline(DecisionTreeClassifier(max_depth=depth, random_state=0), scale=False),
        "로지스틱": lambda: pipeline(LogisticRegression(max_iter=2000), scale=True),
    }
    scores = {BASELINE: []}
    scores.update({name: [] for name in models})
    fitted = {}
    for train, test in splits:
        freq = np.bincount(y[train], minlength=len(classes))
        base = np.where(mask[test], freq, -1).argmax(axis=1)
        scores[BASELINE].append(float(accuracy_score(y[test], base)))
        for name, make in models.items():
            fitted[name] = make().fit(x[train], y[train])
            predicted = predict_offered(fitted[name], x[test], mask[test])
            scores[name].append(float(accuracy_score(y[test], predicted)))
    for name, fold_scores in scores.items():
        print("\n%s  평균 %.3f  (폴드별 %s)"
              % (name, float(np.mean(fold_scores)), " ".join("%.3f" % s for s in fold_scores)))

    baseline = float(np.mean(scores[BASELINE]))
    best = max(models, key=lambda k: float(np.mean(scores[k])))
    gain = float(np.mean(scores[best])) - baseline
    print("\n가장 나은 모델: %s  기준선 대비 %+.3f" % (best, gain))
    beats = gain > MEANINGFUL_GAIN
    if beats:
        print("→ 기준선을 넘었습니다. 이 표본에서는 기록한 상태로 JEV 의 최고 선택이 상당 부분 설명됩니다.")
    else:
        print("→ 기준선을 의미 있게 넘지 못했습니다. 기록한 특징만으로는 이 판단을 설명하지 못했습니다.")
    if len(shifts) < MAX_FOLDS:
        print("  (근무 파일이 %d개뿐이라 폴드마다 점수 차이가 큽니다. 결론으로 삼지 않습니다.)" % len(shifts))

    shown = [t for t in tops if t is not None and math.isfinite(t)]
    top_mean = float(np.mean(shown)) if shown else None
    if top_mean is not None:
        print("JEV 가 고른 선택지에 준 확률 평균 %.2f (%d건). 게임은 확률분포에서 뽑으므로 실제 행동은 최고 선택과 다를 수 있습니다."
              % (top_mean, len(shown)))

    # 마지막 폴드로 클래스별 성적과 규칙을 본다. 평균만 보면 희소 클래스가 숨는다.
    train, test = splits[-1]
    tree = fitted["결정나무"]
    predicted = predict_offered(tree, x[test], mask[test])
    present = sorted(set(y[test]) | set(predicted))
    print("\n클래스별 성적 (마지막 폴드, 결정나무)")
    print(classification_report(y[test], predicted, labels=present,
                                target_names=[classes[i] for i in present], zero_division=0))

    names = tree[:-1].get_feature_names_out()
    ranked = sorted(zip(names, tree[-1].feature_importances_), key=lambda pair: -pair[1])
    print("기여도 상위 특징")
    for name, weight in ranked[:12]:
        if weight <= 0:
            break
        print("  %-34s %.3f" % (name, weight))
    if show_tree:
        print("\n학습한 규칙\n" + export_text(tree[-1], feature_names=list(names), max_depth=3))

    return {
        "title": TASK_TITLES[task],
        "rows": len(samples),
        "shifts": len(shifts),
        "folds": folds,
        "lanes": dict(lane_counts),
        "classes": dict(counts),
        "baseline": finite(baseline),
        "best": best,
        "gain": finite(gain),
        "beats_baseline": beats,
        "models": {name: {"mean": finite(np.mean(f)), "folds": [finite(v) for v in f]} for name, f in scores.items()},
        "features": [{"name": str(n), "importance": finite(w)} for n, w in ranked if w > 0],
        "top_probability": {"mean": top_mean, "rows": len(shown)},
    }


def main():
    parser = argparse.ArgumentParser(description="JEV 군중 판단 증류 시험 (PR #259 이후 군중 질의)")
    parser.add_argument("--logs", default=DEFAULT_LOGS, help="jev-runs 폴더")
    parser.add_argument("--out", default=os.path.join("workers", "learning", "evidence"))
    parser.add_argument("--depth", type=int, default=6, help="결정나무 최대 깊이")
    parser.add_argument("--show-tree", action="store_true", help="학습한 규칙을 글로 출력")
    parser.add_argument("--task", choices=("all", "emergency", "routine"), default="all",
                        help="emergency=관측이 일으킨 급한 판단 · routine=다음 활동 · all=둘 다(따로 학습)")
    args = parser.parse_args()

    files, metrics = find_logs(args.logs)
    rows, skipped, purposes, records = collect(files)
    print("JSONL %d개 · 레코드 %d건" % (len(files), records))
    if metrics:
        print("crowd-*.jsonl %d개는 CrowdMetrics 의 성능·정지 지표라 읽지 않았습니다." % metrics)
    print("purpose 분포:", dict(purposes))
    print("건너뜀:")
    for reason, count in skipped.most_common():
        print("  %6d건  %s" % (count, reason))

    legacy = sum(count for reason, count in skipped.items() if "옛 형식" in reason)
    if legacy:
        print("\n주의: 옛 형식(purpose=crowd, #259 이전) 레코드 %d건은 질문·상태 스키마가 달라 쓰지 않았습니다. 섞어 학습하지 않습니다."
              % legacy)
    drift = sum(count for reason, count in skipped.items() if reason.startswith(DRIFT))
    if drift:
        print("\n주의: 현재 형식의 질문 %d건(%d건 중)이 지금 CrowdMind 머리 문장·상태 모양과 달라 버려졌습니다."
              " CrowdMind.Observation.cs·CrowdMind.Routine.cs 가 바뀌었는지 확인하세요." % (drift, drift + len(rows)))

    slots = slot_stems(rows)
    if slots:
        print("\n번호가 붙은 선택지는 한 라벨로 묶습니다: %s" % ", ".join(sorted(slot + "_N" for slot in slots)))

    done = {}
    for task in ("emergency", "routine") if args.task == "all" else (args.task,):
        result = run_task(task, rows, slots, args.depth, args.show_tree)
        if result is not None:
            done[task] = result
    if not done:
        sys.exit("\n학습할 수 있는 표본이 없습니다. 현재 형식(crowd-urgent·crowd-routine)의 군중 질의가 필요합니다 —"
                 " 위 '건너뜀' 이 옛 형식이면 #259 이후 근무를 새로 수집하세요.")

    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S")
    os.makedirs(args.out, exist_ok=True)
    report = {
        "at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "input": "JevClient run log jev-*.jsonl, purpose crowd-urgent · crowd-routine (PR #259 이후 형식)",
        "files": len(files),
        "records": records,
        "purposes": dict(purposes),
        "skipped": dict(skipped),
        "slot_labels": sorted(slots),
        "label": "JEV 의 Choice(최고 선택). 실제 승객 행동이 아니다.",
        "tasks": done,
    }
    path = os.path.join(args.out, "crowd-%s.json" % stamp)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=2, allow_nan=False)
    print("\n기록: %s" % path)


if __name__ == "__main__":
    main()
