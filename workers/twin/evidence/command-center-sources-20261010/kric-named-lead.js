const tabsNow=await listBrowserTabs();
const t=tabsNow.find(x=>x.url.includes('q=')&&decodeURIComponent(x.url).includes('부산역 공조설비'));
if(t){await attachBrowserTab(t.id);const d=await page.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('h3')].map(h=>({text:h.innerText,url:h.closest('a')?.href}))}));console.log('SOURCE_JSON:'+JSON.stringify({id:'hvac-result-links',acquired_at:new Date().toISOString(),...d}));}
const url='http://www.kric.go.kr/jsp/biz/bss/businessProgressStatusDetail.jsp?p_id=155&pageNo=7&q_name=2';
const p=await openTab(url);
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text),download_elements:[...document.querySelectorAll('[onclick]')].filter(a=>/파일|다운로드|부산역|공조|\.pdf|\.zip/i.test(a.innerText+' '+a.getAttribute('onclick'))).map(a=>({text:a.innerText,onclick:a.getAttribute('onclick')}))}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'kric-hvac-primary',acquired_at:new Date().toISOString(),...d}));
