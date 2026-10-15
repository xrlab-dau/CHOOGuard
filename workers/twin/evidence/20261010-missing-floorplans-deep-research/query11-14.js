for (const [id,query] of [["q11", "\"부산역사 증축\" 건원"], ["q12", "\"Busan Station\" \"Kunwon\" plan"], ["q13", "site:korailretail.com \"부산역\" \"푸드코트\""], ["q14", "site:korailretail.com \"부산역\" \"3층\""]]) {
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}