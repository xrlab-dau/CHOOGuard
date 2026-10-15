const sources=[
{id:'youngjudang-chulinbone',url:'https://chulinbone.tistory.com/13040'},
{id:'whole-station-amlee',url:'https://m.blog.naver.com/amlee74/224183005994'},
{id:'yubu-aqsd',url:'https://m.blog.naver.com/aqsd2002/224150374021'},
{id:'yubu-mhjoo',url:'https://m.blog.naver.com/mhjoo1209/221855393682'},
{id:'artbox-primary-instagram',url:'https://www.instagram.com/p/DZd7o8VGOYv/'},
{id:'somidam-primary-instagram',url:'https://www.instagram.com/p/DLBgoEBTAsu/'}];
for(const s of sources){const p=await openTab(s.url); const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:Array.from(document.querySelectorAll('img')).map((im,n)=>({n,src:im.src,full:im.getAttribute('data-src')||im.getAttribute('data-lazy-src')||im.getAttribute('data-original')||im.src,w:im.naturalWidth,h:im.naturalHeight,alt:im.alt})),components:Array.from(document.querySelectorAll('.se-component')).map((el,n)=>({n,text:el.innerText,images:Array.from(el.querySelectorAll('img')).map(im=>({src:im.src,full:im.getAttribute('data-src')||im.src}))})),links:Array.from(document.querySelectorAll('a')).map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text)}));console.log('PAGE_JSON:'+JSON.stringify({...s,...d}));}
