const p=await openTab('https://www.korail.com/ticket/train/stationGuide/room?tabIdx=3&csIdx=50');
console.log('SOURCE_JSON:'+JSON.stringify({id:'room-detail-dom',acquired_at:new Date().toISOString(),url:p.url,text:await p.evaluate(()=>[...document.querySelectorAll('a,button')].filter(a=>a.innerText.includes('상세보기')).map(a=>a.outerHTML).join('\n')),links:[]}));
