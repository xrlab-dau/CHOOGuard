// Actual headless DOM exercise against an isolated synthetic localhost sidecar.
const fs=require('fs');
const playwright=require(process.argv[2]);
const config=JSON.parse(fs.readFileSync(process.argv[3],'utf8'));
function check(value,reason){if(!value)throw new Error(reason);}
function next(){return new Promise(resolve=>process.stdin.once('data',resolve));}
(async()=>{
  const browser=await playwright.chromium.launch({headless:true,executablePath:config.executable});
  try{
    const context=await browser.newContext();
    const origin=new URL(config.browser_url).origin;
    await context.route('**/*',route=>new URL(route.request().url()).origin===origin?route.continue():route.abort());
    const page=await context.newPage();
    await page.goto(config.browser_url);
    await page.waitForFunction(()=>!document.getElementById('enable').disabled);
    check(await page.locator('#catalog').isDisabled(),'Catalog blocked until subscription choice saved');
    await page.locator('#cloud').check();
    check((await page.locator('#notice').innerText()).includes('아직 저장되지 않았습니다'),'Checkbox does not pretend to save');
    await page.locator('#enable').click();
    await page.waitForFunction(()=>document.getElementById('notice').dataset.kind==='saved');
    check((await page.locator('#status').innerText()).includes('서버에 사용으로 저장됨'),'Server confirms subscription choice');
    await page.locator('#catalog').click();
    await page.waitForFunction(()=>document.getElementById('model').options.length===3);
    await page.locator('#model').selectOption('second');
    check((await page.locator('#notice').innerText()).includes('아직 저장되지 않았습니다'),'Model selection remains explicitly unsaved');
    await page.locator('#choose').click();
    await page.waitForFunction(()=>!document.getElementById('initial').disabled);
    await page.locator('#initial').click();
    await page.waitForFunction(()=>document.getElementById('readiness').textContent.includes('준비 완료'));
    console.log('READY_FOR_RESTART');await next();
    await page.locator('#check').click();
    await page.waitForFunction(()=>document.getElementById('notice').dataset.kind==='saved');
    check((await page.locator('#readiness').innerText()).includes('구독 모델 사용을 체크'),'Stable cookie survives restart; choices are read from new server state');
    console.log('READY_FOR_STALE');await next();
    await page.locator('#check').click();
    await page.waitForFunction(()=>document.getElementById('notice').dataset.kind==='error');
    check((await page.locator('#notice').innerText()).includes('브라우저 연결이 이전 실행'),'Stale connection failure is specific and visible');
    check(await page.locator('#initial').isDisabled(),'Stale browser cannot pretend to arm');
    console.log(JSON.stringify({passed:true,actual_headless_browser:true,checkbox_unsaved_feedback:true,model_unsaved_feedback:true,server_saved_feedback:true,stable_cookie_across_restart:true,stale_connection_explicit_error:true,real_provider_calls:false}));
  }finally{await browser.close();}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
