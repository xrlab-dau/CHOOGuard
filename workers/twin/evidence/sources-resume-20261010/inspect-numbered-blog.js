const p=await openTab("https://www.google.com/goto?url=CAESaAHrOzAVxQrCw7zD9Ld4NuSJZ1v1Aquao-31NHAM9-MnNkGAeP2LD1aCmLiGu8dtVJ0PLZmQni-YxLUN_1dqyowf24YiIKAp1xig_Wxj8lFsWY42pZXOeh08-PJ_GZHSq-TjQje2KGdu");
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:[...document.images].map(e=>({alt:e.alt,src:e.currentSrc||e.src,width:e.naturalWidth,height:e.naturalHeight}))}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'numbered-blog-candidate',acquired_at:new Date().toISOString(),...d}));
