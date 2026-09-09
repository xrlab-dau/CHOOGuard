## 검증 receipt — `5e339fc4b12876a2c094341dfbeba84b71fe5196`

- 기기: `laptop-A` · Unity 6000.3.23f1 · Windows standalone 모듈 **installed**
- 시각: 2026-09-09 23:28:55 ~ 23:32:08 (UTC+09:00)
- 격리: 대상 SHA 전용 clone에서 수행. 사용 중인 프로젝트·빌드 산출물은 열지 않았다.
- receipt JSON SHA256: `31924bba485db991a66ad5e08cb6816d6ebe71d3cfc186aa46f0d10a39f2f6eb`

| 검사 | 결과 | 종료 코드 | 집계 | 근거 |
|---|---|---|---|---|
| Import and C# compile | **PASS** | `0` | — | `compile.log` `341e372cce22…` |
| EditMode test run | **PASS** | `0` | 119 중 118 통과 / 0 실패 / 1 스킵 | `editmode.log` `368ce933c6e9…`<br>`editmode-results.xml` `440d4dbe91d5…` |
| PlayMode test run | **PASS** | `0` | 16 중 16 통과 / 0 실패 / 0 스킵 | `playmode.log` `77a8d350c576…`<br>`playmode-results.xml` `31686dbd1bf2…` |
| WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting (Windows module installed) | **PASS** | `0` | 1 중 1 통과 / 0 실패 / 0 스킵 | `ownership.log` `d3c5f6d1c652…`<br>`ownership-results.xml` `c0cda4bdddd8…` |
| WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting (Windows module absent) | **미실행** — this machine reports the Windows standalone module as 'installed'; the opposite condition needs a second machine and is left unrun | — | — | — |
| ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch against a foreign output directory | **PASS** | `1` | — | `build-negative.log` `07f4aa2c4ed4…` |
| ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch against a clean output directory | **PASS** | `0` | — | `build-positive.log` `57269d768f8d…` |
| Regenerated assets in the isolated checkout | **PASS** | — | — | `regenerated.diff` `861742d406c2…` |

### EditMode test run 제외 사유
- `SourceAvailableBuildPreservesBoundsOneViewAndSerializedOriginCamera` — Local reconstruction output is intentionally not Git-tracked.

### 실행한 명령

```
<UNITY_INSTALL>\Unity.exe -batchmode -projectPath <CHECKOUT> -accept-apiupdate -quit -logFile <WORKSPACE>\logs\20260909T142855Z-5e339fc\compile.log
<UNITY_INSTALL>\Unity.exe -batchmode -projectPath <CHECKOUT> -accept-apiupdate -runTests -testPlatform EditMode -testResults <WORKSPACE>\logs\20260909T142855Z-5e339fc\editmode-results.xml -logFile <WORKSPACE>\logs\20260909T142855Z-5e339fc\editmode.log
<UNITY_INSTALL>\Unity.exe -batchmode -projectPath <CHECKOUT> -accept-apiupdate -runTests -testPlatform PlayMode -testResults <WORKSPACE>\logs\20260909T142855Z-5e339fc\playmode-results.xml -logFile <WORKSPACE>\logs\20260909T142855Z-5e339fc\playmode.log
<UNITY_INSTALL>\Unity.exe -batchmode -projectPath <CHECKOUT> -accept-apiupdate -runTests -testPlatform EditMode -testFilter WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting -testResults <WORKSPACE>\logs\20260909T142855Z-5e339fc\ownership-results.xml -logFile <WORKSPACE>\logs\20260909T142855Z-5e339fc\ownership.log
<UNITY_INSTALL>\Unity.exe -batchmode -projectPath <CHECKOUT> -accept-apiupdate -quit -executeMethod ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch -logFile <WORKSPACE>\logs\20260909T142855Z-5e339fc\build-negative.log
<UNITY_INSTALL>\Unity.exe -batchmode -projectPath <CHECKOUT> -accept-apiupdate -quit -executeMethod ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch -logFile <WORKSPACE>\logs\20260909T142855Z-5e339fc\build-positive.log
```

개인 경로·계정·호스트명·전자우편·IP와 라이선스 관련 줄은 게시 전 제거했다. 이 기록은 한 기기의 1회 실행 결과이며 승인이 아니다. 작성자 자체 보고는 독립 검토를 대신하지 않는다.
