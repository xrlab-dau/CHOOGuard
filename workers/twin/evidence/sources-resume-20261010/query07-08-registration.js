for(const [id,query] of [['q07','"부산역" "승강장 안내도"'],['q08','"부산역" "8·9" "10·11" "12·13"']]) {
 const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('h3')].map(e=>({text:e.innerText,url:e.closest('a')?.getAttribute('href')})).filter(a=>a.text&&a.url)}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
