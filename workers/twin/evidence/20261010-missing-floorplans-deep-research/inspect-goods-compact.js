
const p=await openTab('https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100081621');
console.log('SOURCE_JSON:'+JSON.stringify({id:'official-goods-detail',acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText.slice(0,14000),links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,raw:a.getAttribute('href'),onclick:a.getAttribute('onclick')})).filter(a=>/첨부|도면|설계|pdf|zip|hwp|fileDown|downFile/i.test(a.text+' '+a.url))})))}));
