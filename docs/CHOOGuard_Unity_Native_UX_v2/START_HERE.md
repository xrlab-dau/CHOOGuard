# CHOOGuard Unity Native UX v2

**기준 제품은 Unity PC다. 웹 실행물을 배포하거나 임베딩하지 않는다.**

1. `UNITY_UX_SPEC.md` §1–5에서 플랫폼, 화면, 입력 소유권을 읽는다.
2. `contracts/native-ux.json`에서 담당 Sxx와 native 구조를 찾는다.
3. `unity/Assets/CHOOGuardUXNativeV2/`는 uGUI/TMP **레이아웃 생성기 소스**다. 게임·물리·기관 코어가 아니다.
4. `contracts/unity-acceptance.json`은 앞으로 Editor/Player에서 실행할 시험이다. 현재 모두 NOT_RUN.

## 지금 있는 것

Native UI/UX 상세 설계, C# 씬·프리팹 생성기와 화면 전환기 소스, 12개 surface 계약, 원문, 정적 패키지 검사.

## 아직 없는 것

이 환경에서 만든 Unity Scene/Prefab 결과, Unity 컴파일·Game View 캡처·Player 빌드, 실제 카메라/박스 선택/minimap/운영 명령 연동. 브라우저 테스트 결과는 승계하지 않는다.

## 실행 진입

별도 Unity 6000.3 시험 프로젝트에서 uGUI/TMP와 한글 TMP font를 준비한 뒤 `unity/Assets/CHOOGuardUXNativeV2`를 Assets에 넣는다. `Tools > CHOOGuard > UX > Create Native Preview`에서 폰트 지정 후 새 씬을 생성한다. 원본 씬을 덮어쓰지 않는다. 패키지·폰트는 자동 설치하지 않는다.

기본 입력 모듈은 설치된 Input System을 사용할 수 있으면 그 모듈, 그렇지 않으면 Legacy 모듈로 생성된다. 최종 제품의 Operations/Camera/Authoring 입력맵과 월드 입력 중재는 EP04 구현·검수 대상이다.

## LLM 반환

`screenId / source ref / changed paths / native input ownership / actual Unity tests / failed or not-run / next consumer`.

문서 품질·소스 정적 검사·Unity 실행·운영 엔진 검증·기관 수용은 별도다. 새 AAA 판정을 발급하지 않는다.
