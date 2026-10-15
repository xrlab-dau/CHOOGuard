
const p=await openTab('https://www.korailretail.com/bbs/selectBbsList.do?bbsId=BBSMSTR_000000000027&searchCnd=0&searchWrd=%EB%B6%80%EC%82%B0');
await p.evaluate(()=>document.querySelector('a[href*="100081621"]').click());
console.log('SOURCE_JSON:'+JSON.stringify({id:'official-goods-detail',acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,raw:a.getAttribute('href'),onclick:a.getAttribute('onclick')})).filter(a=>/첨부|도면|설계|pdf|zip|hwp|fileDown|downFile/i.test(a.text+' '+a.url)),scripts:[...document.scripts].filter(s=>!s.src).map(s=>s.text).filter(t=>/downFile|fileDown|selectBbsNttView/.test(t))})))}));
const a=await openTab('https://www.auric.or.kr/user/rdoc/DocCmag.aspx?returnVal=CMAG&dn=174796');
await a.evaluate(()=>document.querySelector('a[href*="MM_openBrWindow"]').click());
console.log('SOURCE_JSON:'+JSON.stringify({id:'auric-2011-after-click',acquired_at:new Date().toISOString(),...(await a.evaluate(()=>({url:location.href,text:document.body.innerText})))}));
