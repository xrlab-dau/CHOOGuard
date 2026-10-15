const queries = ['부산역 아트박스 2층 매장', '부산역 떡카페 소미담', '부산역 영주당 부산과자점', '부산역 유부주머니 부산오뎅', '부산역 코레일 라운지 내부', '부산역 크리스피크림 2026'];
for (const query of queries) {
  const p = await openTab('https://www.google.com/search?q='+encodeURIComponent(query));
  const result = await p.evaluate(() => ({url:location.href,text:document.body.innerText,links:Array.from(document.querySelectorAll('a')).map(a=>({text:a.innerText,url:a.href})).filter(a=>a.text && !a.url.includes('google.com/search'))}));
  console.log('SEARCH_JSON:'+JSON.stringify({query,...result}));
}
