# Foundation 소스 전달과 개발 시작

`feature/foundation-starter`의 `develop` 대상 PR로 코어·Unity 호스트·Blender 자산·상시 현장 시뮬레이션·생성 장면·문서·코레일 질의서를 전달한다. PR과 현재 원격 커밋은 GitHub #54/#60에서 확인한다. 병합 전에는 해당 feature 브랜치를 사용한다.

## 새 checkout

```sh
git clone --branch feature/foundation-starter https://github.com/xrlab-dau/CHOOGuard.git
cd CHOOGuard
git lfs install --local
git lfs pull
python3 scripts/context/context_graph.py validate
python3 scripts/context/context_graph.py brief --topic handoff --machine local
python3 scripts/dev/check_foundation.py
```

Git LFS 실제 파일 27개(26 FBX와 Blender 원본)가 필요하다. `git lfs fsck`로 확인한다. 이 절차는 저장소 접근 권한이 있는 개발 환경용이며 제한된 기관망 반입 조건은 코레일 회신을 따른다.

Unity Hub에서 저장소 루트를 **6000.3.23f1**로 열고 컴파일 후 `CHOOguard > Foundation > Build Playable Demo`를 실행한다. `FoundationDemo.unity`에서 Play → 임시 직무 → 현장 입장을 누른다. NPC의 평상시 이동 중 예고 없는 상태 변화가 발생하며, 현장 정보에 따라 대응·직접 인솔·복기를 진행한다.

Test Runner에서 Foundation EditMode와 PlayMode를 실행한다. PlayMode 시험 전 생성기를 실행해 최신 장면을 준비한다. `scripts/dev/check_foundation.py`는 Python 데이터 검사만 실행하며 Unity 성공을 주장하지 않는다. Mac 실행본은 `Build Mac Player`, Windows는 학교 PC의 설치된 Windows 모듈과 `Build Desktop Player`를 사용한다. Windows/VR/HMD 실기와 실제 시설/매뉴얼 대조는 별도 검증이다.

## 전달 범위

- 코어: 임시 5직무 계약, 상태/피드백, 가상 팀 알림, canonical fixture와 Python/Unity 시험.
- 호스트: Unity 정확 버전·패키지 lock·ProjectSettings. 사용자 절대 경로 의존 없음.
- 자산: 공개 사진을 참고한 소재별 표면과 곡면 법선의 Blender FBX, Blender 원본과 재생성 스크립트. 원본의 저장된 개인 폴더 경로 제거.
- 게임: 상시 운영, 두 사건 유형과 세 위치, 관측/조건부 대응, 직접 인솔·복기·복구와 기존 회귀 흐름.
- 장면: Editor API로 생성한 장면·17종 prefab·재질·시나리오 사본.
- 질의: [코레일 A4 1쪽 Word 질문지](korail/CHOOGuard_KORAIL_개발질의서_A4_1p.docx), 원문·검수 기록. 전송/회신은 작성과 별도다.

과거 검증 JSON의 `sourcePushed: false`는 당시 상태로 보존한다. 최신 전달은 PR의 실제 head와 CI를 기준으로 확인하며, 자동 시험·같은 제공자 코드 검토를 현업 승인이나 현장 효과 검증으로 바꾸지 않는다.

현재 기본 경험·구현: [상시 현장 훈련](choo-guard-open-world-training.md). 최신 단위는 [역사 레퍼런스·context graph 실행 기록](evidence/foundation/2026-09-07-reference-realism-context.json)과 [문맥 지도](context/README.md)에서 확인한다. Unity EditMode 102/102·PlayMode 16/16, 문맥 Python 15개 검사를 수행했다.

[이전 상시 현장 영수증](evidence/foundation/2026-09-07-open-world.json)과 [이전 게시 준비 기록](evidence/foundation/2026-09-07-publication-review.json)은 당시 소스의 이력이다. 최신 아트 변경 후 과거 해시와 차이가 나는 부분은 context graph가 표시한다.
