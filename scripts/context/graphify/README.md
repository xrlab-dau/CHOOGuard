# Graphify 고정 도구

공식 upstream: https://github.com/Graphify-Labs/graphify

PyPI 이름은 `graphifyy`(y 두 개), 실행 파일은 `graphify`입니다. `tool.json`은 확인한 wheel의 SHA-256과 실행 모드를 기록하고, `requirements.txt`는 실제 설치된 의존 버전을 고정합니다. 전체 플랫폼별 wheel hash lock은 아니며 다른 OS/Python의 설치·동작은 별도 검사합니다.

- 격리 환경에서만 설치합니다. Python 3.12.11에서 0.9.61 설치·실제 C#/Python/JS AST 추출을 확인했습니다.
- 가상환경 실행 파일은 선택한 OS에 맞게 사용합니다. 학교 PC·고사양 소유·특정 장소는 작업 배정 조건이 아닙니다.
- wrapper `../graphify_context.py extract`가 code-only/no-cluster와 `GRAPHIFY_QUERY_LOG_DISABLE=1`을 고정합니다. manifest 밖 파일이나 변경된 corpus는 실행 전에 거부합니다.
- Graphify의 provider/global graph/watch/hook/semantic API를 사용하지 않습니다. 설치 요청을 별도 API·모델 다운로드 승인으로 확대하지 않습니다.
- graph 재현·조회 명령은 [Graphify 문맥 안내](../../../docs/context/graphify/README.md)를 따릅니다.

`LICENSE`, `LICENSE-MIT`, `NOTICE`는 graphifyy 0.9.61 wheel에 포함된 원문을 변경 없이 보존한 것입니다. 이 파일은 별도 도구의 출처 고지이며 CHOOguard 자체의 오픈소스 라이선스 부여가 아닙니다.
