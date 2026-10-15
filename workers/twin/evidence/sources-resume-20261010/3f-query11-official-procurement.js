const id='3f-q11',query='site:korailretail.com "부산역" "배수"';
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('h3')].map(e=>({text:e.innerText,url:e.closest('a')?.getAttribute('href')})).filter(a=>a.text&&a.url)}));
console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
