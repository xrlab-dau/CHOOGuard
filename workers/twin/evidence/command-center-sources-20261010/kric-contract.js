const p=await openTab('https://www.kric.go.kr/jsp/biz/bss/businessProgressStatusDetail.jsp?p_id=155&pageNo=7&q_name=2');
const f=await p.evaluate(()=>String(window.fLink));
console.log('SOURCE_JSON:'+JSON.stringify({id:'kric-contract-link-code',text:f,acquired_at:new Date().toISOString()}));
await p.evaluate(()=>[...document.querySelectorAll('a')].find(a=>a.getAttribute('href')?.includes("2026-1-088-202601-00")).click());
const d=await p.evaluate(async()=>{await new Promise(r=>setTimeout(r,700));return {url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text)}});
console.log('SOURCE_JSON:'+JSON.stringify({id:'kric-contract-follow',acquired_at:new Date().toISOString(),...d}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'kric-contract-tabs',tabs:await listBrowserTabs(),acquired_at:new Date().toISOString()}));
