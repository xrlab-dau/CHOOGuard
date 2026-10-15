const list=await listBrowserTabs();
const prior=list.find(t=>t.url.includes('q=')&&decodeURIComponent(t.url).includes('부산역 증축 감리실무사례 2012'));
if(prior){await attachBrowserTab(prior.id); const d=await page.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));console.log('SOURCE_JSON:'+JSON.stringify({id:'q07',query:'부산역 증축 감리실무사례 2012',acquired_at:new Date().toISOString(),...d}));}
const query='"부산역" "5층" 회의실';
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'q08',query,acquired_at:new Date().toISOString(),...d}));
