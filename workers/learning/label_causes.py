"""JEV 의 임박도 판단을 후보 단위 학습 표본으로 펼친다.

무엇을 하는가
    #259 이후 디렉터는 지금 맵에서 가능한 원인·전개 후보를 매초 나열하고,
    **후보마다 독립된 질문**으로 묶어 JEV 에게 "곧 일어날 정도"를 수준으로 받는다.
    코드는 그 수준을 초당 위험률로 바꿔 경쟁 위험으로 추첨한다 - 추첨은 전부 코드가 한다.

    이 스크립트는 JSONL 에서 그 판단만 꺼내 **후보 하나를 한 줄로** 펼친다.
    임박도 모델이 바로 먹을 수 있는 모양이다.

    요청 하나(judge) = 후보 29~31개 = 표본 29~31개.
    'Choice 로 하나를 고르던' 시절과 달리 후보마다 라벨이 붙으므로 표본이 수십 배다.

무엇을 하지 않는가
    * **모델을 학습하지 않는다.** 여기서는 라벨만 만든다. 학습은 그다음이다.
    * **사건을 만들지 않는다.** 이미 기록된 판단을 읽을 뿐이다.
    * **후보 키를 특징으로 쓰지 않는다.** 키에는 `overheat_23` 처럼 그 근무에만 있는 개체
      번호가 붙는다. kind(번호를 뗀 것)와 설명·상태를 특징으로 쓰고, 키는 추적용으로만 남긴다.

라벨의 모양
    Score 질문의 답은 `Score`(기대 수준, 0이 가장 낮음)와 `Probabilities`(키가 수준 색인
    문자열 "0","1",…)다. 이 스크립트는 확률을 수준 순서대로 배열로 펴서 담는다.
    학습할 때는 기대 수준 회귀로도, 수준 분포 맞추기로도 쓸 수 있다.

실행
    python workers/learning/label_causes.py
    python workers/learning/label_causes.py --logs "D:/cg-data/jsonl-backup" --out "D:/cg-data/labels"
"""

import argparse
import collections
import datetime
import glob
import json
import os
import re
import sys

DEFAULT_LOGS = os.path.join(
    os.path.expanduser("~"), "AppData", "LocalLow", "DefaultCompany", "CHOOGuard", "jev-runs")

# overheat_23 -> overheat. 개체 번호는 그 근무에만 있으므로 특징으로 쓰면 안 된다.
SUFFIX = re.compile(r"_\d+$")


def read(folder):
    files = sorted(glob.glob(os.path.join(folder, "*.jsonl")))
    if not files:
        sys.exit("JSONL 을 찾지 못했습니다: %s" % folder)
    rows, broken = [], 0
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
                    broken += 1       # 근무가 중간에 끊기면 마지막 줄이 잘릴 수 있다
    if broken:
        print("깨진 줄 %d개를 건너뛰었습니다." % broken)
    return rows, files


def level_vector(answer, count):
    """수준 확률을 수준 순서대로 편다. 확률이 없으면 기대 수준에 전부 몰아준다."""
    probabilities = answer.get("Probabilities") or {}
    vector = [0.0] * count
    total = 0.0
    for key, value in probabilities.items():
        try:
            index = int(key)
        except (TypeError, ValueError):
            return None      # 수준 색인이 아니다 - Score 질문이 아니라는 뜻
        if 0 <= index < count and value > 0:
            vector[index] = float(value)
            total += float(value)
    if total > 0:
        return [v / total for v in vector]
    # JEV 가 분포를 안 줬다. 반올림한 기대 수준에 전부 둔다 - 제품 코드와 같은 처리다.
    index = max(0, min(count - 1, int(round(float(answer.get("Score") or 0)))))
    vector[index] = 1.0
    return vector


def state_features(state):
    """지금 판단에 쓰인 상태. JEV 에게 실제로 보낸 것만 담는다."""
    people = state.get("people_by_area") or {}
    visible = state.get("visible_situation") or []
    staff = state.get("staff") or {}
    return {
        "clock": state.get("clock"),
        "train": state.get("train"),
        "people_total": int(sum(people.values())) if people else 0,
        "people_by_area": people,
        # 이미 벌어진 일. 임박도가 이것에 어떻게 반응하는지가 배울 것의 핵심이다.
        "visible_count": len(visible),
        "visible_kinds": [v.get("what") for v in visible if isinstance(v, dict)],
        "visible_oldest_seconds": max([v.get("seconds", 0) for v in visible], default=0),
        "agencies_on_scene": len(state.get("agencies_on_scene") or []),
        # 역무원 대응. 하네스의 행동 정책이 이 값들을 실제로 움직이게 해 둔 이유가 이것이다.
        "staff_reported": int(staff.get("reported", 0)),
        "staff_announced": bool(staff.get("public_announcement")),
        "staff_cordons": int(staff.get("cordons", 0)),
        "staff_alarm": bool(staff.get("fire_alarm_ringing")),
        "staff_train_hold": bool(staff.get("train_hold_requested")),
    }


def flatten(rows, purposes):
    """judge 판단을 후보 단위로 편다."""
    out = []
    seen_purposes = collections.Counter()
    no_levels = 0
    for record, source in rows:
        purpose = record.get("purpose")
        seen_purposes[purpose] += 1
        if purpose not in purposes:
            continue
        request = record.get("request") or {}
        state = request.get("state") or {}
        questions = request.get("questions") or {}
        answers = record.get("answers") or {}
        shared = state_features(state)
        for key, question in questions.items():
            answer = answers.get(key)
            if not answer:
                continue
            # 수준은 `criteria` 에 **배열**로 들어간다. C# 필드 이름(Levels)이 아니다 -
            # JevClient.Body 가 type="score" 일 때 criteria 자리에 수준 배열을 쓰고,
            # choice 일 때는 같은 자리에 선택지 객체를 쓴다. 필드 이름만 보고 짐작하면
            # 정상 데이터에서도 0건이 나온다.
            if question.get("type") != "score":
                no_levels += 1
                continue         # Choice 질문은 다른 스크립트가 다룬다
            levels = question.get("criteria")
            if not isinstance(levels, list) or not levels:
                no_levels += 1
                continue
            vector = level_vector(answer, len(levels))
            if vector is None:
                no_levels += 1
                continue
            row = {
                "at": record.get("at"),
                "source": source,
                "purpose": purpose,
                "lane": record.get("lane"),
                "key": key,                       # 추적용. 특징으로 쓰지 말 것
                "kind": SUFFIX.sub("", key),      # 이쪽이 특징이다
                "description": question.get("instructions"),
                "levels": levels,
                "score": float(answer.get("Score") or 0),
                "confidence": float(answer.get("Confidence") or 0),
                "probabilities": vector,
            }
            row.update(shared)
            out.append(row)
    return out, seen_purposes, no_levels


def main():
    parser = argparse.ArgumentParser(description="JEV 임박도 판단을 후보 단위로 펼친다")
    parser.add_argument("--logs", default=DEFAULT_LOGS, help="jev-runs 폴더")
    parser.add_argument("--out", default=os.path.join("workers", "learning", "labels"))
    parser.add_argument("--purpose", default="judge,magnitude",
                        help="펼칠 purpose, 쉼표 구분 (기본 judge,magnitude)")
    args = parser.parse_args()

    rows, files = read(args.logs)
    purposes = {p.strip() for p in args.purpose.split(",") if p.strip()}
    print("JSONL %d개 · 레코드 %d건" % (len(files), len(rows)))

    samples, seen, no_levels = flatten(rows, purposes)
    print("목적별:", dict(seen))
    if no_levels:
        print("수준이 없는 질문 %d개를 건너뛰었습니다 (Choice 질문)." % no_levels)

    if not samples:
        # 여기서 조용히 빈 파일을 쓰면 '돌긴 돌았다' 로 오해한다. 무엇이 없는지 말한다.
        print("\n펼칠 판단이 없습니다.")
        if "judge" not in seen:
            print("  기록에 judge 판단이 없습니다. #259(임박도 판단) 머지 전에 모은 데이터로 보입니다.")
            print("  그때는 사건을 Choice 로 한 번에 골랐고, 후보별 수준 판단이 없습니다.")
            print("  workers/learning/DESKTOP_SETUP.md 2단계대로 #259 머지를 먼저 확인하세요.")
        sys.exit(1)

    kinds = collections.Counter(r["kind"] for r in samples)
    shifts = sorted({r["source"] for r in samples})
    print("\n표본 %d건 · 종류 %d가지 · 근무 파일 %d개" % (len(samples), len(kinds), len(shifts)))
    print("\n종류별 (상위 20)")
    for name, count in kinds.most_common(20):
        print("  %-24s %5d" % (name, count))

    # 라벨이 한쪽으로 쏠리면 학습해도 기준선을 못 넘는다. 먼저 보여 준다.
    expected = collections.Counter(int(round(r["score"])) for r in samples)
    print("\n기대 수준 분포:", dict(sorted(expected.items())))
    top = max(expected.values()) / float(len(samples))
    print("최다 수준만 찍었을 때 정확도: %.3f  <- 모델은 이걸 넘어야 한다" % top)
    if top > .95:
        print("  경고: 한 수준이 95%% 를 넘습니다. 이대로는 배울 것이 거의 없습니다.")

    # 대응이 실제로 일어난 표본이 있는지. 없으면 대응 뒤의 전개를 배울 수 없다.
    responded = sum(1 for r in samples if r["staff_reported"] or r["staff_cordons"] or r["staff_announced"])
    print("역무원이 대응한 뒤의 표본: %d건 (%.1f%%)" % (responded, 100.0 * responded / len(samples)))
    if responded == 0:
        print("  경고: 대응 뒤 표본이 0건입니다. 하네스의 행동 정책이 돌았는지 확인하세요.")

    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S")
    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, "causes-%s.jsonl" % stamp)
    with open(path, "w", encoding="utf-8") as handle:
        for row in samples:
            handle.write(json.dumps(row, ensure_ascii=False) + "\n")
    print("\n기록: %s  (%d줄)" % (path, len(samples)))


if __name__ == "__main__":
    main()
