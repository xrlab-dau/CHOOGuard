const p=await openTab('https://www.instagram.com/p/C0c-nTipJsp/');
await p.evaluate(()=>{const x=document.querySelector('[aria-label="닫기"],[aria-label="Close"]');const b=x?.closest('button,[role="button"]');if(b)b.click();});
await new Promise(r=>setTimeout(r,1800));
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:[...document.images].map(e=>({alt:e.alt,src:e.currentSrc||e.src,width:e.naturalWidth,height:e.naturalHeight})),close_buttons:[...document.querySelectorAll('svg')].map(x=>x.getAttribute('aria-label')).filter(Boolean)}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'3f-contractor-final-dom',acquired_at:new Date().toISOString(),...d}));
const imageData=await p.screenshot({fullPage:false});
console.log('ASSET_JSON:'+JSON.stringify({id:'3f-contractor-final-browser',filename:'3f-contractor-final-browser.png',source_page:'https://www.instagram.com/p/C0c-nTipJsp/',acquired_at:new Date().toISOString(),role:'Browser viewport inspection after ordinary modal dismissal; not original architectural-plan acquisition',base64:Buffer.from(imageData).toString('base64')}));
