for(const [id,query] of [['q05','"부산역" "승강장" "배치도"'],['q06','"부산역" "8·9" "엘리베이터"']]) {
 const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&a.href&&!a.href.includes('google.com'))}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
