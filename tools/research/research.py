#!/usr/bin/env python
"""choo guard 기술 조사 하네스 실행기.

Pydantic AI Harness의 Researcher 능력과 Exa 검색을 결합해, AI-native 파이프라인 문서
§5 '조사' 단계가 요구하는 필수 산출물(주장, 근거 URL, 조회일, 제한, 상충 자료, 라이선스)을
구조화된 형태로 생성한다.

사용 예:
    uv run research.py "pi-agents 0.21.0 workflow while node semantics" --slug pi-agents-while
    uv run research.py "..." --model openai:gpt-5.6-sol --dry-run

출력:
    docs/research/<YYYY-MM-DD>-<slug>.json  (구조화 레코드)
    docs/research/<YYYY-MM-DD>-<slug>.md    (사람이 읽는 표)

보안 규칙 (AGENTS.md, 파이프라인 v1 §8):
    - 외부 모델·검색 API로 나가는 입력은 PUBLIC_SYNTHETIC 등급의 공개 기술 주제뿐이다.
    - 철도 촬영 자료, 재구성 지오메트리, KORAIL 내부 자료, 개인정보는 입력하지 않는다.
    - 금칙어가 포함된 주제는 실행을 거부한다(아래 FORBIDDEN_TERMS).
"""

from __future__ import annotations

import argparse
import asyncio
import datetime as dt
import json
import os
import re
import sys
from pathlib import Path
from typing import Literal

from pydantic import BaseModel, Field

REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = REPO_ROOT / "docs" / "research"
DEFAULT_MODEL = os.environ.get("RESEARCH_MODEL", "anthropic:claude-sonnet-5")

# 외부 전송 금지 자료를 가리키는 표현. 대소문자 구분 없이 부분 일치로 검사한다.
FORBIDDEN_TERMS = (
    "korail",
    "코레일",
    "역사 촬영",
    "촬영 원본",
    "station capture",
    "point cloud",
    ".ply",
    ".spz",
    ".glb",
    ".ulf",
    "얼굴",
    "차량번호",
    "api_key",
    "secret",
    "password",
)


class Evidence(BaseModel):
    """근거 한 건. URL은 반드시 실제로 읽은 페이지여야 한다."""

    url: str = Field(description="실제로 조회한 근거 URL")
    title: str = Field(description="페이지 제목 또는 문서명")
    accessed_on: str = Field(description="조회일 YYYY-MM-DD")
    excerpt: str = Field(description="주장을 뒷받침하는 인용 또는 요약 (원문 3문장 이내)")
    source_type: Literal["primary", "secondary", "community"] = Field(
        description="primary=공식 문서/저장소/릴리스, secondary=벤더 블로그/논문, community=포럼/블로그"
    )


class Claim(BaseModel):
    """조사 결과의 최소 단위. 파이프라인 v1 §5 '조사' 필수 산출물과 1:1로 대응한다."""

    id: str = Field(description="C-01 형식")
    claim: str = Field(description="검증 가능한 한 문장 주장")
    evidence: list[Evidence] = Field(min_length=1)
    limits: list[str] = Field(default_factory=list, description="주장의 적용 한계, 버전 조건, 미검증 부분")
    conflicts: list[str] = Field(default_factory=list, description="상충하는 자료나 반대 의견과 그 URL")
    license: str | None = Field(default=None, description="관련 소프트웨어·데이터의 라이선스 (SPDX 식별자 권장)")
    confidence: Literal["high", "medium", "low"]


class ResearchRecord(BaseModel):
    topic: str
    accessed_on: str
    model: str
    claims: list[Claim]
    open_questions: list[str] = Field(default_factory=list, description="추가 조사가 필요한 질문")


RESEARCH_INSTRUCTIONS = """\
너는 choo guard 팀의 기술 조사 하네스다. 다음 규칙을 지킨다.
1. 모든 주장은 실제로 읽은 URL로 뒷받침한다. 읽지 않은 URL을 만들어 내지 않는다.
2. 공식 문서, 저장소 소스, 릴리스 노트 같은 1차 자료를 우선한다.
3. 각 주장의 적용 한계(버전, 플랫폼, 미검증 조건)와 상충 자료를 분리해서 적는다.
4. 소프트웨어·모델·데이터의 라이선스를 확인하고 SPDX 식별자로 적는다. 확인 못 하면 null로 둔다.
5. 사실과 추론을 구분한다. 추론은 confidence를 낮춘다.
6. 입력 주제 이외의 조직 내부 정보, 개인 정보, 비공개 자료를 검색어에 넣지 않는다.
결과는 요구된 JSON 스키마로만 반환한다. 본문은 한국어로 쓴다.
"""


def check_topic(topic: str) -> None:
    lowered = topic.lower()
    hits = [term for term in FORBIDDEN_TERMS if term in lowered]
    if hits:
        sys.exit(
            "거부: 외부 전송 금지 표현이 주제에 포함되어 있습니다: "
            + ", ".join(hits)
            + "\n이 하네스에는 PUBLIC_SYNTHETIC 등급의 공개 기술 주제만 입력합니다."
        )


def slugify(text: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return slug[:60] or "topic"


def render_markdown(record: ResearchRecord) -> str:
    lines = [
        f"# 기술 조사 기록: {record.topic}",
        "",
        f"- 조회일: {record.accessed_on}",
        f"- 조사 모델: {record.model}",
        "- 생성기: tools/research/research.py (Pydantic AI Harness Researcher + Exa)",
        "- 등급: PUBLIC_SYNTHETIC 주제만 입력함",
        "",
        "| ID | 주장 | 근거 URL | 조회일 | 제한 | 상충 자료 | 라이선스 | 신뢰도 |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for claim in record.claims:
        urls = "<br>".join(f"[{e.title}]({e.url})" for e in claim.evidence)
        limits = "<br>".join(claim.limits) or "-"
        conflicts = "<br>".join(claim.conflicts) or "-"
        lines.append(
            f"| {claim.id} | {claim.claim} | {urls} | {record.accessed_on} | {limits} | "
            f"{conflicts} | {claim.license or '미확인'} | {claim.confidence} |"
        )
    if record.open_questions:
        lines += ["", "## 추가 조사 질문", ""]
        lines += [f"- {q}" for q in record.open_questions]
    lines.append("")
    return "\n".join(lines)


async def run(topic: str, model: str, num_results: int, deep: bool) -> ResearchRecord:
    from pydantic_ai import Agent
    from pydantic_ai_harness import Researcher
    from pydantic_ai_harness.exa import ExaSearch

    if not os.environ.get("EXA_API_KEY"):
        sys.exit("EXA_API_KEY 가 없습니다. tools/research/.env.example 을 참고하세요.")

    agent = Agent(
        model,
        instructions=RESEARCH_INSTRUCTIONS,
        capabilities=[
            Researcher(),
            ExaSearch(num_results=num_results, include_deep_search=deep),
        ],
        output_type=ResearchRecord,
    )
    today = dt.date.today().isoformat()
    prompt = (
        f"조사 주제: {topic}\n"
        f"오늘 날짜: {today}. 모든 근거의 accessed_on 은 {today} 로 적는다. model 필드는 '{model}' 로 적는다."
    )
    result = await agent.run(prompt)
    return result.output


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("topic", help="공개 기술 주제 (PUBLIC_SYNTHETIC)")
    parser.add_argument("--slug", help="출력 파일 슬러그 (기본: 주제에서 생성)")
    parser.add_argument("--model", default=DEFAULT_MODEL, help="pydantic-ai 모델 문자열 provider:model")
    parser.add_argument("--num-results", type=int, default=5)
    parser.add_argument("--deep", action="store_true", help="Exa deep_search 도구도 노출")
    parser.add_argument("--dry-run", action="store_true", help="LLM 호출 없이 스키마와 프롬프트만 출력")
    args = parser.parse_args()

    check_topic(args.topic)
    slug = args.slug or slugify(args.topic)
    today = dt.date.today().isoformat()

    if args.dry_run:
        print(json.dumps(ResearchRecord.model_json_schema(), ensure_ascii=False, indent=2))
        print(f"\n[dry-run] model={args.model} slug={slug} output={OUTPUT_DIR / f'{today}-{slug}.md'}")
        return

    record = asyncio.run(run(args.topic, args.model, args.num_results, args.deep))
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    json_path = OUTPUT_DIR / f"{today}-{slug}.json"
    md_path = OUTPUT_DIR / f"{today}-{slug}.md"
    json_path.write_text(record.model_dump_json(indent=2), encoding="utf-8")
    md_path.write_text(render_markdown(record), encoding="utf-8")
    print(f"저장: {md_path}\n저장: {json_path}\n주장 {len(record.claims)}건")


if __name__ == "__main__":
    main()
