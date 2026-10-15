for (const [id,query] of [["q05", "\"부산역사\" \"설계\" \"종합건축\""], ["q06", "\"부산역\" \"평면도\" \"코레일\""], ["q07", "\"부산역\" \"도면\" \"푸드코트\""], ["q08", "\"부산역\" \"도면\" \"본관\""], ["q09", "\"부산역\" \"입찰\" \"사무실\" 도면"], ["q10", "\"부산역\" \"5층\" \"배치도\""]]) {
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}