const p=await openTab("https://m.blog.naver.com/alfks4040/223191250122");
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:[...document.querySelectorAll('.se-image')].map((e,i)=>({index:i,context_before:e.previousElementSibling?.innerText,context_after:e.nextElementSibling?.innerText,images:[...e.querySelectorAll('img')].map(x=>({src:x.currentSrc||x.src,attrs:[...x.attributes].map(y=>[y.name,y.value])}))}))}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'numbered-mobile',acquired_at:new Date().toISOString(),...d}));
