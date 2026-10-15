
const p=await openTab('https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100081621');
const d=await p.evaluate(async()=>{const u=new URL('/cmm/fms/FileDown.do?atchFileId=FILE_000000000030641&fileSn=1',location.href).href;const r=await fetch(u);const bytes=new Uint8Array(await r.arrayBuffer());let s='';for(let n=0;n<bytes.length;n+=16384)s+=String.fromCharCode(...bytes.subarray(n,n+16384));return{id:'goods-2026-architecture',filename:'goods-2026-architecture.zip',url:u,resolved_url:r.url,status:r.status,content_type:r.headers.get('content-type'),bytes:bytes.length,headers:Object.fromEntries(r.headers),base64:btoa(s),acquired_at:new Date().toISOString()};});
console.log('ASSET_JSON:'+JSON.stringify(d));
