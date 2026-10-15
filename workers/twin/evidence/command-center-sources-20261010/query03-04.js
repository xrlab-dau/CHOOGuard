for (const [id,query] of [['q03','"부산역사" "단면도"'],['q04','"Busan station" architecture section drawing 2004']]) {
  const p = await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
  const d = await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
  console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
