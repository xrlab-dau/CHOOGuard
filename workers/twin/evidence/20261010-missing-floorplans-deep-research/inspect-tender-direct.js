
for (const [id,url] of [
['official-goods-detail','https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100081621'],
['auric-2011-handler','https://www.auric.or.kr/user/rdoc/cart_cmag.asp?URL=files%2Fauricpdf.pdf%3Fnum%3D174796%26db%3DCMAG&dn=174796&db=CMAG']]) {
const p=await openTab(url);
console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,raw:a.getAttribute('href'),onclick:a.getAttribute('onclick')})).filter(a=>/첨부|도면|설계|pdf|zip|hwp|fileDown|downFile|원문|로그인/i.test(a.text+' '+a.url)),scripts:[...document.scripts].filter(s=>!s.src).map(s=>s.text).filter(t=>/downFile|fileDown|FileDown/.test(t)),frames:[...document.querySelectorAll('iframe,embed,object')].map(a=>({src:a.src,data:a.data}))})))}));
}