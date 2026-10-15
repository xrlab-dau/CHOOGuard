for (const [id,query] of [['q01','site:korail.com "부산역" "3층" 도면'],['q02','"부산역" "4층" "5층" 도면']]) {
  const p = await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
  const d = await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
  console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
