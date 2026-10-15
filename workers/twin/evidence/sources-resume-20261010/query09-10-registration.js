for(const [id,query] of [['q09','site:korail.com 부산역 타는곳 안내도'],['q10','"부산역" "5·6" "8·9"']]) {
 const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('h3')].map(e=>({text:e.innerText,url:e.closest('a')?.getAttribute('href')})).filter(a=>a.text&&a.url)}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
