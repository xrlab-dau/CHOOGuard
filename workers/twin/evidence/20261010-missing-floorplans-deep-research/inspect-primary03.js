
for(const [id,url] of [['auric-2011','https://www.auric.or.kr/user/rdoc/DocCmag.aspx?returnVal=CMAG&dn=174796'],['kunwon-projects','https://www.kunwon.com/board/perform.list.php?BBS_GUBUN=1'],['retail-tender-nav','https://www.korailretail.com/board/boardList.do?boardTypeNum=4']]) {
 const p=await openTab(url);
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href,onclick:a.getAttribute('onclick')})).filter(a=>/부산|원문|pdf|입찰|공고|검색|Station/i.test(a.text+' '+a.url)),inputs:[...document.querySelectorAll('input,select,form')].map(a=>({tag:a.tagName,name:a.name,type:a.type,action:a.action,value:a.value,html:a.outerHTML.slice(0,2000)}))})))}));
}
const a=await openTab('https://www.auric.or.kr/user/rdoc/DocCmag.aspx?returnVal=CMAG&dn=121819');
const d=await a.evaluate(async()=>{const u=new URL('files/auricpdf.pdf?num=121819&db=CMAG',location.href).href;const r=await fetch(u);const ab=await r.arrayBuffer();const bytes=new Uint8Array(ab);let s='';for(let i=0;i<bytes.length;i+=16384)s+=String.fromCharCode(...bytes.subarray(i,i+16384));return{id:'auric-2004-original',filename:'auric-2004-busan-station.pdf',url:u,resolved_url:r.url,status:r.status,content_type:r.headers.get('content-type'),bytes:bytes.length,base64:btoa(s),acquired_at:new Date().toISOString()};});
console.log('ASSET_JSON:'+JSON.stringify(d));
