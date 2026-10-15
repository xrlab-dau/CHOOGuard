for (const [id,query] of [["q21", "\"부산역\" \"4층\" \"개량\""], ["q22", "\"부산역\" \"5층\" \"공사\" 도면 -아파트 -오피스텔"], ["q23", "\"부산역\" \"도면\" \"방화\""], ["q24", "\"경부고속철도 부산역사 증축 기타공사\" pdf"]]) {
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
await p.evaluate(()=>new Promise(r=>setTimeout(r,500)));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}