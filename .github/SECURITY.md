# 보안 정책

## 취약점 신고

공개 이슈에 올리지 말고 GitHub 비공개 취약점 신고를 이용해 주세요.
저장소 **Security → Report a vulnerability**
(<https://github.com/xrlab-dau/CHOOGuard/security/advisories/new>).

커밋·이슈·로그·빌드 산출물에서 비밀값(API 키, Unity 라이선스 키, 토큰, 계정 정보)을 발견해도 같은 경로로
신고해 주세요. 노출된 값은 폐기하고 새로 발급하며, 기록에서 지우는 것만으로는 해결된 것으로 보지 않습니다.

## 범위

- `develop`·`main` 브랜치의 게임 코드(`Assets/`), 에디터 도구(`AgentScripts/`), 워커(`workers/`)
- CI/CD 워크플로와 스크립트(`.github/`)
- 범위 밖: `asset-library/research-public/` 등 실행되지 않는 연구 보존 자료

## 자동 점검

CodeQL, 의존성 검토, gitleaks, zizmor, OpenSSF Scorecard가 CI에서 실행됩니다.
구성과 운영 방법은 [docs/CI_CD.md](../docs/CI_CD.md)에 있습니다.
