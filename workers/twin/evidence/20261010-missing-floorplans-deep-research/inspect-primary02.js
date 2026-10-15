for (const [id,url] of [["auric-2004-links", "https://www.auric.or.kr/user/rdoc/DocCmag.aspx?returnVal=CMAG&dn=121819"], ["retail-goods-search", "https://www.korailretail.com/board/boardList.do?boardTypeNum=4&searchCnd=0&searchWrd=%EC%B2%A0%EB%8F%84%EA%B8%B0%EB%85%90%ED%92%88"]]) {
const p=await openTab(url);
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,onclick:a.getAttribute('onclick')})).filter(a=>/부산|프로젝트|원문|다운로드|pdf|기념품|도면/i.test(a.text+' '+a.url+' '+a.onclick)),images:[...document.images].map(i=>({alt:i.alt,src:i.src,w:i.naturalWidth,h:i.naturalHeight})).filter(i=>/원문|pdf|down/i.test(i.alt+' '+i.src))}));
console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...d}));
}
const k=await openTab('https://www.kunwon.com/intro/index.php');
await k.locator('a').filter({hasText:'건축디자인'}).click();
console.log('SOURCE_JSON:'+JSON.stringify({id:'kunwon-design',acquired_at:new Date().toISOString(),...(await k.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,onclick:a.getAttribute('onclick')})).filter(a=>a.text)})))}));