#!/usr/bin/env python3
"""Export the shared scene as 17 vector/text PDF pages, with original photos.

This is a standard local document export. It does not operate a browser or
modify the semantic source, native Figma batches, or original capture files.
"""
from pathlib import Path
import json
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor
from reportlab.lib.utils import ImageReader
from PIL import Image

HERE=Path(__file__).resolve().parent
TARGET=HERE.parent/'CHOOGuard_VR_Proposal.pdf'
SCENE=json.loads((HERE/'scene.json').read_text())
W,H=SCENE['width'],SCENE['height']
FONTS={'regular':'IBM-Plex-Sans-KR','bold':'IBM-Plex-Sans-KR-Bold','mono':'IBM-Plex-Mono-Medium'}
for kind,family in FONTS.items():
    pdfmetrics.registerFont(TTFont(family,str(HERE/'qa-fonts'/(kind+'.ttf'))))

def wrap(value,font,size,width):
    """Respect explicit breaks, use Korean character breaks and Latin words."""
    lines=[]
    for paragraph in value.split('\n'):
        if not paragraph:lines.append('');continue
        line=''
        for ch in paragraph:
            if line and pdfmetrics.stringWidth(line+ch,font,size)>width:
                # Prefer the last space for a Latin word; Korean may break freely.
                cut=line.rfind(' ')
                trailing=line[cut+1:] if cut>=0 else ''
                if ch.isascii() and ch.isalpha() and trailing.isascii() and trailing.isalpha() and cut>0:
                    lines.append(line[:cut]);line=trailing+ch
                else:
                    lines.append(line.rstrip());line=ch.lstrip() if ch==' ' else ch
            else:line+=ch
        lines.append(line.rstrip())
    return lines

overflow=[]
text_count=0
photo_count=0
link_count=0
current_page=0

def draw_text(c,value,x,y,width,size,color,bold=False,mono=False,allocated=None,url=None,align='left'):
    global text_count,link_count
    family=FONTS['mono' if mono else 'bold' if bold else 'regular']
    rows=wrap(value,family,size,width)
    leading=size*1.28
    face=pdfmetrics.getFont(family).face
    baseline_from_top=size*face.ascent/1000 + (leading-size*(face.ascent-face.descent)/1000)/2
    c.setFillColor(HexColor(color));c.setFont(family,size)
    for i,row in enumerate(rows):
        baseline=H-y-baseline_from_top-i*leading
        row_width=pdfmetrics.stringWidth(row,family,size)
        row_x=x+(width-row_width)/2 if align=='center' else x+width-row_width if align=='right' else x
        c.drawString(row_x,baseline,row)
        if url and row:
            row_width=pdfmetrics.stringWidth(row,family,size)
            c.linkURL(url,(row_x,H-y-(i+1)*leading,row_x+row_width,H-y-i*leading),relative=0,thickness=0)
            link_count+=1
    height=len(rows)*leading
    if allocated is not None and height>allocated+3:
        overflow.append(dict(page=current_page,text=value[:90],height=round(height,2),allocated=allocated))
    text_count+=1
    return height

def draw_photo(c,n):
    global photo_count
    source=HERE/'assets'/n['asset']
    with Image.open(source) as im:iw,ih=im.size
    if n.get('crop'):
        cx,cy,cw,ch,_,_=n['crop'];scale=min(n['w']/cw,n['h']/ch)
        dw,dh=iw*scale,ih*scale
        x=n['x']+(n['w']-cw*scale)/2-cx*scale;y=n['y']+(n['h']-ch*scale)/2-cy*scale
    else:
        scale=(min if n.get('fit')=='contain' else max)(n['w']/iw,n['h']/ih)
        dw,dh=iw*scale,ih*scale
        x=n['x']+(n['w']-dw)/2;y=n['y']+(n['h']-dh)/2
    c.saveState()
    clip=c.beginPath();clip.rect(n['x'],H-n['y']-n['h'],n['w'],n['h']);c.clipPath(clip,stroke=0,fill=0)
    c.drawImage(ImageReader(str(source)),x,H-y-dh,dw,dh,mask='auto')
    c.restoreState();photo_count+=1

def draw_node(c,n):
    kind=n['type'];x,y=n['x'],n['y']
    if kind=='text':
        draw_text(c,n['text'],x,y,n['w'],n['size'],n['color'],n['bold'],n['mono'],n['h'],n.get('url'),n.get('align','left'))
    elif kind=='block':
        ht=draw_text(c,n['heading'],x,y,n['w'],n['headingSize'],n['color'],True,allocated=60)
        draw_text(c,n['body'],x,y+ht+n['gap'],n['w'],n['bodySize'],n['bodycolor'],allocated=n['h']-65)
    elif kind=='line':
        c.saveState();c.setStrokeColor(HexColor(n['color']));c.setLineWidth(n['width'])
        if n['dashed']:c.setDash(12,10)
        c.line(x,H-y,n['x2'],H-n['y2']);c.restoreState()
    elif kind=='image':draw_photo(c,n)
    else:
        c.saveState();c.setFillColor(HexColor(n['fill']))
        if n.get('opacity',1)!=1:c.setFillAlpha(n['opacity'])
        stroke=int(bool(n.get('stroke')))
        if stroke:c.setStrokeColor(HexColor(n['stroke']));c.setLineWidth(2)
        if kind=='ellipse':c.ellipse(x,H-y-n['h'],x+n['w'],H-y,stroke=stroke,fill=1)
        else:c.roundRect(x,H-y-n['h'],n['w'],n['h'],n.get('radius',0),stroke=stroke,fill=1)
        c.restoreState()

def main():
    global current_page
    page_tags=[]
    c=canvas.Canvas(str(TARGET),pagesize=(W,H),pageCompression=1,invariant=1)
    c.setTitle('CHOOGuard · 가상현실 Term Project 제안서')
    c.setAuthor('CHOOGuard Project')
    c.setSubject('관측 근거 기반 디지털 공간, AI 판단과 시드 기반 사건, XR 상호작용 제안')
    c.setKeywords('CHOOGuard, 부산역, 디지털 트윈, 가상현실, 3DGS, NeRF, Hunyuan3D, NPC, JEV')
    for page in SCENE['slides']:
        current_page=page['index']
        c.setFillColor(HexColor(page['background']));c.rect(0,0,W,H,stroke=0,fill=1)
        mark='slide-'+str(page['index']);c.bookmarkPage(mark)
        c.addOutlineEntry(f'{page["index"]:02d} · {page["title"] or "CHOOGuard"}',mark,level=0,closed=False)
        ordered=sorted(page['nodes'],key=lambda n: 0 if n.get('name')=='Fixed title' else 1 if n.get('name')=='Fixed header' else 3 if n.get('name','').startswith('Fixed footer') or n.get('name')=='Fixed page number' else 2)
        tags=[]
        for node in ordered:
            semantic=node['type'] in ['text','block','image'] and not node.get('name','').startswith('Fixed footer') and node.get('name')!='Fixed page number'
            if semantic:
                role='H1' if node.get('name')=='Fixed title' else 'Figure' if node['type']=='image' else 'P'
                tag=dict(mcid=len(tags),role=role)
                if role=='Figure':tag['alt']=node['alt']
                tags.append(tag);c._code.append(f'/{role} <</MCID {tag["mcid"]}>> BDC')
            else:c._code.append('/Artifact BMC')
            draw_node(c,node);c._code.append('EMC')
        page_tags.append(tags)
        c.showPage()
    c.save()
    if overflow:raise RuntimeError('PDF text overflow: '+json.dumps(overflow,ensure_ascii=False))
    (HERE/'pdf_page_tags.json').write_text(json.dumps(page_tags,ensure_ascii=False,indent=2))
    from accessibility_support import tag_pdf
    tag_report=tag_pdf(TARGET,page_tags)
    (HERE/'pdf_accessibility_checks.json').write_text(json.dumps(tag_report,ensure_ascii=False,indent=2))
    print(json.dumps(dict(path=str(TARGET),pages=len(SCENE['slides']),vectorAndText=True,
                         textBlocks=text_count,photoPlacements=photo_count,hyperlinkRects=link_count,
                         textOverflow=overflow,bytes=TARGET.stat().st_size),ensure_ascii=False))

if __name__=='__main__':main()
