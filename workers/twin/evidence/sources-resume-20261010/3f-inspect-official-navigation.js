const p=await openTab('https://www.korailretail.com/');
await new Promise(r=>setTimeout(r,1200));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,iframes:[...document.querySelectorAll('iframe')].map(x=>({src:x.src,title:x.title})),links:[...document.querySelectorAll('a')].map(e=>({text:e.innerText,url:e.href})).filter(a=>a.text&&a.url)}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'3f-official-navigation',acquired_at:new Date().toISOString(),...d}));
