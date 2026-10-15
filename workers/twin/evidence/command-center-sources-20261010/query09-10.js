for (const [id,query] of [['q09','"혜원까치" "부산역"'],['q10','"부산역사" "4층" "5층" 사무실']]) {
  const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
  const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
  console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}
