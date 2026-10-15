
for(const [id,url] of [['kunwon-busan','https://www.kunwon.com/board/perform.list.php?BBS_GUBUN=1&SC_word=%EB%B6%80%EC%82%B0'],['auric-link-resolution','https://www.auric.or.kr/user/rdoc/DocCmag.aspx?returnVal=CMAG&dn=174796']]) {
 const p=await openTab(url);
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,baseURI:document.baseURI,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,raw:a.getAttribute('href'),onclick:a.getAttribute('onclick')})).filter(a=>/부산|원문|pdf|이전|다음|Station/i.test(a.text+' '+a.url)),images:[...document.images].map(i=>({alt:i.alt,src:i.src,w:i.naturalWidth,h:i.naturalHeight})),scripts:[...document.scripts].map(s=>({src:s.src,text:s.src?'':s.text.slice(0,30000)}))})))}));
}
