"""JEV 의 군중 판단을 재현하는 작은 분류기를 학습해 본다.

무엇을 하는가
    ShiftSampleTests 하네스가 근무를 돌리면 JevClient 가 질의·응답을 JSONL 로 남긴다.
    이 스크립트는 그 기록에서 "승객이 다음에 무엇을 하는가" 판단만 추려
    상태·승객 설명으로 JEV 의 선택을 맞출 수 있는지 본다.

무엇을 하지 않는가 (읽는 사람이 오해하지 않도록 먼저 적는다)
    * **사건 합성(compose)을 학습하지 않는다.** 2026-09-30 기준 수집분 516건 중 compose 는
      5건뿐이다. 시간 압축(CG_SHIFT_SCALE>1)을 쓰면 분당 90건 실시간 상한을 군중 질의가
      먼저 채워 합성이 굶는다. compose 를 배우려면 1배속 수집이 필요하다.
    * **정답을 배우는 것이 아니다.** 라벨은 JEV 가 고른 값이다. 그래서 이 학습은 '증류'다 —
      잘 맞으면 "JEV 의 이 판단은 규칙으로 대체할 수 있다" 는 뜻이고,
      못 맞으면 "이 판단에는 기록하지 않은 맥락이 들어간다" 는 뜻이다. 둘 다 쓸모가 있다.
    * **층 이동(route) 판단은 다루지 않는다.** 실측 분포가 escalator 565 · elevator 15 ·
      stairs 0 으로 다수 클래스만 찍어도 97.4% 다. 배울 것이 없다.

왜 이 과제인가
    다음 행동은 11개 클래스에 749건이고 최다 클래스가 37.3% 다. 기준선을 이길 여지가 있다.

사건 합성으로 넘어갈 때 (2026-10-01 갱신 — PR #259 로 과제 자체가 바뀐다)
    **라벨이 Choice 에서 Score 로 바뀐다.** 지금까지는 JEV 가 후보 하나를 Choice 로 골랐다.
    #259 이후에는 후보마다 **곧 일어날 정도(임박도)를 Score 말 단계로** 판단하고, 코드가 그
    단계를 연속시간 위험률로 바꿔 경쟁 위험으로 추첨한다. 그래서 학습 과제는

        (가변 선택지 중 어느 것을 골랐나)  ->  (후보 하나하나에 몇 단계를 줬나)

    로 바뀐다. **이쪽이 학습하기 더 낫다.** 선택지 개수가 매 요청 달라지는 문제(실측 5~10개,
    #259 이후 29~31개)가 사라지고, 후보 하나가 표본 하나가 되어 표본 수가 수십 배로 는다.
    한 요청에서 고른 하나만 양성이고 나머지는 음성이던 불균형도 없어진다.
    평가 지표도 정확도가 아니라 단계 회귀 오차 / 순위 상관이 맞다.

    **후보 키를 특징으로 쓰면 안 된다.** 키는 `overheat_23` · `collapse_8` 처럼 그 근무에만
    있는 개체 번호가 붙는다. 그대로 쓰면 다음 근무에서 전부 미지 라벨이 된다. 후보의
    **성질**(무엇이·어디서·사람이 몇 명 근처인지·이미 난 사건이 몇 건인지)로 특징을 만들어야 한다.
    이것은 하드룰(비상상황은 합성한다 / 닫힌 목록을 만들지 않는다 / 원인은 맵에 있는 것에서
    나온다)과도 같은 방향이다.

    **수집 조건도 바뀐다.** 키가 없으면 비상상황이 아예 만들어지지 않으므로(로컬 대체 합성 금지)
    compose 표본은 유효한 키로 돌린 근무에서만 나온다. 또 판단이 1초마다 일어나 분당 90건
    실시간 상한에 더 쉽게 걸리므로, 시간 압축(CG_SHIFT_SCALE>1)은 수집에 쓰면 안 된다.

실행
    python workers/learning/distil_crowd.py
    python workers/learning/distil_crowd.py --logs "<jev-runs 폴더>" --out workers/learning/evidence
"""

import argparse
import collections
import datetime
import glob
import json
import os
import re
import sys

try:
    import numpy as np
    from sklearn.dummy import DummyClassifier
    from sklearn.feature_extraction import DictVectorizer
    from sklearn.linear_model import LogisticRegression
    from sklearn.metrics import accuracy_score, classification_report
    from sklearn.model_selection import GroupKFold
    from sklearn.tree import DecisionTreeClassifier, export_text
except ImportError as missing:
    sys.exit("필요한 패키지가 없습니다: %s\n  pip install -r workers/learning/requirements-core.txt" % missing.name)


# Unity 의 Application.persistentDataPath 아래에 JevClient 가 쓴다.
DEFAULT_LOGS = os.path.join(
    os.path.expanduser("~"), "AppData", "LocalLow", "DefaultCompany", "CHOOGuard", "jev-runs")

FAMILY = re.compile(r"_\d+$")          # shop_0 · shop_1 을 같은 families 로 묶는다
MINUTES = re.compile(r"leaves in about (\d+) min")
PASSENGER = re.compile(r"Passenger #(\d+) \(([^)]*)\)")
TRIP = re.compile(r"Trip: (.*?)\. Train:")
TRAIN = re.compile(r"Train: (.*?)\. Now:")
NOW = re.compile(r"Now: ([^(]*)\(([^)]*)\)")
CLOCK = re.compile(r"Clock (\d+):(\d+)")


def read(folder):
    """JSONL 을 읽어 (레코드, 출처 파일) 목록으로 돌려준다."""
    files = sorted(glob.glob(os.path.join(folder, "*.jsonl")))
    if not files:
        sys.exit("JSONL 을 찾지 못했습니다: %s" % folder)
    rows = []
    broken = 0
    for path in files:
        name = os.path.basename(path)
        with open(path, encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                try:
                    rows.append((json.loads(line), name))
                except ValueError:
                    broken += 1     # 근무가 중간에 끊기면 마지막 줄이 잘릴 수 있다
    if broken:
        print("깨진 줄 %d개를 건너뛰었습니다." % broken)
    return rows, files


def features(state, question):
    """JEV 에게 실제로 보낸 것만 특징으로 쓴다. 로그에 없는 값을 지어내지 않는다."""
    text = question.get("instructions", "")
    bag = {}

    person = PASSENGER.search(text)
    description = person.group(2) if person else ""
    bag["짐있음"] = "with a bag" in description
    bag["성별"] = "여" if "woman" in description else ("남" if "man" in description else "미상")

    trip = TRIP.search(text)
    bag["여정"] = trip.group(1)[:40] if trip else "미상"

    train = TRAIN.search(text)
    bag["열차상태"] = train.group(1) if train else "미상"

    now = NOW.search(text)
    bag["현재행동"] = now.group(1).strip() if now else "미상"
    zone = now.group(2).strip() if now else "미상"
    bag["현재구역"] = zone

    minutes = MINUTES.search(text)
    # 출발까지 남은 시간이 없는 승객(쇼핑객·배웅객)은 -1 로 구분한다. 0 으로 두면
    # '곧 출발' 과 '출발 없음' 이 같은 값이 되어 모델이 둘을 섞는다.
    bag["출발까지분"] = int(minutes.group(1)) if minutes else -1
    bag["출발정보있음"] = minutes is not None

    clock = CLOCK.search(text)
    bag["시"] = int(clock.group(1)) if clock else -1
    bag["분"] = int(clock.group(2)) if clock else -1

    people = state.get("people_by_area") or {}
    bag["구역인원"] = int(people.get(zone, 0))
    bag["역전체인원"] = int(sum(people.values()))
    bag["선택지수"] = len(question.get("criteria") or {})

    # 비상상황이 이미 눈에 보이는지. 이 값이 승객 행동을 바꾸는지가 궁금한 부분이다.
    bag["보이는상황수"] = len(state.get("visible_situation") or [])
    staff = state.get("staff") or {}
    bag["방송했음"] = bool(staff.get("public_announcement"))
    bag["보고건수"] = int(staff.get("reported", 0))
    bag["통제건수"] = int(staff.get("cordons", 0))
    return bag


def dataset(rows):
    """다음 행동 판단만 추린다. route 판단은 분포가 한쪽으로 쏠려 제외한다."""
    samples, labels, groups = [], [], []
    skipped = collections.Counter()
    for record, source in rows:
        if record.get("purpose") != "crowd":
            skipped[record.get("purpose") or "미상"] += 1
            continue
        state = (record.get("request") or {}).get("state") or {}
        questions = (record.get("request") or {}).get("questions") or {}
        answers = record.get("answers") or {}
        for key, question in questions.items():
            if key.startswith("route"):
                skipped["route"] += 1
                continue
            answer = answers.get(key)
            if not answer or not answer.get("Choice"):
                skipped["답없음"] += 1
                continue
            options = question.get("criteria") or {}
            # 선택지가 하나면 고를 것이 없다. 학습에 넣으면 정확도를 공짜로 올린다.
            if len(options) < 2:
                skipped["선택지1개"] += 1
                continue
            samples.append(features(state, question))
            labels.append(FAMILY.sub("", answer["Choice"]))
            groups.append(source)
    return samples, labels, groups, skipped


def main():
    parser = argparse.ArgumentParser(description="JEV 군중 판단 증류 시험")
    parser.add_argument("--logs", default=DEFAULT_LOGS, help="jev-runs 폴더")
    parser.add_argument("--out", default=os.path.join("workers", "learning", "evidence"))
    parser.add_argument("--depth", type=int, default=6, help="결정나무 최대 깊이")
    parser.add_argument("--show-tree", action="store_true", help="학습한 규칙을 글로 출력")
    args = parser.parse_args()

    rows, files = read(args.logs)
    print("JSONL %d개 · 레코드 %d건" % (len(files), len(rows)))

    samples, labels, groups, skipped = dataset(rows)
    print("건너뜀:", dict(skipped))
    if len(samples) < 50:
        sys.exit("표본이 %d건뿐입니다. 근무를 더 돌려 수집하세요." % len(samples))

    counts = collections.Counter(labels)
    shifts = sorted(set(groups))
    print("\n표본 %d건 · 클래스 %d개 · 근무 파일 %d개" % (len(samples), len(counts), len(shifts)))
    for name, count in counts.most_common():
        print("  %-16s %4d  (%4.1f%%)" % (name, count, 100.0 * count / len(labels)))

    vectoriser = DictVectorizer(sparse=False)
    x = vectoriser.fit_transform(samples)
    y = np.array(labels)
    group = np.array(groups)

    # 같은 근무의 질의는 상태가 거의 같다. 무작위로 나누면 같은 근무가 학습·시험에
    # 함께 들어가 정확도가 부풀려진다. 그래서 파일(근무) 단위로 가른다.
    folds = min(len(shifts), 5)
    if folds < 2:
        sys.exit("근무 파일이 %d개뿐입니다. 누수 없는 검증을 못 합니다 — 다른 시드로 더 수집하세요." % len(shifts))
    splitter = GroupKFold(n_splits=folds)

    candidates = {
        "최다클래스(기준선)": DummyClassifier(strategy="most_frequent"),
        "결정나무": DecisionTreeClassifier(max_depth=args.depth, random_state=0),
        "로지스틱": LogisticRegression(max_iter=2000),
    }

    scores = {}
    for name, model in candidates.items():
        fold_scores = []
        for train, test in splitter.split(x, y, group):
            model.fit(x[train], y[train])
            fold_scores.append(accuracy_score(y[test], model.predict(x[test])))
        scores[name] = fold_scores
        print("\n%s  평균 %.3f  (폴드별 %s)"
              % (name, float(np.mean(fold_scores)), " ".join("%.3f" % s for s in fold_scores)))

    baseline = float(np.mean(scores["최다클래스(기준선)"]))
    best = max((k for k in scores if "기준선" not in k), key=lambda k: float(np.mean(scores[k])))
    gain = float(np.mean(scores[best])) - baseline
    print("\n가장 나은 모델: %s  기준선 대비 %+.3f" % (best, gain))
    if gain <= 0.02:
        print("→ 기준선을 의미 있게 넘지 못했습니다. 기록한 특징만으로는 이 판단을 설명할 수 없습니다.")
    else:
        print("→ 기준선을 넘었습니다. 이 판단의 상당 부분은 기록한 상태로 설명됩니다.")

    # 마지막 폴드로 클래스별 성적과 규칙을 본다. 평균만 보면 희소 클래스가 숨는다.
    train, test = list(splitter.split(x, y, group))[-1]
    tree = DecisionTreeClassifier(max_depth=args.depth, random_state=0).fit(x[train], y[train])
    print("\n클래스별 성적 (마지막 폴드, 결정나무)")
    print(classification_report(y[test], tree.predict(x[test]), zero_division=0))

    names = vectoriser.get_feature_names_out()
    ranked = sorted(zip(names, tree.feature_importances_), key=lambda pair: -pair[1])
    print("기여도 상위 특징")
    for name, weight in ranked[:12]:
        if weight <= 0:
            break
        print("  %-34s %.3f" % (name, weight))

    if args.show_tree:
        print("\n학습한 규칙\n" + export_text(tree, feature_names=list(names), max_depth=3))

    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S")
    os.makedirs(args.out, exist_ok=True)
    report = {
        "at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "rows": len(samples),
        "shifts": len(shifts),
        "classes": dict(counts),
        "baseline": baseline,
        "models": {name: {"mean": float(np.mean(f)), "folds": f} for name, f in scores.items()},
        "features": [{"name": n, "importance": float(w)} for n, w in ranked if w > 0],
        "skipped": dict(skipped),
    }
    path = os.path.join(args.out, "crowd-%s.json" % stamp)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=2)
    print("\n기록: %s" % path)


if __name__ == "__main__":
    main()
