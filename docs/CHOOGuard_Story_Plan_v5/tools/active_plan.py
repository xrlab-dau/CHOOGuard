#!/usr/bin/env python3
"""Single-story plan inspection and one derived Markdown view; never executes work."""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
sys.path.insert(0, str(REPO / "docs/CHOOGuard_Story_Plan_v4/tools"))
from planlib import PlanError, digest, read_json, safe_path

STORY_ID = "CS-EXEC.01.01"
LEGACY_PATH = "docs/CHOOGuard_Story_Plan_v4/plan.json"


def require(condition, message):
    if not condition:
        raise PlanError(message)


def fields(value, names, label):
    require(isinstance(value, dict) and set(value) == set(names.split()), "FIELDS:" + label)


def text(value, label):
    require(isinstance(value, str) and bool(value.strip()), "TEXT:" + label)


def rows(value, label):
    require(isinstance(value, list), "LIST:" + label)
    return value


def existing_path(repo, value):
    path = safe_path(repo, value)
    require(path.is_file(), "MISSING_REFERENCE:" + value)
    return path


def references(repo, values):
    for value in rows(values, "references"):
        existing_path(repo, value)


def referenced_stories(plan, repo):
    """Resolve each ID to the whole original object; no copied/filtered contract."""
    legacy = read_json(existing_path(repo, plan["legacy"]["planPath"]))
    by_id = {story["id"]: story for story in legacy["stories"]}
    return [by_id[sid] for area in plan["functionalAreas"] for sid in area["legacyStoryIds"]]


def validate(plan, state, repo):
    """Structure/reference consistency only, not evidence authenticity or acceptance."""
    try:
        fields(plan, "version activeStories legacy functionalAreas policy boundaries historyRefs", "plan")
        require(type(plan["version"]) is int and plan["version"] == 5, "VERSION")
        stories = rows(plan["activeStories"], "activeStories")
        require(len(stories) == 1, "ONE_ACTIVE_STORY_REQUIRED")
        fields(stories[0], "id title", "activeStory")
        require(stories[0]["id"] == STORY_ID, "ACTIVE_STORY_ID")
        text(stories[0]["title"], "title")
        fields(plan["legacy"], "planPath planDigest mode", "legacy")
        legacy_ref = plan["legacy"]
        require(legacy_ref["planPath"] == LEGACY_PATH, "LEGACY_CANONICAL_PATH")
        require(legacy_ref["mode"] == "REFERENCE_ONLY", "LEGACY_MODE")
        legacy = read_json(existing_path(repo, legacy_ref["planPath"]))
        require(digest(legacy) == legacy_ref["planDigest"], "LEGACY_DIGEST_CHANGED")
        original = {story["id"]: story for story in legacy["stories"]}
        areas = rows(plan["functionalAreas"], "functionalAreas")
        area_ids, refs = [], []
        for area in areas:
            fields(area, "id title legacyStoryIds", "functionalArea")
            text(area["id"], "areaId")
            text(area["title"], "areaTitle")
            area_ids.append(area["id"])
            ids = rows(area["legacyStoryIds"], "legacyStoryIds")
            require(bool(ids), "EMPTY_AREA")
            for sid in ids:
                text(sid, "legacyStoryId")
                require(sid in original, "UNKNOWN_LEGACY_ID:" + sid)
                require(original[sid]["epicId"] == area["id"], "WRONG_AREA:" + sid)
            refs.extend(ids)
        require(len(area_ids) == len(set(area_ids)), "DUPLICATE_AREA")
        require(set(area_ids) == {e["id"] for e in legacy["epics"]}, "AREA_COVERAGE")
        require(len(refs) == len(set(refs)), "DUPLICATE_LEGACY_REFERENCE")
        require(set(refs) == set(original), "LEGACY_REFERENCE_COVERAGE")
        for key in ("policy", "boundaries"):
            require(bool(rows(plan[key], key)), "EMPTY:" + key)
            for value in plan[key]:
                text(value, key)
        references(repo, plan["historyRefs"])

        fields(state, "storyId status acceptanceStatus acceptanceRef currentWork areas tests issues", "progress")
        require(state["storyId"] == STORY_ID, "PROGRESS_STORY_ID")
        require(state["status"] in ("NOT_STARTED", "IN_PROGRESS", "BLOCKED", "COMPLETED"), "PROGRESS_STATUS")
        require(state["acceptanceStatus"] in ("NOT_ACCEPTED", "ACCEPTED"), "ACCEPTANCE_STATUS")
        text(state["currentWork"], "currentWork")
        progress = rows(state["areas"], "progress.areas")
        progress_ids = []
        for area in progress:
            fields(area, "areaId status summary evidenceRefs", "areaProgress")
            text(area["areaId"], "progress.areaId")
            progress_ids.append(area["areaId"])
            require(area["status"] in ("NOT_STARTED", "IN_PROGRESS", "PARTIAL", "BLOCKED", "COMPLETED"), "AREA_STATUS")
            text(area["summary"], "areaSummary")
            references(repo, area["evidenceRefs"])
            if area["status"] == "COMPLETED":
                require(bool(area["evidenceRefs"]), "COMPLETED_AREA_REQUIRES_EVIDENCE")
        require(len(progress_ids) == len(set(progress_ids)) and set(progress_ids) == set(area_ids), "PROGRESS_AREA_COVERAGE")
        for test in rows(state["tests"], "tests"):
            fields(test, "name scope result summary evidenceRef", "test")
            for key in ("name", "scope", "summary"):
                text(test[key], "test." + key)
            require(test["result"] in ("PASS", "FAIL", "NOT_RUN"), "TEST_RESULT")
            if test["evidenceRef"] is not None:
                existing_path(repo, test["evidenceRef"])
            else:
                require(test["result"] == "NOT_RUN", "EXECUTED_TEST_REQUIRES_EVIDENCE")
        issues = rows(state["issues"], "issues")
        issue_ids = []
        for issue in issues:
            fields(issue, "id status requiredForAcceptance scope reason evidenceRefs", "issue")
            for key in ("id", "scope", "reason"):
                text(issue[key], "issue." + key)
            issue_ids.append(issue["id"])
            require(issue["status"] in ("OPEN", "RESOLVED"), "ISSUE_STATUS")
            require(type(issue["requiredForAcceptance"]) is bool, "REQUIRED_FLAG")
            references(repo, issue["evidenceRefs"])
            if issue["status"] == "RESOLVED" and issue["requiredForAcceptance"]:
                require(bool(issue["evidenceRefs"]), "RESOLVED_ISSUE_REQUIRES_EVIDENCE")
        require(len(issue_ids) == len(set(issue_ids)), "DUPLICATE_ISSUE")
        if state["status"] == "COMPLETED" or state["acceptanceStatus"] == "ACCEPTED":
            require(all(a["status"] == "COMPLETED" for a in progress), "PARTIAL_PRODUCT_CANNOT_COMPLETE")
        if state["acceptanceStatus"] == "ACCEPTED":
            require(state["status"] == "COMPLETED", "ACCEPTANCE_REQUIRES_COMPLETION")
            require(not any(i["requiredForAcceptance"] and i["status"] == "OPEN" for i in issues), "OPEN_REQUIRED_VERIFICATION")
            existing_path(repo, state["acceptanceRef"])
        else:
            require(state["acceptanceRef"] is None, "UNACCEPTED_REFERENCE_MUST_BE_NULL")
        return []
    except (PlanError, KeyError, TypeError, ValueError, OSError) as error:
        return [str(error)]


def brief(plan, state):
    return {"storyId": state["storyId"], "title": plan["activeStories"][0]["title"],
            "status": state["status"], "acceptanceStatus": state["acceptanceStatus"],
            "currentWork": state["currentWork"], "areas": state["areas"], "issues": state["issues"],
            "view": "docs/CHOOGuard_Story_Plan_v5/ACTIVE_STORY.md",
            "scope": "MANAGEMENT_STRUCTURE_ONLY", "executionAuthorized": False,
            "productAcceptanceVerified": False}


def render(plan, state):
    def link(path):
        return f"[{path}](../../{path})"

    lines = ["# CHOOGuard 통합 Story", "",
             "<!-- AUTO-GENERATED: plan.json + state/progress.json; edit JSON, then active_plan.py render -->", "",
             f"## {state['storyId']} — {plan['activeStories'][0]['title']}", "",
             f"상태 **{state['status']}** / 수용 **{state['acceptanceStatus']}**", "",
             state["currentWork"], "",
             "이 도구의 PASS는 관리 구조·참조 정합만 뜻하며 제품 실행·완료·기관 수용 검증이 아니다.", "",
             "## 기능 체크리스트", "",
             "아래 영역과 legacy ID는 별도 활성 Story·승인·인계 단위가 아니다. ID는 정본의 원문 객체 전체를 참조한다.", "",
             "정본: " + link(plan["legacy"]["planPath"]),
             f"전환 digest: `{plan['legacy']['planDigest']}`", ""]
    progress = {area["areaId"]: area for area in state["areas"]}
    for area in plan["functionalAreas"]:
        current = progress[area["id"]]
        lines.extend([f"### {area['title']} · {current['status']}", "", current["summary"], "",
                      "원문 ID: " + ", ".join(f"`{sid}`" for sid in area["legacyStoryIds"]), ""])
        lines.extend("- 증거: " + link(ref) for ref in current["evidenceRefs"])
        lines.append("")
    for key, title in (("policy", "최소 개발 절차"), ("boundaries", "제품·안전 경계")):
        lines.extend(["## " + title, ""])
        lines.extend("- " + item for item in plan[key])
        lines.append("")
    lines.extend(["## 기록된 시험", "", "시험 수를 제품 진척률로 해석하지 않는다. 과거 증거와 신규 실행을 구분한다.", ""])
    for test in state["tests"]:
        lines.extend([f"- **{test['name']} — {test['result']}**: {test['summary']}",
                      "  - 범위: " + test["scope"],
                      "  - 결과: " + (link(test["evidenceRef"]) if test["evidenceRef"] else "미실행; 결과 파일 없음")])
    lines.extend(["", "## 실패·보류·미착수", ""])
    for issue in state["issues"]:
        required = "필수 수용 관련" if issue["requiredForAcceptance"] else "비필수"
        lines.append(f"- **{issue['id']} · {issue['status']} · {required}** — {issue['scope']}: {issue['reason']}")
        lines.extend("  - " + link(ref) for ref in issue["evidenceRefs"])
    if state["acceptanceRef"]:
        lines.extend(["", "기록된 수용 근거(자동 검증 아님): " + link(state["acceptanceRef"])])
    lines.extend(["", "## 보존된 과거 기록", ""])
    lines.extend("- " + link(ref) for ref in plan["historyRefs"])
    lines.extend(["", "## 사용", "", "기본 반복은 `prompts/02-implement-story-low.md` 하나다. JSON 진행 기록을 바꾼 뒤 다음 명령으로 이 뷰를 갱신한다.", "",
                  "```sh", "python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py validate",
                  "python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py brief",
                  "python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render",
                  "python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render --check", "```", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("validate")
    sub.add_parser("brief")
    sub.add_parser("render").add_argument("--check", action="store_true")
    args = parser.parse_args()
    plan = read_json(ROOT / "plan.json")
    state = read_json(ROOT / "state/progress.json")
    errors = validate(plan, state, REPO)
    if errors or args.command == "validate":
        print(json.dumps({"valid": not errors, "errors": errors, "scope": "MANAGEMENT_STRUCTURE_ONLY",
                          "executionAuthorized": False, "productAcceptanceVerified": False}, ensure_ascii=False))
        return 1 if errors else 0
    if args.command == "brief":
        print(json.dumps(brief(plan, state), ensure_ascii=False, indent=2))
        return 0
    view = ROOT / "ACTIVE_STORY.md"
    content = render(plan, state)
    if args.check:
        current = view.is_file() and view.read_text(encoding="utf8") == content
        print("VIEW_CURRENT" if current else "VIEW_DRIFT")
        return 0 if current else 1
    view.write_text(content, encoding="utf8")
    print("UPDATED: docs/CHOOGuard_Story_Plan_v5/ACTIVE_STORY.md")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (PlanError, ValueError, OSError) as error:
        print(json.dumps({"error": str(error), "executionAuthorized": False}, ensure_ascii=False))
        sys.exit(2)
