# 2026-09-08 작업 단위와 전달 상태

이 기록은 진행 이력과 소스 경계다. 현재 원격 head/CI·게시 권한은 별도 확인하며 이전 실패를 성공으로 바꾸지 않는다.

## 현재 작업 소유

- **로컬 L-CV:** 현재 세션에서 sequence spec CV-01~06 구현을 계속한다. 기존 엔진/입력 복구 결과는 재사용하며 새 SOTA 조사부터 반복하지 않는다.
- **학교:** S-ART-01 고해상도 베이크/검수 렌더, S-BENCH-01 대용량 데이터·모델 캐시/독립 upstream GPU benchmark, S-WIN-01 Windows 빌드/성능 검증. 아직 실행 전이며 [독립 작업 계약](school-pc-handoff-2026-09-08.md)에 입력/출력/브랜치 경계를 기록했다.
- VARCO는220크레딧 시제품 이후 사용자 비용 판단으로 폐기했다. 추가 생성/과금/사운드 가입은 하지 않는다.

## 커밋 단위

| ID | 소스 범위 | 현재 구현/검증의 의미 | 커밋 |
|---|---|---|---|
| WU-01 | 고정 BMAD/SpecKit/Pi 리소스·라이선스·프로필 parser 회귀·개인 자료 제외 | 기존 도구 소스의 공유 및 native 도구 이름 파싱 오류 수정. 모든 학교 role/model 실행 검증은 아님 | `822b7ab192395c63086d25a6f4d483f581a824dc` |
| WU-02 | `scripts/references`, 시설 범위와 공식 출처/취득 메타데이터 | 영상4개·사진13개 기록 복구, 공식28파일 취득 이력. 원본/실측/직원 SOP는 별도 | `ad574e635e476cde6b0dfee7c123514b55554f41` |
| WU-03 | DA3/SfM/MapAnything 실행·고정 의존성·provenance/export 회귀 | 두 연속4장 구간 engine-stage 완료, 독립 SfM0/4 미등록. 새 CV 후처리/전달은 미완료 | `f3efd029d277a1ef2f7e1a262baae9cc4c611f3f` |
| WU-04 | 구조 관측 수학·한 접합부 authored study·Unity review shading/navigation/시험 | 합성 구조fixture 및 기존2사진 검토 경로 검증. 전체 시설/새 시퀀스/훈련 collision 수용 아님 | `4a2916e8adcbe097af5692fa40ddd36ff451a904` |
| WU-05 | CV 연구·spec 재개·VARCO 폐기 이력·실행/검토 근거·사용자 결정 | 문헌/가용성 확인과 계획·실제 실행을 구분. raw 논문/계정/모델 없음 | `13aa552a1de1e404731f2fbace64f1dd256488d9` |
| WU-06 | context graph·HTML·학교 독립 작업·본 ledger/시작 문서 | 로컬과 학교의 소유 분리 및 공유 가능한 진행 상태 | 이 문서의 Git 이력으로 식별 |

## 보존/제외

원본 영상/사진·raw camera/depth·가중치·VARCO 파일·계정/서명 URL·대화 HTML·`.planning` 원시 로그/캡처/일회성 scratch·개인 installer 답변·다른 locked worktree는 게시하지 않는다. 삭제하지 않고 로컬/개인 복구 snapshot에 보존했다. 연구 논문 전체 텍스트/API 원문은 `imports/`에서 제외하고 출처 링크·정제된 metadata/분석만 공유한다.

Unity 검증 중 기존16개 재질에 `_EMISSION` 키워드가 직렬화된 테스트 부수효과를 diff에서 확인했다. 변경 내용을 보존한 뒤 검증 전 사본/HEAD와 byte 일치함을 확인하고 **그16파일만 원상 복원**했다. YAML 필드를 수동 편집하지 않았고 의도하지 않은 재질 변경은 커밋에서 제외했다.

기존 `docs/korail/CHOOGuard_KORAIL_개발질의서_A4_1p.pdf` 삭제는 이 작업 전에 존재했고 의도를 확인하지 못했다. 문서 링크가 남아 있으므로 **해당 삭제는 커밋하지 않고 작업트리 그대로 보존**한다. 원격/새 checkout에는 기존 PDF가 남는다.

## 검증과 제한

[공개 검증 receipt](../evidence/foundation/2026-09-08-publication-validation.json)에 소스 hash, Python168·Node7, Ruff/LFS, Unity117통과/1skip·구조fixture12·PlayMode16, 초기 legacy receipt 실패 및 native 검증자 도구 설정 실패/수정/재시도를 기록했다. 재시도 검토는 중요 소스의 표본 검토이며 신규 P0/P1을 찾지 못했다. 전체 기능/현장/학교/HMD 승인을 뜻하지 않는다.

공개 후보 전체를 임시 index로 검사했을 때 repository policy는834파일/위반0, 자격증명·임베디드 이미지·개인 경로·서명 query 문자열 검사도0이었다. 전체 `git diff --check`에는 기존 BMAD 템플릿3개의 공백과 Unity가 생성한 shader `.meta`의 빈 값 공백 경고14개가 있다. 원문/자동 생성 YAML을 보존하며 이를 기능 검사 PASS로 숨기지 않는다. 구현 소스는 Ruff 및 관련 시험을 통과했다.

원래 sequence spec의 baseline은 `b25413a06ec1943e180219241aad8880c9003f5d`로 유지한다. 이번 소스 기록/푸시와 CV-01~06의 실제 완료를 구분한다.
