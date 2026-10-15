const p=await openTab('https://www.instagram.com/p/C0c-nTipJsp/');
const imageData=await p.screenshot({fullPage:false});
console.log('ASSET_JSON:'+JSON.stringify({id:'3f-contractor-browser-inspection',filename:'3f-contractor-browser-inspection.png',source_page:'https://www.instagram.com/p/C0c-nTipJsp/',acquired_at:new Date().toISOString(),role:'Browser viewport inspection of potential drawing; not original architectural-plan acquisition',base64:Buffer.from(imageData).toString('base64')}));
