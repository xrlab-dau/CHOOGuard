
for(const [id,url] of [['retail-contract-board','https://www.korailretail.com/bbs/selectBbsList.do?bbsId=BBSMSTR_000000000027'],['retail-commercial-board','https://www.korailretail.com/board/boardList.do?boardTypeNum=46']]) {
 const p=await openTab(url);
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,onclick:a.getAttribute('onclick')})).filter(a=>/부산|푸드|기념품|다음|마지막/i.test(a.text+' '+a.url)),inputs:[...document.querySelectorAll('input,select,form')].map(a=>({tag:a.tagName,name:a.name,type:a.type,action:a.action,value:a.value,html:a.outerHTML.slice(0,1000)}))})))}));
}
