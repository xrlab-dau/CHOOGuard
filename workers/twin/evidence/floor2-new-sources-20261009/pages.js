const sources = [
{id:'artbox-gtee',url:'https://m.blog.naver.com/gtee/224351997813'},
{id:'somidam-lsknim',url:'https://m.blog.naver.com/lsknim/224426872683'},
{id:'somidam-ounlove',url:'https://m.blog.naver.com/ounlove1143/224177114244'},
{id:'lounge-chulinbone',url:'https://chulinbone.tistory.com/13042'},
{id:'lounge-321fast',url:'https://m.blog.naver.com/321fast/224083151252'},
{id:'krispy-inyung',url:'https://m.blog.naver.com/inyung14/224395974828'},
{id:'artbox-primary-musicart',url:'https://www.musicart.kr/m/board.html?code=musicart_board2&page=1&type=v&board_cate=&num1=999762&num2=00000&number=191&lock=N'},
{id:'somidam-primary-stores',url:'https://somidam.kr/33'}];
for (const s of sources) {
 const p = await openTab(s.url);
 const d = await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:Array.from(document.querySelectorAll('img')).map((im,n)=>({n,src:im.src,full:im.getAttribute('data-src')||im.getAttribute('data-lazy-src')||im.getAttribute('data-original')||im.src,w:im.naturalWidth,h:im.naturalHeight,alt:im.alt})),components:Array.from(document.querySelectorAll('.se-component')).map((el,n)=>({n,text:el.innerText,images:Array.from(el.querySelectorAll('img')).map(im=>({src:im.src,full:im.getAttribute('data-src')||im.src}))})),links:Array.from(document.querySelectorAll('a')).map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text)}));
 console.log('PAGE_JSON:'+JSON.stringify({...s,...d}));
}
