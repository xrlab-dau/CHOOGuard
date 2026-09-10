## 검증 receipt — `8090f8a8732d76873e622e4c66347ee17e9dcc8c`

- 기기: `laptop-A` · Unity 6000.3.23f1 · Windows standalone 모듈 **installed**
- 시각: 2026-09-10 10:47:01 ~ 10:53:18 (UTC+09:00)
- 격리: 대상 SHA 전용 clone에서 수행. 사용 중인 프로젝트·빌드 산출물은 열지 않았다.
- receipt JSON SHA256: `06f559c03b9f873a2cf131c0dfd517de9aab23c0b4bb4d0c0f44e55d8947eba0`

| 검사 | 결과 | 종료 코드 | 집계 | 근거 |
|---|---|---|---|---|
| Import and C# compile | **PASS** | `0` | — | `compile.log` `40460723476f…` |
| EditMode test run | **PASS** | `0` | 119 중 118 통과 / 0 실패 / 1 스킵 | `editmode.log` `6208f057e0f2…`<br>`editmode-results.xml` `1820f134de47…` |
| PlayMode test run | **PASS** | `0` | 16 중 16 통과 / 0 실패 / 0 스킵 | `playmode.log` `cf80b9b542f4…`<br>`playmode-results.xml` `f18ac4b43846…` |
| WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting (Windows module installed) | **PASS** | `0` | 1 중 1 통과 / 0 실패 / 0 스킵 | `ownership.log` `fd01ebf11847…`<br>`ownership-results.xml` `cba469ba7d64…` |
| WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting (Windows module absent) | **미실행** — this machine reports the Windows standalone module as 'installed'; the opposite condition needs a second machine and is left unrun | — | — | — |
| ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch against a foreign output directory | **PASS** | `1` | — | `build-negative.log` `05a1df5a2781…` |
| ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch against a clean output directory | **PASS** | `0` | — | `build-positive.log` `8a8e655a5206…` |
| Regenerated assets in the isolated checkout | **PASS** | — | — | `regenerated.diff` `2528af2eacb1…` |

### EditMode test run 제외 사유
- `SourceAvailableBuildPreservesBoundsOneViewAndSerializedOriginCamera` — Local reconstruction output is intentionally not Git-tracked.

### 실행한 명령

```
"<UNITY_INSTALL>\Unity.exe" -batchmode -projectPath <CHECKOUT> -accept-apiupdate -quit -logFile <WORKSPACE>\logs\20260910T014701Z-8090f8a\compile.log
"<UNITY_INSTALL>\Unity.exe" -batchmode -projectPath <CHECKOUT> -accept-apiupdate -runTests -testPlatform EditMode -testResults <WORKSPACE>\logs\20260910T014701Z-8090f8a\editmode-results.xml -logFile <WORKSPACE>\logs\20260910T014701Z-8090f8a\editmode.log
"<UNITY_INSTALL>\Unity.exe" -batchmode -projectPath <CHECKOUT> -accept-apiupdate -runTests -testPlatform PlayMode -testResults <WORKSPACE>\logs\20260910T014701Z-8090f8a\playmode-results.xml -logFile <WORKSPACE>\logs\20260910T014701Z-8090f8a\playmode.log
"<UNITY_INSTALL>\Unity.exe" -batchmode -projectPath <CHECKOUT> -accept-apiupdate -runTests -testPlatform EditMode -testFilter WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting -testResults <WORKSPACE>\logs\20260910T014701Z-8090f8a\ownership-results.xml -logFile <WORKSPACE>\logs\20260910T014701Z-8090f8a\ownership.log
"<UNITY_INSTALL>\Unity.exe" -batchmode -projectPath <CHECKOUT> -accept-apiupdate -quit -executeMethod ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch -logFile <WORKSPACE>\logs\20260910T014701Z-8090f8a\build-negative.log
"<UNITY_INSTALL>\Unity.exe" -batchmode -projectPath <CHECKOUT> -accept-apiupdate -quit -executeMethod ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerBatch -logFile <WORKSPACE>\logs\20260910T014701Z-8090f8a\build-positive.log
```

개인 경로·계정·호스트명·전자우편·IP와 라이선스 관련 줄은 게시 전 제거했다. 이 기록은 한 기기의 1회 실행 결과이며 승인이 아니다. 작성자 자체 보고는 독립 검토를 대신하지 않는다.
