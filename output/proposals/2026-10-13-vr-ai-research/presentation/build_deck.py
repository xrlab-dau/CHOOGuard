#!/usr/bin/env python3
"""One semantic scene -> standalone HTML + native-editable Figma Slides batches.
No source project, Unity asset, or external resource is modified.
"""
from pathlib import Path
import base64, html, json, math, shutil

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
C = dict(paper='#F3F0E8', ink='#172821', forest='#0E473C', muted='#65746A',
         line='#CCD4C9', amber='#F0B300', sage='#E2E9DD', white='#FFFFFF', dark='#102C25')
SLIDES=[]

def slide(title, tag, note, dark=False, footer=''):
    s=dict(index=len(SLIDES)+1,title=title,tag=tag,notes=note,dark=dark,
           name=f'{len(SLIDES)+1:02d} · {title}',background=C['dark' if dark else 'paper'],nodes=[])
    SLIDES.append(s)
    text(s,96,58,1560,44,tag.upper(),28,'amber' if dark else 'forest',mono=tag.isascii())
    if title: text(s,96,130,1730,110,title,64,'white' if dark else 'ink',bold=True)
    rect(s,96,1004,1728,2,'forest' if dark else 'line')
    text(s,96,1021,1600,38,footer or 'CHOOGuard  /  가상현실 Term Project · AI시스템설계및개발Ⅱ 공통 기반',28,'sage' if dark else 'muted')
    text(s,1754,1021,70,38,f'{s["index"]:02d}',28,'amber' if dark else 'forest',mono=True)
    return s

def add(s,n): s['nodes'].append(n); return n
def rect(s,x,y,w,h,fill='sage',stroke=None,r=0,opacity=1,name='Shape'):
    return add(s,dict(type='rect',x=x,y=y,w=w,h=h,fill=C.get(fill,fill),stroke=C.get(stroke,stroke),radius=r,opacity=opacity,name=name))
def circle(s,x,y,d,fill='forest',stroke=None,name='Marker'):
    return add(s,dict(type='ellipse',x=x,y=y,w=d,h=d,fill=C.get(fill,fill),stroke=C.get(stroke,stroke),name=name))
def text(s,x,y,w,h,value,size=34,color='ink',bold=False,mono=False,name='Text',url=None,align='left'):
    return add(s,dict(type='text',x=x,y=y,w=w,h=h,text=value,size=size,color=C.get(color,color),bold=bold,mono=mono,name=name,url=url,align=align))
def line(s,x1,y1,x2,y2,color='line',width=4,dashed=False):
    return add(s,dict(type='line',x=x1,y=y1,x2=x2,y2=y2,color=C.get(color,color),width=width,dashed=dashed,name='Route'))
def arrow(s,x,y,w=75,color='forest'):
    text(s,x,y,w,62,'→',46,color,mono=True,name='Flow arrow')
def block(s,x,y,w,heading,body,color='ink',bodycolor='muted',h=185,hs=38,bs=32):
    return add(s,dict(type='block',x=x,y=y,w=w,h=h,heading=heading,body=body,
                      headingSize=hs,bodySize=bs,color=C[color],bodycolor=C[bodycolor],gap=16,name=heading))
def pill(s,x,y,w,label,fill='forest',color='white'):
    rect(s,x,y,w,52,fill,r=26);text(s,x+22,y+7,w-44,40,label,28,color,bold=True)
def photo(s,x,y,w,h,asset,label):
    add(s,dict(type='image',x=x,y=y,w=w,h=h,asset=asset,name='PHOTO · '+asset))
    if label:text(s,x,y+h+18,w,78,label,28,'muted',name='Development image caption')
def stage(s,x,y,w,title,sub,fill='sage'):
    rect(s,x,y,w,172,fill,r=8)
    text(s,x+25,y+24,w-50,58,title,38,'ink',bold=True)
    text(s,x+25,y+90,w-50,82,sub,30,'muted')

# 01 — statement + genuine current development view.
s=slide('', 'VR × AI SYSTEMS  /  PROJECT PROPOSAL',
'CHOOGuard는 부산역을 배경으로 역무 직원의 대응을 경험하는 가상현실 게임 제안입니다. 오른쪽은 현재 구축 중인 1층 공간의 실제 Unity 개발 화면입니다. 이 공간을 완성됐다고 제시하는 것이 아니라, 확인한 현장 자료와 생성한 후보를 구분하며 플레이 가능한 공간으로 발전시키려 합니다. AI 시스템 과제에서 만든 공간과 NPC·상황 기반을 공유하고, 이번 가상현실 과제에서는 고개를 돌려 단서를 찾고 손으로 선택하고 행동하는 경험을 구현합니다. 오늘은 공간을 만드는 과정과 재현 가능한 상황, 그리고 VR에서의 완결된 플레이를 연결해 설명하겠습니다.',True)
photo(s,845,116,1075,824,'escalator.png','')
text(s,96,225,750,150,'CHOOGuard',106,'white',bold=True)
text(s,102,398,685,250,'현장 자료에서\n체험 가능한\n철도역 대응 게임으로',62,'white',bold=True)
line(s,100,740,960,740,'amber',8);circle(s,932,726,28,'amber')
text(s,102,802,660,145,'관측 근거를 잇는 디지털 공간\nAI 판단 · 시드 기반 사건 · XR 행동',33,'sage')
text(s,1050,956,770,40,'부산역 1F 재구성 개발 화면 · 2026-10-12',28,'sage')

# 02 — course fit, purpose and one shared foundation.
s=slide('하나의 프로젝트, 두 수업의 기여','01  제작 취지',
'출발점은 사용자가 설명한 코레일 문제정의의 맥락입니다. 철도역에서는 무엇이 일어났는지만큼 어디서 보았는지, 어떤 통로가 열렸는지, 승객에게 무엇을 전달했는지가 대응을 바꿉니다. 이를 공간 안에서 반복 경험하는 게임으로 다룹니다. 가운데 공통 기반은 부산역 공간, NPC, 사건과 기록입니다. 왼쪽 AI 시스템 과제는 공간 제작과 판단·시뮬레이션을, 오른쪽 VR 과제는 머리와 손의 입력을 실제 행동과 피드백으로 연결하는 데 기여합니다. 같은 프로젝트를 서로 다른 관점으로 검증하는 구조입니다. 기관의 공식 승인이나 훈련 효과를 이미 확보했다는 의미는 아닙니다.')
text(s,96,282,1710,105,'공간을 알아보고, 정보를 전하고, 주변 흐름의 변화를 경험한다.',42,'forest',bold=True)
rect(s,310,440,1300,150,'forest',r=8)
text(s,350,469,1220,65,'공통 기반  ·  부산역 공간 + NPC + 사건 디렉터 + 실행 기록',42,'white',bold=True)
line(s,730,590,730,660,'forest',5);line(s,1200,590,1200,660,'forest',5)
block(s,250,700,650,'AI시스템설계및개발Ⅱ','자료 정합 · 자산 제작\n판단 모델과 재현 가능한 실행',h=185)
block(s,1080,700,650,'가상현실 Term Project','머리·양손 입력 · 공간 UI\n직접 행동과 몰입형 피드백',color='forest',h=185)
text(s,96,934,1720,42,'KORAIL 적용 맥락: 사용자 제공 · 공식 문제정의서 원문과 기관 도입 요건은 별도 확인',28,'muted')

# 03 — proposed game loop with branching action.
s=slide('같은 사건에서도 대응의 선택이 달라진다','02  플레이 경험 · 제안',
'플레이는 평상시 순찰에서 시작합니다. 주변을 둘러보다 평소와 다른 소리나 행동을 발견하면 단서를 확인하고 보고합니다. 이후 안내나 통제를 선택하고, 전문기관에 정보를 인계한 뒤 자신의 대응을 돌아봅니다. 아래의 분기는 그 과정에서 생기는 선택의 예입니다. 현장을 먼저 확인할지, 주변 승객에게 통로를 안내할지는 확보한 정보와 현재 혼잡에 따라 달라집니다. 정답 버튼을 빠르게 누르는 경험보다, 무엇을 보고 왜 그 행동을 했는지 이해할 수 있는 피드백을 목표로 합니다. 사건이 끝나면 같은 조건에서 다른 대응을 다시 시도할 수 있도록 연결합니다.')
steps=['관찰','단서 확인','보고','안내·통제','인계','회고']
for i,t in enumerate(steps):
    x=98+i*291
    circle(s,x+78,380,60,'forest' if i!=3 else 'amber')
    if i<5:line(s,x+138,410,x+369,410,'forest',5)
    text(s,x,472,225,70,t,39,'ink',bold=True)
    text(s,x+90,392,42,42,f'{i+1}',28,'white' if i!=3 else 'ink',mono=True)
line(s,1060,548,1060,614,'forest',4);line(s,650,614,1450,614,'forest',4)
line(s,650,614,650,660,'forest',4);line(s,1450,614,1450,660,'forest',4)
block(s,370,684,550,'현장을 먼저 확인','관측 정보를 보강하고\n다음 행동의 우선순위를 정한다',h=180)
block(s,1170,684,570,'승객을 먼저 안내','열린 통로와 이동 흐름을 보고\n안내의 방향을 선택한다',h=180)
pill(s,96,921,690,'핵심 재미  ·  제한된 시야 + 선택의 결과 + 재도전')

# 04 — requirements and environment are explicitly visible.
s=slide('재사용할 기반과 새로 구현할 요구','03  개발 요구사항 및 개발 환경',
'현재 코드에서 재사용 가능한 것은 Unity의 1인칭 공간, 장비 상태, NPC와 사건 판단, 실행 로그입니다. 현재 설정은 Unity 6000.3.23f1과 URP, Input System입니다. 반면 XR 상호작용 도구와 OpenXR 제공자 설정, 머리와 양손의 추적 입력은 이번 구현과 검증의 대상입니다. 수업의 최소 완결 단위는 검토된 한 구역과 대표 사건 하나에서 관찰부터 결과 확인까지 이어지는 플레이입니다. 우선 시뮬레이터에서 입력과 행동을 확인하고, 지원 장치를 확정한 뒤 실제 HMD 검증을 분리해 기록합니다. 도구를 설치했다는 사실과 장치에서 경험이 잘 작동했다는 사실은 구별하겠습니다.')
text(s,96,286,790,52,'현재 기반 · 코드와 설정 확인',36,'forest',bold=True)
text(s,1035,286,790,52,'추가 구현 · VR 과목의 산출물',36,'forest',bold=True)
line(s,96,357,1824,357,'line',3);line(s,958,286,958,906,'line',2)
rows=[('Unity 6000.3.23f1','XR Origin · 머리와 양손 포즈'),('URP 17.3.0 · Input System 1.20.0','Direct / Grab · Ray / Select'),('공간 · 장비 · NPC · 사건 · JSON 기록','이동 옵션 · 공간 UI · 입력 기록')]
for i,(a,b) in enumerate(rows):
    y=397+i*130
    text(s,96,y,790,82,a,36,'ink');text(s,1035,y,790,82,b,36,'ink')
    line(s,96,y+100,1824,y+100,'line',2)
text(s,96,826,1710,97,'완료 단위: 한 구역 · 대표 사건 1개 · 시작부터 회고까지 이어지는 VR 플레이\n검증 경로: XR 입력 시뮬레이터 → 지원 HMD 실행·가독성·성능 확인',34,'forest',bold=True)

# 05 — complete pipeline with distinct synthetic feedback route.
s=slide('관측 근거를 보존하며 자산과 플레이를 만든다','04  기술적 특징 · 전체 파이프라인',
'전체 제작 흐름을 한 장으로 보면 왼쪽의 현장 자료에서 시작해 오른쪽의 플레이 경험으로 이어집니다. 사진과 영상을 모으고 카메라와 좌표를 정합한 뒤, 자료의 조건에 따라 두 제작 경로로 나눕니다. 다중뷰가 충분하면 표면 복원을, 사진이 부족한 객체는 Hunyuan3D 생성 후보를 검토합니다. 노란 경로는 부족한 시점의 보완입니다. 이 보완은 관측 자료와 별도로 표시하고 미사용 실제 사진으로 효과를 확인합니다. 두 경로의 자산은 Blender의 후처리와 Unity 배치로 합쳐지고, 충돌과 이동을 검토한 다음 NPC·사건·XR 경험에 연결됩니다. 논문과 도구는 선택의 근거이며 이 프로젝트에서의 검증은 별도 단계입니다.')
stage(s,96,450,285,'현장 자료','사진·영상·도면')
arrow(s,392,502,60)
stage(s,455,450,290,'카메라·좌표','SfM · VGGT')
line(s,745,536,815,536,'forest',4);line(s,815,365,815,710,'forest',4)
line(s,815,365,858,365,'forest',4);line(s,815,710,858,710,'forest',4)
stage(s,865,282,402,'다중뷰 표면 복원','NeRF / NeuS / 2DGS 비교')
stage(s,865,624,402,'객체 생성 후보','Hunyuan3D')
line(s,1267,367,1310,367,'forest',4);line(s,1267,710,1310,710,'forest',4)
line(s,1310,367,1310,710,'forest',4);line(s,1310,536,1350,536,'forest',4)
stage(s,1360,450,210,'후처리','Blender',fill='sage');arrow(s,1576,502,50)
stage(s,1630,450,194,'배치','Unity·XR',fill='sage')
pill(s,455,278,290,'선택적 시점 보강','amber','ink')
line(s,600,330,600,433,'amber',5,True)
text(s,96,855,1725,85,'관측 기반 3DGS 렌더와 생성 pseudo-view를 구분 → 실제 미사용 뷰로 재평가\n공통 검토: 사진·치수 일치 / 단위·방향 / 충돌 / 통행 / 실행 비용',32,'forest',bold=True)

# 06 — field capture manifest and camera coverage.
s=slide('부족한 각도는 후보로 보완하고 실제 뷰로 확인한다','05  수집 · 정합 · 3DGS 시점 보강',
'웹 자료는 수량만 늘리는 것으로 충분하지 않습니다. 장소와 층, 게시 시점, 영상의 프레임 위치와 사용 조건을 남기고, 서로 겹치는 관측을 선별해야 합니다. 왼쪽은 자료가 어떤 면을 보았는지 기록하는 개념도입니다. 초록은 실제 관측이고 노랑은 보완할 시점입니다. 3DGS는 학습한 장면을 다른 시점에서 렌더링하지만, 같은 모델에서 이미지를 더 뽑았다고 현장 정보가 새로 생기는 것은 아닙니다. GS-GS와 GIFSplat 같은 최근 연구는 생성 선험으로 희소뷰를 보완하는 선택지를 제시합니다. 우리는 실제 자료만 사용한 결과, 렌더를 추가한 결과, 생성 보강한 결과를 비교하고 학습에 쓰지 않은 실제 사진으로 판단합니다.')
rect(s,96,296,750,590,'sage',r=8)
rect(s,355,450,240,225,'paper','forest',r=8)
text(s,391,521,180,100,'현장\n대상',39,'forest',bold=True)
cams=[(190,390),(180,720),(680,385),(690,725)]
for x,y in cams:
    circle(s,x,y,58,'forest');line(s,x+29,y+29,475,563,'forest',3)
circle(s,455,320,60,'amber');line(s,485,380,475,450,'amber',4,True)
text(s,132,825,650,46,'초록: 실제 관측  /  노랑: 시점 보완 후보',28,'forest')
block(s,937,296,850,'① 수집·카메라 정합','URL · 시점 · 층 · 프레임 · 사용 조건\nCOLMAP 기준선 / VGGT 초기화 비교',h=160)
block(s,937,496,850,'② 시점의 출처 구분','실제 관측 / 3DGS 렌더 / 생성 pseudo-view\n[GS-GS · CVPR 2025]  [GIFSplat · 2026]',h=160)
block(s,937,696,850,'③ 독립 자료로 재평가','미사용 실제 사진 + 독립 치수·기준점\n영상·촬영 구간 단위로 학습·평가 분리',h=160)
text(s,96,926,1720,48,'새 시점 합성의 시각 품질과 실제 형상 정확도는 각각 평가한다.',34,'forest',bold=True)

# 07 — paired fabrication routes.
s=slide('입력 조건에 따라 두 제작 경로를 비교한다','06  NeRF 계열 표면 복원 × Tencent Hunyuan3D',
'첫 경로는 같은 대상이 겹쳐 보이는 다중뷰가 확보된 경우입니다. NeRF는 새로운 시점을 표현하는 방사장이고, 표면이 필요하면 NeuS나 2DGS 같은 방법을 함께 비교합니다. mesh를 추출했다고 바로 게임 자산이 되지는 않으므로 구멍, 부유 형상과 치수를 확인합니다. 두 번째 경로는 사진이 희소한 객체의 생성입니다. Tencent Hunyuan3D 2.1은 형상과 PBR 재질 제작을 나누며, Omni는 점군이나 경계 상자 같은 제약의 활용을 제안합니다. 생성한 뒷면과 연결부는 사진과 도면으로 다시 검토해야 합니다. 두 결과는 같은 기준으로 비교합니다. 얼마나 닮았는지와 함께 수동 보정 시간, 자산 크기와 실행 비용을 기록합니다.')
text(s,96,286,1640,62,'A  겹치는 다중뷰가 충분한 대상',39,'forest',bold=True)
stage(s,96,382,405,'정합된 실제 다중뷰','관측 기하 + 카메라')
arrow(s,523,429,80);stage(s,625,382,555,'방사장·표면 복원','NeRF / NeuS / 2DGS')
arrow(s,1200,429,80);stage(s,1305,382,519,'검토할 표면 자산','mesh 추출 + 형상 검토')
text(s,96,618,1640,62,'B  객체 사진이 희소한 대상',39,'forest',bold=True)
stage(s,96,708,405,'사진·형상 제약','이미지 / point cloud / bbox')
arrow(s,523,755,80);stage(s,625,708,555,'형상 + PBR 생성','Hunyuan3D 2.1 / Omni')
arrow(s,1200,755,80);stage(s,1305,708,519,'검토할 생성 자산','뒷면·접합부의 실물 대조')
text(s,96,938,1710,44,'동일 기준: 사진·치수 일치  /  수동 보정 시간  /  자산 크기  /  실행 비용',30,'muted')

# 08 — visual/collision/navigation layers + actual development view.
s=slide('외형, 충돌, 이동을 각각 완성한다','07  Blender 후처리 → Unity 배치',
'Blender와 Unity 구간에서는 보이는 결과와 실제 플레이의 조건을 분리합니다. 가장 위의 시각 표면은 형상과 재질, UV와 normal을 정리한 결과입니다. 아래의 충돌 형상은 손이나 몸이 벽과 문을 통과하지 않도록 하는 별도 구조이고, 이동 그래프는 NPC가 어디로 갈 수 있는지를 나타냅니다. 표면이 그럴듯해도 충돌이 틀리거나 경로가 끊기면 대응 게임은 성립하지 않습니다. 오른쪽은 현재 1층 유리 전면의 실제 개발 화면입니다. 이 자료는 외형과 배치의 중간 상태를 보여줍니다. 이후 문·계단·통로의 연결을 시험하고 단위, pivot, LOD를 검토해 공간을 실행 가능한 조건으로 바꾸겠습니다.')
for y,w,x,c,title,body in [(328,650,96,'forest','시각 표면','topology · UV · normal · PBR'),(510,650,151,'sage','충돌 형상','몸·손·장비의 접촉과 통과'),(692,650,206,'amber','이동 그래프','NPC 경로 · 문 · 계단 · 통로')]:
    rect(s,x,y,w,152,c,r=8)
    text(s,x+30,y+22,w-60,55,title,39,'white' if c=='forest' else 'ink',bold=True)
    text(s,x+30,y+89,w-60,50,body,30,'sage' if c=='forest' else 'ink')
photo(s,950,310,874,546,'glazing.png','부산역 1F 유리 전면 개발 화면 · 2026-10-12\n현재 배치·외형의 중간 결과 · 경로 검증과 별개')
text(s,96,933,1650,50,'공통 자산 검토: 단위 · 방향 · pivot · LOD · 충돌 · 독립 기준점 배치',32,'forest',bold=True)

# 09 — embodied interaction.
s=slide('VR에서는 공간을 보고 손으로 행동한다','08  XR 상호작용 · 구현 제안',
'가상현실의 기여는 기존 화면을 헤드셋에 띄우는 데서 끝나지 않습니다. 고개를 돌려 보아야 알 수 있는 단서, 손으로 가리킨 대상, 몸이 있는 방향과 안내의 방향이 게임의 판단과 연결되어야 합니다. 가운데 도식은 머리와 두 손의 입력이 하나의 플레이어 상태로 들어오는 구조입니다. 가까운 무전기나 장비는 직접 잡고, 떨어진 대상이나 공간 UI는 가리켜 선택하며, 이동은 텔레포트와 회전 옵션부터 검토합니다. 현재 데스크톱 카메라의 제어와 추적 카메라의 소유권을 분리하고, 버튼 입력이 두 번 처리되지 않는지 확인합니다. 시뮬레이터의 입력 검증과 실제 헤드셋의 성능·불편감 검증은 각각 남깁니다.')
circle(s,813,332,240,'forest');text(s,849,407,175,90,'HEAD\n시야·단서',34,'white',bold=True)
circle(s,624,655,155,'sage','forest');text(s,652,698,116,60,'LEFT',28,'forest',mono=True)
circle(s,1094,655,155,'sage','forest');text(s,1116,698,121,60,'RIGHT',28,'forest',mono=True)
line(s,700,655,860,562,'forest',4);line(s,1170,655,1010,562,'forest',4)
block(s,96,322,490,'잡기 · Direct / Grab','무전기·장비의 집기와 놓기\n손의 움직임을 작업에 연결',h=170)
block(s,1314,322,510,'선택 · Ray / Select','안내 대상·표지·공간 UI\n가리키기와 확인 피드백',h=170)
block(s,96,684,490,'이동 · Locomotion','텔레포트 · 회전 옵션\n경계·충돌·불편감 확인',h=170)
block(s,1314,684,510,'확인 · Spatial UI','사건 정보·무전·회고\n거리·크기·한국어 가독성',h=170)
text(s,689,900,600,52,'공통 시뮬레이션 상태와 로그',32,'forest',bold=True)

# 10 — decision + motion stack and map.
s=slide('NPC는 관측을 판단하고 경로를 실행한다','09  NPC 이동과 판단의 분리',
'NPC 시스템은 세 층으로 설명합니다. 위험 단서와 방송, 직원 지시, 통행 상태가 바뀌면 관측이 갱신됩니다. 판단 계층은 허용된 행동 후보 중에서 선택하고, 실행 계층은 Unity의 상태 규칙과 경로를 통해 실제 이동과 행동을 수행합니다. 아래의 지도는 전역 경로와 주변 회피가 다른 문제임을 보여주는 개념도입니다. NavMesh와 통행 조건이 목적지까지의 길을 정한다면 ORCA나 social force는 사람과 장애물 주변의 국소 움직임을 비교하는 기준선입니다. 이런 모델이 실제 대피 심리까지 보장하는 것은 아닙니다. 속도와 반응 지연, 경로 선택의 가정을 남기고 자료가 확보되면 별도로 보정하겠습니다.')
stage(s,96,305,465,'관측 변화','위험 · 방송 · 지시 · 통행')
arrow(s,590,350,80);stage(s,707,305,475,'제약된 행동 판단','규칙 / JEV 비교')
arrow(s,1211,350,80);stage(s,1330,305,494,'경로·상태 실행','NavMesh + 실행 규칙')
rect(s,96,560,1090,344,'sage',r=8)
for x,y,w,h in [(245,632,240,80),(612,755,240,80),(906,610,130,180)]:rect(s,x,y,w,h,'paper','line')
for a,b,c,d in [(150,805,548,805),(548,805,548,685),(548,685,868,685),(868,685,868,610)]:line(s,a,b,c,d,'forest',6)
for x,y in [(160,777),(333,771),(518,755),(580,662),(743,654),(838,614)]:circle(s,x,y,34,'forest')
circle(s,536,646,36,'amber')
block(s,1280,570,545,'전역 경로','목적지 · 열린 통로 · 연결성',h=130)
block(s,1280,739,545,'국소 회피','주변 사람·장애물의 간격\nORCA / social force 비교 후보',h=160)
text(s,96,936,1700,44,'이동 모델의 성능과 실제 군중·대피 행동의 타당성은 별개로 측정한다.',30,'muted')

# 11 — seeded simulator's replay contract.
s=slide('시드는 같은 출발점을 만들고 기록은 재생을 만든다','10  시드 기반 사건 시뮬레이터',
'시드 기반이라는 말은 같은 숫자만 넣으면 모든 결과가 같아진다는 약속이 아닙니다. 사용자의 행동과 외부 모델 응답, 비동기 처리 순서가 실행 결과를 바꿀 수 있습니다. 위쪽 두 줄은 같은 사건 조건에서 다른 대응을 비교하는 예입니다. 초기 위치와 시점은 같지만 사용자가 행동을 바꾸면 NPC와 사건의 전개는 달라져야 합니다. 이를 비교하려면 아래의 실행 묶음이 필요합니다. 시드와 공간 버전, 초기 상태, 입력, 실제 모델 응답과 사건 순서를 함께 기록합니다. 같은 기록을 재생할 때 상태 체크포인트와 이동 궤적을 확인하고 처음 달라진 지점을 찾겠습니다. 현재 시드와 로그 기반을 완전한 재생 계약으로 확장하는 제안입니다.')
text(s,96,286,540,90,'SEED 1042',62,'forest',bold=True,mono=True)
text(s,718,308,1098,68,'같은 초기 사건 조건 → 다른 대응을 비교',36,'ink',bold=True)
for y,label,end in [(465,'대응 A · 먼저 확인','관측 보강 → 다음 안내'),(650,'대응 B · 먼저 안내','동선 변화 → 현장 확인')]:
    text(s,96,y-48,480,60,label,34,'ink',bold=True)
    line(s,575,y,1760,y,'forest',5)
    for x in [610,990,1380,1730]:circle(s,x,y-20,40,'forest')
    text(s,612,y+42,1170,55,'사건 발생       단서 확인       행동 선택       '+end,30,'muted')
rect(s,96,825,1728,140,'forest',r=8)
text(s,128,850,1664,50,'실행 manifest  =  시드 + 공간·자산 버전 + 초기 상태 + 입력 + 응답 + 사건 순서',30,'white',bold=True)
text(s,128,913,1600,38,'개발 시드와 미공개 평가 시드 분리 · 도달 불가 목표와 모순되는 사건 조합 제거',28,'sage')

# 12 — JEV scope and experiment.
s=slide('JEV의 통합과 판단의 정합성을 구분한다','11  JEV 적합성 · 비교 실험',
'프로젝트의 JEV는 TypeSafe AI의 구조화된 의미 판단 모델이며 컴퓨터비전 복원 모델이 아닙니다. 현재 코드에는 관측에 반응한 NPC 후보 판단과 사건 디렉터 연결이 있고, 과거 내부 기록에서는 Unity와 JEV의 실행 연결을 확인할 수 있습니다. 다만 당시 버전과 현재 latest 별칭은 구별해야 하며, 그 실행이 현재 공간의 행동 정합성을 보장하지는 않습니다. 이번 제안에서는 입력 관측과 행동 후보, 이동 계층을 고정하고 규칙 정책과 JEV를 비교합니다. 후보의 유효성, 관측과의 일치, 오래된 응답의 배제, 작업 완료와 지연·실패 처리를 보겠습니다. 모델이 내린 신뢰도 자체를 외부 정답으로 사용하지 않겠습니다.',True)
for x,label,sub in [(96,'관측 + 행동 후보','허용된 상태·선택지'),(705,'구조화된 판단','JEV / 규칙 비교'),(1314,'검증 후 실행','유효성 · 상태 · 경로')]:
    rect(s,x,343,510,174,'forest',r=8);text(s,x+28,372,454,60,label,39,'white',bold=True)
    text(s,x+28,444,454,55,sub,32,'sage')
arrow(s,625,397,60,'amber');arrow(s,1232,397,60,'amber')
line(s,96,606,1824,606,'forest',3)
block(s,96,664,740,'확인한 근거','현재 코드 연결 + 과거 Unity 실행 기록\n2026-09-30 전후 내부 실험',color='white',bodycolor='sage',h=185)
block(s,1035,664,789,'이번에 검증할 질문','같은 조건의 규칙 정책보다 유용한가?\n유효성 · 완료 · 응답 지연 · 실패 처리',color='white',bodycolor='sage',h=185)
text(s,96,926,1700,46,'모델 버전과 실제 응답을 기록 · 현재 행동 정합성과 성능은 미검증',30,'amber',bold=True)

# 13 — real weekly milestones, relative not official deadline.
s=slide('매주 실행 가능한 결과로 범위를 좁힌다','12  주단위 구현계획 · 착수 후 6주 제안',
'일정은 공식 마감을 뜻하지 않는 착수 후 6주의 상대 계획입니다. 첫 주에는 대상 구역과 대표 사건, 지원 XR 장치를 확정합니다. 둘째 주에는 작은 대상을 정해 자산 제작과 시점 보완을 비교하고 배치와 통로를 확인합니다. 셋째 주의 결과는 XR 입력으로 핵심 작업 하나를 끝낼 수 있는 플레이입니다. 넷째 주에는 NPC·사건을 연결하고 입력과 모델 응답을 기록해 같은 조건의 재생을 점검합니다. 다섯째 주에는 성능과 조작의 파일럿에서 실패를 모으고, 마지막 주에는 범위 안의 수정과 시연·근거 자료를 정리합니다. 각 주의 산출물이 다음 주의 출발 조건이며 전체 역의 완성보다 수업에서 검토 가능한 완결성을 우선합니다.')
weeks=[('범위 확정','구역·사건·XR 장치','근거·요구·상태표'),('공간·자산 비교','정합·보강·제작 비교','배치·통로 점검'),('XR 핵심 행동','입력·이동·공간 UI','한 작업 완료 구간'),('NPC·재생 연결','사건·응답·입력 기록','반복 실행 차이 보고'),('파일럿 평가','성능·조작·지연','실패 목록·우선 수정'),('수정·시연','범위 내 통합·정리','빌드·재현·한계 자료')]
line(s,150,431,1760,431,'forest',6)
for i,(h,a,b) in enumerate(weeks):
    x=96+i*290
    circle(s,x+55,403,56,'forest' if i<5 else 'amber')
    text(s,x+62,414,49,40,str(i+1),28,'white' if i<5 else 'ink',mono=True)
    text(s,x,310,260,60,f'{i+1}주차',39,'forest',bold=True)
    text(s,x,505,259,110,h,39,'ink',bold=True)
    text(s,x,644,259,140,a.replace('·',' · '),32,'muted')
    line(s,x,809,x+248,809,'line',3)
    text(s,x,843,257,110,b,30,'forest',bold=True)

# 14 — actual development + native illustrative planned result.
s=slide('현재 공간에서 완결된 XR 사건으로 확장한다','13  결과물 형상 · 현재 중간 결과 / 계획한 최종 모습',
'왼쪽은 실제 개발 중인 부산역 1층 공간이고, 오른쪽은 최종 플레이를 설명하기 위한 개념도입니다. 두 자료의 성격을 구분해서 봐 주시면 됩니다. 현재 화면은 공간 배치와 외형의 중간 결과이며 3DGS나 NeRF 실험의 성공, NPC 성능 또는 VR 실행을 증명하는 자료는 아닙니다. 계획한 결과에서는 역내 한 구역에서 사건 단서를 찾고, 공간에 놓인 정보와 장비를 이용해 안내와 보고를 수행합니다. 종료 후에는 선택 시점과 이동 흐름을 돌아보고 같은 초기 조건으로 다른 대응을 시도합니다. 최종 제출물은 실행 빌드와 함께 공간 근거, 입력·응답 기록, 확인한 성능과 한계를 포함하도록 구성하겠습니다.')
photo(s,96,330,1000,575,'escalator.png','실제 중간 결과 · 부산역 1F Unity 개발 화면 · 2026-10-12')
rect(s,1175,330,649,625,'sage',r=8)
pill(s,1205,354,540,'계획한 최종 형상 · 개념도','amber','ink')
# schematic spatial scene, intentionally native editable.
rect(s,1232,466,525,293,'paper','forest',r=4)
line(s,1232,759,1350,641,'forest',3);line(s,1757,759,1650,641,'forest',3)
line(s,1350,641,1650,641,'forest',3)
rect(s,1416,506,185,102,'forest',r=4);text(s,1436,519,150,80,'안내 대상\n선택',28,'white',bold=True)
for x,y in [(1292,620),(1656,620),(1498,675)]:circle(s,x,y,36,'forest')
circle(s,1293,812,75,'forest');circle(s,1639,812,75,'forest')
line(s,1330,812,1470,608,'amber',4,True)
text(s,1210,910,575,42,'단서 확인 → 안내·보고 → 회고',30,'forest',bold=True)

# 15 — expected effects anchored to proposed evaluation.
s=slide('기대효과를 세 가지 검증 질문으로 연결한다','14  기대효과 · 평가 계획',
'기대효과는 세 가지입니다. 첫째는 제작 근거를 추적할 수 있는 공간입니다. 이를 위해 시점 보강을 했을 때 미사용 실제 사진과 독립 치수에서 어떤 차이가 생기는지 확인합니다. 둘째는 AI 판단을 비교할 수 있는 상황입니다. 같은 조건에서 규칙과 JEV의 유효한 행동, 작업 완료와 지연을 측정하고 재생의 일치 범위를 남깁니다. 셋째는 공간적 판단을 몸으로 경험하는 VR 게임입니다. 과제 완료와 조작 오류, 불편감과 주관적 재미를 파일럿에서 확인합니다. 이러한 측정은 각각 제작 품질, 판단·재현성, 경험의 사용성을 다룹니다. 재미나 점수가 높다는 이유만으로 실제 대응 역량이 향상됐다고 결론짓지 않겠습니다. 검토 가능한 프로토타입과 근거가 이번 제안의 목표입니다.')
text(s,96,294,735,56,'기대하는 변화',36,'forest',bold=True)
text(s,995,294,829,56,'확인할 증거',36,'forest',bold=True)
effects=[('제작 근거가 남는 공간','실제/합성 구분 · 사진·치수 일치\n수동 보정 시간과 실행 비용'),('비교할 수 있는 AI·시나리오','규칙 ↔ JEV · 유효성·완료·지연\n같은 기록의 사건·상태·궤적 비교'),('행동의 결과를 경험하는 VR','완료·조작 오류·불편감\n몰입·재미·회고 이해도 파일럿')]
for i,(a,b) in enumerate(effects):
    y=397+i*178;line(s,96,y-26,1824,y-26,'line',3)
    text(s,96,y+5,760,80,a,43,'ink',bold=True);arrow(s,844,y+8,90)
    text(s,995,y,829,120,b,34,'muted')
text(s,96,940,1728,44,'최종 산출물: 완결된 사건 플레이 + 실행·재생 자료 + 평가 결과 + 한계',34,'forest',bold=True)

# References: source titles remain exact and are linked in both formats.
CV=[
('R1 · CVPR 2016','Structure-from-Motion Revisited','https://openaccess.thecvf.com/content_cvpr_2016/html/Schonberger_Structure-From-Motion_Revisited_CVPR_2016_paper.html'),
('R2 · CVPR 2025','VGGT: Visual Geometry Grounded Transformer','https://openaccess.thecvf.com/content/CVPR2025/html/Wang_VGGT_Visual_Geometry_Grounded_Transformer_CVPR_2025_paper.html'),
('R3 · TOG / SIGGRAPH 2023','3D Gaussian Splatting for Real-Time Radiance Field Rendering','https://doi.org/10.1145/3592433'),
('R4 · CVPR 2025','Generative Sparse-View Gaussian Splatting','https://openaccess.thecvf.com/content/CVPR2025/html/Kong_Generative_Sparse-View_Gaussian_Splatting_CVPR_2025_paper.html'),
('R5 · 2026 · arXiv author manuscript','GIFSplat: Generative Prior-Guided Iterative Feed-Forward 3D Gaussian Splatting from Sparse Views','https://arxiv.org/abs/2602.22571'),
('R6 · ECCV 2020','NeRF: Representing Scenes as Neural Radiance Fields for View Synthesis','https://www.matthewtancik.com/nerf'),
('R7 · NeurIPS 2021','NeuS: Learning Neural Implicit Surfaces by Volume Rendering for Multi-view Reconstruction','https://proceedings.nips.cc/paper/2021/hash/e41e164f7485ec4a28741a2d0ea41c74-Abstract.html'),
('R8 · SIGGRAPH 2024','2D Gaussian Splatting for Geometrically Accurate Radiance Fields','https://surfsplatting.github.io/'),
('R9 · arXiv technical report 2025','Hunyuan3D 2.1: From Images to High-Fidelity 3D Assets with Production-Ready PBR Material','https://arxiv.org/abs/2506.15442'),
('R10 · arXiv technical report 2025','Hunyuan3D-Omni: A Unified Framework for Controllable Generation of 3D Assets','https://arxiv.org/abs/2509.21245')]
s=slide('컴퓨터비전 · 원문 근거','APPENDIX A  /  REFERENCES',
'이 페이지는 공간 제작에 사용한 원문 근거입니다. 오래된 기준선과 최근의 시점 보강·생성 연구를 함께 두었고, 학회 논문과 arXiv 기술 보고서를 구분했습니다. 특히 새 논문에 나온 성능을 현재 프로젝트의 성능으로 바꾸어 말하지 않았습니다. 발표 중에는 이 목록을 읽기보다 질문받은 제작 경로의 원문을 확인하는 용도로 사용하겠습니다.',footer='제목을 선택하면 원문으로 이동 · 상세 출처와 주장 범위는 SOURCE_LEDGER.md 참조')
for i,(meta,title,url) in enumerate(CV):
    x=96+(i//5)*880;y=280+(i%5)*140
    text(s,x,y,815,38,meta,28,'forest',mono=True,url=url)
    text(s,x,y+42,815,98,title,28,'ink',url=url)

SIM=[
('R11 · ISRR / Robotics Research 2011','Reciprocal n-body collision avoidance','https://gamma-web.iacs.umd.edu/ORCA/'),
('R12 · Physical Review E 1995','Social force model for pedestrian dynamics','https://journals.aps.org/pre/abstract/10.1103/PhysRevE.51.4282'),
('R13 · RSS 2025','Demonstrating Arena 5.0: A Photorealistic ROS2 Simulation Framework for Developing and Benchmarking Social Navigation','https://roboticsproceedings.org/rss21/p092.html'),
('R14 · Frontiers in Built Environment 2026','PeopleFlow: a reproducible geometry-aware multi-agent framework for evacuation safety evaluation across diversified building environments','https://www.frontiersin.org/journals/built-environment/articles/10.3389/fbuil.2026.1850477/full'),
('R15 · Unity · 공식 구현 문서','ECS Galaxy Sample — Determinism Overview','https://github.com/Unity-Technologies/ECSGalaxySample/blob/main/_Documentation/determinism.md'),
('R16 · ICML / PMLR 2020','Leveraging Procedural Generation to Benchmark Reinforcement Learning','https://proceedings.mlr.press/v119/cobbe20a.html'),
('XR01 · Unity XRI 3.3 · 공식 개발 문서','XR Interaction Simulator overview','https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/xr-interaction-simulator-overview.html'),
('XR02 · Unity XRI 3.3 · 공식 개발 문서','XR Interaction Toolkit — Components','https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/components.html')]
s=slide('시뮬레이션 · 재현성 · XR 구현 근거','APPENDIX B  /  REFERENCES',
'이 페이지는 이동과 시뮬레이션, XR 구현의 참고 자료입니다. ORCA와 social force는 비교 기준선이고, Arena와 PeopleFlow는 다른 도메인의 반복 평가 사례입니다. Unity의 결정론 문서는 우리의 현재 시스템이 이미 결정론적이라는 증거가 아닙니다. XR 문서 역시 제안할 도구의 공식 참고이며 현재 설치와 실행의 증거로 사용하지 않았습니다. 코드와 개발 화면, 과거 내부 실험 기록의 상세 경로는 함께 제공하는 출처 원장에 정리했습니다.',footer='논문과 공식 개발 문서를 구분 · 프로젝트 실행·교육 효과의 증거는 별도 평가')
for i,(meta,title,url) in enumerate(SIM):
    x=96+(i//4)*880;y=282+(i%4)*177
    text(s,x,y,815,40,meta,28,'forest',url=url)
    text(s,x,y+46,815,127,title,28,'ink',url=url)

ASSETS={
    'escalator.png':'workers/twin/output/claude-main1f-completion-20261012/_writer/022-OPT1-CLEAT/captures-after/B5-E-1F-FOOT.png',
    'glazing.png':'workers/twin/output/claude-main1f-completion-20261012/_writer/023-GLZ-B7/captures-after/B7-S1-P.png'}

JS_RUNTIME=r'''
const createdIds=[], textMetrics=[], noteChecks=[], photoNodes=[], slideIds=[], reusedIds=[];
const fonts={regular:{family:'IBM Plex Sans KR',style:'Regular'},bold:{family:'IBM Plex Sans KR',style:'Bold'},mono:{family:'IBM Plex Mono',style:'Medium'}};
await Promise.all(Object.values(fonts).map(f=>figma.loadFontAsync(f)));
const rgb=h=>({r:parseInt(h.slice(1,3),16)/255,g:parseInt(h.slice(3,5),16)/255,b:parseInt(h.slice(5,7),16)/255});
const paint=h=>[{type:'SOLID',color:rgb(h)}];
const reg=n=>{createdIds.push(n.id);return n};
const attach=(p,n,name)=>{p.appendChild(n);n.name=name||n.name;reg(n);return n};
function makeText(p,n){
 const a=attach(p,figma.createText(),n.name||n.text.slice(0,50));
 a.fontName=n.mono?fonts.mono:(n.bold?fonts.bold:fonts.regular);a.fontSize=n.size;
 a.lineHeight={unit:'PERCENT',value:128};a.fills=paint(n.color);a.characters=n.text;
 a.textAlignHorizontal=(n.align||'left').toUpperCase();
 a.resize(n.w,Math.max(n.size,1));a.textAutoResize='HEIGHT';a.x=n.x;a.y=n.y;
 if(n.url)a.hyperlink={type:'URL',value:n.url};
 textMetrics.push({id:a.id,slide:activeSlide.id,text:n.text.slice(0,80),x:a.x,y:a.y,width:a.width,height:a.height,allocatedHeight:n.h,font:n.size,overflow:a.height>n.h+3});
 return a;
}
let activeSlide;
function make(p,n){
 let a;
 if(n.type==='text')return makeText(p,n);
 if(n.type==='block'){
  a=attach(p,figma.createFrame(),n.name);a.fills=[];a.resize(n.w,n.h);a.x=n.x;a.y=n.y;
  a.layoutMode='VERTICAL';a.primaryAxisSizingMode='AUTO';a.counterAxisSizingMode='FIXED';a.itemSpacing=n.gap;
  a.paddingLeft=a.paddingRight=a.paddingTop=a.paddingBottom=0;
  const t=makeText(a,{name:n.heading,text:n.heading,w:n.w,h:60,size:n.headingSize,color:n.color,bold:true,x:0,y:0});
  const b=makeText(a,{name:n.heading+' description',text:n.body,w:n.w,h:n.h-65,size:n.bodySize,color:n.bodycolor,x:0,y:0});
  t.layoutAlign=b.layoutAlign='STRETCH';return a;
 }
 if(n.type==='line'){
  a=attach(p,figma.createLine(),n.name);a.resize(Math.hypot(n.x2-n.x,n.y2-n.y),0);
  a.x=n.x;a.y=n.y;a.rotation=-Math.atan2(n.y2-n.y,n.x2-n.x)*180/Math.PI;
  a.strokes=paint(n.color);a.strokeWeight=n.width;if(n.dashed)a.dashPattern=[12,10];return a;
 }
 if(n.type==='image'&&n.crop){
  a=attach(p,figma.createFrame(),n.name);a.resize(n.w,n.h);a.x=n.x;a.y=n.y;a.fills=[];a.clipsContent=true;
  const [cx,cy,cw,ch,iw,ih]=n.crop,sc=Math.min(n.w/cw,n.h/ch);
  const im=attach(a,figma.createRectangle(),n.name+' image');im.resize(iw*sc,ih*sc);im.x=(n.w-cw*sc)/2-cx*sc;im.y=(n.h-ch*sc)/2-cy*sc;im.fills=paint('#F5F5F5');im.strokes=[];
  photoNodes.push({nodeId:im.id,slideId:activeSlide.id,asset:n.asset,width:im.width,height:im.height,crop:true,fit:'contain'});return a;
 }
 a=attach(p,n.type==='ellipse'?figma.createEllipse():figma.createRectangle(),n.name);
 a.resize(n.w,n.h);a.x=n.x;a.y=n.y;
 a.fills=paint(n.fill||'#E2E9DD');if(n.stroke){a.strokes=paint(n.stroke);a.strokeWeight=2}else a.strokes=[];
 if(n.radius!==undefined)a.cornerRadius=n.radius;if(n.opacity!==undefined)a.opacity=n.opacity;
 if(n.type==='image'){photoNodes.push({nodeId:a.id,slideId:activeSlide.id,asset:n.asset,width:n.w,height:n.h,fit:n.fit||'cover'});a.name='FIGURE · '+n.asset;}
 return a;
}
const existing=figma.getSlideGrid().flat();
for(const spec of specs){
 const old=existing.find(s=>s.name===spec.name);
 if(old){reusedIds.push(old.id);slideIds.push({id:old.id,index:spec.index,name:old.name,reused:true});continue;}
 const s=figma.createSlide();reg(s);activeSlide=s;s.name=spec.name;s.fills=paint(spec.background);
 for(const n of spec.nodes)make(s,n);
 s.speakerNotes=spec.notes;
 noteChecks.push({id:s.id,exact:s.speakerNotes===spec.notes,chars:spec.notes.length});
 slideIds.push({id:s.id,index:spec.index,name:s.name});
}
const allNodes=createdIds.map(id=>figma.getNodeById(id)).filter(Boolean);
const bounds=[];
for(const n of allNodes){
 if(n.type==='SLIDE'||!('absoluteBoundingBox'in n)||!n.absoluteBoundingBox)continue;
 let s=n;while(s&&s.type!=='SLIDE')s=s.parent;if(!s)continue;
 const b=n.absoluteBoundingBox,sb=s.absoluteBoundingBox;
 if(b.x<sb.x-4||b.y<sb.y-4||b.x+b.width>sb.x+sb.width+4||b.y+b.height>sb.y+sb.height+4)bounds.push({id:n.id,name:n.name,x:b.x-sb.x,y:b.y-sb.y,w:b.width,h:b.height});
}
return {createdIds,slideIds,reusedIds,photoNodes,noteChecks,textMetrics,overflow:textMetrics.filter(x=>x.overflow),bounds};
'''

def html_node(n,embedded):
    t=n['type'];style=f"left:{n['x']}px;top:{n['y']}px;width:{n.get('w',0)}px;height:{n.get('h',0)}px;"
    if t=='line':
        length=math.hypot(n['x2']-n['x'],n['y2']-n['y']);angle=math.degrees(math.atan2(n['y2']-n['y'],n['x2']-n['x']))
        return f'<div class="route" style="left:{n["x"]}px;top:{n["y"]}px;width:{length}px;border-top:{n["width"]}px {"dashed" if n["dashed"] else "solid"} {n["color"]};transform:rotate({angle}deg)"></div>'
    if t=='text':
        style+=f"font-size:{n['size']}px;color:{n['color']};font-weight:{700 if n['bold'] else 400};font-family:{'var(--mono)' if n['mono'] else 'var(--sans)'};text-align:{n.get('align','left')};"
        value=html.escape(n['text']).replace('\n','<br>')
        if n.get('url'):value=f'<a href="{html.escape(n["url"])}" target="_blank" rel="noreferrer">{value}</a>'
        role=' role="heading" aria-level="2"' if n.get('name')=='Fixed title' else ''
        return f'<div class="el text"{role} data-allocated-height="{n["h"]}" style="{style}">{value}</div>'
    if t=='block':
        return f'<div class="el block" style="{style}gap:{n["gap"]}px"><div style="font-size:{n["headingSize"]}px;color:{n["color"]};font-weight:700">{html.escape(n["heading"])}</div><div style="font-size:{n["bodySize"]}px;color:{n["bodycolor"]}">{html.escape(n["body"]).replace(chr(10),"<br>")}</div></div>'
    if t=='image':
        src=embedded[n['asset']]
        if n.get('crop'):
            cx,cy,cw,ch,iw,ih=n['crop'];sc=min(n['w']/cw,n['h']/ch)
            dx=(n['w']-cw*sc)/2-cx*sc;dy=(n['h']-ch*sc)/2-cy*sc
            return f'<div class="el" style="{style}overflow:hidden"><img alt="{html.escape(n["alt"])}" src="{src}" style="position:absolute;left:{dx}px;top:{dy}px;width:{iw*sc}px;height:{ih*sc}px"></div>'
        return f'<img class="el photo" alt="{html.escape(n.get("alt","설명용 개념도"))}" src="{src}" style="{style}object-fit:{n.get("fit","cover")};">'
    style+=f"background:{n['fill']};border-radius:{'50%' if t=='ellipse' else str(n.get('radius',0))+'px'};opacity:{n.get('opacity',1)};"
    if n.get('stroke'):style+=f"border:2px solid {n['stroke']};"
    return f'<div class="el shape" aria-hidden="true" style="{style}"></div>'

CSS='''
@import url('https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+KR:wght@400;700&family=IBM+Plex+Mono:wght@500&display=swap');
:root{--sans:'IBM Plex Sans KR','Apple SD Gothic Neo',sans-serif;--mono:'IBM Plex Mono',monospace;--title-size:clamp(1.5rem,5vw,4rem);--body-size:clamp(.75rem,1.5vw,1.125rem);--slide-padding:clamp(1rem,4vw,4rem)}
*{box-sizing:border-box}html,body{height:100%;overflow-x:hidden;margin:0}html{scroll-snap-type:y mandatory;scroll-behavior:smooth}body{background:#102C25;font-family:var(--sans)}
.slide{width:100vw;height:100vh;height:100dvh;overflow:hidden;scroll-snap-align:start;display:flex;flex-direction:column;position:relative;align-items:center;justify-content:center}
.slide-content{width:1920px;height:1080px;position:relative;flex:none;overflow:hidden;transform-origin:center;max-height:none}
.el,.route{position:absolute}.text{line-height:1.28;white-space:normal;overflow:visible}.text a{color:inherit;text-decoration:none}.text a:hover{text-decoration:underline}.block{display:flex;flex-direction:column;line-height:1.28}.photo{object-fit:cover}.route{height:0;transform-origin:left center}
.slide.revealed .slide-content{animation:arrive .35s ease both}@keyframes arrive{from{opacity:.45;translate:0 10px}to{opacity:1;translate:0 0}}
.controls{position:fixed;bottom:12px;right:18px;display:flex;gap:8px;z-index:100;background:#102c25dd;padding:8px;border-radius:10px;color:white}.controls button{border:1px solid #65746A;background:transparent;color:white;cursor:pointer;border-radius:5px;font:14px var(--sans);padding:7px 12px}.controls output{font:14px var(--mono);padding:9px}.notes{display:none;position:fixed;z-index:101;left:3vw;bottom:3vh;width:min(1050px,92vw);max-height:45vh;overflow:auto;background:#f3f0e8f5;color:#172821;border:2px solid #0e473c;padding:28px;border-radius:8px;font-size:clamp(16px,1.5vw,24px);line-height:1.7;box-shadow:0 10px 60px #0008}.notes.open{display:block}
@media(prefers-reduced-motion:reduce){*{animation:none!important;scroll-behavior:auto!important}}
@media print{@page{size:16in 9in;margin:0}html,body{height:auto;overflow:visible;background:white}.slide{width:1920px!important;height:1080px!important;break-after:page;page-break-after:always}.slide-content{transform:none!important}.controls,.notes{display:none!important}.slide-content{animation:none!important}}
'''
CONTROLLER='''
class PresentationController{
 constructor(){this.slides=[...document.querySelectorAll('.slide')];this.index=0;this.output=document.querySelector('#index');this.notes=document.querySelector('.notes');this.t0=0;
 this.resize=()=>{const scale=Math.min(innerWidth/1920,innerHeight/1080);document.querySelectorAll('.slide-content').forEach(e=>e.style.transform=`scale(${scale})`)};addEventListener('resize',this.resize);this.resize();
 this.observer=new IntersectionObserver(es=>es.forEach(e=>{if(e.isIntersecting){e.target.classList.add('revealed');this.index=this.slides.indexOf(e.target);this.sync()}}),{threshold:.6});this.slides.forEach(s=>this.observer.observe(s));
 addEventListener('keydown',e=>{if(['ArrowRight','ArrowDown','PageDown',' '].includes(e.key)){e.preventDefault();this.go(1)}else if(['ArrowLeft','ArrowUp','PageUp'].includes(e.key)){e.preventDefault();this.go(-1)}else if(e.key.toLowerCase()==='n')this.toggleNotes();else if(e.key==='Home')this.jump(0);else if(e.key==='End')this.jump(this.slides.length-1);else if(e.key.toLowerCase()==='f')document.documentElement.requestFullscreen?.();});
 addEventListener('wheel',e=>{if(this.notes.classList.contains('open'))return;if(Date.now()-this.t0>650&&Math.abs(e.deltaY)>20){e.preventDefault();this.go(e.deltaY>0?1:-1);this.t0=Date.now()}},{passive:false});let touchY=0;addEventListener('touchstart',e=>touchY=e.changedTouches[0].clientY,{passive:true});addEventListener('touchend',e=>{const dy=touchY-e.changedTouches[0].clientY;if(Math.abs(dy)>45)this.go(dy>0?1:-1)},{passive:true});
 document.querySelector('#prev').onclick=()=>this.go(-1);document.querySelector('#next').onclick=()=>this.go(1);document.querySelector('#notes').onclick=()=>this.toggleNotes();document.querySelector('#print').onclick=()=>print();this.sync();}
 sync(){this.output.textContent=`${String(this.index+1).padStart(2,'0')} / ${this.slides.length}`;this.notes.textContent=window.SLIDE_NOTES[this.index];document.title=`CHOOGuard · ${this.index+1} · ${this.slides[this.index].getAttribute('aria-label')}`;}
 go(d){this.jump(this.index+d)}jump(i){this.index=Math.max(0,Math.min(i,this.slides.length-1));this.slides[this.index].scrollIntoView({behavior:matchMedia('(prefers-reduced-motion:reduce)').matches?'instant':'smooth'});this.sync()}
 toggleNotes(){this.notes.classList.toggle('open');this.sync()}
}window.deck=new PresentationController();
'''

def render_html(embedded,font_mode='embedded'):
    sections=[]
    for s in SLIDES:
        ordered=[n for n in s['nodes'] if not n.get('name','').startswith('Fixed footer') and n.get('name')!='Fixed page number']+[n for n in s['nodes'] if n.get('name','').startswith('Fixed footer') or n.get('name')=='Fixed page number']
        sections.append(f'<section class="slide" aria-label="{html.escape(s["title"] or "CHOOGuard")}" id="slide-{s["index"]}"><div class="slide-content" style="background:{s["background"]}">'+''.join(html_node(n,embedded) for n in ordered)+'</div></section>')
    faces=[]
    for kind,family,weight in [('regular','IBM Plex Sans KR',400),('bold','IBM Plex Sans KR',700),('mono','IBM Plex Mono',500)]:
        src='data:font/ttf;base64,'+base64.b64encode((HERE/'qa-fonts'/(kind+'.ttf')).read_bytes()).decode() if font_mode=='embedded' else 'fonts/'+kind+'.ttf'
        faces.append(f'@font-face{{font-family:"{family}";font-weight:{weight};font-style:normal;font-display:block;src:url("{src}") format("truetype")}}')
    css='\n'.join(faces)+CSS[CSS.index(':root'):]
    return '<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>CHOOGuard · VR Term Project</title><style>'+css+'</style></head><body><main>'+''.join(sections)+'</main><nav class="controls" aria-label="발표 탐색"><button id="prev" aria-label="이전 슬라이드">←</button><output id="index" aria-live="polite" aria-atomic="true"></output><button id="next" aria-label="다음 슬라이드">→</button><button id="notes" aria-controls="speaker-notes" aria-expanded="false">발표 노트 Alt+N</button><button id="fullscreen">전체 화면 Alt+F</button><button id="print">인쇄</button></nav><aside id="speaker-notes" class="notes" tabindex="-1" aria-label="발표자 노트, Escape로 닫기"></aside><script>window.SLIDE_NOTES='+json.dumps([s['notes'] for s in SLIDES],ensure_ascii=False)+';'+CONTROLLER+'</script></body></html>'

def main():
    from audience_revision import apply, PALETTE
    apply(globals())
    globals()['CONTROLLER']=(HERE/'accessible_controller.js').read_text()
    (HERE/'assets').mkdir(exist_ok=True)
    embedded={}
    for name,source in ASSETS.items():
        src=ROOT/source
        shutil.copyfile(src,HERE/'assets'/name)
        embedded[name]='data:image/png;base64,'+base64.b64encode(src.read_bytes()).decode()
    scene=dict(title='CHOOGuard · 가상현실 Term Project',width=1920,height=1080,palette=PALETTE,
               slides=SLIDES,assets=ASSETS,referenceDate='2026-10-13',provenance='Cover plus six required sections. Generated conceptual figures originate in gpt-image-2.5-sunburst; background removal is separately recorded. No Unity scene screenshots. Figures show proposed results. Demo media belongs within the result-appearance section; no file is connected.')
    (HERE/'scene.json').write_text(json.dumps(scene,ensure_ascii=False,indent=2))
    (HERE/'deck.html').write_text(render_html(embedded))
    (HERE/'deck-cloud.html').write_text(render_html({k:'assets/'+k for k in ASSETS},font_mode='relative'))
    chunks=[SLIDES[:3],SLIDES[3:7]]
    for i,chunk in enumerate(chunks,1):
        (HERE/f'figma_batch_{i:02d}.js').write_text('const specs = '+json.dumps(chunk,ensure_ascii=False)+';\n'+JS_RUNTIME)
    notes='\n\n'.join(f'## {s["index"]:02d}. {s["title"] or "CHOOGuard"}\n\n{s["notes"]}' for s in SLIDES)
    (HERE/'PRESENTER_NOTES.md').write_text('# 한국어 발표자 노트\n\n'+notes)
    bad=[]
    for s in SLIDES:
        for n in s['nodes']:
            if n['type']!='line' and (n['x']<0 or n['y']<0 or n['x']+n['w']>1920 or n['y']+n['h']>1080):bad.append([s['index'],n['name']])
            if n['type']=='text' and n['size']<28:bad.append([s['index'],'font below28'])
    assert not bad,bad
    assert len(SLIDES)==7 and all(s['notes'] for s in SLIDES)
    report=dict(slides=7,coverSlides=1,mainSlides=6,referenceSlides=0,notes=7,images=len(ASSETS),nativeShapes=sum(len(s['nodes']) for s in SLIDES),minimumFont=28,declaredBoundsViolations=bad,requiredTopics=['제작 취지','개발 요구사항 및 개발 환경','기술적 특징','주단위 구현계획','결과물 형상','기대효과'],batches=[len(x) for x in chunks],remoteMutations=False)
    (HERE/'build_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
