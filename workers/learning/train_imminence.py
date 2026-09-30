"""JEV 의 임박도 판단을 재현하는 모델을 학습하고, 정직하게 평가한다.

입력
    label_causes.py 가 펼친 `causes-*.jsonl`. 한 줄이 후보 하나다.

배우려는 것
    "지금 이 후보가 곧 일어날 정도" 를 JEV 가 매긴 수준(0 이 가장 낮음).
    추첨은 코드가 한다(수준 -> 초당 위험률 -> 경쟁 위험). 모델은 **수준 매기기만** 대신한다.

무엇이 진짜 질문인가
    단순히 "수준을 맞힐 수 있나"가 아니라, 종류만 본 기준선에 현재 Focus 상태를 더했을 때
    기록된 JEV 판단을 더 잘 재현하는지 묻는다. 종류가 상태와 무관하게 임박도를 결정한다고
    가정하지 않으며, 비교 기준선은 전역 평균보다 엄격한 **종류별 평균**으로 둔다.

        종류별 평균을 못 넘으면, 이 표본과 모델이 기록된 상태에서
        추가 예측력을 찾지 못했다는 뜻이다. 상태의 무가치·대응의 인과 효과를 입증하지는 않는다.

    이 기준선을 두지 않으면 MAE 가 낮게 나오는 것을 보고 "잘 배웠다" 고 오해한다.

세 가지로 잰다
    1. **수준 오차(MAE)** - 평균 몇 단계 틀리는가.
    2. **요청 안 수준 순위 상관(Spearman)** - 같은 요청에서 JEV 수준의 순서를 맞히는가.
       scale 별 위험률은 다르므로 이것만으로 실제 사건 추첨 순위를 재현했다고 하지 않는다.
    3. **최상위 일치** - 그 요청에서 가장 임박한 후보를 맞히는가.

읽을 때 조심할 것
    `hour` · `lastNewEmergency` 와 대응은 근무 진행에 함께 변한다. 예측력이 좋아져도
    대응 때문에 임박도가 변했다고 해석하지 않는다. 현재 Focus 에 없는 사람 수나
    사건 경과 초를 0으로 지어내지 않는다.

아직 못 재는 것
    **시간 보정** - 모델 수준으로 돌렸을 때 첫 사건까지 걸리는 시간 분포가 JEV 로 돌린 것과
    같은지. `Imminence.Rate` 의 scale 별 변환과 경쟁 위험을 재현해 별도로 검증해야 한다.
    **이 워커는 연구용이다. 제품의 실시간 JEV 판단을 대체하지 않는다.**

실행
    python workers/learning/train_imminence.py --labels workers/learning/labels
    python workers/learning/train_imminence.py --labels "D:/cg-data/labels" --show-tree
"""

import argparse
import collections
import datetime
import glob
import json
import os
import sys

from label_causes import LABEL_SCHEMA

try:
    import numpy as np
    from scipy.stats import spearmanr
    from sklearn.feature_extraction import DictVectorizer
    from sklearn.linear_model import Ridge
    from sklearn.model_selection import GroupKFold
    from sklearn.tree import DecisionTreeRegressor, export_text
except ImportError as missing:
    sys.exit("필요한 패키지가 없습니다: %s\n  pip install -r workers/learning/requirements-core.txt" % missing.name)


def read(folder):
    files = sorted(glob.glob(os.path.join(folder, "causes-*.jsonl")))
    if not files:
        sys.exit("causes-*.jsonl 을 찾지 못했습니다: %s\n  먼저 label_causes.py 를 돌리세요." % folder)
    rows = []
    for path in files:
        with open(path, encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if line:
                    row = json.loads(line)
                    if row.get("schema") != LABEL_SCHEMA or row.get("purpose") != "judge" or not row.get("scale"):
                        sys.exit("현재 judge 라벨이 아닙니다: %s · 최신 label_causes.py 로 다시 펼치세요." % path)
                    rows.append(row)
    return rows, files


def features(row):
    """모델이 볼 것. key·description 은 넣지 않는다 - 아래 주석 참조."""
    bag = {
        # 무엇인가. 임박도를 가장 크게 가르는 값이라 기준선도 이것으로 만든다.
        "kind": row.get("kind") or "미상",
        "levelCount": len(row.get("levels") or []),
        # 지금 역 상태
        "scale": row["scale"],
        "describedEmergencyCount": row["described_emergency_count"],
        "load": row["load"],
        "lastNewEmergency": row["last_new_emergency"],
        "agencies": row["agencies_on_scene"],
        "agenciesEnRoute": row["agencies_on_the_way"],
        "knows": row["staff_knows"],
        "reported": row["staff_reported"],
        "announced": row["staff_announced"],
        "cordoned": row["staff_cordoned"],
        "alarm": row["staff_alarm"],
        "trainHold": row["staff_train_hold"],
        "train": row.get("train") or "미상",
    }
    for kind in row["emergency_kinds"]:
        name = "emergency:" + kind
        bag[name] = bag.get(name, 0) + 1
    clock = (row.get("clock") or "").split(":")
    bag["hour"] = int(clock[0]) if clock and clock[0].isdigit() else -1
    # key 는 개체 번호(overheat_23)라 다음 근무에서 전부 미지 라벨이 된다.
    # description 은 그 번호와 좌표를 글로 담고 있어 같은 문제를 일으킨다. 둘 다 뺀다.
    return bag


class PerKindMean:
    """종류별 평균. 이 실험의 진짜 기준선이다."""

    def __init__(self):
        self.mean = {}
        self.overall = 0.0

    def fit(self, kinds, y):
        total = collections.defaultdict(list)
        for kind, value in zip(kinds, y):
            total[kind].append(value)
        self.mean = {k: float(np.mean(v)) for k, v in total.items()}
        self.overall = float(np.mean(y))
        return self

    def predict(self, kinds):
        # 학습에서 못 본 종류는 전체 평균으로 둔다. 0 으로 두면 없던 확신이 생긴다.
        return np.array([self.mean.get(k, self.overall) for k in kinds])


def within_request(rows, y_true, y_pred):
    """같은 요청의 수준 순위. 예측 동점은 균등 추첨의 기대 적중률로 센다."""
    groups = collections.defaultdict(list)
    for i, row in enumerate(rows):
        groups[(row["source"], row["at"])].append(i)
    rhos, hits, counted = [], 0.0, 0
    for indices in groups.values():
        if len(indices) < 2:
            continue
        truth = y_true[indices]
        guess = y_pred[indices]
        winners = guess == np.max(guess)
        hits += float(np.mean(truth[winners] == np.max(truth)))
        counted += 1
        if np.ptp(truth) != 0 and np.ptp(guess) != 0:
            rho = spearmanr(truth, guess).correlation
            if np.isfinite(rho):
                rhos.append(rho)
    return (float(np.mean(rhos)) if rhos else None,
            hits / counted if counted else None,
            counted)


def mean_available(values):
    finite = [value for value in values if value is not None]
    return float(np.mean(finite)) if finite else None


def main():
    parser = argparse.ArgumentParser(description="JEV 임박도 판단 재현 모델")
    parser.add_argument("--labels", default=os.path.join("workers", "learning", "labels"))
    parser.add_argument("--out", default=os.path.join("workers", "learning", "evidence"))
    parser.add_argument("--depth", type=int, default=6)
    parser.add_argument("--show-tree", action="store_true")
    args = parser.parse_args()

    rows, files = read(args.labels)
    print("라벨 파일 %d개 · 후보 %d건" % (len(files), len(rows)))

    shifts = sorted({r.get("source") for r in rows})
    kinds = collections.Counter(r.get("kind") for r in rows)
    print("근무 파일 %d개 · 종류 %d가지" % (len(shifts), len(kinds)))
    if len(rows) < 200:
        sys.exit("후보가 %d건뿐입니다. 근무를 더 모으세요 (DESKTOP_SETUP.md)." % len(rows))
    if len(shifts) < 2:
        sys.exit("근무 파일이 %d개뿐입니다. 누수 없는 검증을 못 합니다." % len(shifts))

    y = np.array([float(r.get("score") or 0) for r in rows])
    kind_list = np.array([r.get("kind") or "미상" for r in rows])
    group = np.array([r.get("source") for r in rows])
    vectoriser = DictVectorizer(sparse=False)
    x = vectoriser.fit_transform([features(r) for r in rows])

    levels = collections.Counter(int(round(v)) for v in y)
    print("\n수준 분포:", dict(sorted(levels.items())))
    top = max(levels.values()) / float(len(y))
    print("최다 수준 비율 %.3f" % top)
    if top > .95:
        print("  경고: 한 수준이 95%% 를 넘습니다. 이 표본으로는 배울 것이 거의 없습니다.")

    # 같은 근무의 후보는 상태가 거의 같다. 무작위로 나누면 같은 근무가 학습·시험에
    # 함께 들어가 점수가 부풀려진다. 그래서 근무 파일 단위로 가른다.
    folds = min(len(shifts), 5)
    splitter = GroupKFold(n_splits=folds)

    results = {}
    for name in ["전체평균(약한 기준선)", "종류별평균(진짜 기준선)", "결정나무", "릿지"]:
        mae, rho, hit = [], [], []
        for train, test in splitter.split(x, y, group):
            if name.startswith("전체평균"):
                guess = np.full(len(test), float(np.mean(y[train])))
            elif name.startswith("종류별평균"):
                guess = PerKindMean().fit(kind_list[train], y[train]).predict(kind_list[test])
            elif name == "결정나무":
                guess = DecisionTreeRegressor(max_depth=args.depth, random_state=0) \
                    .fit(x[train], y[train]).predict(x[test])
            else:
                guess = Ridge(alpha=1.0).fit(x[train], y[train]).predict(x[test])
            mae.append(float(np.mean(np.abs(y[test] - guess))))
            r, h, _ = within_request([rows[i] for i in test], y[test], guess)
            rho.append(r)
            hit.append(h)
        results[name] = {"mae": float(np.mean(mae)),
                         "spearman": mean_available(rho),
                         "top1": mean_available(hit)}
        # 상수 예측의 상관은 정의되지 않지만 최상위 기대 적중률은 계산할 수 있다.
        def show(value):
            return "  해당없음" if value is None else "%9.3f" % value
        print("\n%-22s MAE %.3f · 요청내 순위상관 %s · 최상위 일치 %s"
              % (name, results[name]["mae"], show(results[name]["spearman"]), show(results[name]["top1"])))

    base = results["종류별평균(진짜 기준선)"]
    best = min(("결정나무", "릿지"), key=lambda k: results[k]["mae"])
    gain = base["mae"] - results[best]["mae"]
    print("\n가장 나은 모델: %s · 종류별평균 대비 MAE %+.3f" % (best, -gain))
    if gain <= 0.02:
        print("→ 종류별 평균을 의미 있게 넘지 못했습니다. 이 표본과 모델의 상태 특징에서 추가 예측력을 찾지 못했습니다.")
    else:
        print("→ 종류별 평균을 넘었습니다. 이 표본에서 상태가 추가 예측력을 줬지만 대응 효과의 인과 증거는 아닙니다.")

    train, test = list(splitter.split(x, y, group))[-1]
    tree = DecisionTreeRegressor(max_depth=args.depth, random_state=0).fit(x[train], y[train])
    names = vectoriser.get_feature_names_out()
    ranked = sorted(zip(names, tree.feature_importances_), key=lambda pair: -pair[1])
    print("\n기여도 상위 특징")
    for name, weight in ranked[:12]:
        if weight <= 0:
            break
        print("  %-38s %.3f" % (name, weight))
    if args.show_tree:
        print("\n학습한 규칙\n" + export_text(tree, feature_names=list(names), max_depth=3))

    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S")
    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, "imminence-%s.json" % stamp)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump({
            "at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "rows": len(rows), "shifts": len(shifts), "kinds": dict(kinds),
            "levelShare": top,
            "results": results,
            "features": [{"name": n, "importance": float(w)} for n, w in ranked if w > 0],
            "note": "수준 재현 연구용. scale 별 위험률·경쟁 위험·시간 분포를 검증하지 않았으며 제품 JEV 판단을 대체하지 않는다.",
        }, handle, ensure_ascii=False, indent=2, allow_nan=False)
    print("\n기록: %s" % path)


if __name__ == "__main__":
    main()
