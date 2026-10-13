# 승무원 열차점검 자료 조사 · Issue #281

## 재사용

- 열차/객실: 기존 `ktx-single-10car-left-open.fbx`의 2호차. `FpsStation`의 실제 메시 경계와 `station-points.json`의 통로·문 위치/열림 변위를 조사했다. 원본 FBX/역사 씬을 변경하지 않고 별도 씬에 인스턴스를 배치한다.
- 장비: 기존 객실 소화기 수납함과 출입문 옆 패널의 텍스처·기하, `CeilingSpeaker` 프리팹. 수납함·문 옆 패널은 실제 렌더링과 레이 충돌 위치를 대조해 점검 표면을 배치했다. `ShutterControl` 후보는 방화셔터 인쇄가 문 교육과 혼동되므로 최종 씬에서는 사용하지 않는다. 실제 KTX의 내부 취급 절차를 재현한다고 주장하지 않는다.
- 소품: 기존 Objaverse `MedicalBag`, `Suitcase` 모델. 기존 머티리얼/출처를 유지한다.
- 소리: 기존 인터페이스 확인음과 문 열림/닫힘 음원. 최초 검수본은 무전 확인 문장을 자막으로 보여주었다. 2026-10-12 플레이 피드백에서는 오프라인 교육·교신 음성을 추가했다. 생성 문장과 출처는 프로젝트 루트 기준 `asset-library/research-public/2026-10-12/tutorial-feedback/`에 기록한다.
- UI: 기존 FPS uGUI/TMP 공통 생성 함수와 한국어 폰트.

## 휴대 무전기 조사

프로젝트 장비 프리팹, 응급 대응자 모델, KTX 메시 경계와 내부 장비 이름을 확인했으나 독립적으로 사용할 휴대 무전기를 찾지 못했다.

공개 검색: Kenney/Quaternius의 `radio`, `walkie talkie`, Sketchfab의 `low poly walkie talkie`, GitHub의 `walkie talkie model`, Objaverse/HuggingFace의 `walkie talkie`, Poly Haven의 `radio` 후보를 확인했다. Kenney Survival Kit(CC0)을 내려받아 파일 목록을 확인했으나 휴대 무전기가 없었다. Sketchfab 후보 `ab6fdc5f5d534c12add11c3e68ebbf9f`(CC BY)은 다운로드를 얻지 못했다. 무관한 생존 장비를 게임에 추가하지 않았다.

사용 가능한 코드 자산으로 **musehq/spacesvr**의 `TalkieModel.tsx` 및 `walkie.ts`를 선택했다. 본체와 안테나의 비율·부품 구성을 참고해 Unity 메시 생성으로 옮겼다. Three.js 코드나 GLSL을 게임 런타임에서 실행하지 않는다. Unity용 외형은 모서리를 다듬은 압출 메시이며, 이 저장소의 기존 열차 모델을 원시 도형으로 대체하지 않는다.

원본 두 파일과 MIT 라이선스를 함께 보관한다. 정확한 URL, Git blob, 취득 시각, SHA256, 크기는 `receipts.json`에 기록했다.

## 교육 범위

참고 영상: https://www.youtube.com/watch?v=4LuRzOOyOO8

영상의 통신/객실 점검 및 승하차 출입문 업무를 짧은 첫 학습으로 재구성한다. 열차팀장 업무 일부를 포함하며 실제 내부 규정이나 자격 교육을 재현하지 않는다. 문 주변의 짐은 영상에서 실제로 발생한 사고를 인용한 것이 아니라, 사용자가 승인한 안내형 교육 사례다. 기계 수리·임의 강제 닫힘을 가르치지 않는다.
