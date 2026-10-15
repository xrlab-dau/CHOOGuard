
for(const [id,url] of [['tender-busan-page2','https://www.korailretail.com/bbs/selectBbsList.do?bbsId=BBSMSTR_000000000027&searchCnd=0&searchWrd=%EB%B6%80%EC%82%B0&pageIndex=2'],['tender-busan-page3','https://www.korailretail.com/bbs/selectBbsList.do?bbsId=BBSMSTR_000000000027&searchCnd=0&searchWrd=%EB%B6%80%EC%82%B0&pageIndex=3'],['tender-busan-page4','https://www.korailretail.com/bbs/selectBbsList.do?bbsId=BBSMSTR_000000000027&searchCnd=0&searchWrd=%EB%B6%80%EC%82%B0&pageIndex=4']]) {
const p=await openTab(url);
console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>/부산역/.test(a.text))})))}));
}
