for(const [id,url] of [['method-raster-vector','https://art-programmer.github.io/floorplan-transformation.html'],['method-floornet','https://arxiv.org/abs/1804.00090']]){
 const p=await openTab(url);
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,links:[...document.querySelectorAll('a')].map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text)}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...d}));
}
