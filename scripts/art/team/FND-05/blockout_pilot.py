#!/usr/bin/env python3
"""FND-05 pilot blockout: 부산역 지하연결통로 (underground_connector).

공개 자료 P04 (Busan Eurasia Platform 시설 안내) 가 공표한 통로 길이 99.6 m x 폭 8.0 m
위에 세운 파일럿 블록아웃을 만들고, 그 블록아웃의 치수·좌표 정렬·portal 경계를 검사한다.

산출물은 박스/와이어프레임 근사이며 실측 모델이 아니다. 높이·바닥고·portal 개구는
추정값(estimated_synthetic_measure)이고, 길이·폭만 운영자 공표값(confirmed_public_measure)이다.
원본 평면도 이미지는 읽지도 쓰지도 않는다. 수치와 출처 메타데이터만 사용한다.

표준 라이브러리만 사용한다: argparse, json, math, os, sys, unittest.

Usage:
  python3 scripts/art/team/FND-05/blockout_pilot.py --check
  python3 scripts/art/team/FND-05/blockout_pilot.py --generate [--out PATH]
  python3 scripts/art/team/FND-05/blockout_pilot.py --test
"""

import argparse
import json
import math
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, os.pardir, os.pardir, os.pardir, os.pardir))
DEFAULT_MAPPING = os.path.join(ROOT, "foundation", "world", "team", "FND-05", "plan-mapping.json")
DEFAULT_PROFILE = os.path.join(ROOT, "foundation", "world", "connected-world-profile.json")
DEFAULT_OUT = os.path.join(ROOT, "foundation", "world", "team", "FND-05", "pilot-underground-connector.obj")

PILOT_REGION_ID = "underground_connector"
CLASSIFICATION = "PUBLIC_PROJECT_CONTEXT"
SCHEMA_VERSION = 1
UNIT_LENGTH = "meters"
LENGTH_TOL = 0.05          # confirmed_public_measure 절대 허용 오차 (m)
ESTIMATED_TOL = 0.10       # estimated_synthetic_measure 절대 허용 오차 (m)
RATIO_TOL_REL = 0.001      # 길이/폭 비율 상대 허용 오차
COORD_TOL = 1e-6           # 좌표 정렬 허용 오차 (m)

ANCHOR_PORTALS = (
    "rail_terminal_public--underground_connector",       # 서단 캡 x = 80.0
    "underground_connector--underground_shopping_passage",  # 동단 캡 x = 179.6
    "forecourt_eurasia--underground_connector",          # 북측벽 중앙 z = 4.0
)

EXPECTED_REGION_IDS = (
    "rolling_stock_mainline",
    "rail_tracks_mainline",
    "rail_platforms_mainline",
    "rail_terminal_public",
    "station_concourse_2f",
    "station_hall_1f",
    "station_ticket_area",
    "forecourt_eurasia",
    "underground_connector",
    "underground_shopping_passage",
    "metro_concourse",
    "metro_platforms",
    "rolling_stock_metro",
)

EXPECTED_PORTAL_IDS = (
    "rolling_stock_mainline--rail_platforms_mainline",
    "rail_tracks_mainline--rail_platforms_mainline",
    "rail_platforms_mainline--station_concourse_2f",
    "rail_terminal_public--station_concourse_2f",
    "station_hall_1f--station_concourse_2f",
    "station_ticket_area--rail_terminal_public",
    "rail_terminal_public--underground_connector",
    "forecourt_eurasia--underground_connector",
    "underground_connector--underground_shopping_passage",
    "underground_shopping_passage--metro_concourse",
    "metro_concourse--metro_platforms",
    "rolling_stock_metro--metro_platforms",
)

_SOURCE_IDS = ("P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08", "P09")
_MEASURE_CLASSES = (
    "confirmed_public_measure",
    "estimated_synthetic_measure",
    "prohibited_measure",
)

_SETTINGS = {"mapping": DEFAULT_MAPPING, "profile": DEFAULT_PROFILE}


# --------------------------------------------------------------------------- io


def load_json(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def find_region(mapping, region_id):
    for region in mapping["regions"]:
        if region["regionId"] == region_id:
            return region
    raise KeyError("region not found: %s" % region_id)


def find_portal(mapping, portal_id):
    for portal in mapping["portals"]:
        if portal["portalId"] == portal_id:
            return portal
    raise KeyError("portal not found: %s" % portal_id)


def region_size(region):
    bounds = region["worldBounds"]
    return (
        bounds["max"]["X"] - bounds["min"]["X"],
        bounds["max"]["Y"] - bounds["min"]["Y"],
        bounds["max"]["Z"] - bounds["min"]["Z"],
    )


def on_box_boundary(point, bounds, tol=COORD_TOL):
    """점이 상자 경계면 위에 있는가 (모든 축이 범위 안이고, 최소 한 축이 면에 닿음)."""
    coords = (point["X"], point["Y"], point["Z"])
    lows = (bounds["min"]["X"], bounds["min"]["Y"], bounds["min"]["Z"])
    highs = (bounds["max"]["X"], bounds["max"]["Y"], bounds["max"]["Z"])
    on_face = False
    for value, low, high in zip(coords, lows, highs):
        if value < low - tol or value > high + tol:
            return False
        if math.isclose(value, low, abs_tol=tol) or math.isclose(value, high, abs_tol=tol):
            on_face = True
    return on_face


# ------------------------------------------------------------------------ mesh


class Mesh(object):
    """OBJ 로 내보낼 수 있는 최소 사각형 메시 (1 단위 = 1 m)."""

    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.quads = []
        self.lines = []
        self.groups = {}

    def _v(self, point, group):
        self.vertices.append((float(point[0]), float(point[1]), float(point[2])))
        index = len(self.vertices)
        self.groups.setdefault(group, {"v": [], "q": [], "l": []})["v"].append(index)
        return index

    def face(self, points, group="shell"):
        indices = tuple(self._v(point, group) for point in points)
        self.quads.append(indices)
        self.groups.setdefault(group, {"v": [], "q": [], "l": []})["q"].append(indices)
        return indices

    def line(self, a, b, group="portals"):
        ia = self._v(a, group)
        ib = self._v(b, group)
        self.lines.append((ia, ib))
        self.groups.setdefault(group, {"v": [], "q": [], "l": []})["l"].append((ia, ib))

    def box(self, low, high, group="anchors"):
        (x0, y0, z0), (x1, y1, z1) = low, high
        corners = [
            (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
            (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1),
        ]
        for face_indices in ((0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
                             (2, 3, 7, 6), (1, 2, 6, 5), (0, 3, 7, 4)):
            self.face([corners[i] for i in face_indices], group)

    def group_bounds(self, group):
        indices = self.groups[group]["v"]
        if not indices:
            raise KeyError("empty group: %s" % group)
        xs = [self.vertices[i - 1][0] for i in indices]
        ys = [self.vertices[i - 1][1] for i in indices]
        zs = [self.vertices[i - 1][2] for i in indices]
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))

    def all_bounds(self):
        xs = [v[0] for v in self.vertices]
        ys = [v[1] for v in self.vertices]
        zs = [v[2] for v in self.vertices]
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))

    def obj_text(self):
        lines = [
            "# FND-05 pilot blockout - %s" % self.name,
            "# issue: #119",
            "# classification: %s" % CLASSIFICATION,
            "# unit: %s (1 unit = 1 m, scale 1:1)" % UNIT_LENGTH,
            "# geometry_status: synthetic_design_not_surveyed",
            "# confirmed_public_measure: %s P04 length 99.6 x width 8.0" % PILOT_REGION_ID,
            "# estimated_synthetic_measure: height 3.2, floor -4.0, portal clear 3.2 x 2.7",
            "# no original plan image is embedded; metadata and numbers only",
            "o %s" % self.name,
        ]
        for x, y, z in self.vertices:
            lines.append("v %.6f %.6f %.6f" % (x, y, z))
        for group in ("shell", "portals", "anchors"):
            if group not in self.groups:
                continue
            lines.append("g %s" % group)
            for quad in self.groups[group]["q"]:
                lines.append("f %d %d %d %d" % quad)
            for a, b in self.groups[group]["l"]:
                lines.append("l %d %d" % (a, b))
        return "\n".join(lines) + "\n"


def _wall_panels(axis_low, axis_high, base, top, opening):
    """개구가 있는 벽 하나를 최대 3개 패널로 나눈다.

    축(axis_low..axis_high)은 벽이 뻗은 방향, base..top 은 높이. 패널은
    (lo, hi, y_lo, y_hi) 네 값이며 개구가 없으면 벽 전체가 한 패널이다.
    """
    panels = []
    if opening is None:
        return [(axis_low, axis_high, base, top)]
    o0, o1, clearance = opening
    if o0 > axis_low:
        panels.append((axis_low, o0, base, top))
    if o1 < axis_high:
        panels.append((o1, axis_high, base, top))
    if base + clearance < top:
        panels.append((o0, o1, base + clearance, top))
    return panels


def build_connector_blockout(mapping):
    """P04 앵커 위에 지하연결통로 블록아웃을 세운다 (Y-up, 1 unit = 1 m)."""
    region = find_region(mapping, PILOT_REGION_ID)
    bounds = region["worldBounds"]
    x0, y0, z0 = bounds["min"]["X"], bounds["min"]["Y"], bounds["min"]["Z"]
    x1, y1, z1 = bounds["max"]["X"], bounds["max"]["Y"], bounds["max"]["Z"]

    west = find_portal(mapping, ANCHOR_PORTALS[0])
    east = find_portal(mapping, ANCHOR_PORTALS[1])
    north = find_portal(mapping, ANCHOR_PORTALS[2])

    mesh = Mesh("fnd05_pilot_%s" % PILOT_REGION_ID)

    # 바닥 / 천장
    mesh.face([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], "shell")
    mesh.face([(x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)], "shell")

    # 남측벽 z = z0 (개구 없음)
    mesh.face([(x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)], "shell")

    # 북측벽 z = z1 (광장 portal 개구, X 축으로 뚫림)
    north_x = north["toPoint"]["X"]
    north_half = north["clearWidthM"] / 2.0
    for lo, hi, base, top in _wall_panels(x0, x1, y0, y1,
                                          (north_x - north_half, north_x + north_half,
                                           north["clearHeightM"])):
        mesh.face([(lo, base, z1), (hi, base, z1), (hi, top, z1), (lo, top, z1)], "shell")

    # 서단 캡 x = x0, 동단 캡 x = x1 (KORAIL 측 / 지하상가·도시철도 측 portal 개구)
    for cap_x, portal in ((x0, west), (x1, east)):
        point = portal["toPoint"] if cap_x == x0 else portal["fromPoint"]
        center_z = point["Z"]
        half = portal["clearWidthM"] / 2.0
        for lo, hi, base, top in _wall_panels(z0, z1, y0, y1,
                                              (center_z - half, center_z + half,
                                               portal["clearHeightM"])):
            mesh.face([(cap_x, base, lo), (cap_x, base, hi), (cap_x, top, hi), (cap_x, top, lo)],
                      "shell")

    # portal 개구 와이어프레임 (개구 4변)
    for portal in (west, east, north):
        if portal is north:  # 북측벽 개구는 X 축으로 뚫린다
            center = portal["toPoint"]
            half = portal["clearWidthM"] / 2.0
            base = y0
            top = y0 + portal["clearHeightM"]
            a = (center["X"] - half, base, z1)
            b = (center["X"] + half, base, z1)
            c = (center["X"] + half, top, z1)
            d = (center["X"] - half, top, z1)
        else:
            cap = x0 if portal["from"] != PILOT_REGION_ID else x1
            center = portal["toPoint"] if cap == x0 else portal["fromPoint"]
            half = portal["clearWidthM"] / 2.0
            base = y0
            top = y0 + portal["clearHeightM"]
            a = (cap, base, center["Z"] - half)
            b = (cap, base, center["Z"] + half)
            c = (cap, top, center["Z"] + half)
            d = (cap, top, center["Z"] - half)
        mesh.line(a, b)
        mesh.line(b, c)
        mesh.line(c, d)
        mesh.line(d, a)

    # 앵커 마커: 세 portal 끝점에 0.2 m 큐브 (셸 안쪽으로만 뻗음)
    mesh.box((x0, y0, -0.1), (x0 + 0.2, y0 + 0.2, 0.1), "anchors")
    mesh.box((x1 - 0.2, y0, -0.1), (x1, y0 + 0.2, 0.1), "anchors")
    mesh.box((north_x - 0.1, y0, z1 - 0.2), (north_x + 0.1, y0 + 0.2, z1), "anchors")

    # 측정축: 통로 중심선 (길이 앵커)
    mesh.line((x0, y0, 0.0), (x1, y0, 0.0), "shell")
    return mesh


# ------------------------------------------------------------------ validators


def validate_mapping(mapping):
    """매핑 JSON 자체의 스키마·등급·정합성 검사."""
    errors = []

    def check(condition, message):
        if not condition:
            errors.append(message)

    check(mapping.get("schemaVersion") == SCHEMA_VERSION,
          "schemaVersion must be %d" % SCHEMA_VERSION)
    check(mapping.get("classification") == CLASSIFICATION,
          "classification must be %s" % CLASSIFICATION)
    check(mapping.get("issue") == "#119", "issue must be #119")

    units = mapping.get("units", {})
    check(units.get("length") == "meters", "units.length must be meters")
    check(units.get("area") == "m2", "units.area must be m2")
    check(units.get("angle") == "degrees", "units.angle must be degrees")
    check("1:1" in units.get("scaleRatio", ""), "units.scaleRatio must declare 1:1")

    for key in ("confirmed_public_measure", "estimated_synthetic_measure",
                "estimated_synthetic_measure_unanchored", "angle"):
        check(key in mapping.get("tolerances", {}), "tolerances missing %s" % key)
    for key in _MEASURE_CLASSES:
        check(key in mapping.get("measureClasses", {}), "measureClasses missing %s" % key)

    # sources: P01~P09, 메타데이터만
    sources = mapping.get("sources", [])
    check(tuple(s.get("id") for s in sources) == _SOURCE_IDS,
          "sources must be exactly P01..P09 in order")
    for source in sources:
        sid = source.get("id")
        check(bool(source.get("title")), "%s missing title" % sid)
        check(bool(source.get("sourceUrl")), "%s missing sourceUrl" % sid)
        check(bool(source.get("planDocumentType")), "%s missing planDocumentType" % sid)
        check(source.get("dimensionsPresent") in (True, False),
              "%s missing dimensionsPresent" % sid)
        check("scalePresent" in source, "%s missing scalePresent" % sid)
        check(bool(source.get("rights")), "%s missing rights" % sid)
        check(source.get("imageInRepo") is False,
              "%s must keep the original image out of the repository" % sid)
        check(source.get("measureClassProduced") in _MEASURE_CLASSES,
              "%s measureClassProduced invalid" % sid)

    p04 = next(s for s in sources if s.get("id") == "P04")
    check(p04.get("publishedDimensionsM") == {"length": 99.6, "width": 8.0},
          "P04 published dimensions must be exactly 99.6 x 8.0 m")
    check(p04.get("measureClassProduced") == "confirmed_public_measure",
          "P04 must produce a confirmed_public_measure")
    p02 = next(s for s in sources if s.get("id") == "P02")
    check(p02.get("licenseId") == "KOGL-4", "P02 must record KOGL type 4")

    # regions: 13개, ID 일치, 등급별 분리 기록
    regions = mapping.get("regions", [])
    region_ids = tuple(r.get("regionId") for r in regions)
    check(region_ids == EXPECTED_REGION_IDS, "regions must be the 13 expected ids in order")
    check(len(set(region_ids)) == len(region_ids), "region ids must be unique")
    for region in regions:
        rid = region.get("regionId")
        bounds = region.get("worldBounds", {})
        check(set(bounds) == {"min", "max"}, "%s worldBounds must have min/max" % rid)
        if bounds:
            for corner in ("min", "max"):
                check(set(bounds[corner]) == {"X", "Y", "Z"},
                      "%s worldBounds.%s must have X/Y/Z" % (rid, corner))
            for axis in ("X", "Y", "Z"):
                check(bounds["min"][axis] <= bounds["max"][axis],
                      "%s worldBounds inverted on %s" % (rid, axis))
        check(isinstance(region.get("confirmedPublicMeasures"), list),
              "%s must always list confirmedPublicMeasures (possibly empty)" % rid)
        check(isinstance(region.get("estimatedSyntheticMeasures"), list),
              "%s must always list estimatedSyntheticMeasures" % rid)
        for measure in region.get("confirmedPublicMeasures", []):
            for field in ("id", "metric", "value", "unit", "measureClass", "sourceRef",
                          "provenance", "surveyedByProject"):
                check(field in measure, "%s confirmed measure missing %s" % (rid, field))
            check(measure.get("measureClass") == "confirmed_public_measure",
                  "%s confirmed measure has wrong class" % rid)
            check(measure.get("unit") == "m", "%s confirmed measure unit must be m" % rid)
            check(measure.get("surveyedByProject") is False,
                  "%s confirmed measure must not claim project survey" % rid)
        for measure in region.get("estimatedSyntheticMeasures", []):
            check(measure.get("measureClass") == "estimated_synthetic_measure",
                  "%s estimated measure has wrong class" % rid)
            check(measure.get("unit") == "m", "%s estimated measure unit must be m" % rid)

    # 확정 수치는 파일럿 구역에서만, 그것도 P04 에서만 나온다
    for region in regions:
        if region["regionId"] == PILOT_REGION_ID:
            continue
        check(region.get("confirmedPublicMeasures") == [],
              "%s must not claim a confirmed public measure yet" % region["regionId"])
    pilot_confirmed = find_region(mapping, PILOT_REGION_ID)["confirmedPublicMeasures"]
    check({m["sourceRef"] for m in pilot_confirmed} == {"P04"},
          "pilot confirmed measures must cite P04 only")

    constraint = mapping.get("numericConstraints", [{}])[0]
    check(constraint.get("sourceRef") == "P04", "numeric constraint must cite P04")
    check(constraint.get("lengthM") == 99.6 and constraint.get("widthM") == 8.0,
          "numeric constraint must be 99.6 x 8.0 m")
    forbidden = set(constraint.get("notScaleAnchorFor", []))
    check(PILOT_REGION_ID not in forbidden, "pilot region cannot forbid itself as anchor")
    for region_id in forbidden:
        check(region_id in region_ids, "notScaleAnchorFor lists unknown region %s" % region_id)

    # portals: 12개, 양끝이 구역 경계면 위, 끝점이 구역 안
    portals = mapping.get("portals", [])
    portal_ids = tuple(p.get("portalId") for p in portals)
    check(portal_ids == EXPECTED_PORTAL_IDS, "portals must be the 12 expected ids in order")
    for portal in portals:
        pid = portal.get("portalId")
        check(pid == "%s--%s" % (portal.get("from"), portal.get("to")),
              "%s id must be <from>--<to>" % pid)
        check(portal.get("from") in region_ids, "%s from-region unknown" % pid)
        check(portal.get("to") in region_ids, "%s to-region unknown" % pid)
        check(bool(portal.get("topologyStatus")), "%s missing topologyStatus" % pid)
        check(portal.get("geometryStatus") == "synthetic_design_not_surveyed",
              "%s geometryStatus must stay synthetic" % pid)
        check(isinstance(portal.get("publicTopologyConfirmed"), bool),
              "%s missing publicTopologyConfirmed" % pid)
        for endpoint in ("fromPoint", "toPoint"):
            check(set(portal.get(endpoint, {})) == {"X", "Y", "Z"},
                  "%s %s must have X/Y/Z" % (pid, endpoint))
        for side, endpoint in (("from", "fromPoint"), ("to", "toPoint")):
            region = find_region(mapping, portal[side])
            check(on_box_boundary(portal[endpoint], region["worldBounds"]),
                  "%s %s does not lie on the %s boundary" % (pid, endpoint, portal[side]))
        check(portal.get("clearWidthM", 0) > 0 and portal.get("clearHeightM", 0) > 0,
              "%s must have positive clear dimensions" % pid)

    # pilot 블록: 구역·portal·앵커가 실재해야 한다
    pilot = mapping.get("pilot", {})
    check(pilot.get("regionId") == PILOT_REGION_ID, "pilot.regionId mismatch")
    check(find_region(mapping, pilot["regionId"])["isPilotRegion"] is True,
          "pilot region must be flagged isPilotRegion")
    check(pilot.get("anchoredDimension", {}).get("value") == 99.6,
          "pilot anchored dimension must be 99.6 m")
    check(pilot.get("anchoredDimension", {}).get("measureClass") == "confirmed_public_measure",
          "pilot anchor must be a confirmed_public_measure")
    check(pilot.get("secondaryDimension", {}).get("value") == 8.0,
          "pilot secondary dimension must be 8.0 m")
    for anchor in pilot.get("scaleCheck", {}).get("endpointAnchors", []):
        portal = find_portal(mapping, anchor["portalId"])
        check(anchor.get("point") in ("fromPoint", "toPoint"),
              "pilot anchor %s must name its endpoint" % anchor["portalId"])
        point = portal[anchor["point"]]
        check(anchor["face"].startswith(PILOT_REGION_ID + "."),
              "pilot anchor %s must name a %s face" % (anchor["portalId"], PILOT_REGION_ID))
        check(math.isclose(point[anchor["axis"]], anchor["coordinate"], abs_tol=COORD_TOL),
              "pilot anchor %s does not match portal coordinate" % anchor["portalId"])
    for portal_id in ANCHOR_PORTALS:
        check(portal_id in portal_ids, "pilot anchor portal missing: %s" % portal_id)

    # 확정 불가 목록
    for key in ("unverifiedFacilities", "unverifiedRegulations"):
        items = mapping.get(key, [])
        check(len(items) > 0, "%s must not be empty" % key)
        for item in items:
            check(bool(item.get("id")) and bool(item.get("item")) and bool(item.get("reason")),
                  "%s entry must carry id/item/reason" % key)
    unverified_text = " ".join(i.get("item", "") for i in mapping.get("unverifiedFacilities", []))
    for needle in ("선로", "차량"):
        check(needle in unverified_text, "unverified facilities must name %s" % needle)

    check(len(mapping.get("prohibitedInferences", [])) > 0, "prohibitedInferences must not be empty")
    combined = " ".join(mapping.get("prohibitedInferences", []))
    for phrase in ("원본 이미지", "P04", "실측"):
        check(phrase in combined, "prohibitedInferences must cover %s" % phrase)

    # 권리: 모든 출처에 라이선스 기록, 원본 이미지 미포함
    rights = mapping.get("rightsCompliance", {})
    licenses = {entry.get("sourceRef"): entry for entry in rights.get("licenses", [])}
    check(set(licenses) == set(_SOURCE_IDS), "rightsCompliance must cover P01..P09")
    for sid, entry in licenses.items():
        check(entry.get("originalImageInRepo") is False,
              "%s license entry must keep image out of repo" % sid)
        check(entry.get("recordedContent") == "metadata_only",
              "%s license entry must record metadata only" % sid)
    check("저장소" in rights.get("rule", ""), "rightsCompliance.rule must state the repo rule")
    kogl = licenses["P02"]
    check(kogl.get("modificationAllowed") is False and kogl.get("commercialUseAllowed") is False,
          "P02 KOGL-4 must forbid modification and commercial use")
    check(set(kogl.get("terms", [])) == {"출처표시", "상업적 이용금지", "변경금지"},
          "P02 KOGL-4 must list the three terms")

    # 평면도 → 구역 매핑 표가 13구역 전부를 덮는가
    covered = {row.get("regionId") for row in mapping.get("planToRegionCoverage", [])}
    check(covered == set(EXPECTED_REGION_IDS),
          "planToRegionCoverage must cover all 13 regions")
    for row in mapping.get("planToRegionCoverage", []):
        check(row.get("result") in ("공개 평면도로 확정", "공개 자료로도 미확정"),
              "coverage row %s has invalid result" % row.get("regionId"))

    return errors


def validate_profile_crosscheck(mapping, profile):
    """connected-world-profile.json 과 매핑의 구역 크기·portal 끝점을 대조한다."""
    errors = []
    profile_regions = {r["Id"]: r for r in profile.get("Regions", [])}
    if set(profile_regions) != set(EXPECTED_REGION_IDS):
        errors.append("profile region ids differ from the expected 13")
        return errors

    for region in mapping["regions"]:
        rid = region["regionId"]
        source = profile_regions[rid]
        center = source["Center"]
        bounds = region["worldBounds"]
        expect_min = {
            "X": center["X"] - source["SizeX"] / 2.0,
            "Y": center["Y"],
            "Z": center["Z"] - source["SizeZ"] / 2.0,
        }
        expect_max = {
            "X": center["X"] + source["SizeX"] / 2.0,
            "Y": center["Y"] + source["Height"],
            "Z": center["Z"] + source["SizeZ"] / 2.0,
        }
        for corner, expected in (("min", expect_min), ("max", expect_max)):
            for axis in ("X", "Y", "Z"):
                actual = bounds[corner][axis]
                if not math.isclose(actual, expected[axis], abs_tol=COORD_TOL):
                    errors.append("%s worldBounds.%s.%s %.6f != profile %.6f"
                                  % (rid, corner, axis, actual, expected[axis]))

    profile_portals = {p["Id"]: p for p in profile.get("Portals", [])}
    for portal in mapping["portals"]:
        pid = portal["portalId"]
        if pid not in profile_portals:
            errors.append("portal %s missing from profile" % pid)
            continue
        source = profile_portals[pid]
        for mine, theirs in (("fromPoint", "FromPoint"), ("toPoint", "ToPoint")):
            for axis in ("X", "Y", "Z"):
                if not math.isclose(portal[mine][axis], source[theirs][axis], abs_tol=COORD_TOL):
                    errors.append("%s %s.%s differs from profile" % (pid, mine, axis))
        for mine, theirs in (("clearWidthM", "ClearWidth"), ("clearHeightM", "ClearHeight")):
            if not math.isclose(portal[mine], source[theirs], abs_tol=LENGTH_TOL):
                errors.append("%s %s differs from profile" % (pid, mine))
    return errors


def validate_blockout(mesh, mapping):
    """생성된 블록아웃의 치수·축척·좌표 정렬·경계 검사."""
    errors = []
    region = find_region(mapping, PILOT_REGION_ID)
    bounds = region["worldBounds"]

    (sx0, sy0, sz0), (sx1, sy1, sz1) = mesh.group_bounds("shell")
    length, height, width = sx1 - sx0, sy1 - sy0, sz1 - sz0

    confirmed = {m["metric"]: m["value"] for m in region["confirmedPublicMeasures"]}
    estimated = {m["metric"]: m["value"] for m in region["estimatedSyntheticMeasures"]}

    if not math.isclose(length, confirmed["size_x"], abs_tol=LENGTH_TOL):
        errors.append("blockout length %.3f != confirmed %.3f" % (length, confirmed["size_x"]))
    if not math.isclose(width, confirmed["size_z"], abs_tol=LENGTH_TOL):
        errors.append("blockout width %.3f != confirmed %.3f" % (width, confirmed["size_z"]))
    if not math.isclose(height, estimated["height_y"], abs_tol=ESTIMATED_TOL):
        errors.append("blockout height %.3f != estimated %.3f" % (height, estimated["height_y"]))

    ratio = length / width
    expected_ratio = confirmed["size_x"] / confirmed["size_z"]
    if abs(ratio - expected_ratio) > expected_ratio * RATIO_TOL_REL:
        errors.append("blockout ratio %.6f != published ratio %.6f" % (ratio, expected_ratio))

    for axis, actual, want in (("length", length, confirmed["size_x"]),
                               ("width", width, confirmed["size_z"])):
        if not math.isclose(actual, want, abs_tol=LENGTH_TOL):
            errors.append("blockout %s drift" % axis)

    # 셸도 앵커도 구역 경계를 넘지 않는다
    (ax0, ay0, az0), (ax1, ay1, az1) = mesh.all_bounds()
    for axis, low, high, blo, bhi in (
            ("X", ax0, ax1, bounds["min"]["X"], bounds["max"]["X"]),
            ("Y", ay0, ay1, bounds["min"]["Y"], bounds["max"]["Y"]),
            ("Z", az0, az1, bounds["min"]["Z"], bounds["max"]["Z"])):
        if low < blo - COORD_TOL or high > bhi + COORD_TOL:
            errors.append("blockout escapes region bounds on %s" % axis)

    # 좌표 정렬: 바닥고·중심·끝단
    if not math.isclose(sy0, bounds["min"]["Y"], abs_tol=COORD_TOL):
        errors.append("blockout floor elevation %.3f != %.3f" % (sy0, bounds["min"]["Y"]))
    if not math.isclose((sx0 + sx1) / 2.0, region["worldBounds"]["min"]["X"] + length / 2.0,
                        abs_tol=COORD_TOL):
        errors.append("blockout center X drift")
    if not math.isclose((sz0 + sz1) / 2.0, 0.0, abs_tol=COORD_TOL):
        errors.append("blockout center Z must be 0.0")
    if not math.isclose(sx0, find_portal(mapping, ANCHOR_PORTALS[0])["toPoint"]["X"],
                        abs_tol=COORD_TOL):
        errors.append("blockout west cap does not sit on the KORAIL-side portal")
    if not math.isclose(sx1, find_portal(mapping, ANCHOR_PORTALS[1])["fromPoint"]["X"],
                        abs_tol=COORD_TOL):
        errors.append("blockout east cap does not sit on the shopping-passage portal")
    if not math.isclose(sz1, find_portal(mapping, ANCHOR_PORTALS[2])["toPoint"]["Z"],
                        abs_tol=COORD_TOL):
        errors.append("blockout north wall does not sit on the forecourt portal")

    # portal 개구가 셸 안에 들어간다
    for portal_id in ANCHOR_PORTALS:
        portal = find_portal(mapping, portal_id)
        if portal["clearWidthM"] > width + COORD_TOL:
            errors.append("%s clear width exceeds shell" % portal_id)
        if portal["clearHeightM"] > height + COORD_TOL:
            errors.append("%s clear height exceeds shell" % portal_id)

    # 앵커 마커가 실제 portal 끝점 좌표에 놓인다
    west_point = find_portal(mapping, ANCHOR_PORTALS[0])["toPoint"]
    east_point = find_portal(mapping, ANCHOR_PORTALS[1])["fromPoint"]
    north_point = find_portal(mapping, ANCHOR_PORTALS[2])["toPoint"]
    if "anchors" not in mesh.groups:
        errors.append("blockout is missing its portal anchor markers")
    else:
        anchors = anchor_positions(mesh)
        if not math.isclose(anchors["west"], west_point["X"], abs_tol=COORD_TOL):
            errors.append("west anchor marker is off the KORAIL-side portal endpoint")
        if not math.isclose(anchors["east"], east_point["X"], abs_tol=COORD_TOL):
            errors.append("east anchor marker is off the shopping-passage portal endpoint")
        if not math.isclose(anchors["north"], north_point["Z"], abs_tol=COORD_TOL):
            errors.append("north anchor marker is off the forecourt portal endpoint")

    # 셸은 사각형 면으로만 구성된다
    if len(mesh.groups.get("shell", {}).get("q", [])) < 12:
        errors.append("shell must have at least 12 quad faces")
    if len(mesh.groups.get("portals", {}).get("l", [])) != 12:
        errors.append("three portal openings must contribute 12 wireframe lines")
    if len(mesh.lines) and len(mesh.lines) < 12:
        errors.append("blockout is missing its portal wireframes")

    return errors


def anchor_positions(mesh):
    """앵커 큐브 3개의 기준 좌표를 축별 스칼라로 돌려준다."""
    indices = mesh.groups["anchors"]["v"]
    xs = [mesh.vertices[i - 1][0] for i in indices]
    zs = [mesh.vertices[i - 1][2] for i in indices]
    return {"west": min(xs), "east": max(xs), "north": max(zs)}


def check_report(mapping_path, profile_path):
    lines = []
    errors = []
    mapping = load_json(mapping_path)
    lines.append("mapping: %s" % mapping_path)
    lines.append("classification: %s | schemaVersion: %s"
                 % (mapping.get("classification"), mapping.get("schemaVersion")))

    mapping_errors = validate_mapping(mapping)
    errors.extend(mapping_errors)
    lines.append("schema/registry checks: %s (%d errors)"
                 % ("PASS" if not mapping_errors else "FAIL", len(mapping_errors)))

    if os.path.exists(profile_path):
        profile_errors = validate_profile_crosscheck(mapping, load_json(profile_path))
        errors.extend(profile_errors)
        lines.append("profile cross-check (%s): %s (%d errors)"
                     % (os.path.basename(profile_path),
                        "PASS" if not profile_errors else "FAIL", len(profile_errors)))
    else:
        lines.append("profile cross-check: SKIP (profile not found)")

    mesh = build_connector_blockout(mapping)
    geometry_errors = validate_blockout(mesh, mapping)
    errors.extend(geometry_errors)
    lines.append("blockout geometry checks: %s (%d errors)"
                 % ("PASS" if not geometry_errors else "FAIL", len(geometry_errors)))

    region = find_region(mapping, PILOT_REGION_ID)
    (x0, y0, z0), (x1, y1, z1) = mesh.group_bounds("shell")
    lines.append("pilot region: %s (%s)" % (PILOT_REGION_ID, region["label"]))
    lines.append("  confirmed_public_measure  length %.3f m x width %.3f m  (P04)"
                 % (x1 - x0, z1 - z0))
    lines.append("  estimated_synthetic_measure  height %.3f m, floor Y %.3f m, portal 3.2 x 2.7 m"
                 % (y1 - y0, y0))
    lines.append("  scale ratio length/width = %.6f (published %.6f)"
                 % ((x1 - x0) / (z1 - z0), 99.6 / 8.0))
    lines.append("  shell quads=%d  portal lines=%d  anchor markers=%d"
                 % (len(mesh.groups["shell"]["q"]), len(mesh.groups["portals"]["l"]),
                    len(mesh.groups["anchors"]["q"]) // 6))
    lines.append("regions mapped=%d  portals=%d  sources=%d  unverified facilities=%d  regulations=%d"
                 % (len(mapping["regions"]), len(mapping["portals"]), len(mapping["sources"]),
                    len(mapping["unverifiedFacilities"]), len(mapping["unverifiedRegulations"])))
    for message in errors:
        lines.append("ERROR: %s" % message)
    lines.append("RESULT: %s" % ("PASS" if not errors else "FAIL"))
    return lines, errors


# ------------------------------------------------------------------------ tests


class BlockoutPilotTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mapping = load_json(_SETTINGS["mapping"])
        cls.profile = (load_json(_SETTINGS["profile"])
                       if os.path.exists(_SETTINGS["profile"]) else None)
        cls.mesh = build_connector_blockout(cls.mapping)
        cls.region = find_region(cls.mapping, PILOT_REGION_ID)
        cls.bounds = cls.region["worldBounds"]

    def test_01_mapping_is_schema_compliant(self):
        self.assertEqual(validate_mapping(self.mapping), [])

    def test_02_classification_and_units(self):
        self.assertEqual(self.mapping["schemaVersion"], 1)
        self.assertEqual(self.mapping["classification"], "PUBLIC_PROJECT_CONTEXT")
        self.assertEqual(self.mapping["units"]["length"], "meters")
        self.assertEqual(self.mapping["units"]["area"], "m2")
        self.assertEqual(self.mapping["units"]["angle"], "degrees")
        self.assertIn("1:1", self.mapping["units"]["scaleRatio"])

    def test_03_thirteen_regions(self):
        ids = [r["regionId"] for r in self.mapping["regions"]]
        self.assertEqual(len(ids), 13)
        self.assertEqual(tuple(ids), EXPECTED_REGION_IDS)
        self.assertEqual(len(set(ids)), 13)

    def test_04_sources_are_p01_to_p09_metadata_only(self):
        self.assertEqual(tuple(s["id"] for s in self.mapping["sources"]), _SOURCE_IDS)
        for source in self.mapping["sources"]:
            self.assertFalse(source["imageInRepo"], source["id"])
            self.assertTrue(source["rights"])
        self.assertEqual(
            {s["sourceRef"] for s in self.mapping["rightsCompliance"]["licenses"]}, set(_SOURCE_IDS))

    def test_05_pilot_anchor_is_confirmed_public_measure(self):
        confirmed = {m["metric"]: m for m in self.region["confirmedPublicMeasures"]}
        self.assertEqual(set(confirmed), {"size_x", "size_z"})
        self.assertEqual(confirmed["size_x"]["value"], 99.6)
        self.assertEqual(confirmed["size_z"]["value"], 8.0)
        for measure in confirmed.values():
            self.assertEqual(measure["measureClass"], "confirmed_public_measure")
            self.assertEqual(measure["unit"], "m")
            self.assertEqual(measure["sourceRef"], "P04")
            self.assertFalse(measure["surveyedByProject"],
                             "운영자 공표값을 프로젝트 실측으로 표기했다")

    def test_06_confirmed_measures_exist_only_in_the_pilot_region(self):
        offenders = [r["regionId"] for r in self.mapping["regions"]
                     if r["regionId"] != PILOT_REGION_ID and r["confirmedPublicMeasures"]]
        self.assertEqual(offenders, [])

    def test_07_p04_is_not_used_as_a_scale_anchor_elsewhere(self):
        constraint = self.mapping["numericConstraints"][0]
        self.assertEqual(constraint["sourceRef"], "P04")
        for region_id in constraint["notScaleAnchorFor"]:
            region = find_region(self.mapping, region_id)
            self.assertEqual(region["confirmedPublicMeasures"], [],
                             "%s 이 P04 앵커를 전용했다" % region_id)

    def test_08_blockout_dimensions_match_published_measure(self):
        (x0, y0, z0), (x1, y1, z1) = self.mesh.group_bounds("shell")
        self.assertAlmostEqual(x1 - x0, 99.6, delta=LENGTH_TOL)
        self.assertAlmostEqual(z1 - z0, 8.0, delta=LENGTH_TOL)
        self.assertAlmostEqual(y1 - y0, 3.2, delta=ESTIMATED_TOL)

    def test_09_blockout_scale_ratio_is_one_to_one(self):
        (x0, y0, z0), (x1, y1, z1) = self.mesh.group_bounds("shell")
        published = 99.6 / 8.0
        self.assertAlmostEqual((x1 - x0) / (z1 - z0), published,
                               delta=published * RATIO_TOL_REL)
        # 1 단위 = 1 m 이므로 좌표 자체가 미터 값이어야 한다
        self.assertAlmostEqual(x1 - x0, 99.6, delta=LENGTH_TOL)

    def test_10_blockout_stays_inside_region_bounds(self):
        (ax0, ay0, az0), (ax1, ay1, az1) = self.mesh.all_bounds()
        self.assertGreaterEqual(ax0, self.bounds["min"]["X"] - COORD_TOL)
        self.assertLessEqual(ax1, self.bounds["max"]["X"] + COORD_TOL)
        self.assertGreaterEqual(ay0, self.bounds["min"]["Y"] - COORD_TOL)
        self.assertLessEqual(ay1, self.bounds["max"]["Y"] + COORD_TOL)
        self.assertGreaterEqual(az0, self.bounds["min"]["Z"] - COORD_TOL)
        self.assertLessEqual(az1, self.bounds["max"]["Z"] + COORD_TOL)

    def test_11_coordinate_alignment_floor_and_centerline(self):
        (x0, y0, z0), (x1, y1, z1) = self.mesh.group_bounds("shell")
        self.assertAlmostEqual(y0, -4.0, delta=COORD_TOL)
        self.assertAlmostEqual((x0 + x1) / 2.0, 129.8, delta=COORD_TOL)
        self.assertAlmostEqual((z0 + z1) / 2.0, 0.0, delta=COORD_TOL)

    def test_12_portal_boundaries_pin_the_shell_ends(self):
        west = find_portal(self.mapping, ANCHOR_PORTALS[0])
        east = find_portal(self.mapping, ANCHOR_PORTALS[1])
        north = find_portal(self.mapping, ANCHOR_PORTALS[2])
        (x0, y0, z0), (x1, y1, z1) = self.mesh.group_bounds("shell")
        self.assertAlmostEqual(x0, west["toPoint"]["X"], delta=COORD_TOL)
        self.assertAlmostEqual(x1, east["fromPoint"]["X"], delta=COORD_TOL)
        self.assertAlmostEqual(z1, north["toPoint"]["Z"], delta=COORD_TOL)
        self.assertAlmostEqual(west["toPoint"]["Y"], y0, delta=COORD_TOL)
        self.assertAlmostEqual(east["fromPoint"]["Y"], y0, delta=COORD_TOL)

    def test_13_anchor_markers_sit_on_the_portal_endpoints(self):
        anchors = anchor_positions(self.mesh)
        west = find_portal(self.mapping, ANCHOR_PORTALS[0])["toPoint"]
        east = find_portal(self.mapping, ANCHOR_PORTALS[1])["fromPoint"]
        north = find_portal(self.mapping, ANCHOR_PORTALS[2])["toPoint"]
        self.assertAlmostEqual(anchors["west"], west["X"], delta=COORD_TOL)
        self.assertAlmostEqual(anchors["east"], east["X"], delta=COORD_TOL)
        self.assertAlmostEqual(anchors["north"], north["Z"], delta=COORD_TOL)

    def test_14_portal_openings_fit_inside_the_shell(self):
        (x0, y0, z0), (x1, y1, z1) = self.mesh.group_bounds("shell")
        for portal_id in ANCHOR_PORTALS:
            portal = find_portal(self.mapping, portal_id)
            self.assertLessEqual(portal["clearWidthM"], z1 - z0 + COORD_TOL)
            self.assertLessEqual(portal["clearHeightM"], y1 - y0 + COORD_TOL)
            self.assertGreaterEqual(portal["clearHeightM"], 2.0,
                                    "통행 개구가 사람이 지나갈 수 없는 높이로 저작됐다")

    def test_15_every_portal_endpoint_lies_on_a_region_boundary(self):
        for portal in self.mapping["portals"]:
            for side, endpoint in (("from", "fromPoint"), ("to", "toPoint")):
                region = find_region(self.mapping, portal[side])
                self.assertTrue(
                    on_box_boundary(portal[endpoint], region["worldBounds"]),
                    "%s %s 이 %s 경계면 위에 없다" % (portal["portalId"], endpoint, portal[side]))

    def test_16_unverified_items_are_listed_not_assumed(self):
        self.assertTrue(self.mapping["unverifiedFacilities"])
        self.assertTrue(self.mapping["unverifiedRegulations"])
        for item in self.mapping["unverifiedFacilities"] + self.mapping["unverifiedRegulations"]:
            self.assertTrue(item["id"] and item["item"] and item["reason"])
        regulations = " ".join(i["item"] for i in self.mapping["unverifiedRegulations"])
        self.assertIn("소방시설법", regulations)

    def test_17_plan_to_region_coverage_covers_all_regions(self):
        covered = {row["regionId"] for row in self.mapping["planToRegionCoverage"]}
        self.assertEqual(covered, set(EXPECTED_REGION_IDS))

    def test_18_obj_export_is_wellformed_and_carries_provenance(self):
        text = self.mesh.obj_text()
        self.assertIn("# unit: meters (1 unit = 1 m, scale 1:1)", text)
        self.assertIn("confirmed_public_measure", text)
        self.assertIn("synthetic_design_not_surveyed", text)
        self.assertNotIn("base64", text)
        self.assertNotIn("data:", text)
        self.assertNotIn("http", text, "OBJ 에 원본 이미지/URL 을 넣지 않는다")
        counted = sum(1 for line in text.splitlines() if line.startswith("v "))
        self.assertEqual(counted, len(self.mesh.vertices))
        faces = sum(1 for line in text.splitlines() if line.startswith("f "))
        self.assertEqual(faces, len(self.mesh.quads))
        self.assertEqual(sum(1 for line in text.splitlines() if line.startswith("l ")),
                         len(self.mesh.lines))

    def test_19_profile_crosscheck(self):
        if self.profile is None:
            self.skipTest("connected-world-profile.json not available")
        self.assertEqual(validate_profile_crosscheck(self.mapping, self.profile), [])

    def test_20_generated_blockout_passes_geometry_validation(self):
        self.assertEqual(validate_blockout(self.mesh, self.mapping), [])

    def test_21_kogl_type4_terms_are_recorded(self):
        kogl = next(l for l in self.mapping["rightsCompliance"]["licenses"]
                    if l["sourceRef"] == "P02")
        self.assertEqual(kogl["licenseId"], "KOGL-4")
        self.assertFalse(kogl["modificationAllowed"])
        self.assertFalse(kogl["commercialUseAllowed"])
        self.assertEqual(sorted(kogl["terms"]), sorted(["출처표시", "상업적 이용금지", "변경금지"]))

    def test_22_mutations_are_caught(self):
        """변이시험: 검사기가 조용히 통과시키지 않는지 확인한다."""
        def mutated(mutate):
            clone = json.loads(json.dumps(self.mapping))
            mutate(clone)
            return validate_mapping(clone)

        def bump_version(clone):
            clone["schemaVersion"] = 2

        def reclassify(clone):
            clone["classification"] = "PUBLIC"

        def inflate_length(clone):
            clone["sources"][3]["publishedDimensionsM"]["length"] = 100.0
            clone["numericConstraints"][0]["lengthM"] = 100.0
            for measure in clone["regions"][8]["confirmedPublicMeasures"]:
                if measure["metric"] == "size_x":
                    measure["value"] = 100.0

        def leak_confirmed_measure(clone):
            clone["regions"][4]["confirmedPublicMeasures"] = [{
                "id": "station_concourse_2f.extent_x", "metric": "size_x", "value": 24.0,
                "unit": "m", "measureClass": "confirmed_public_measure", "sourceRef": "P04",
                "provenance": "invented", "surveyedByProject": True,
            }]

        def move_portal_off_boundary(clone):
            clone["portals"][8]["fromPoint"]["X"] = 150.0

        def embed_image(clone):
            clone["sources"][0]["imageInRepo"] = True

        def drop_confirmed_anchor(clone):
            clone["regions"][8]["confirmedPublicMeasures"] = []

        def overclaim_survey(clone):
            clone["regions"][8]["confirmedPublicMeasures"][0]["surveyedByProject"] = True

        for mutate in (bump_version, reclassify, inflate_length, leak_confirmed_measure,
                       move_portal_off_boundary, embed_image, drop_confirmed_anchor,
                       overclaim_survey):
            errors = mutated(mutate)
            self.assertTrue(errors, "%s 변이를 검사기가 잡지 못했다" % mutate.__name__)
            self.assertEqual(validate_mapping(self.mapping), [],
                             "원본이 변이시험으로 오염됐다")

    def test_23_geometry_validator_catches_drift(self):
        bounds = self.bounds
        drifted = Mesh("drift")
        drifted.face([(bounds["min"]["X"], bounds["min"]["Y"], bounds["min"]["Z"]),
                      (bounds["max"]["X"] + 5.0, bounds["min"]["Y"], bounds["min"]["Z"]),
                      (bounds["max"]["X"] + 5.0, bounds["min"]["Y"], bounds["max"]["Z"]),
                      (bounds["min"]["X"], bounds["min"]["Y"], bounds["max"]["Z"])], "shell")
        for _ in range(12):
            drifted.face([(80.0, -4.0, -4.0), (80.1, -4.0, -4.0),
                          (80.1, -3.9, -4.0), (80.0, -3.9, -4.0)], "shell")
        errors = validate_blockout(drifted, self.mapping)
        self.assertTrue(any("escapes region bounds" in e for e in errors),
                        "구역 경계를 넘은 셸을 검사기가 통과시켰다")
        self.assertTrue(any("length" in e for e in errors),
                        "치수 이탈을 검사기가 통과시켰다")


def run_tests():
    suite = unittest.TestLoader().loadTestsFromTestCase(BlockoutPilotTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    return 0 if result.wasSuccessful() else 1


# ------------------------------------------------------------------------- main


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="FND-05 pilot blockout for %s (P04, 99.6 m x 8.0 m)" % PILOT_REGION_ID)
    parser.add_argument("--check", action="store_true",
                        help="매핑 스키마·치수·정렬·portal 경계 검사")
    parser.add_argument("--generate", action="store_true", help="블록아웃 OBJ 생성")
    parser.add_argument("--test", action="store_true", help="단위 테스트 실행")
    parser.add_argument("--mapping", default=DEFAULT_MAPPING, help="plan-mapping.json 경로")
    parser.add_argument("--profile", default=DEFAULT_PROFILE,
                        help="connected-world-profile.json 경로 (없으면 내부 정합성만 검사)")
    parser.add_argument("--out", default=DEFAULT_OUT, help="--generate 출력 OBJ 경로")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)
    if not (args.check or args.generate or args.test):
        args.check = True

    _SETTINGS["mapping"] = args.mapping
    _SETTINGS["profile"] = args.profile

    exit_code = 0

    if args.check:
        if not os.path.exists(args.mapping):
            sys.stderr.write("mapping not found: %s\n" % args.mapping)
            return 2
        lines, errors = check_report(args.mapping, args.profile)
        sys.stdout.write("\n".join(lines) + "\n")
        if errors:
            exit_code = 1

    if args.generate:
        mapping = load_json(args.mapping)
        mapping_errors = validate_mapping(mapping)
        if mapping_errors:
            for message in mapping_errors:
                sys.stderr.write("ERROR: %s\n" % message)
            sys.stderr.write("refusing to generate from a non-compliant mapping\n")
            return 1
        mesh = build_connector_blockout(mapping)
        geometry_errors = validate_blockout(mesh, mapping)
        if geometry_errors:
            for message in geometry_errors:
                sys.stderr.write("ERROR: %s\n" % message)
            return 1
        out_dir = os.path.dirname(os.path.abspath(args.out))
        if out_dir and not os.path.isdir(out_dir):
            os.makedirs(out_dir)
        with open(args.out, "w", encoding="utf-8") as handle:
            handle.write(mesh.obj_text())
        sys.stdout.write("wrote %s\n" % args.out)
        (x0, y0, z0), (x1, y1, z1) = mesh.group_bounds("shell")
        sys.stdout.write("  length %.3f m x width %.3f m x height %.3f m, floor Y %.3f m\n"
                         % (x1 - x0, z1 - z0, y1 - y0, y0))

    if args.test:
        test_code = run_tests()
        exit_code = exit_code or test_code

    return exit_code


if __name__ == "__main__":
    sys.exit(main())
