---
title: 'Foundation 영상 표본의 순서·시각·출처 보존'
type: 'bugfix'
created: '2026-09-07'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**문제:** 자료 확보 우선이라는 인수인계에 따라 로컬 영상 표본을 후속 복원 입력 검토에 사용할 수 있어야 한다. 현재 `scripts/references/sample_video_frames.py`는 같은 출력 폴더를 덮어쓰고 반올림 시각이 겹치는 표본을 같은 JPEG에 기록한다. 잘못된 입력이나 중간 실패로 파일과 영수증의 결속이 깨질 수 있다.

**접근:** 표본 추출 한 경로만 보강한다. 유한한 시각·개수·크기를 API와 CLI에서 검사하고, 요청 순서를 바꾸지 않으며 이름 충돌을 거부한다. 새 출력만 허용하고 모든 프레임과 원본 해시를 검증한 뒤에만 성공 영수증을 작성한다. 실제 FFmpeg 합성 영상과 기존 로컬 영상으로 원본 PTS와 파일 해시를 대조한다. 시간 기준과 미검증 범위를 인수인계/context graph에 기록한다. 영상·프레임은 외부 모델로 전송하지 않으며 다운로드·의존성 설치·Unity 장면·훈련 절차·충돌 경계·측량 변환·원격 Git 작업은 변경하지 않는다.

</frozen-after-approval>

## Implementation Notes

- 경로 판정: 사용자 판단이 필요한 의도 공백 없음, 외부 부작용/기존 자료 삭제 없음. 추출기 한 파일과 회귀 시험, 사용/문맥 기록의 작고 되돌릴 수 있는 변경이므로 oneshot.
- 사용자는 기존 미커밋 변경의 보존을 필수로 요구하지 않는다. 이 단위와 무관한 구조물 저작·셰이더·PDF 변경은 일괄 정리하지 않는다.
- 출처: `docs/choo-guard-foundation-handoff.md`, `docs/context/accepted-decisions.md`, `.planning/2026-09-07-facility-source-collection/task_plan.md`. 역사적 영수증이나 다운로드 성공을 현행 공간/시각/사생활/실측 승인으로 승격하지 않는다.
- 조사: `sample()`에서 `exist_ok=True`와 FFmpeg `-y`를 사용하고, `round(seconds*1000)`으로 같은 파일명이 생기며, 개수/폭 검사는 `main()`에만 있다. 관련 경계 회귀 시험부터 작성한다. 기존 `samples.json` 핵심 필드는 유지한다.
- 2026-09-07 사용자 재지시로 이 소규모 수정은 보류한다. 실제 현장 사진·영상·건축 도면 확보와 최신 실행 가능한 CV 복원 파이프라인이 우선이다. `test_sample_video_frames.py`만 작성했으며 아직 실행하지 않았고 추출기 구현은 변경하지 않았다. 이 문서의 완료나 RED/GREEN을 주장하지 않는다.

## Code Map

- `scripts/references/sample_video_frames.py`: 기존 추출기, 이번 세션 구현 변경 없음.
- `scripts/references/test_sample_video_frames.py`: 새 회귀 시험 초안, 미실행·보류.
- `.planning/2026-09-07-facility-source-collection/task_plan.md`: 자료 확보 우선의 후속 출처.

## Open Questions

- 이 수정의 재개 시점은 실제 자료 확보와 복원 파일럿의 우선순위가 정해진 뒤 결정한다. 현재 작업을 막는 선행 조건으로 사용하지 않는다.
