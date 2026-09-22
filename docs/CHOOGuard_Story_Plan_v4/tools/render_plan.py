"""Generate the human/agent read views from plan.json. --check detects drift."""
from pathlib import Path
import json,sys
from planlib import ROOT,load,candidate_order,digest

def views(p):
 S={s['id']:s for s in p['stories']};files={}
 def story_md(s):
  lines=[f"# {s['id']} · {s['title']}",'',f"**상위:** {s['parentTaskId']} / {s['epicId']} · **작업창:** {s['window']} · **상태:** NOT_STARTED / NOT_RUN",'',
   f"**Goal:** {s['goal']}",'', '**Architecture:** 새 Unity native PC의 단일 운영 권위·명시적 저장/worker port를 따른다. 이 story는 자기 출력 범위만 만든다.',
   '**Tech Stack:** Unity uGUI/TMP/Input System, C#, JSON contracts와 이 story가 사용하는 저장/worker 도구.',
   f"**Spec:** [{s['parentSpecRef']}](../{s['parentSpecRef']})",'',
   '> 실행 AI는 GLOBAL_CONTRACT.md와 아래 연결 명세를 먼저 읽는다. 파일 경로는 새 저장소의 대상이며 이 패키지에 구현됐다는 뜻이 아니다.',
   '', '## 사용자 또는 후행 작업이 받는 결과',s['outputs'][0]['contract'],'', '## 입력·출력 인터페이스',s['parentContract'],'',
   f"**직접 담당 요구:** {', '.join(s['requirementIds']) or '없음 — 아래 지원 요구를 위한 기반'}",
   f"**지원 요구:** {', '.join(s['supportsRequirementIds']) or '별도 없음'}",
   f"**부모 제품 시험:** {', '.join(s['parentProductTestIds'])}",'', '## 정확 수정 경로',
   '| 작업 | 경로 | 최초 작성 story |','|---|---|---|']
  for w in s['writes']:lines.append(f"| {w['operation']} | `{w['path']}` | {w['createdBy']} |")
  lines += ['',f"**이 story 전용 시험:** `{s['testFile']}`",f"**시험 매체:** {', '.join(s['testKinds'])}",
    'Unity .meta는 같은 경로의 claim에 포함한다. 공통 파일의 추가 수정은 별도 경계 조정 후 반영한다.', '', '## 단계별 선행 산출물',
    '| 생산 story | 산출물 단계 | 소비 단계 | 조건 |','|---|---|---|---|']
  for d in s['requires']:lines.append(f"| {d['producer']} | `{d['artifactId']}@{d['producerStage']}` | {d['consumerStage']} | {d['condition']} |")
  if not s['requires']:lines.append('| 제품 선행 산출물 없음 | — | — | 환경/권한 확인은 별도 |')
  lines += ['', '## 외부 입력과 보류 범위']
  for e in s['externalInputs']:lines.append(f"- `{e['id']}` / {e['requiredAt']} / {e['condition']}: {e['description']} — {e['effect']}")
  if not s['externalInputs']:lines.append('추가 외부 입력이 별도로 선언되지 않았다. 그렇다고 선행 output이 실제 준비됐다는 뜻은 아니다.')
  lines+=['','## 구현 순서','```text']+[f'{n+1}. {a}' for n,a in enumerate(s['behaviorSteps'])]+['```','','## 시험 벡터']
  for a in s['acceptance']:lines += [f"### {a['id']} · {a['kind']} · NOT_RUN",f"Given: {a['given']}",f"When: {a['when']}",f"Then: {a['then']}",'']
  lines+=['## 실행 체크리스트']
  for x in s['workSteps']:lines.append('- [ ] '+x['action'])
  lines+=['','제품 테스트는 이 문서의 Given–When–Then을 코드로 작성한 뒤 해당 runtime에서 실행한다. 아래 명령은 **계획 읽기/검사**만 수행한다.','```sh',f"python tools/plan.py brief {s['id']} --phase candidate --profile fixture",'```',
   '', '## 반려·재분할 조건']+['- '+x for x in s['stopRules']]+['','## 인계 계약',
   '`storyId, phase, profile, planDigest, storyDigest, codeRevision, inputReceiptIds, artifacts(path/hash), raw testResult, evidenceFiles, reviewerId, reviewScope, failure/notRun`를 반환한다. 제출 기록의 스키마는 `schemas/progress.schema.json`이다. 해시만으로 실제 시험과 기관 승인의 진실성을 증명하지 않는다.',
   '', '## 필요한 읽기']
  for ref in s['specRefs']:lines.append(f'- [{ref}](../{ref})')
  for ref in s['sourceRefs']:lines.append(f"- [{ref['sourceId']}]({ref['url']}) · 이전 자료 등록부에서 연결, 이번 원본 재취득/현행성 검증 아님")
  return '\n'.join(lines)+'\n'
 for s in p['stories']:
  files['stories/'+s['id']+'.md']=story_md(s)
  files['stories/'+s['id']+'.json']=json.dumps(s,ensure_ascii=False,indent=2)+'\n'
 table=['# 신규 실행 스토리 목록','',f"11개 에픽 / {len(p['parentGates'])}개 상위 작업 / {len(S)}개 실행 story. 숫자는 제품 진척률이나 일정 추정치가 아니다.",'',
 '| 에픽 | 상위작업 | 실행 story |','|---|---:|---:|']
 for e in p['epics']:table.append(f"| {e['id']} | {len([g for g in p['parentGates'] if g['parentTaskId'].startswith(e['id']+'.')])} | {len([s for s in S.values() if s['epicId']==e['id']])} |")
 for e in p['epics']:
  table += ['',f"## {e['id']} · {e['title']}",'','| Story | 결과 | 작업창 |','|---|---|---|']
  for s in S.values():
   if s['epicId']==e['id']:table.append(f"| [{s['id']}](stories/{s['id']}.md) | {s['title']} | {s['window']} |")
 files['STORIES.md']='\n'.join(table)+'\n'
 hs={i for w in p['windows'] for i in w['storyIds']};order=[i for i in candidate_order(p) if i in hs]
 lines=['# 단기 작업 계획 · 신규 구축 v4','', '**Goal:** 새 Unity에서 두 기관 fixture의 요청을 실제 디스크에 원자 저장하고, 응답을 화면에 표시한 뒤 재시작해도 동일 상태를 복구한다.',
 '**Architecture:** native UI와 순수 C# 운영 코어를 분리하고 SQLite의 durable receipt를 표시한다. 초기화 이전 코드·에픽을 가져오지 않는다.',
 '**Tech Stack:** Unity 6 LTS 후보의 실제 호환 버전 잠금, uGUI/TMP/Input System, C#, SQLite, JSON Schema.',
 '**Spec:** `GLOBAL_CONTRACT.md`, `basis/v3/PRODUCT_BASELINE.md`, 각 story의 명시적 specRefs.','',
 '> 인원·가용시간·납기·실제 처리량이 제공되지 않았다. W0–W3은 수용 산출물 중심의 작업창이며 4일·4주·2주 Sprint 약속이 아니다. 먼저 W0를 착수 대상으로 두고 다음 창은 수용·실측 작업량에 따라 갱신한다.',
 '', '## 변경하지 않은 목표',
 '최종 공조 운영·A/B·두 모드·대본·독립 정량·기관 활용 목표는 그대로다. 이 단기 창은 작지만 실제 저장과 native 조작을 포함한다. 모형 데모 성공을 최종 정확도로 바꾸지 않는다.',
 '', '## 단기 창과 수용점', '| 창 | 이야기 수 | 통과할 결과 |','|---|---:|---|']
 for w in p['windows']:lines.append(f"| {w['id']} · {w['title']} | {len(w['storyIds'])} | {w['exit']} |")
 for w in p['windows']:
  lines+=['',f"## {w['id']} · {w['title']}",w['goal'],'','| 순서 후보 | Story | 검수할 결과 |','|---:|---|---|']
  for i in [i for i in order if S[i]['window']==w['id']]:lines.append(f"| {order.index(i)+1} | [{i}](stories/{i}.md) | {S[i]['title']} |")
  lines+=['','**종료 기준:** '+w['exit']]
 lines+=['','## 병행 후보 — 단기 수용의 숨은 선행이 아님',
 '| Story | 별도 진행할 결과 |','|---|---|']
 for i in p['sideLaneStories']:lines.append(f'| [{i}](stories/{i}.md) | {S[i]["title"]} |')
 lines += ['', '자료 취득은 원본이 없으면 그 story의 입고/현장 수용만 HOLD한다. 실제 사용자 관찰은 동의된 자료가 있어야 하며 합성 인터뷰로 채우지 않는다. 워커 연구는 별도 checkout/process를 쓰며 core/UI의 첫 요청 수용을 막지 않는다.',
  '', '## 순서 결정 원칙',
  '① 첫 작동 구간에 필요한 위험부터(부팅·타입·영속성) → ② 실제 dependency 선행 → ③ 입력 준비 → ④ 공유파일·Editor 경합 제거 → ⑤ 검수 가능한 작은 결과 순으로 진행한다. 중요도가 높아도 선행 artifact를 건너뛰지 않는다.',
  '기본 실행은 단일 작성자/agent, WIP=1이다. 병렬 후보를 2~3개로 늘리려면 입력·checkout·Editor·빌드출력·DB·검수능력이 분리돼야 한다. 문서의 정적 병렬 계산은 실제 잠금이 아니다.',
  '', '## 작업시간을 확인한 뒤 달력 계획을 만든다',
  '실제 story 시작/완료, 능동 구현시간, 리뷰·대기·재작업, 중단·미완료를 따로 기록한다. 첫 수용 결과 뒤 비슷한 story의 관측 범위를 이용해 나머지 forecast를 갱신한다. 임의 고정 속도나 LLM 수로 납기를 계산하지 않는다.',
  '', '## 단기 이후의 순서',
  '1. 보고/지식/업무망/원인 분석을 연결해 두 기관의 요청→수행→회신 전체 운영 흐름.',
  '2. 완전 checkpoint와 불변 A/B, 비교의 native 화면, 동일 코어 두 모드.',
  '3. 근거 AI·조건부 ScriptIR·수동 검수·JSON/Markdown 출력·새 run. 이후 고객 DOCX 템플릿과 수정 회수.',
  '4. 실제 자료가 준비되는 범위에서 첫 철도 공간·보행·위험장·결합·독립 QoI 검증과 사용자 평가.',
  '5. 오프라인 배포·복구·새 현장/기관 확장. 과거 고정 구역 수나 코드 호환은 요구하지 않는다.',
  '', '## 매 작업의 인계',
  'plan/story digest, 실제 새 revision, artifact 경로·hash, 실패·green 로그, 영향 회귀, 입력 receipt, reviewer/scope, 실패·NOT_RUN·현재 blockers를 남긴다. 원래 91개 제품 AT는 leaf 시험으로 대체하지 않는다.',
  '', '## 현재 진행 상태',
  '**계획·온톨로지·검사 도구를 생성한 상태이며 제품 story 109개는 모두 NOT_STARTED다.** 단기 목표가 아직 달성됐다고 표시하지 않는다. 저장소 초기화·원격 이슈·코드·assignee는 변경하지 않았다.']
 files['SHORT_TERM_PLAN.md']='\n'.join(lines)+'\n'
 trac=['# 요구사항→부모→실행 스토리','', '직접 소유 requirement와 지원 requirement를 구별한다. 아래 연결은 계획상 커버리지이며 실행 충족 증거가 아니다.','', '| 요구 | 부모 작업 | 실행 story | 원 제품시험 |','|---|---|---|---|']
 for q in json.loads((ROOT/'contracts/requirement-coverage.json').read_text()):trac.append(f"| {q['requirementId']} | {q['parentTask']} | {', '.join(q['storyIds'])} | {q['acceptanceTestId']} |")
 files['TRACEABILITY.md']='\n'.join(trac)+'\n'
 src=['# 원문과 이전 자료 기록','', '새 방법론 원문만 이번에 확인했다. 기존 제품·에셋 자료는 원문 주소와 이전 상태를 보존했으며 원본 취득·현행성·Unity 임포트를 재실행하지 않았다.','']
 for r in json.loads((ROOT/'contracts/method-sources.json').read_text()):src.append(f"- [{r['id']}]({r['url']}): {r['use']}")
 src+=['','제품 자료: [기술·매뉴얼 48개 기록](basis/v3/reference/sources.json) / [무료 에셋 25개 기록](basis/v3/reference/free-assets.json). 필요한 자료는 story.sourceIds/sourceRefs로 조회한다.']
 files['SOURCES.md']='\n'.join(src)+'\n'
 # Combined spec uses local story docs as the primary readable sources.
 files['IMPLEMENTATION_STORIES.md']='# CHOOGuard 작은 실행 스토리 구현 명세\n\n'+files['SHORT_TERM_PLAN.md']+'\n\n---\n\n'+'\n\n---\n\n'.join(story_md(s).replace('](../','](') for s in p['stories'])
 return files

def main():
 files=views(load());check='--check' in sys.argv;drift=[]
 for name,text in files.items():
  q=ROOT/name
  if check:
   if not q.exists() or q.read_text(encoding='utf8')!=text:drift.append(name)
  else:q.parent.mkdir(parents=True,exist_ok=True);q.write_text(text,encoding='utf8')
 print(json.dumps({'generatedFiles':len(files),'drift':drift,'checkOnly':check},ensure_ascii=False,indent=2));return bool(drift)
if __name__=='__main__':raise SystemExit(main())
