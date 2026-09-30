"""JEV 의 임박도 판단을 재현하는 모델을 학습하고, 정직하게 평가한다.

입력
    label_causes.py 가 펼친 `causes-*.jsonl`. 한 줄이 후보 하나다.

배우려는 것
    "지금 이 후보가 곧 일어날 정도" 를 JEV 가 매긴 수준(0 이 가장 낮음).
    추첨은 코드가 한다(수준 -> 초당 위험률 -> 경쟁 위험). 모델은 **수준 매기기만** 대신한다.

무엇이 진짜 질문인가
    "수준을 맞힐 수 있나" 는 질문이 아니다. 임박도는 대부분 **무엇인가(kind)** 로 정해진다 -
    주방 가스 누출과 승강기 갇힘은 상태와 무관하게 기본 임박도가 다르다.
    그래서 이 스크립트의 기준선은 전역 평균이 아니라 **종류별 평균**이다.

        종류별 평균을 못 넘으면, 상태 특징(사람 수·이미 난 사건·역무원 대응)이
        판단에 아무 값어치가 없다는 뜻이다.

    이 기준선을 두지 않으면 MAE 가 낮게 나오는 것을 보고 "잘 배웠다" 고 오해한다.

세 가지로 잰다
    1. **수준 오차(MAE)** - 평균 몇 단계 틀리는가.
    2. **요청 안 순위 상관(Spearman)** - 같은 순간 후보들 중 어느 것이 더 임박한지 순서를
       맞히는가. 추첨이 위험률 *비율* 로 도므로 절대값보다 이쪽이 실질적이다.
    3. **최상위 일치** - 그 요청에서 가장 임박한 후보를 맞히는가.

읽을 때 조심할 것 — 시간을 대신 재는 특징
    `peopleTotal` · `visibleOldest` · `hour` 는 근무가 흐르면 함께 변한다. 역무원 대응도
    근무 후반에 몰리므로, 모델이 '대응했으니 임박도가 낮다' 가 아니라 '근무 후반이니 낮다'
    를 배우고도 같은 점수가 나올 수 있다. 기여도 상위에 이 셋이 올라오면 **대응 효과로
    해석하지 말고**, 대응이 이른 시각에 일어난 근무를 따로 모아 다시 재야 한다.
    (합성 데이터로 시험할 때 `peopleTotal` 이 기여도 2위로 올라온 것이 이 경우였다.)

아직 못 재는 것
    **시간 보정** - 모델 수준으로 돌렸을 때 첫 사건까지 걸리는 시간 분포가 JEV 로 돌린 것과
    같은지. 이게 최종 판정이지만, 수준을 위험률로 바꾸는 표(`Imminence.Rate`)가 #259 에 있고
    아직 develop 에 없다. 머지된 뒤 네 번째 지표로 붙인다. **그때까지 이 모델을 제품에
    넣지 않는다** - 순위가 맞아도 시간 분포가 어긋나면 게임 경험이 달라진다.

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
                    rows.append(json.loads(line))
    return rows, files


def features(row):
    """모델이 볼 것. key·description 은 넣지 않는다 - 아래 주석 참조."""
    bag = {
        # 무엇인가. 임박도를 가장 크게 가르는 값이라 기준선도 이것으로 만든다.
        "kind": row.get("kind") or "미상",
        "levelCount": len(row.get("levels") or []),
        # 지금 역 상태
        "peopleTotal": int(row.get("people_total") or 0),
        "visibleCount": int(row.get("visible_count") or 0),
        "visibleOldest": float(row.get("visible_oldest_seconds") or 0),
        "agencies": int(row.get("agencies_on_scene") or 0),
        # 역무원 대응. 이 값들이 임박도를 낮추는지가 이 실험의 핵심 질문이다.
        "reported": int(row.get("staff_reported") or 0),
        "announced": bool(row.get("staff_announced")),
        "cordons": int(row.get("staff_cordons") or 0),
        "alarm": bool(row.get("staff_alarm")),
        "trainHold": bool(row.get("staff_train_hold")),
        "train": row.get("train") or "미상",
    }
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
    """같은 요청(같은 source + at) 안에서 순위를 맞히는가."""
    groups = collections.defaultdict(list)
    for i, row in enumerate(rows):
        groups[(row.get("source"), row.get("at"))].append(i)
    rhos, hits, counted = [], 0, 0
    for indices in groups.values():
        if len(indices) < 2:
            continue      # 후보가 하나뿐이면 순위를 논할 수 없다
        truth = y_true[indices]
        guess = y_pred[indices]
        if np.ptp(truth) == 0 or np.ptp(guess) == 0:
            continue      # 전부 같은 값이면 상관이 정의되지 않는다
        rho = spearmanr(truth, guess).correlation
        if not np.isnan(rho):
            rhos.append(rho)
        counted += 1
        if indices[int(np.argmax(guess))] == indices[int(np.argmax(truth))]:
            hits += 1
    return (float(np.mean(rhos)) if rhos else float("nan"),
            (hits / counted) if counted else float("nan"),
            counted)


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
                         "spearman": float(np.nanmean(rho)),
                         "top1": float(np.nanmean(hit))}
        # 상수 예측(전체평균)은 순위를 매기지 않으므로 상관·일치가 정의되지 않는다.
        # nan 을 그대로 찍으면 '실패했다' 로 읽히므로 '해당 없음' 으로 구분해 쓴다.
        def show(value):
            return "  해당없음" if value != value else "%9.3f" % value
        print("\n%-22s MAE %.3f · 요청내 순위상관 %s · 최상위 일치 %s"
              % (name, results[name]["mae"], show(results[name]["spearman"]), show(results[name]["top1"])))

    base = results["종류별평균(진짜 기준선)"]
    best = min(("결정나무", "릿지"), key=lambda k: results[k]["mae"])
    gain = base["mae"] - results[best]["mae"]
    print("\n가장 나은 모델: %s · 종류별평균 대비 MAE %+.3f" % (best, -gain))
    if gain <= 0.02:
        print("→ 종류별 평균을 의미 있게 넘지 못했습니다. **상태 특징이 임박도를 설명하지 못합니다.**")
        print("  종류만으로 충분하다는 뜻이거나, 대응이 일어난 표본이 너무 적다는 뜻입니다.")
    else:
        print("→ 종류별 평균을 넘었습니다. 상태가 임박도를 바꾼다는 근거입니다.")

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
            "note": "시간 보정(첫 사건까지의 분포)은 Imminence.Rate 가 develop 에 들어온 뒤 붙인다.",
        }, handle, ensure_ascii=False, indent=2)
    print("\n기록: %s" % path)


if __name__ == "__main__":
    main()
