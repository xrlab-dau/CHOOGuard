const p=await openTab('https://www.kjebi.com/search/smart/order-plan/detail/R25DD20227617-0');
await new Promise(r=>setTimeout(r,4500));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(e=>({text:e.innerText,url:e.href})).filter(a=>a.text&&a.url)}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'3f-drainage-final-dom',acquired_at:new Date().toISOString(),...d}));
