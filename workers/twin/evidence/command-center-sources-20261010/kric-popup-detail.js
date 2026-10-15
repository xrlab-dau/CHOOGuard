for(const [id,code] of [['kric-hvac-construction','2026-1-088-202601-00'],['kric-hvac-design','2024-2-048-202603-07']]){
 const url='https://www.kric.go.kr/jsp/industry/bss/businessProgressPopupDetail.jsp?p_id='+code;
 const p=await openTab(url);
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})),download_elements:[...document.querySelectorAll('[onclick]')].filter(a=>/파일|다운로드|\.pdf|\.zip|\.hwp/i.test(a.innerText+' '+a.getAttribute('onclick'))).map(a=>({text:a.innerText,onclick:a.getAttribute('onclick')}))}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...d}));
}
