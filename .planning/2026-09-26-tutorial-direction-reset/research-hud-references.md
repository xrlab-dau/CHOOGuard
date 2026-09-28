# 부산역 튜토리얼 HUD·UI/UX 레퍼런스 조사

- 조사 기준일: **2026-09-26**
- 범위: 상용 게임의 HUD와 첫사람 상호작용 표현, 관련 공개 Unity uGUI 소스의 구현 단위·라이선스 조사. 코드·씬·에셋·기존 문서는 변경하지 않았으며 게임 실행/빌드/테스트는 하지 않았다.
- 표기: **A**=본문·이미지 직접 확인, **L**=목록·헤더·파일 경로만 확인, **R**=접근 제한/오류, **S**=검색 결과 스니펫만 확인. 날짜·버전이 드러나지 않으면 미표기/미확인으로 남긴다. `[추정]`은 CHOOGuard 적용 아이디어이지 원작의 기능이나 실제 코레일 절차의 주장으로 읽지 않는다.
- 중복 방지: 아래 `[G01]` 등의 표기는 `docs/CHOOGuard_FPS_Prior_Research_20260925/GAMEPLAY_VISUAL_REFERENCES.md`의 자료 ID를 재인용한다. `[FPTS §…]`는 같은 날짜의 `FPS_TECH_SOURCES.md`를 가리킨다. 두 선행 보고서에 있는 URL·발행기관·판본·열람수준은 그 문서의 원장을 기준으로 한다.

## 요약

1. **가져올 것은 배틀로얄의 정보 위치 규칙이지 전투 정보가 아니다.** `[G14]`의 통신·핑 분리는 요청/응답 의미를 구분하는 사례이고, 과거 PUBG 커뮤니티 스크린샷 `[H01]`은 화면 가장자리의 작은 정보와 중앙의 상호작용 진행을 관찰할 수 있다. 캡처의 버전·날짜는 미확인이다.
2. My Summer Car 공식 이미지 `[G01]`에서는 작고 상시 표시되는 욕구 HUD와 시선 대상 명칭을 확인했다. **손 모양 아이콘이 현재 상호작용 기본 UI라는 증거는 이번 조사에서 확인하지 못했다.**
3. 실제 작업감은 중심 시야의 대상 프롬프트와 짧은 진행 표시, 선택형 문서/세부정보 화면을 조합하는 쪽이 비교에 유효하다. TSW5의 검표 근거-판단 분리 `[G06]`, PowerWash의 놓친 대상 강조 `[G10]`, 공개 상호작용 코드의 시선 대상-프롬프트 패턴 `[H09]`가 참고 축이다.
4. `[추정]` CHOOGuard 첫 레슨은 `현재 단계/8단계 진행`을 작은 주변 HUD에 두고, 소화기 ID·현재 확인 대상·행동 프롬프트는 조준 대상에 붙이며, 긴 절차/기준은 필요할 때만 펼치는 구성이 적합하다. 사용자 확정대로 같은 FpsStation 맵·튜토리얼 모드 분리·승객 NPC 없음이므로, 분대 상태·생존자 수·킬피드·탄약 등은 튜토리얼 HUD에서 제외한다.
5. 확인하지 못한 항목: PUBG/Fortnite/Apex의 최신 게임 빌드 화면과 CHOOGuard 실기 HUD, My Summer Car의 손 아이콘/현재 키 프롬프트, CHOOGuard 입력 바인딩·해상도별 가독성, OSS 후보의 현 프로젝트 결합 및 빌드 호환성. 이 조사는 이를 테스트하지 않는다.

## 1. 레퍼런스에서 실제로 확인한 HUD 문법

| 레퍼런스 | 직접 확인한 증거 | 사용할 수 있는 관찰 포인트 | 제한/주의 |
|---|---|---|---|
| PUBG | 공식 업데이트 32.1의 문장형 라디오 휠/핑 의미 분리는 `[G14]`; 공식 업데이트 32.2는 훈련에서도 핑·라디오를 쓸 수 있고 사용 불가 조건 메시지를 명확히 했다고 기록 `[G15]`. 구버전 Steam 커뮤니티 스크린샷 `[H01]`에는 상단 나침반, 우상단 경기 상태/이벤트 피드, 화면 하단의 팀 상태·무장 정보, 우하단 미니맵, 중앙 하단의 `Reviving` 진행 원형과 취소 키가 보임. | `[추정]` HUD의 기본 정보는 가장자리에 두고 중심은 시선/현재 작업에 남긴다. 위치 표시(ping), 문장형 요청/응답(radio), 행동 진행/취소를 서로 다른 정보 유형으로 유지한다. 조준 대상과 진행 중 행동이 있을 때만 상호작용 프롬프트를 보이게 한다. | `[H01]`은 Steam 이용자 게시물의 과거 캡처이며 판본·캡처일 미확인. 현재 PUBG 레이아웃으로 단정 불가. PUBG 공식 이미지 URL 중 일부는 이번에 HTTP 오류로 직접 확인하지 못했으므로 `[G14]`에서 직접 열람한 이미지 이상을 새로 주장하지 않는다. 킬/생존/무기/팀 HUD는 전투 장르 기능이지 역무원 업무 UI의 요구사항이 아니다. |
| Apex Legends | 2019년 RPS 안내 `[H05]`는 탭/두 번 탭/홀드 핑, 대상 문맥에 따른 오브젝트 핑, 인벤토리 자원 핑, 취소/응답 흐름을 설명한다. | `[추정]` 지원요청/표지 체계가 필요할 경우 행동을 ‘위치 표시’와 ‘요청’과 ‘응답 확인’으로 나누고, 대상 문맥에 따라 선택지를 좁히는 패턴을 비교한다. | 2차 매체의 2019년 안내이며 공식 EA 원문 UI 문서가 아니다. 해당 입력·메뉴가 현재 버전에서도 동일한지 미확인. 승객 NPC 없는 튜토리얼에서 핑/동료 응답 HUD를 먼저 둘 근거로 쓰지 않는다. |
| Fortnite | Epic의 v5.40 공식 패치 노트 검색 스니펫은 플레이어·분대 체력 바를 Custom HUD Layout에서 이동 가능하다고 표시 `[H06]`. | `[추정]` 자막/상태 영역 위치를 이용자가 조정할 수 있는 설정은 접근성·화면비 차이에 도움 될 수 있으므로 후속 프로토타입에서 검토할 수 있다. | 원문 페이지는 HTTP 403으로 열리지 않아 **S(검색 스니펫)** 수준. 버전 표기 `v5.40`; 공지일은 이 조회로 미확인. 조정 가능한 항목·현재 지원 여부·화면을 확인했다고 쓰지 않는다. |
| My Summer Car | 공식 스크린샷 `[G01]`의 좌상단에는 작은 생존 상태 문자열/바, `[G01]`의 엔진룸 스크린샷(공식 이미지 `https://www.amistech.com/msc/game/02.jpg`) 중앙에는 시선 대상 이름 `TWIN CARBURATORS`가 보인다. Gamepressure 조작표 `[G03]`는 잡기·놓기·회전·활성화·손/도구 모드의 행위 범주를 설명한다. | `[추정]` 플레이어 시야 중앙에는 바라보는 물체를 식별하는 짧은 이름을 두고, 절차 정보는 주변으로 밀어 상호작용 대상의 실제 형상과 작업면이 남도록 비교한다. 소화기 번호와 현재 관찰항목을 대상 옆에 표시하는 방식을 테스트할 가치가 있다. | 공식 사이트/이미지는 현행 빌드 화면 설명서가 아니다. 제공된 정지 이미지에서 손 모양 상호작용 아이콘, 프롬프트의 동작 시점, 현재 키 표시를 관찰하지 못했다. `[G03]`의 2019 조작표는 오래된 2차 자료다. |
| Train Sim World 5 Conductor Mode | 공식 검표 이미지 및 설명 `[G06]`은 왼쪽 정차역/날짜, 오른쪽 표 정보, 아래 판단 선택을 분리한다. Dovetail 지원 안내 `[H08]`는 운행을 선택해 처음 몇 정거장을 안내받는 흐름과 HUD & Gameplay에서 instructor를 끄는 선택지를 설명한다. | `[추정]` 긴 절차를 한 화면에 전부 띄우기보다 `대상 증거 확인 → 판단/기록`을 다른 인터랙션 구간으로 표현하고, 안내 보조를 끌 수 있는 모드는 옵션 후보로 본다. | 영국 노선의 승무원 업무·승차권 판단 기준은 코레일 규정이 아니다. 튜토리얼의 기능/콘텐츠 인증 근거가 아니라 상용 게임 UX 비교다. |
| PowerWash Simulator | 개발사 설명의 작업 완료 DING 및 도움말 개발일지의 잔여 오염 강조 색/지속시간은 `[G09]`, `[G10]`에 기록되어 있다. | `[추정]` 작업 대상 자체에 주의를 되돌리는 국소 강조와 짧은 완료 피드백을 비교한다. 전역 배너보다 누락한 확인 위치를 명확히 알려주는 도움말이 소화기 점검 대상에 적합할 수 있다. | `[G10]`의 GIF는 실제 프레임을 관찰하지 않은 것으로 기록되어 있다. 이번 보고서도 색상·지속시간을 신규 빌드에서 확인하지 않았다. |
| Sudden Attack (비교 한계) | 공식 HUD 캡처 `[G17]`는 상단/모서리별 레이더·상황/장비 정보 분리를 확인한다. | `[추정]` 모서리 배치의 공간적 일관성을 비교하는 예시. | 전투 보상/상황 로그의 밀도는 비상대응 업무에 옮기지 않는다. 상세는 `[G17]`에 이미 기록되어 중복 서술하지 않는다. |

### HUD 요소별 배치·조건 가설

아래는 화면을 직접 검증한 배치표가 아니라 **위 레퍼런스를 CHOOGuard 맵/레슨에 옮긴 [추정]**이다. 게임 내 실제 폰트 크기, 안전 여백, 위치 비율을 확정하지 않는다.

| 정보 | 제안 배치/노출 조건 | 첫 레슨에서의 의미 |
|---|---|---|
| 레슨/진행 상태 | 좌상단의 작고 고정된 카드. `소화기 점검`과 `n/8`, 현재 단계 한 줄만 항상 보이고 세부근거는 열었을 때만 표시. | 기존 8단계 코드의 진행 가시성을 살리되 화면을 체크리스트로 덮지 않는다. 순서·근거·실제 단계 내용은 기존 코드/철도 자료를 기준으로 별도 검토하며 이 UI 조사는 이를 재확정하지 않는다. |
| 조준점/대상 이름 | 화면 중앙의 작은 조준점. 조작 가능한 대상에 가까이 조준했을 때만 이름, 고유번호(`BSN-CONC-FE-001~012`), 해당 상태에서 가능한 동작을 인접 표시. | 소화기 외관·지시압력계·제원표처럼 현재 관찰할 대상이 바뀌면 프롬프트 내용도 상태/대상에 맞게 변한다. 정확한 상호작용 키는 CHOOGuard의 입력 설정 확인 전 표기하지 말고, 우선 `상호작용` 의미 레이블로 설계한다. |
| 오래 걸리는 조작 진행 | 진행 중인 동작이 있을 때만 중앙 대상 가까이에 링/바와 취소 안내. 짧은 탭 검사에 불필요한 타이머는 띄우지 않음. | `[추정]` PUBG 캡처의 진행/취소 문법을 시간 소모가 실제 존재하는 상호작용에만 차용한다. 검사 동작을 인위적으로 지연시켜서는 안 된다. |
| 판정/기록 피드백 | 현재 작업 카드 근처에 한 줄 결과를 잠시 표시하고, 완료/보류/인계 의미는 색 외에 텍스트·아이콘으로도 구분. | `확인됨`, `기록 필요`, `교체요구 인계` 같은 상태 표현 후보. 실제 판정 로직/문구는 현장 기준 검토 전 확정하지 않는다. |
| 방향/지도 | 첫 소화기가 현 위치에서 보이지 않거나 다음 장소로 이동할 때만 간단한 방향 도움/월드 표식을 제공. 별도 미니맵은 필요성이 확인된 경우에만 검토. | 현재 맵의 2F 맞이방·역무실·게이트·안전설비 표지 등 재사용 가능한 표식이 있다. `[추정]` 한 장소에서 수행하는 점검에는 상시 미니맵이 불필요할 수 있으나 실제 동선이 확정되지 않아 미결정. |
| 알림/통신 | 조작 피드백(개인 결과)과 타인에게 보낼 요청/인계 통신을 서로 다른 위치/표현으로 구분. | 튜토리얼은 승객 NPC가 없으므로 승객 수·분대 슬롯·시민 응답 표시를 렌더링하지 않는다. 나중의 NPC 대응 모드에서 `[G14]`의 핑/발화 분리를 별도 검토한다. |
| 문서/기준 열람 | 점검표·절차 상세·출처는 필요할 때 여는 단일 보조 화면으로 묶고, 작업 중 자동 상시 노출하지 않음. 닫으면 같은 1인칭 맵으로 복귀. | 사용자는 튜토리얼을 본게임과 같은 `FpsStation`에서 모드만 분리하고 NPC 없이 시작하도록 확정했다(2026-09-26 사용자 제공). 따라서 문서가 전체 화면이 되더라도 타이틀/별도 훈련장으로 이동하지 않는 흐름과 비교한다. |

**접근성 비교 기준:** 아이콘/색만으로 상태를 의미화하지 않고 짧은 텍스트도 붙이는 방안은 WCAG 2.2의 색상만으로 정보 전달 금지(1.4.1) 및 명도 대비 원칙(1.4.3)을 참고한 **[추정]**이다. WCAG는 웹 콘텐츠 표준이며 Unity 게임 준수/인증 결과를 뜻하지 않는다. UI 확대, 고대비 모드, 입력장치별 키 표기는 현재 프로젝트에 있는지 조사하지 않았다. [H13]

## 2. 공개 Unity 코드/OSS 후보

| 후보·확인한 경로 | 확인된 기능과 한계 | 라이선스·적합성 |
|---|---|---|
| **Unity-UI-Extensions** `Runtime/Scripts/Layout/RadialLayout.cs` | 패키지 `package.json`은 `3.0.0`, Unity `6000.0`, uGUI `2.0.0` 의존성을 표시. `RadialLayout`은 자식 RectTransform을 각도로 원형 배치하는 `LayoutGroup`이다. 메뉴 항목 선택·키보드/마우스 포커스·닫기/취소·문맥에 따른 항목 활성화까지 구현하는 완성형 방사형 메뉴가 아니다. | 패키지 `LICENSE.md`/manifest는 BSD-3-Clause로 표기하지만 이 개별 `RadialLayout.cs` 파일 머리말에는 Danny Goodayle의 2015 MIT 문구가 있다. 파일별 라이선스 표기를 유지하고 실제 사용 파일의 고지/기여자 정보를 다시 확인할 것. 패키지 릴리스 공개일은 미확인. Unity 6000.3.23f1/URP 17.3.0/Input System 1.20.0 조합은 검증하지 않았다. [H10] |
| **Unity-FirstPersonInteractionToolkit** `Assets/Scripts/Interact.cs`, `Assets/Scripts/InspectNote.cs`, 애니메이션 `An_InteractTextPopup`/`An_InteractTextPopout` | `Interact.cs`는 시선 Raycast → 상호작용 태그/거리 조건 → 대상 이름/문구 표시 및 진입/이탈 애니메이션 → Interact/Squint 메시지 호출 구조를 보인다. `InspectNote.cs`는 시선 대상 피드백과 중앙 대형 이미지 읽기 패널, 읽는 동안 플레이어 이동/시선을 멈추는 예를 보인다. | 저장소 README는 Unity 2022.3.19f1 지원, MPL 2.0. 실제 코드가 `UnityEngine.UI.Text`와 레거시 `Input.GetButtonDown`를 사용하므로 TMP/Input System 프로젝트에 그대로 임포트하는 샘플로 보지 않는다. 상호작용 개념/씬 흐름의 참고 후보이며 전체 FPS 컨트롤러·아트·사운드 재사용은 별도 범위다. Unity 6/현 프로젝트 호환성 미검증. [H09] |
| **Unity FPS Sample** (역사적 비교) | 선행 기술 조사 `[FPTS §3 #12, §9]`에 공식 저장소의 Unity 2018.3/HDRP 및 유지보수 중단 상태, Unity Companion License가 기록되어 있다. 이번 HUD 조사에서는 현 uGUI에 바로 옮길 UI 파일/컴포넌트 경로를 추가로 확인하지 않았다. | Unity Companion License는 일반 MIT/BSD/Apache처럼 엔진 비종속으로 취급할 수 없다. 현재 URP 기반 프로젝트에 UI만 떼어오기보다 역사적 참고로 제한. 정확한 적용성·경로 미확인. |

**코드 재사용 결론 [추정]:** 우선 새 컨벤션이나 UI 패키지 도입을 전제하지 않는다. 현재 uGUI/TMP에서 `Image.fillAmount` 형태의 원형 진행 표시, 기존 reticle 주위의 조건부 프롬프트, 현재 단계 텍스트를 작은 자체 컴포넌트로 구성하는 방향이 단순하다. 이 문서는 실제 코드를 복사하지 않았고, `RadialLayout`을 레슨 메뉴 전체로 오인하지 않도록 구분했다. BSD/MIT/MPL/Unity Companion 표기는 파일별 원문/의존성 고지의 대체가 아니며 법률 자문도 아니다.

## 3. 미확인·접근 제한과 후속 결정 경계

- **화면·버전:** PUBG HUD 이미지의 캡처 버전·날짜 미확인 `[H01]`; Fortnite는 검색 스니펫만 `[H06]`; Apex 자료는 2019년 2차 매체 `[H05]`. 현행 게임의 배치/사용성으로 일반화할 수 없다.
- **손 아이콘:** My Summer Car의 공식 정지 이미지에서는 중심 대상 이름은 보이나 손 아이콘/키 안내는 확인되지 않았다. 이를 “My Summer Car 표준 프롬프트”로 제시하지 않는다.
- **CHOOGuard 런타임:** 현재 HUD 위치·입력 바인딩·Canvas 해상도 대응·TMP 폰트 설정·작업 중 취소 가능 여부·소화기 점검 실제 동선은 이 조사에서 실행/검증하지 않았다. 소스 후보도 프로젝트에 설치하거나 빌드하지 않았다.
- **교육 기준:** 이 문서는 사용자 제공의 기존 8단계 튜토리얼 범위를 UX 예시로 사용했을 뿐, 한국철도공사 실제 사원 교육과 매뉴얼 내용/승인 절차를 별도 검증하거나 튜토리얼의 법정교육 효력을 주장하지 않는다. 그 조사 범위는 안전/정비 매뉴얼 선행 보고서 및 해당 조사 담당에서 다룬다.
- **접근 제한:** Epic/Fortnite 패치 원문은 403, 따라서 검색 스니펫만 인용. 인증·로그인·유료벽 우회는 하지 않았다. 미확인/오류는 부재 증거가 아니다.

## 4. 새로 열람한 출처 장부

기존 게임 근거 `[G01, G03, G06, G09, G10, G14, G15, G17]`는 선행 `GAMEPLAY_VISUAL_REFERENCES.md`의 같은 ID에 URL·기관·발행일/판본·열람 깊이가 있다. 엔진/라이선스의 FPS Sample 배경은 `FPS_TECH_SOURCES.md`에서 `[FPTS §3 #12, §9]`로 참조한다.

- **[H01] PUBG HUD 과거 커뮤니티 캡처** — Steam Community 이용자 캡처, 게시일/게임 판본 미표기; 스크린샷 페이지와 이미지 직접 관찰(**A**): [게시물](https://steamcommunity.com/sharedfiles/filedetails/?id=1407477753), [이미지](https://images.steamusercontent.com/ugc/924808427806849985/BB3530E37B31D14C8499077683085B4038F49768/?imw=1024&imh=576&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true). 시각 배치만 참고, 최신판 보증 아님.
- **[H02] PUBG 공식 Support — 아이템 버리기** — PUBG Support, 2017-12-27 수정; 공개 본문 직접 확인(**A**): [How do I drop items?](https://support.pubg.com/hc/en-us/articles/115004201094-How-do-I-drop-items). TAB/I 인벤토리 및 드래그/버리기 안내. 오래된 조작 설명이며 CHOOGuard 키 바인딩 근거 아님.
- **[H03] PUBG 공식 Update 30.2** — KRAFTON/PUBG, 2024-07-10; 패치 본문 직접 확인(**A**): [Update 30.2](https://pubg.com/en/news/7494). 근처 아이템과 인벤토리 UX 개선 설명; 해당 패치의 전체 시각 레이아웃을 직접 확인했다는 뜻은 아님.
- **[H04] PUBG 조작표** — PUBG Wiki.gg(비공식 커뮤니티 위키), 날짜/게임 판본 미표기; 페이지 본문 직접 확인(**A**): [Controls](https://pubg.wiki.gg/wiki/Controls). 독립 위키의 구판/기본 키 설명이므로 현재 공식 입력표로 취급하지 않음.
- **[H05] Apex 핑 안내** — Rock Paper Shotgun, 2019-03-18; 공개 본문 직접 확인(**A**): [Apex Legends ping guide](https://www.rockpapershotgun.com/apex-legends-ping-guide-ping-system-apex-legends-ping-menu-advanced-tips-2). 2차 자료; 이미지 화면은 직접 검토하지 않음.
- **[H06] Fortnite v5.40 Patch Notes** — Epic Games, 게임 버전 `v5.40`, 게시일 이 조회에서 미확인. 페이지 접근 403, 검색 결과 스니펫만 확인(**S**): [공식 패치노트](https://www.fortnite.com/patch-notes/v5-40). 커스텀 HUD에서 플레이어/분대 체력 바 이동 가능 문구만 근거로 사용.
- **[H08] Train Sim World Conductor Mode 도움말** — Dovetail Games Support, 2026-04-22; 본문 직접 확인(**A**): [How do I play Conductor Mode?](https://support.dovetailgames.com/hc/en-us/articles/29235998868370-How-do-I-play-Conductor-Mode). 튜토리얼/인스트럭터/업무 UX 참조. 기존 직접 검표 이미지·상용게임 맥락은 `[G06]` 참고.
- **[H09] Unity First Person Interaction Toolkit** — Steven Harmon, GitHub `main` README와 실제 C# 파일 열람, 저장소 릴리스/갱신일 미표기; **A**: [저장소](https://github.com/stevenharmongames/Unity-FirstPersonInteractionToolkit), [README](https://raw.githubusercontent.com/stevenharmongames/Unity-FirstPersonInteractionToolkit/main/README.md), [Interact.cs](https://raw.githubusercontent.com/stevenharmongames/Unity-FirstPersonInteractionToolkit/main/Assets/Scripts/Interact.cs), [InspectNote.cs](https://raw.githubusercontent.com/stevenharmongames/Unity-FirstPersonInteractionToolkit/main/Assets/Scripts/InspectNote.cs), [LICENSE](https://raw.githubusercontent.com/stevenharmongames/Unity-FirstPersonInteractionToolkit/main/LICENSE). README가 Unity 2022.3.19f1 및 MPL-2.0 명시.
- **[H10] Unity UI Extensions** — Unity UI Extensions 커뮤니티, `release` 브랜치의 manifest 버전 3.0.0/Unity 6000.0; 패키지 날짜 미표기; **A**: [package.json](https://raw.githubusercontent.com/Unity-UI-Extensions/com.unity.uiextensions/release/package.json), [RadialLayout.cs](https://raw.githubusercontent.com/Unity-UI-Extensions/com.unity.uiextensions/release/Runtime/Scripts/Layout/RadialLayout.cs), [LICENSE.md](https://raw.githubusercontent.com/Unity-UI-Extensions/com.unity.uiextensions/release/LICENSE.md). 패키지 BSD-3-Clause와 개별 소스 머리말 MIT 표기 차이를 그대로 기록.
- **[H13] WCAG 2.2** — W3C Recommendation, 최종판 2024-12-12; 공식 표준 본문/메타데이터 확인(**A**): [WCAG 2.2](https://www.w3.org/TR/WCAG22/). 웹 콘텐츠 지침을 게임 HUD의 접근성 설계 비교원칙으로만 참고; 게임 준수 인증 아님.
