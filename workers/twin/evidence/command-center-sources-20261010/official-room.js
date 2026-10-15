const url='https://www.korail.com/ticket/train/stationGuide/room?tabIdx=3&csIdx=50';
const p=await openTab(url);
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text),images:[...document.querySelectorAll('img')].map((im,n)=>({n,src:im.src,alt:im.alt,width:im.naturalWidth,height:im.naturalHeight,outer:im.outerHTML.slice(0,1200)}))}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'official-room',acquired_at:new Date().toISOString(),...d}));
