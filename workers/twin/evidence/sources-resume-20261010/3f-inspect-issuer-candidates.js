for(const [id,url] of [['3f-contract-secondary',"https://www.google.com/goto?url=CAESbQHrOzAVJhSAtQjxSj_QTU8PQjMlgGsuuoJhSJMbaRIbLLjNwVbYw4ky_17Il3qn5TQhb-5g8zQz051trW_ucuqWWYCOafBB9FeO2qDFcShczgwkJJTh4A3KIkzem1fTSmGyuak54V6y32JAUt8"],['3f-official-home','https://www.korailretail.com/']]) {
 const p=await openTab(url);
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(e=>({text:e.innerText,url:e.href})).filter(a=>a.text&&a.url)}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...d}));
}
