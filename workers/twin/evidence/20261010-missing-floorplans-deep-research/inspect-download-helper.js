
const p=await openTab('https://www.korailretail.com/bbs/selectBbsDetail.do?bbsId=BBSMSTR_000000000027&nttId=100081621');
console.log('SOURCE_JSON:'+JSON.stringify({id:'retail-download-function',acquired_at:new Date().toISOString(),...(await p.evaluate(()=>({url:location.href,downloadFunction:window.fn_egov_downFile.toString()})))}));
console.log('SESSIONDIR:'+process.cwd());
