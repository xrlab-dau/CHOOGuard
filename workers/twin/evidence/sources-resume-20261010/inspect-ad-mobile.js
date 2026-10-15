const p=await openTab('https://m.blog.naver.com/sunnnyad/222058783782');
const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:[...document.images].map(e=>({alt:e.alt,src:e.currentSrc||e.src,width:e.naturalWidth,height:e.naturalHeight})),links:[...document.querySelectorAll('a')].map(e=>({text:e.innerText,url:e.href})).filter(a=>a.text&&a.url)}));
console.log('SOURCE_JSON:'+JSON.stringify({id:'ad-blog-mobile',acquired_at:new Date().toISOString(),...d}));
