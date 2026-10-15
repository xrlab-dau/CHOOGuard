for (const [id,query] of [
['q01','"부산역" "4층" "평면도" -지하도상가 -오페라'],
['q02','"부산역" "설계" "도면" 역사 증축'],
['q03','"부산역" "3층" "임대" "도면"'],
['q04','"부산역" "5층" "평면도" -지하도상가']]) {
const p=await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text&&!a.url.includes('google.com'))}));
console.log('SOURCE_JSON:'+JSON.stringify({id,query,acquired_at:new Date().toISOString(),...d}));
}