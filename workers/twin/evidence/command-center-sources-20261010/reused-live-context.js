for(const [id,url] of [['kgnwrite-live','https://m.blog.naver.com/kgnwrite/224291656711'],['pshbadgirl-live','https://m.blog.naver.com/pshbadgirl/224373114211']]){
 const p=await openTab(url);
 const d=await p.evaluate(()=>({url:location.href,title:document.title,text:document.body.innerText,images:[...document.querySelectorAll('img')].filter(im=>im.src.includes('blogthumb')).map((im,n)=>({n,src:im.src,original:im.getAttribute('data-src')||im.getAttribute('data-original')||im.src,original_width:im.getAttribute('data-width'),original_height:im.getAttribute('data-height'),width:im.naturalWidth,height:im.naturalHeight}))}));
 console.log('SOURCE_JSON:'+JSON.stringify({id,acquired_at:new Date().toISOString(),...d}));
}
