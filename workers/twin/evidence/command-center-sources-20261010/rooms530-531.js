const url='https://www.korail.com/ticket/train/stationGuide/room?tabIdx=3&csIdx=50';
for(const [room,button] of [['530','89'],['531','90']]){
 const p=await openTab(url);
 await p.evaluate(id=>document.querySelector('#ciIdx_'+id).click(),button);
 const d=await p.evaluate(async room=>{await new Promise(r=>setTimeout(r,600));const text=document.body.innerText;return {url:location.href,title:document.title,text:text.slice(text.lastIndexOf('부산역 '+room+'호')),images:[...document.querySelectorAll('img')].filter(im=>im.src.includes('/file/cubedata/')).map(im=>({src:im.src,alt:im.alt,width:im.naturalWidth,height:im.naturalHeight}))};},room);
 console.log('SOURCE_JSON:'+JSON.stringify({id:'room'+room+'-detail',room,button,acquired_at:new Date().toISOString(),...d}));
 for(const [n,im] of d.images.entries()){const r=await fetch(im.src);const b=new Uint8Array(await r.arrayBuffer());console.log('ASSET_JSON:'+JSON.stringify({id:room+'-asset'+(n+1),filename:'korail-'+room+'-asset'+(n+1)+'.jpg',url:im.src,source_page:url,modal:room,alt:im.alt,resolved_url:r.url,status:r.status,content_type:r.headers.get('content-type'),bytes:b.length,base64:Buffer.from(b).toString('base64'),acquired_at:new Date().toISOString()}));}
}
