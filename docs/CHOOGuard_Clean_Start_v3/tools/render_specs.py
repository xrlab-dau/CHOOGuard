"""Deterministic document projections from the reviewed task contract; does not generate a Unity project."""
import json,sys
from pathlib import Path
R=Path(__file__).resolve().parents[1]
def outputs():
 tasks=json.loads((R/'contracts/tasks.json').read_text())['tasks'];epics=json.loads((R/'contracts/epics.json').read_text())['epics'];reqs=json.loads((R/'contracts/requirements.json').read_text())['requirements']
 table='\n'.join(f'| [{e["id"]}](epics/{e["id"]}.md) | {e.get("title",e.get("name",e.get("goal","")))} | {len(e["taskIds"])} |' for e in epics)
 intro=f'''# CHOOGuard 클린 스타트 에픽·구현 명세 v3

**2026-09-19 · 신규 구축 · 검수 개정판 · 제품 실행 NOT_RUN**

빈 저장소에서 만드는 제품이며 초기화 이전 코드·에픽·구역 ID·성공 기록을 요구하지 않는다. 사용자 요구는 유지하고 누락된 기능 단위를 보완해 **{len(epics)}개 에픽·{len(tasks)}개 작업·{len(reqs)}개 제품 요구**로 연결한다.

## 에픽
| 에픽 | 목표 | 작업 수 |
|---|---|---:|
{table}

## 이번에 보완한 경계
입출력은 [타입·API](specs/02-wire-and-ports.md)와 [schemas](schemas/CommandIntent.schema.json), build는 [assembly](specs/01-build-and-assemblies.md), 저장은 [원자 계약](specs/03-durable-operations.md), 분기는 [복원·비교](specs/04-checkpoint-and-comparison.md), 물리는 [worker 계약](specs/05-worker-and-cosimulation.md)를 따른다. 화면은 [네이티브 화면 책임](specs/06-native-surfaces.md)을 따른다.

- CS-OPS.07: 실제 사람 작업량 계측 생산자.
- CS-PLAY.06: checkpoint/분기/A-B 비교의 실제 native presenter·prefab.
- CS-PLAY.07: 동일 코어의 연구용 표·타임라인. 세 번째 사용자 모드가 아니다.

## 시작과 병행
CS-BOOT.01의 첫 smoke는 후행 CompositionRoot를 요구하지 않는다. CS-BOOT.02에서 assembly/typed ports를 고정한 뒤 CS-PACK·CS-OPS·CS-WORLD·CS-PLAY·worker fixture가 필요한 계약을 소비한다. candidate는 명시된 test double로 단위 개발할 수 있다. integration은 선택 profile에서 활성화된 실제 산출물을 소비한다. 현장 qualification은 독립 자료·검수 범위가 필요하다. 전체 에픽 종료를 기다리는 관계로 자동 변환하지 않는다.

첫 완성 구간: 두 기관의 요청→자원 예약→업무→보고/인계→A 보존→B 변경→비교→대본 출력. 크게 보이는 맵이나 그럴듯한 UI만으로 완료하지 않는다.

## 수용 구분
문서 구조 / native 실행 / 사용자 효용 / 정량 모델 / 기관 활용 / 현실 갱신은 각각 [claim gate](contracts/claim-gates.json)로 검토한다. 이 패키지의 Python 검사는 Unity·물리·현장 성능시험이 아니다. 제품 인수시험과 48개 작업의 상태는 NOT_RUN / NOT_IMPLEMENTED다.
'''
 out={'EPICS.md':intro};alltext=[intro]
 for e in epics:
  ts=[t for t in tasks if t['epicId']==e['id']]
  def section(t,prefix):
   hdr=f'''## {t['id']} · {t['title']}

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** {t['artifact']['id']} · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** {t['contract']}

### 필수 상세 명세
'''
   hdr+='\n'.join(f'- [{Path(x).stem}]({prefix}{x})' for x in t['implementationSpecRefs'])+'\n'
   hdr+='\n### 새 구현 경로\n'+'\n'.join('- `'+p+'`' for p in t['plannedFiles'])+'\n'
   hdr+='\n### 선행 산출물과 소비 단계\n'
   hdr+=('\n'.join(f'- `{x["taskId"]}:{x["stage"]}` → `{t["id"]}:{x["consumerStage"]}` / 조건 `{x["condition"]}`.' for x in t['dependsOn']) or '선행 구현 산출물 없음. 현재 작업의 입력과 안전 경계는 확인한다.')+'\n'
   hdr+='\n### 구현 절차\n'+'\n'.join(f'{i+1}. {a}' for i,a in enumerate(t['algorithm']))+'\n'
   hdr+='\n### 정확한 인수 oracle · 아직 미실행\n'
   for a in t['acceptance']:hdr+=f'\n**{a["id"]}**\n\nGiven: {a["given"]}  \nWhen: {a["when"]}  \nThen: {a["then"]}  \nResult: NOT_RUN\n'
   hdr+='\n### 실제 시험 매체\n'+'\n'.join(f'- {x["kind"]}: `{x["path"]}` · NOT_RUN' for x in t['testTargets'])+'\n'
   hdr+='\n### 연결과 인계\n제품 요구: '+', '.join(t['supportsRequirementIds'])+'.\n'
   hdr+='신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.\n'
   hdr+='\n원문 ID: '+(', '.join(t['sourceIds']) or '사용자 제품 요구/이 문서의 설계 계약')+f'. [출처 등록부]({prefix}reference/sources.json).\n'
   return hdr
  title=e.get('title',e.get('name',e.get('goal','')))
  out[f'epics/{e["id"]}.md']=f'# {e["id"]} · {title}\n\n[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)\n\n'+'\n---\n\n'.join(section(t,'../') for t in ts)
  alltext.append(f'\n# {e["id"]} · {title}\n\n'+'\n---\n\n'.join(section(t,'') for t in ts))
 out['IMPLEMENTATION_SPEC.md']='\n'.join(alltext)
 out['TRACEABILITY.md']='# 신규 요구–생산자–시험 추적\n\n| 요구 | 제목 | 구현 책임 | 제품 시험 | 상태 |\n|---|---|---|---|---|\n'+'\n'.join(f'|{r["id"]}|{r["title"]}|{r["taskId"]}|{r["acceptanceTestId"]}|NOT_RUN|' for r in reqs)+'\n\n참고: 지원 작업은 tasks의 supportsRequirementIds에 별도로 연결된다. 제품 인수시험의 실행은 미수행이다.\n'
 return out

def main():
 out=outputs();check='--check' in sys.argv;bad=[]
 for rel,text in out.items():
  p=R/rel
  if check:
   if not p.exists() or p.read_text()!=text:bad.append(rel)
  else:p.parent.mkdir(exist_ok=True,parents=True);p.write_text(text,encoding='utf-8')
 print(json.dumps({'ok':not bad,'generatedFiles':len(out),'drift':bad},ensure_ascii=False));return int(bool(bad))
if __name__=='__main__':sys.exit(main())
