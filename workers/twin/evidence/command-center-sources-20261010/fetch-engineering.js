const url='http://www.ekacem.or.kr/pds/gamribull/2012/201202/%EA%B9%80%EB%A6%AC3-4%EC%9B%94-%EA%B0%90%EB%A6%AC%EC%8B%A4%EB%AC%B4%EC%82%AC%EB%A1%801.pdf';
const r=await fetch(url);
const b=new Uint8Array(await r.arrayBuffer());
console.log('ASSET_JSON:'+JSON.stringify({id:'engineering-download',filename:'ekacem-2012-busan-extension.pdf',url,resolved_url:r.url,status:r.status,content_type:r.headers.get('content-type'),bytes:b.length,base64:Buffer.from(b).toString('base64'),acquired_at:new Date().toISOString()}));
