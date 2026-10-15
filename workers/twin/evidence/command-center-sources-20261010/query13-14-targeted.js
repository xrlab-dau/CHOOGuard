for(const [id,query] of [['q13','"경부선 부산역 공조설비 개량공사" 도면'],['q14','"부산역 공조설비" "설계"']]){
 const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
