# 본관 2층–탑승구 연결 수정

공식 2층 건축 배경, 원본 부산역 메시의 객체 계층, 저장 씬의 실제 충돌·렌더 검사를 기준으로 수정했다. 전체 부산역의 실측 정확도 99%를 인증하는 자료는 아니다.

현재 저장 씬 SHA256: `9662eb911537fd2bed6659bd68cb320a20729d25a9ecf761c32ee4b439862640`.

## 씬에 적용한 내용

- 본관–탑승구 서측 진입부: WALL038과 중복 벽, 도면 밖 구획, 잘못 놓인 남천 파사드를 교정하고 바닥의 실제 틈을 연결했다. 주변 서비스실 벽은 보존했다.
- 첫째·둘째·넷째 계단/에스컬레이터 묶음: SourcePart 302개, 이동 설비 Transform 147개와 독립 본체를 도면 위치로 함께 배치했다. 난간·계단·설비를 분리해서 임의 배치하지 않고 원본 묶음의 관계를 유지했다. 이전 자리 바닥과 새 개구부도 함께 수정했다.
- 파란 칸막이: 도면의 빈 대합 공간을 막던 `ROOT/g58/g9`의 정확한 1,246면을 렌더·충돌 역할별로 제거했다. 계단·기둥·바닥과 기존 배치·재질은 보존했다.
- 넷째 계단 앞 검은 면: `ROOT/g58/g62/g2/geometry`의 원본 4면 중 공식 개구부와 겹친 3면만 잘랐다. 북쪽·서비스 공간 쪽 부분은 보존했다.

## 실제 확인 결과

- 실제 플레이어의 모든 CharacterController 설정을 복사해 본관↔탑승구 및 기존 파란 칸막이 구간을 왕복 통과했다.
- 연결 바닥 383곳의 지지, X 개방부 446곳의 활성 렌더·2층 높이 충돌 비움과 아래 1층 바닥 지지를 통과했다.
- 검은 면의 비움 12곳·보존 9곳, 기존 B1의 바닥/개구부/보존 916/118/6곳과 B24의 545/253/389곳을 재검사해 통과했다.
- 저장 전후 YAML 28,296개 기록의 추가·삭제는 없다. 메시 참조 네 곳만 변경됐고 Transform·GameObject·Renderer 등 다른 블록은 바이트 단위로 동일하다. 실제 런타임 원본 기록 28,291개와 재질 663개의 값·dirty 상태·파일 보존 검사도 통과했다.
- 임시 카메라로 저장 씬의 6개 시점을 촬영했고 본관 입구, 탑승구 통로, 칸막이 이전 자리와 세 계단의 실제 화면을 직접 검토했다.
- 이번 저장 씬을 실제로 다시 열고 동일한 칸막이·검은 면 충돌 검사, 기존 B1/B24 바닥·개구부 검사, 실제 플레이어 왕복과 연결 바닥 383곳을 재실행해 모두 통과했다.
- 최신 저장 상태로 NavMesh를 갱신했다. 본관 보호 경로와 탑승구 연결 3구간이 완전 연결됐고, 파란 칸막이 이전 구간도 양방향 완전 경로와 직접 통행을 통과했다. 기존 경유점·경로 파일 및 씬은 보존됐다. 전체 경로 그래프 재생성은 기존 3층 경유점의 좌표 문제로 보류돼 있다.

## 미완료 범위

- 셋째 묶음(B3): 도면의 꺾인 계단과 원본 직선 계단의 대응이 입증되지 않았다. 해당 이동안은 REVIEW_ONLY로 보관했고 설치는 차단했다. 기존 구조 기둥은 보존한다.
- 하부 승강장: 일부 계단 끝과 플랫폼의 정합이 남아 있다. 플랫폼만 옮기는 방식은 선로·인접 승강장과 겹치고 북측 접근 계단을 끊으므로 적용하지 않았다. 전체 승강장 배치를 결정할 추가 기준점이 필요하다.
- 전체 건물 99% 일치, 전체 4·5층, 모든 계단의 승강장 왕복을 완료로 판정하지 않았다. 오브젝트 상호작용 기능은 이번 작업 범위에 포함하지 않았다.

## 확인 파일

- [적용 Unity 씬](../../Assets/ChooGuard/Scenes/FpsStation.unity)
- [공식 2층 건축 배경](../../workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083811-store-plan-page-1-embedded-2.jpeg)
- [차단 면 적용·원본 보존·실제 이동 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/apply-v2/receipt.json)
- [저장 씬 재열기·실제 이동·충돌 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/saved-reopen-v2/receipt.json)
- [최신 연결 바닥·X 실제 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/final-stock-X-v2/receipt.json)
- [최신 NavMesh·본관·새 통로 연결 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/final-navigation-9662eb91/receipt.json)
- [6개 저장 화면과 검사값](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/final-saved-views-v2/capture-receipt.json)

아래 화면은 위 저장 씬을 임시 카메라로 직접 렌더한 결과다. 실제 플레이어 카메라나 씬 오브젝트는 이동하지 않았다.

![본관–탑승구 진입부](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/final-saved-views-v2/main2f-platform6-entry.png)

![파란 칸막이 제거 후 통로](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/final-saved-views-v2/blue-partition-crossing.png)

![넷째 계단 개구부의 검은 면 교정 후](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/concourse-surface-transaction/final-saved-views-v2/B4-upper-bank.png)
