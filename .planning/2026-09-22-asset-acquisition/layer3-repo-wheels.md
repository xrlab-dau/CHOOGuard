# Layer 3 조사 결과 — 감사 열거 → 판정 단말 이동

## 1. DebriefScroll 구성 (확정)
`Assets/ChooGuard/App/Mvp/MvpWorkspace.cs:248-251`
```
var scrollRect=Rect(root,"DebriefScroll",1080,294,324,180);
scrollRect.gameObject.AddComponent<Image>();  // background .01 alpha
scrollRect.gameObject.AddComponent<RectMask2D>();
var scroll=scrollRect.gameObject.AddComponent<ScrollRect>();
scroll.viewport=scrollRect;scroll.horizontal=false;scroll.vertical=true;
scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
```
L252-253: `timeline=Label(scrollRect,"DebriefTimeline",...)`, anchor top-stretch,
sizeDelta=(0,180) 고정, `scroll.content=timeline.rectTransform`.
→ 코드생성, 프리팹 아님. Content 크기는 **고정 180**(가변 텍스트 길이에 안 맞음 — VerticalLayoutGroup/ContentSizeFitter 없음).
동일 패턴이 `MvpProgressionGraphView.cs:44,47`에도 있음(독립 2번째 사례, Mvp 네임스페이스 관례 확인).

## 2. MvpWorkspaceBuilder.cs
ScrollRect/VerticalLayoutGroup/ContentSizeFitter/LayoutElement — **0건**(grep 확인).

## 3. 저장소 전체 열거 (grep -rn, *.cs)
ScrollRect/VLG/CSF/LayoutElement 총 **9건**:
- MvpProgressionGraphView.cs:44,47 (AddComponent<ScrollRect>+config)
- MvpWorkspace.cs:251(AddComponent), :71(Find<ScrollRect> lookup)
- IntegratedInputPlayModeTests.cs:244-245, CSPLAY0202Tests.cs:189,267 (테스트 스캐폴드)
- CommandPreviewPresenter.cs:29 (`[SerializeField] ScrollRect detailsScroll` — Presentation, 프리팹배선 추정)
**VerticalLayoutGroup·ContentSizeFitter: 저장소 전체 0건.** 표준 "가변 리스트 자동확장" 패턴 부재 — DebriefScroll도 고정 sizeDelta.

## 4. asmdef 경계 (Fps→Mvp)
`find Assets -name "*.asmdef"` 15개 전수 확인. `App/Mvp/`, `App/Fps/` 하위에 별도 asmdef **없음** — 둘 다 `Assets/ChooGuard/App/ChooGuard.App.asmdef` 단일 어셈블리에 포함.
namespace 확인: `MvpWorkspace.cs:16` → `ChooGuard.App.Mvp`, `FpsInteractable.cs:4` → `ChooGuard.App.Fps`.
→ **네임스페이스만 분리, 컴파일 경계 없음.** Fps 코드가 Mvp 타입을 직접 참조 가능(같은 어셈블리).

## 5. 한글 폰트 획득 경로 (PreparedAssetSetup.cs)
L53 `ConfigureFont()` 호출, L59 소스 `SourceRoot+"/Fonts/NotoSansCJKkr-Regular.otf"` 존재 검사.
L185-211 `ConfigureFont()`:
- `AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath)` 없으면
  `TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true)` 생성
- Dynamic atlas, `HasCharacters(KoreanSeed)` 없으면 `TryAddCharacters` 로 보강
- material/atlas를 `AssetDatabase.AddObjectToAsset(font)`로 폰트 자산에 귀속, `SetDirty`
→ **에디터 전용 원샷 셋업**(런타임 생성 아님). 판정 단말도 이미 생성된 동일 `TMP_FontAsset`(FontPath)을 참조하면 됨 — 새 폰트 파이프라인 불필요.

unverified: 없음 (5개 항목 전부 코드 직접 확인).
