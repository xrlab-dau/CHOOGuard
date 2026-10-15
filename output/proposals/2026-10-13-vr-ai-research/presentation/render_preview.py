"""Static local layout proof using exact IBM fonts, not a browser/HMD proof."""
from pathlib import Path
import json, math
from PIL import Image,ImageDraw,ImageFont,ImageOps
P=Path(__file__).resolve().parent
scene=json.loads((P/'scene.json').read_text())
def wrap(text,font,width):
    rows=[]
    for paragraph in text.split('\n'):
        row=''
        for ch in paragraph:
            if font.getlength(row+ch)>width and row:rows.append(row);row=ch
            else:row+=ch
        rows.append(row)
    return rows
def text(draw,tx,x,y,w,size,color,bold=False,mono=False):
    f=ImageFont.truetype(str(P/'qa-fonts'/('mono.ttf' if mono else 'bold.ttf' if bold else 'regular.ttf')),size)
    rows=wrap(tx,f,w)
    for i,row in enumerate(rows):draw.text((x,y+i*size*1.28),row,font=f,fill=color,anchor='lt')
def render(s):
    im=Image.new('RGB',(1920,1080),s['background']);d=ImageDraw.Draw(im)
    for n in s['nodes']:
        x,y=n['x'],n['y'];w,h=n.get('w',0),n.get('h',0);t=n['type']
        if t=='text':text(d,n['text'],x,y,w,n['size'],n['color'],n['bold'],n['mono'])
        elif t=='block':
            text(d,n['heading'],x,y,w,n['headingSize'],n['color'],True)
            count=len(wrap(n['heading'],ImageFont.truetype(str(P/'qa-fonts/bold.ttf'),n['headingSize']),w))
            text(d,n['body'],x,y+count*n['headingSize']*1.28+n['gap'],w,n['bodySize'],n['bodycolor'])
        elif t=='image':im.paste(ImageOps.fit(Image.open(P/'assets'/n['asset']).convert('RGB'),(w,h)),(x,y))
        elif t=='line':
            if n['dashed']:
                length=math.hypot(n['x2']-x,n['y2']-y)
                for pos in range(0,int(length),22):
                    end=min(pos+12,length)
                    d.line((x+(n['x2']-x)*pos/length,y+(n['y2']-y)*pos/length,x+(n['x2']-x)*end/length,y+(n['y2']-y)*end/length),fill=n['color'],width=n['width'])
            else:d.line((x,y,n['x2'],n['y2']),fill=n['color'],width=n['width'])
        elif t=='ellipse':d.ellipse((x,y,x+w,y+h),fill=n['fill'],outline=n.get('stroke'),width=2)
        else:d.rounded_rectangle((x,y,x+w,y+h),radius=n.get('radius',0),fill=n['fill'],outline=n.get('stroke'),width=2)
    return im
dest=P/'previews';dest.mkdir(exist_ok=True)
sheet=Image.new('RGB',(2400,1080),'#CCD4C9')
for i,s in enumerate(scene['slides']):
    im=render(s);im.save(dest/f'slide-{i+1:02d}.png')
    sheet.paste(im.resize((480,270)),((i%5)*480,(i//5)*270))
sheet.save(dest/'contact-sheet.png')
print('17 local exact-font layout proofs and contact sheet generated.')
