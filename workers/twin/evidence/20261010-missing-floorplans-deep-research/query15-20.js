for (const [id,query] of [["q15", "\"부산역\" \"푸드코트\" \"운영\" 공고"], ["q16", "\"부산역\" \"부산옥\" 도면"], ["q17", "\"부산역\" \"비빔가\" 공고"], ["q18", "\"부산역\" \"4층\" 공사 도면 -오피스텔 -아파트 -지하도"], ["q19", "\"부산역사 증축\" \"평면\""], ["q20", "\"부산역\" \"승강장\" \"평면도\""]]) {
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}