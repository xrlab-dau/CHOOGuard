#!/usr/bin/env python3
"""R-09 공개 자료 전제 전수 정합·재발 검사 (public-premise audit).

배경
----
저장소 문서 상당수는 2026-09-12 지시 이전에 작성되었다. 그 문서들은 다음 세
전제를 "앞으로 성립할 것"으로 서술한다.

* ``korail_provision``    -- 코레일이 개발 요구·도면·평면·매뉴얼·자료를 제공하거나
  서면 회신·승인을 준다.
* ``on_site_filming``     -- 팀이 사전 서면 승인 후 실제 역사를 직접 촬영한다.
* ``non_public_drawings`` -- 승인 도면·준공도면·측량 자료로 합성 치수를 실측으로
  승격한다.

2026-09-12 지시 이후 세 전제는 성립하지 않는다. 코레일은 아무것도 제공하지 않고,
실제 역사 촬영도 하지 않으며, 모든 참조는 공개 자료로만 취득한다. 그렇다고 옛
문서를 삭제하지는 않았으므로 저장소에는 폐기된 전제가 그대로 남아 있다. 이 검사는
그 출현을 **전수**로 찾아 분류하고, 각 발견에 처분(disposition)을 붙여 미판정이
남지 않게 한다.

분류
----
``positive_premise``
    전제를 계획·의존성·요구로 **긍정**하는 문장. 예: "촬영은 … 사전 서면 승인 후
    수행한다", "KORAIL 회신을 받으면 O-항목을 갱신한다".
``negative_exclusion``
    전제가 성립하지 않음을 **명시적으로 배제**하는 문장. 예: "촬영안함",
    "코레일 제공 없음", "KORAIL 회신 없이 … 개발한다", "제공하지 않는다". 이미
    현행 정책과 일치하므로 결함이 아니다.
``historical_statement``
    날짜가 박힌 스냅샷·대체된 기록, 또는 문서 스스로가 이력임을 선언한 범위
    안의 문장. 예: 문서 상단 "역사 범위 안내 · 2026-09-12", "과거 계획".

판정 우선순위는 **배제 > 이력 > 긍정** 이다. 부정이 모든 다른 표지를 이긴다.
따라서 "KORAIL 회신 없이 개발한다"는 긍정으로 새지 않는다.

집계 규칙
--------
*발생(occurrence)* 하나는 한 줄에서 분류된 전제 문장 하나다. 정규화한 문장과
분류가 같은 발생들은 *발견(finding)* 하나로 묶이고, finding은 모든 ``path:line``
위치를 보존한다. 이 묶음이 재발 검사의 핵심이다. 같은 폐기 문장이 작업지시서,
work graph, 생성 번들에 복사되어 있어도 판정은 하나로 수렴한다.

CLI
---
    python3 scripts/context/audit_public_premises.py
    python3 scripts/context/audit_public_premises.py --input . --output report.json
    python3 scripts/context/audit_public_premises.py \
        --dispositions docs/context/reviews/public-premise-dispositions.json
    python3 scripts/context/audit_public_premises.py \
        --check docs/context/reviews/public-premise-dispositions.json

종료 코드: 0 = 완료(미판정 없음), 1 = ``--fail-on-unresolved`` 상태에서 미판정
발견 존재, 2 = 사용법/IO/스키마 오류.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

SCHEMA_VERSION = 1
CLASSIFICATION = "PUBLIC_PROJECT_CONTEXT"
WORK_ID = "R-09"
ISSUE = 121

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_ROOTS = ("docs", "scripts")
#: 처분표의 저장소 상대 경로. 아래 분류별 기본 처분 표와 이름이 겹치지 않도록
#: ``_PATH`` 접미어를 붙인다.
DEFAULT_DISPOSITIONS_PATH = "docs/context/reviews/public-premise-dispositions.json"

TEXT_SUFFIXES = frozenset(
    {
        ".md",
        ".markdown",
        ".json",
        ".jsonld",
        ".jsonl",
        ".py",
        ".mjs",
        ".js",
        ".ts",
        ".tsx",
        ".html",
        ".htm",
        ".txt",
        ".yml",
        ".yaml",
        ".css",
        ".csv",
        ".sh",
    }
)
SKIP_DIRS = frozenset(
    {".git", "node_modules", "__pycache__", ".venv", "venv", ".mypy_cache", "Library"}
)

#: 검사기·그 시험·그 출력물은 자기가 찾는 문구를 그대로 담는다. 이 파일들을
#: 스캔하면 보고서가 자기 소스 텍스트에 의존하게 되므로 제외한다. 재발 검사가
#: 검사기 편집이나 자기 출력의 재인용에 흔들리지 않게 하는 장치다. 처분표를
#: 제외하지 않으면 매 실행이 자기 발견을 다시 발견해 발견 수가 끝없이 늘어난다.
SELF_EXCLUDED = frozenset(
    {
        "scripts/context/audit_public_premises.py",
        "scripts/context/test_public_premises.py",
        DEFAULT_DISPOSITIONS_PATH,
    }
)

# --------------------------------------------------------------------------- #
# 전제 탐지
# --------------------------------------------------------------------------- #

#: 주제별 전제 표지. 전제를 "말하는" 명사구·서술구만 잡고, 단순히 낱말이
#: 스쳐 지나가는 경우는 잡지 않는다.
TOPIC_PATTERNS = {
    "korail_provision": re.compile(
        r"(?:코레일|KORAIL|기관)[^\n]{0,24}?"
        r"(?:제공|제공받|수신|회신|전달|답변|승인|매뉴얼|측량|도면|평면|자료)"
        r"|(?:자료|도면|평면|매뉴얼|측량\s*자료)[^\n]{0,8}?(?:제공|수신|회신|반입)"
        # "회신을 기다린다"류는 주체를 반드시 코레일·기관으로 묶는다. 주체를
        # 비워 두면 "PM 승인 대기", "리뷰 승인 대기" 같은 내부 거버넌스 문장이
        # 코레일 전제로 오탐된다.
        r"|(?:코레일|KORAIL|기관)[^\n]{0,24}?(?:회신|답변|승인)[^\n]{0,8}?"
        r"(?:을|를)?\s*(?:받|수신|대기|기다)"
    ),
    "on_site_filming": re.compile(
        r"(?:역사|현장|실제|직접|승인된|사전\s*승인)[^\n]{0,8}?촬영"
        r"|촬영[^\n]{0,8}?(?:승인|계획|동선|체크리스트|대상|범위|기기|시간|수행|진행|시작|이미지|원본|자료)"
        r"|촬영[^\n]{0,4}?(?:을|를|은|는)[^\n]{0,12}?(?:한다|수행|진행|시작|완료)"
        # 부정 형태도 표지로 잡는다. 잡아 두면 배제로 기록되어 "전수"의 근거가
        # 된다. 잡지 않으면 요구사항의 예시 문구가 아예 보고서에 나타나지 않는다.
        # 분류는 부정이 이기므로 긍정으로 새지 않는다.
        r"|촬영[^\n]{0,8}?(?:없이|없음|안\s*함)"
        r"|filming\s+(?:approval|plan|permit|record)"
    ),
    "non_public_drawings": re.compile(
        r"(?:승인|승인된|비공개|준공|현행|제한|실측|시공)[^\n]{0,6}?도면"
        r"|도면[^\n]{0,8}?(?:승인|확보|제공|수령|반입)"
        r"|측량\s*자료"
        r"|(?:실측|승인)[^\n]{0,8}?측량"
    ),
}

TOPIC_LABELS = {
    "korail_provision": "코레일 자료·도면·매뉴얼 제공 / 서면 회신",
    "on_site_filming": "실제 역사 현장 촬영 · 촬영 승인",
    "non_public_drawings": "비공개 도면·측량 자료 / 실측 승격",
}

#: 주제 표지가 걸렸지만 전제가 아니라 다른 대상(점수·피드백 제공 등)을 말하는
#: 문장을 취소하는 가드.
TOPIC_GUARDS = {
    "korail_provision": re.compile(
        r"(?:점수|이수\s*판정|피드백|평가기준|판정|합격|영상\s*녹화|음성\s*녹음)[^\n]{0,12}?제공"
        r"|(?:MCP|서브에이전트|승인창|도구)[^\n]{0,10}?제공"
    ),
    "on_site_filming": re.compile(
        # 툴체인 위생 검사에서 "촬영 원본"은 금지 파일 *부류 이름*이지 역사 촬영
        # 전제가 아니다. 금지·차단·위생 어휘와 함께 쓰이면 주제 표지를 취소한다.
        r"(?:금지|차단|위생|prune|미추적|의존성\s*트리|자격\s*파일|환경\s*파일|forbidden)"
        r"[^\n]{0,12}?촬영\s*원본"
        r"|촬영\s*원본[^\n]{0,12}?(?:파일|금지|차단|위생|검사|잡는다|거부)"
    ),
    "non_public_drawings": re.compile(
        r"공개\s*(?:평면도|도면|안내지도)[^\n]{0,10}?(?:아니다|아님|없)"
        r"|(?:실측|승인)[^\n]{0,6}?도면[^\n]{0,6}?이\s*아니다"
    ),
}

#: 문서 단위 주제 억제. 창(window)만으로는 갈리지 않는, 그 문서에서만 특정 주제
#: 낱말이 다른 뜻으로 쓰이는 경우를 경로로 고정한다. 근거를 함께 남긴다.
DOCUMENT_TOPIC_GUARDS = {
    "scripts/bootstrap/verify_toolchain.py": {
        "topics": frozenset({"on_site_filming"}),
        "reason": (
            "이 파일의 '촬영 원본'은 워크스페이스 금지 파일 부류 이름이다. 실제 역사 "
            "촬영을 전제하는 문장이 아니므로 on_site_filming 표지를 억제한다."
        ),
    },
    "scripts/bootstrap/test_verify_toolchain.py": {
        "topics": frozenset({"on_site_filming"}),
        "reason": (
            "verify_toolchain.py의 시험. 같은 금지 파일 부류 이름을 쓰므로 "
            "on_site_filming 표지를 억제한다."
        ),
    },
}

#: 전제를 명시적으로 취소하는 부정 표지. 이력 표지보다 먼저 검사한다.
NEGATION_PATTERNS = (
    # "촬영 없이", "회신 없이", "제공 없이", "승인 없이", "자료 없이" 등.
    # 요구사항의 핵심 회귀 지점이라 별도로 고정한다.
    re.compile(
        r"(?:촬영|회신|제공|수신|승인|자료|도면|평면|매뉴얼|답변|전달|측량)"
        r"[^\n]{0,10}?없이"
    ),
    re.compile(
        r"(?:촬영|회신|제공|수신|승인|자료|도면|매뉴얼|답변|측량)[^\n]{0,16}?없(?:음|다|으며|고)"
    ),
    # 한국어 부정 종결 "-지 않-"과 "-지 못-". 동사 어간에 묶지 않는다.
    # "포괄 상속하지 않는다", "우선하지 않음", "판정하지 않습니다",
    # "기다리지 않고", "바꾸지 않음", "확보하지 못했다"가 모두 여기 걸린다.
    # 부정이 긍정으로 새는 것이 이 검사의 최대 회귀 위험이므로 어간 목록에
    # 기대지 않고 부정 어미 자체를 고정한다.
    re.compile(r"지\s*않"),
    re.compile(r"지\s*못"),
    # 짧은 부정 "안 함/안 한다": "촬영안함", "제공 안 함".
    re.compile(r"안\s*(?:함|한다|했다|하며|하고|해|됨|된다)"),
    # 서술적 부정: "…이 아니다/아니며/아니고/아니라".
    re.compile(r"아님|아니(?:다|며|고|라|었|라는|함)"),
    # 동일성 부정: "회신·촬영 승인과 다르다".
    re.compile(r"[과와]\s*다르다"),
    # "미확인/미검증/미판정"처럼 전제가 성립하지 않음을 말하는 서술만 잡는다.
    # "미추적 촬영 원본"의 "미추적"은 명사를 수식하는 관형어이지 전제의 부정이
    # 아니므로 넣지 않는다. 접두어를 넓히면 부정이 아니라 수식이 삼켜진다.
    re.compile(r"미(?:확인|검증|판정|제공|수신|승인|촬영|회신|반입|도착|공개)"),

    # 서술적 부정: "…이 아니다", "…이 아니며", "…은 아님".
    re.compile(
        r"(?:제공|회신|승인|촬영|요구|의존|선행\s*조건|입력|전제)[^\n]{0,10}?"
        r"(?:아니다|아니며|아님|아니고)"
    ),
    re.compile(
        r"not\s+(?:provided|required|necessary|a\s+dependency)"
        r"|no\s+(?:provision|reply|approval|filming)"
        r"|does\s+not\s+provide"
        r"|without\s+(?:KORAIL|official|written|on-site)"
    ),
    # 개발 차단이 아님을 밝히는 문장.
    re.compile(r"차단하지\s*않|막지\s*않|전역\s*차단이\s*아님"),
)

#: 줄 수준 이력 표지.
HISTORICAL_MARKERS = re.compile(
    r"과거\s*계획|과거\s*지시|과거\s*결정|과거\s*전략|과거\s*싱글플레이"
    r"|dated\s+historical\s+snapshot|historical\s+snapshot|역사적\s*스냅샷"
    r"|이력이다|이력이며|이력으로\s*남|당시\s*작업|이전\s*판|이전\s*스냅샷"
    r"|superseded|deprecated"
)

#: 문서 수준 이력 표지. 문서가 스스로 날짜 박힌 스냅샷이라 선언하면 그 안의
#: 모든 전제 발생은 이력이다.
DOCUMENT_HISTORICAL_MARKERS = re.compile(
    r"역사\s*범위\s*안내|dated\s+historical\s+snapshot|historical\s+snapshot"
    r"|역사적\s*스냅샷|이전\s*스냅샷|이\s*문서는\s*[^\n]{0,40}이력"
    r"|이\s*문서는\s*과거|아래는\s*[^\n]{0,30}당시|아래\s*실행환경\s*기록은\s*역사"
)

#: 이력 선언을 찾는 머리말 줄 수.
DOCUMENT_HISTORICAL_SCAN_LINES = 40

#: 매치를 담은 문장을 잘라낼 경계 문자.
#: 대시('—')는 부가 설명 절(예: "KORAIL 원본 자료 실물 대조 — 원본에 접근하지 않았다")을
#: 이어주는 구문이므로 문장 경계로 잘라내면 후속 부정절이 유실된다. 경계에서 제외한다.
_BOUNDARY_CHARS = (".", "|", '"', "\n", ">", "\\")

#: 문장이 아닌 것(셸 명령, 경로/URL 잡음).
NOISE_PATTERN = re.compile(
    #: 경로·식별자 토막만 잡는다. ``\w``를 쓰면 한글도 단어 문자라 "촬영안함"
    #: 같은 한글 단일 토큰이 경로로 오인되어 통째로 사라진다. ASCII로 좁힌다.
    r"grep\s|--|^[A-Za-z0-9_./-]+$|^https?://|^\s*(?:if|for|while|def|export|python3?)\b"
)

CLASSIFICATIONS = ("positive_premise", "negative_exclusion", "historical_statement")

# --------------------------------------------------------------------------- #
# 처분 정책
# --------------------------------------------------------------------------- #

DISPOSITION_SUPERSEDED = "superseded_by_2026_09_12_directive"
DISPOSITION_EXCLUDED = "affirmed_public_source_exclusion"
DISPOSITION_HISTORICAL = "retained_historical_snapshot"
DISPOSITION_ALIGNED = "public_source_aligned"
DISPOSITION_GENERATED = "generated_artifact_derivative"
DISPOSITION_UNRESOLVED = "unresolved"

RESOLVED_DISPOSITIONS = frozenset(
    {
        DISPOSITION_SUPERSEDED,
        DISPOSITION_EXCLUDED,
        DISPOSITION_HISTORICAL,
        DISPOSITION_ALIGNED,
        DISPOSITION_GENERATED,
    }
)

POLICY_AUTHORITY = {
    "directive": (
        "2026-09-12 사용자 지시: 코레일은 개발 요구·도면·평면·매뉴얼·자료를 제공하지 "
        "않으며 실제 역사 촬영도 수행하지 않는다. 참조는 공개 자료로 취득한다."
    ),
    "restatedIn": [
        "docs/choo-guard-foundation-multiplayer.md:26",
        "docs/choo-guard-voice-delivery-2026-09-13.md:102",
    ],
    "scope": (
        "코레일 제공·현장 촬영·승인 도면을 전제하는 텍스트는 모두 대체되었다. "
        "텍스트는 이력으로 보존하며 현행 의존성이 아니다."
    ),
}

#: 분류별 기본 처분. 여기 없는 분류는 미판정으로 남긴다(fail-closed).
DEFAULT_DISPOSITIONS = {
    "positive_premise": DISPOSITION_SUPERSEDED,
    "negative_exclusion": DISPOSITION_EXCLUDED,
    "historical_statement": DISPOSITION_HISTORICAL,
}

#: 문서별 판정. ``disposition``은 분류 기본값을 덮어쓰고 ``note``는 발견에 실린다.
#: 여기 없는 경로는 기본값으로 떨어지며, 긍정 전제에는 기본 처분을 주지 않는다.
DOCUMENT_RULINGS = {
    "docs/choo-guard-requirements-baseline-v1.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "지시 이전 요구사항 기준선. MAP-01~MAP-06과 O-02/O-03이 코레일 서면 승인과 "
            "현장 촬영을 전제한다. 2026-09-12 지시로 대체되었고 낡은 기준선으로 보존한다."
        ),
    },
    "docs/choo-guard-execution-backlog-v1.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "지시 이전 실행 백로그. M0-02a/M0-02b/M3-01/M3-02와 'KORAIL 회신 후 별도 "
            "백로그' 절이 서면 승인·촬영을 일정에 넣는다. 지시로 대체된 실행 기록이다."
        ),
    },
    "docs/choo-guard-platform-architecture-v4.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "아키텍처 v4가 역할·배포·맵 항목을 코레일 회신으로 미룬다. 2026-09-12 "
            "지시로 대체되었다."
        ),
    },
    "docs/choo-guard-work-unit-graph-v1.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "작업 단위 그래프가 M0-02a -> M0-02b(코레일 승인 -> 촬영 기록) 경로를 "
            "유지한다. 2026-09-12 지시로 대체되었다."
        ),
    },
    "docs/choo-guard-ai-native-pipeline-v1.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "파이프라인 역할에 '승인/촬영 취득' 의무가 남아 있다. 2026-09-12 지시로 "
            "대체되었으며 실행 경계 문구는 영향받지 않는다."
        ),
    },
    "docs/choo-guard-foundation-v1.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "Foundation v1이 승인·촬영 마일스톤(#15, #28~29, #30, #31)을 유지한다. "
            "2026-09-12 지시로 대체되었다."
        ),
    },
    "docs/choo-guard-foundation-handoff.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": "핸드오프 노트가 제한망 반입을 코레일 회신에 위임한다. 2026-09-12 지시로 대체되었다.",
    },
    "docs/choo-guard-fps-foundation-progress.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "진행 노트가 실제 촬영·도면·직원 매뉴얼을 '수신 대기'로 둔다. 2026-09-12 "
            "지시로 대체되었고 현행 차단 요인으로 읽으면 안 된다."
        ),
    },
    "docs/choo-guard-open-world-training.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "'실제 자료 연결 항목' 표가 승인 도면·측량·촬영과 승인 현장 훈련을 재구성 "
            "승격 근거로 나열한다. 지시 이후 그 입력은 도착하지 않으므로 미검증 경계로 남는다."
        ),
    },
    "docs/art/public-station-references.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "'향후 승인 도면·측량 자료를 받으면 실측으로 승격' 경로가 승인 도면 도착을 "
            "전제한다. 지시로 대체: 공개 자료만 존재하므로 값은 synthetic_design으로 "
            "남고 미확인 치수는 '공개 자료로 확정 불가'다."
        ),
    },
    "docs/art/public-facility-plans.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": (
            "비공개 도면을 쓸 수 없는 이유를 이미 기록한 공개 자료 목록이다. 남은 "
            "'공개 사진 추가 필요' 줄은 공개 자료 범위 안이다."
        ),
    },
    "docs/art/facility-source-followup-2026-09-07.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 다운로드 가능한 설계 문서와 그 사용 한계를 기록한다. 제공 전제가 없다.",
    },
    "docs/art/object-reference-audit.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 레퍼런스로 확보한 개체 근거를 감사한다. 코레일 제공을 전제하지 않는다.",
    },
    "docs/korail/development-questionnaire-v1.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "질문지 본문이 '제공받을 수 있습니까', 보안·촬영 조건 확인을 묻는다. 즉 코레일 "
            "제공과 촬영 승인을 전제한 문장이다. 질문지 자체는 공개 산출물로 보존하되 그 "
            "문장들은 2026-09-12 지시로 대체되었다: 코레일은 제공하지 않고 촬영도 없다."
        ),
    },
    "docs/korail/questionnaire-review-v1.json": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "질문지 자체 검토 기록. 확인 질문 목록이며 제공 전제가 아니다.",
    },
    "docs/korail/README.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": (
            "질문지 작성·자체 검토가 코레일 회신이나 촬영·처리 승인이 아님을 명시한다."
        ),
    },
    "scripts/docs/build_korail_questionnaire.py": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": (
            "공개 질문지 문서 생성기. 담는 문장이 development-questionnaire-v1.md와 같은 "
            "제공·촬영 전제이므로 같은 처분을 받는다."
        ),
    },
    "docs/adr/0005-school-pc-unity-workstation.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": (
            "코레일 취급 조건을 자료 등급 가드(PUBLIC_SYNTHETIC)로만 쓴다. 제공 전제가 "
            "아니며, 장소 독점 결정은 이력 범위 안내로 이미 대체되었다."
        ),
    },
    "docs/adr/0006-official-unity-editor-mcp.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "도구 후보의 superseded 표기이며 공개 전제와 무관하다.",
    },
    "docs/context/graphify/source-manifest.json": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 저장소에서 제외하는 제한 자료 등급을 나열한다. 공급 전제가 아니다.",
    },
    "docs/context/project-context.json": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "제한 원본·연락처·도면을 저장하지 않는다는 가드 노드다. 제공 전제가 아니다.",
    },
    "docs/context/accepted-decisions.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "폐기된 결정을 이력으로 구분해 보존하는 결정 로그다.",
    },
    "docs/context/work-units-2026-09-08.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "2026-09-08 기준 작업 단위 스냅샷. 역사 범위 안내로 대체되었다.",
    },
    "docs/context/school-pc-handoff-2026-09-08.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "학교 PC 인계 기록. 장소 제한은 폐기되었고 이력으로 보존한다.",
    },
    "docs/context/team01-local-checkout-2026-09-08.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "2026-09-08 로컬 체크아웃 검증 스냅샷이다.",
    },
    "docs/choo-guard-school-pc-bootstrap-v1.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "학교 PC 부트스트랩 기록. 장소 제한 폐기로 이력이 되었다.",
    },
    "docs/choo-guard-pi-agentic-dev-spec-v1.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "Pi 개발 spec. 역사 범위 안내가 붙은 이력 문서다.",
    },
    "docs/reviews/2026-09-06-review-dispositions.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "2026-09-06 리뷰 처분 기록. 날짜 박힌 스냅샷이다.",
    },
    "docs/reviews/2026-09-06-r01-r03-review-dispositions.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "2026-09-06 R-01/R-03 리뷰 처분 기록. 날짜 박힌 스냅샷이다.",
    },
    "docs/reviews/2026-09-06-r01-consistency-record.md": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "2026-09-06 정합 기록. 날짜 박힌 스냅샷이다.",
    },
    "docs/evidence/foundation/2026-09-07-publication-review.json": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "2026-09-07 게시 검토 증거 스냅샷이다.",
    },
    "docs/team/TEAM-04/acceptance-matrix.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": "수용 매트릭스가 승인 촬영 자료를 수용 근거로 참조한다. 2026-09-12 지시로 대체되었다.",
    },
    "docs/team/TEAM-25/mapping-template.md": {
        "disposition": DISPOSITION_SUPERSEDED,
        "note": "매핑 템플릿이 승인 도면·촬영 기반 승격 절차를 담는다. 2026-09-12 지시로 대체되었다.",
    },
    "docs/reconstruction/engine-selection.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 자료 기반 재구성 엔진 선택 기록이다.",
    },
    "docs/proposals/kkosso-20260914/M3-01/README.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 근거 범위 안의 M3-01 제안서다.",
    },
    "docs/choo-guard-voice-delivery-2026-09-13.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "2026-09-12 지시를 재확인하는 현행 음성 전달 문서다.",
    },
    "docs/choo-guard-foundation-multiplayer.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "2026-09-12 지시를 원문으로 재진술하는 현행 기준 문서다.",
    },
    "docs/context/work-units-2026-09-08.md#source-availability": {
        "disposition": DISPOSITION_HISTORICAL,
        "note": "폐기 전 가용성 기록이다.",
    },
    "docs/research/2026-09-06-agentic-toolchain-research.md": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "도구 체인 조사 기록. LLM 키·비용 한계를 명시하며 제공 전제가 아니다.",
    },
    "scripts/foundation/validate.py": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "검증기가 자료 등급 가드를 구현한다. 제공 전제가 아니다.",
    },
    "scripts/art/station_equipment.py": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 자료 기반 설비 목록 빌더다.",
    },
    "scripts/team/M1-05/record_pipeline.py": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "공개 소스 처리 파이프라인 기록기다.",
    },
    "scripts/bootstrap/verify_toolchain.py": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "툴체인 검증기. 도구 부재를 실패로 처리하는 가드다.",
    },
    "scripts/bootstrap/test_verify_toolchain.py": {
        "disposition": DISPOSITION_ALIGNED,
        "note": "툴체인 검증기 시험. 도구 부재 가드를 검증한다.",
    },
    "docs/context/work-orders/policy.json": {
        "disposition": DISPOSITION_GENERATED,
        "note": "작업지시 생성기의 정책 문구다. 생성물을 다시 만들면 사라진다.",
    },
    "docs/context/work-orders/index.jsonld": {
        "disposition": DISPOSITION_GENERATED,
        "note": "작업지시 번들에서 생성된 색인이다. 생성물을 다시 만들면 사라진다.",
    },
    "docs/context/work-graph.json": {
        "disposition": DISPOSITION_GENERATED,
        "note": (
            "context graph 생성물. 노드 정의·인용이 원본 문서 문장을 복사한다. 원본 "
            "문서의 판정을 따르며 생성물을 다시 만들면 함께 갱신된다."
        ),
    },
    "docs/context/work-graph-index.md": {
        "disposition": DISPOSITION_GENERATED,
        "note": "work graph 생성 색인이다.",
    },
    "docs/context/graphify/graph.json": {
        "disposition": DISPOSITION_GENERATED,
        "note": "graphify 생성 그래프다. 원본 문서 인용을 담는다.",
    },
    "docs/context/index.html": {
        "disposition": DISPOSITION_GENERATED,
        "note": "context view 생성 HTML이다.",
    },
}

#: 기본 노트. 문서 판정이 없는 경로에 적용된다.
DEFAULT_NOTES = {
    "negative_exclusion": "현행 정책과 이미 일치한다: 전제가 성립하지 않음을 명시한다.",
    "historical_statement": "날짜가 박힌 스냅샷으로 보존한다. 현행 의존성이 아니다.",
    "positive_premise": (
        "2026-09-12 지시로 대체되었다: 코레일은 제공하지 않고 현장 촬영도 없다. "
        "이 텍스트는 이력이며 현행 의존성이 아니다."
    ),
}

#: 생성물·파생 사본. 여기만 가리키는 긍정 전제는 생성물 처분을 받는다.
GENERATED_PATHS = frozenset(
    {
        "docs/context/work-graph.json",
        "docs/context/work-graph-index.md",
        "docs/context/graphify/graph.json",
        "docs/context/project-context.json",
        "docs/context/index.html",
        "docs/context/work-orders/index.jsonld",
        "docs/context/work-orders/policy.json",
    }
)

#: 생성물 디렉터리 접두어. 그 아래 모든 파일은 생성물로 본다. 빌더가 작업지시서
#: 파일을 계속 늘리므로 파일 이름을 하나씩 나열하면 새 작업지시서마다 판정이
#: 비어 미판정이 생긴다. 접두어로 묶어 그 재발을 막는다.
GENERATED_PREFIXES = ("docs/context/work-orders/",)


def is_generated_path(path: str) -> bool:
    """``path``가 생성물이거나 생성물 디렉터리 아래에 있는가."""
    return path in GENERATED_PATHS or path.startswith(GENERATED_PREFIXES)


# --------------------------------------------------------------------------- #
# 스캔
# --------------------------------------------------------------------------- #


def iter_text_files(root: Path):
    """``root``(파일 또는 디렉터리) 아래의 스캔 가능한 텍스트 파일을 낸다."""
    root = Path(root)
    if root.is_file():
        yield root
        return
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        if path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        yield path


def document_is_historical(text: str) -> bool:
    """문서가 스스로 날짜 박힌 이력 스냅샷이라 선언하면 True."""
    head = "\n".join(text.splitlines()[:DOCUMENT_HISTORICAL_SCAN_LINES])
    return bool(DOCUMENT_HISTORICAL_MARKERS.search(head))


def statement_window(line: str, start: int, end: int) -> str:
    """``[start, end)`` 매치를 담은 문장 구간을 돌려준다."""
    left = 0
    for char in _BOUNDARY_CHARS:
        index = line.rfind(char, 0, start)
        if index != -1:
            left = max(left, index + 1)
    right = len(line)
    for char in _BOUNDARY_CHARS:
        index = line.find(char, end)
        if index != -1:
            right = min(right, index)
    return line[left:right]


def canonical_statement(window: str) -> str:
    """다른 파일의 사본이 한 발견으로 접히도록 문장을 정규화한다."""
    text = re.sub(r"\s+", " ", window).strip()
    text = text.strip("\"'`*|>- \t")
    text = re.sub(r'^(?:quote|text|summary|note|label|title|definition)"?\s*:\s*"?', "", text)
    return text.strip("\"'`*| \t")


def is_negated(window: str) -> bool:
    """문장이 전제를 명시적으로 배제하는가."""
    return any(pattern.search(window) for pattern in NEGATION_PATTERNS)


def classify(window: str, document_historical: bool) -> str:
    """문장 하나를 분류한다. 부정이 모든 다른 표지를 이긴다."""
    if is_negated(window):
        return "negative_exclusion"
    if document_historical or HISTORICAL_MARKERS.search(window):
        return "historical_statement"
    return "positive_premise"


def scan_text(text: str, relpath: str, document_historical: bool):
    """문서 하나에서 발생 목록을 낸다."""
    occurrences = []
    seen = set()
    suppressed = DOCUMENT_TOPIC_GUARDS.get(relpath, {}).get("topics", frozenset())
    for lineno, line in enumerate(text.splitlines(), start=1):
        for topic, pattern in TOPIC_PATTERNS.items():
            if topic in suppressed:
                continue
            guard = TOPIC_GUARDS.get(topic)
            for match in pattern.finditer(line):
                window = statement_window(line, match.start(), match.end())
                if guard and guard.search(window):
                    continue
                statement = canonical_statement(window)
                if not statement or NOISE_PATTERN.search(statement):
                    continue
                classification = classify(window, document_historical)
                key = (lineno, topic, classification, statement)
                if key in seen:
                    continue
                seen.add(key)
                occurrences.append(
                    {
                        "path": relpath,
                        "line": lineno,
                        "topic": topic,
                        "classification": classification,
                        "statement": statement,
                        "documentHistorical": document_historical,
                    }
                )
    return occurrences


def scan_root(root: Path, base: Path | None = None):
    """한 루트를 스캔해 ``(발생 목록, 읽은 파일 수)``를 낸다."""
    root = Path(root)
    base = Path(base) if base is not None else root
    if base.is_file():
        base = base.parent
    occurrences = []
    scanned = 0
    for path in iter_text_files(root):
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError):
            continue
        scanned += 1
        relpath = path.relative_to(base).as_posix()
        if relpath in SELF_EXCLUDED:
            continue
        occurrences.extend(scan_text(text, relpath, document_is_historical(text)))
    return occurrences, scanned


def resolve_roots(input_path: Path):
    """``--input``에 대한 스캔 루트 목록과 기준 디렉터리를 정한다.

    ``docs/``와 ``scripts/``를 모두 가진 저장소 루트면 그 둘을 스캔하고,
    그 밖에는 주어진 경로 자체를 스캔한다.
    """
    input_path = Path(input_path)
    if input_path.is_file():
        return [input_path], input_path.parent
    nested = [input_path / name for name in DEFAULT_ROOTS]
    if all(candidate.is_dir() for candidate in nested):
        return nested, input_path
    return [input_path], input_path


# --------------------------------------------------------------------------- #
# 발견 묶기와 처분
# --------------------------------------------------------------------------- #


def finding_id(classification: str, topic: str, statement: str) -> str:
    """내용 주소 기반의 안정적 id. 무관한 삽입에도 id가 유지된다."""
    digest = hashlib.sha1(f"{classification}|{topic}|{statement}".encode("utf-8"))
    return "PP-" + digest.hexdigest()[:10].upper()


def build_findings(occurrences):
    """발생을 (분류, 주제, 문장)으로 묶어 발견 목록을 만든다."""
    grouped = {}
    for occurrence in occurrences:
        key = (
            occurrence["classification"],
            occurrence["topic"],
            occurrence["statement"],
        )
        entry = grouped.get(key)
        if entry is None:
            entry = {
                "id": finding_id(*key),
                "classification": occurrence["classification"],
                "topic": occurrence["topic"],
                "statement": occurrence["statement"],
                "occurrences": [],
            }
            grouped[key] = entry
        location = {"path": occurrence["path"], "line": occurrence["line"]}
        if location not in entry["occurrences"]:
            entry["occurrences"].append(location)
    findings = list(grouped.values())
    for finding in findings:
        finding["occurrences"].sort(key=lambda item: (item["path"], item["line"]))
        finding["occurrenceCount"] = len(finding["occurrences"])
    findings.sort(
        key=lambda item: (item["classification"], item["topic"], item["statement"])
    )
    return findings


def primary_path(finding) -> str:
    """문서 판정을 찾을 때 쓰는 대표 경로. 생성물이 아닌 첫 위치를 쓴다."""
    for occurrence in finding["occurrences"]:
        if not is_generated_path(occurrence["path"]):
            return occurrence["path"]
    return finding["occurrences"][0]["path"]


def all_generated(finding) -> bool:
    """모든 위치가 생성물이면 True."""
    return all(
        is_generated_path(occurrence["path"]) for occurrence in finding["occurrences"]
    )


def disposition_for(finding):
    """발견의 ``(처분, 노트)``를 낸다. 규칙이 없으면 미판정이다."""
    classification = finding.get("classification")
    topic = finding.get("topic")
    if classification not in CLASSIFICATIONS or topic not in TOPIC_PATTERNS:
        return DISPOSITION_UNRESOLVED, "이 발견을 덮는 처분 정책이 없다."

    if classification == "negative_exclusion":
        # 배제는 스스로 판정한다: 전제가 성립하지 않음을 이미 말하고 있다.
        return DISPOSITION_EXCLUDED, DEFAULT_NOTES["negative_exclusion"]

    path = primary_path(finding)
    ruling = DOCUMENT_RULINGS.get(path)

    if classification == "historical_statement":
        disposition = DISPOSITION_HISTORICAL
        note = ruling["note"] if ruling else DEFAULT_NOTES["historical_statement"]
        return disposition, note

    # 남은 것은 긍정 전제다. 생성물만 가리키면 생성물 처분, 아니면 문서 판정이
    # 있어야 한다. 판정 없는 긍정 전제는 미판정으로 남겨 검사를 실패시킨다.
    if all_generated(finding):
        return DISPOSITION_GENERATED, DOCUMENT_RULINGS[path]["note"] if ruling else (
            "생성물 안의 문구다. 같은 전제를 서술하는 원본 문서는 따로 감사한다. "
            "생성물을 다시 만들면 이 문구는 사라진다."
        )
    if ruling is None:
        return DISPOSITION_UNRESOLVED, f"{path}에 대한 문서 판정이 없다."
    return ruling["disposition"], ruling["note"]


def build_registry(occurrences, scanned_files=0, roots=None):
    """R-09 처분 레지스트리 문서를 만든다."""
    findings = build_findings(occurrences)
    for finding in findings:
        disposition, note = disposition_for(finding)
        finding["disposition"] = disposition
        finding["resolved"] = disposition in RESOLVED_DISPOSITIONS
        finding["note"] = note
    counts = {
        "scannedFiles": scanned_files,
        "findings": len(findings),
        "occurrences": sum(finding["occurrenceCount"] for finding in findings),
        "locations": sum(finding["occurrenceCount"] for finding in findings),
        "positivePremise": sum(
            1 for f in findings if f["classification"] == "positive_premise"
        ),
        "negativeExclusion": sum(
            1 for f in findings if f["classification"] == "negative_exclusion"
        ),
        "historicalStatement": sum(
            1 for f in findings if f["classification"] == "historical_statement"
        ),
        "unresolved": sum(1 for f in findings if not f["resolved"]),
    }
    return {
        "schemaVersion": SCHEMA_VERSION,
        "classification": CLASSIFICATION,
        "workId": WORK_ID,
        "issue": ISSUE,
        "generatedBy": "scripts/context/audit_public_premises.py",
        "scanRoots": list(roots or DEFAULT_ROOTS),
        "policy": POLICY_AUTHORITY,
        "classifications": list(CLASSIFICATIONS),
        "dispositions": sorted(RESOLVED_DISPOSITIONS),
        "counts": counts,
        "unresolvedCount": counts["unresolved"],
        "findings": findings,
    }


# --------------------------------------------------------------------------- #
# 스키마 검증
# --------------------------------------------------------------------------- #


def validate_registry(document):
    """처분 레지스트리의 스키마 오류 목록을 낸다(비면 정상)."""
    errors = []
    if not isinstance(document, dict):
        return ["document must be a JSON object"]

    if document.get("schemaVersion") != SCHEMA_VERSION:
        errors.append(f"schemaVersion must be {SCHEMA_VERSION}")
    if document.get("classification") != CLASSIFICATION:
        errors.append(f"classification must be {CLASSIFICATION!r}")
    if document.get("workId") != WORK_ID:
        errors.append(f"workId must be {WORK_ID!r}")
    if document.get("issue") != ISSUE:
        errors.append(f"issue must be {ISSUE!r}")
    if document.get("generatedBy") != "scripts/context/audit_public_premises.py":
        errors.append("generatedBy must name the auditor")

    findings = document.get("findings")
    if not isinstance(findings, list):
        return errors + ["findings must be a list"]

    seen_ids = set()
    unresolved = 0
    for index, finding in enumerate(findings):
        label = f"findings[{index}]"
        if not isinstance(finding, dict):
            errors.append(f"{label} must be an object")
            continue
        identifier = finding.get("id")
        if not isinstance(identifier, str) or not identifier:
            errors.append(f"{label}.id must be a non-empty string")
        elif identifier in seen_ids:
            errors.append(f"{label}.id {identifier!r} is duplicated")
        else:
            seen_ids.add(identifier)

        if finding.get("classification") not in CLASSIFICATIONS:
            errors.append(f"{label}.classification is not recognised")
        if finding.get("topic") not in TOPIC_PATTERNS:
            errors.append(f"{label}.topic is not recognised")
        if not str(finding.get("statement", "")).strip():
            errors.append(f"{label}.statement must be non-empty")

        occurrences = finding.get("occurrences")
        if not isinstance(occurrences, list) or not occurrences:
            errors.append(f"{label}.occurrences must be a non-empty list")
        else:
            if finding.get("occurrenceCount") != len(occurrences):
                errors.append(f"{label}.occurrenceCount does not match occurrences")
            for occurrence in occurrences:
                if not isinstance(occurrence, dict):
                    errors.append(f"{label}.occurrences entry must be an object")
                    continue
                if not isinstance(occurrence.get("path"), str) or not occurrence["path"]:
                    errors.append(f"{label}.occurrences entry path must be a string")
                if not isinstance(occurrence.get("line"), int) or occurrence["line"] < 1:
                    errors.append(f"{label}.occurrences entry line must be a positive int")

        disposition = finding.get("disposition")
        if disposition == DISPOSITION_UNRESOLVED:
            unresolved += 1
            if finding.get("resolved") is not False:
                errors.append(f"{label}.resolved must be false when unresolved")
        elif disposition in RESOLVED_DISPOSITIONS:
            if finding.get("resolved") is not True:
                errors.append(f"{label}.resolved must be true for disposition {disposition!r}")
        else:
            errors.append(f"{label}.disposition {disposition!r} is not recognised")

    if document.get("unresolvedCount") != unresolved:
        errors.append(
            f"unresolvedCount {document.get('unresolvedCount')!r} does not match findings "
            f"({unresolved})"
        )
    counts = document.get("counts")
    if not isinstance(counts, dict):
        errors.append("counts must be an object")
    else:
        if counts.get("findings") != len(findings):
            errors.append("counts.findings does not match the findings list")
        if counts.get("unresolved") != unresolved:
            errors.append("counts.unresolved does not match the findings list")
        total = sum(
            len(f.get("occurrences", [])) for f in findings if isinstance(f, dict)
        )
        if counts.get("occurrences") != total:
            errors.append("counts.occurrences does not match the findings list")
        for key, want in (
            ("positivePremise", "positive_premise"),
            ("negativeExclusion", "negative_exclusion"),
            ("historicalStatement", "historical_statement"),
        ):
            seen = sum(
                1
                for f in findings
                if isinstance(f, dict) and f.get("classification") == want
            )
            if counts.get(key) != seen:
                errors.append(f"counts.{key} does not match the findings list")
    return errors


def load_dispositions(path: Path):
    """처분 레지스트리 JSON을 읽는다."""
    document = json.loads(Path(path).read_text(encoding="utf-8"))
    errors = validate_registry(document)
    if errors:
        raise ValueError("; ".join(errors))
    return document


# --------------------------------------------------------------------------- #
# CLI
# --------------------------------------------------------------------------- #


def parse_args(argv=None):
    parser = argparse.ArgumentParser(
        prog="audit_public_premises.py",
        description="R-09 공개 자료 전제 전수 정합·재발 검사.",
    )
    parser.add_argument(
        "--input",
        default=str(REPO_ROOT),
        help="스캔할 저장소 루트·디렉터리·단일 파일 (기본: 저장소 루트)",
    )
    parser.add_argument(
        "--output",
        default=None,
        help="처분 레지스트리 JSON을 쓸 경로 (기본: stdout, '-'는 stdout)",
    )
    parser.add_argument(
        "--dispositions",
        default=None,
        help=f"레지스트리를 추가로 쓸 경로 (기본 대상: {DEFAULT_DISPOSITIONS_PATH})",
    )
    parser.add_argument(
        "--check",
        default=None,
        help="기존 처분 레지스트리를 스키마 검증만 하고 종료한다",
    )
    parser.add_argument(
        "--fail-on-unresolved",
        action="store_true",
        help="미판정 발견이 있으면 종료 코드 1로 끝낸다",
    )
    parser.add_argument("--quiet", action="store_true", help="stderr 요약 줄을 쓰지 않는다")
    return parser.parse_args(argv)


def run_audit(input_path: Path):
    """``(레지스트리, 스캔 루트 라벨)``을 만든다."""
    roots, base = resolve_roots(input_path)
    occurrences = []
    scanned = 0
    for root in roots:
        found, count = scan_root(root, base)
        occurrences.extend(found)
        scanned += count
    labels = [
        Path(root).name if Path(root).is_dir() else Path(root).as_posix()
        for root in roots
    ]
    return build_registry(occurrences, scanned_files=scanned, roots=labels)


def main(argv=None):
    args = parse_args(argv)

    if args.check:
        check_path = Path(args.check)
        if not check_path.exists():
            print(f"error: --check {check_path} does not exist", file=sys.stderr)
            return 2
        try:
            document = load_dispositions(check_path)
        except (ValueError, json.JSONDecodeError) as error:
            print(f"error: {check_path}: {error}", file=sys.stderr)
            return 2
        if not args.quiet:
            print(
                f"{check_path}: schema ok, {len(document['findings'])} findings, "
                f"unresolved={document['unresolvedCount']}",
                file=sys.stderr,
            )
        if args.fail_on_unresolved and document["unresolvedCount"] > 0:
            return 1
        return 0

    input_path = Path(args.input).resolve()
    if not input_path.exists():
        print(f"error: --input {input_path} does not exist", file=sys.stderr)
        return 2

    registry = run_audit(input_path)

    errors = validate_registry(registry)
    if errors:
        for error in errors:
            print(f"internal schema error: {error}", file=sys.stderr)
        return 2

    payload = json.dumps(registry, ensure_ascii=False, indent=2) + "\n"

    if args.output in (None, "-"):
        sys.stdout.write(payload)
    else:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(payload, encoding="utf-8")

    if args.dispositions:
        target = Path(args.dispositions)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(payload, encoding="utf-8")

    if not args.quiet:
        counts = registry["counts"]
        print(
            "R-09 공개 자료 전제 감사: "
            f"{counts['scannedFiles']}개 파일, {counts['findings']}개 발견 "
            f"(긍정 {counts['positivePremise']}, 배제 {counts['negativeExclusion']}, "
            f"이력 {counts['historicalStatement']}), "
            f"미판정={registry['unresolvedCount']}",
            file=sys.stderr,
        )

    if args.fail_on_unresolved and registry["unresolvedCount"] > 0:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
