# 부산역 본관 5개 층과 증축 맞이방 복원 근거

2026-10-09 사용자가 확인한 본관 지상 5층을 전체 복원 범위에 포함한다. 벽, 천장, 바닥판, 점포와 업무시설의 경계, 개방된 대공간, 계단 및 승강기 코어를 함께 복원한다. 현재 씬의 외형 높이나 이동 가능한 바닥만으로 층별 내부 구현을 완료했다고 판정하지 않는다.

## 직접 확인한 자료

| 자료 | 실제로 확인한 내용 | 적용 한계 |
| --- | --- | --- |
| [부산시 2004 건축상 기록](https://www.busan.go.kr/depart/ablandscape02/331342) | 건원 설계, 지하 1층·지상 5층 | 2004 건축물 기록이며 현재 실내 구획도는 없음 |
| [건원 원설계 프로젝트](https://www.kunwon.com/board/perform.read.php?BBS_GUBUN=1&SC_word=%EB%B6%80%EC%82%B0%EC%97%AD&page=1&BBS_IDX=32) | B1, 5FL; 외관·내부 대공간·승강장 사진 원본 9장 확보 | 표기 연도 1996.09와 사진 촬영일은 다름. 도면·단면도는 제공하지 않음 |
| 2012년 부산역사 증축공사 감리실무사례 | 기존 본관 4·5층 사무실과 증축부의 분리, 중2층 대합시설, 기존 연결통로 재사용, 설계변경 | 당초 설계와 변경 설계를 구분해야 함. 작은 비교도는 현재 실측 평면도가 아님 |
| [코레일유통 2층 공식 매장 위치도 원본](../BusanStation_2F_Guide_20261009/official-source-maps-20261009/README.md) | 2026-10-07·03-24·03-11 공고의 원본 PDF와 631×496픽셀 건축 배경을 실제 브라우저에서 재확인·확보 | 각 PDF의 부산역 쪽만 사용. 게시 시점별 점포명 차이를 구분하며, 전체 실측 CAD는 아님 |
| [코레일 공식 회의실 안내](https://www.korail.com/ticket/train/stationGuide/room?tabIdx=3&csIdx=50) | 5층 512·530·531·532·533호의 공식 면적·사진 및 벽·문·복도·승강기 2개·계단이 보이는 약도; 2층 201·202호 | 5층 약도는 부분 도면이며 전체 외곽과 층 높이는 알 수 없음. 다섯 약도 파일은 동일한 한 장임 |
| [4층 전기처 근무자 기록](https://www.redaily.co.kr/news/articleView.html?idxno=7280) | 2024년 기사에서 부산역 4층 전기처 사무실 사용 확인 | 사무실 평면·현재 구획을 확인하는 자료는 아님 |
| 2026년 화장실 비상통화장치 공사 설계서 | 나라장터 원본 PDF 75쪽 확보·전쪽 검토 | 건축 도면은 별첨 표기만 있고 공개된 다섯 첨부파일에는 없음. 벽 배치 근거로 사용하지 않음 |
| [코레일 2025년 정보공개 처리현황](https://info.korail.com/info/previewInfoRelFile.do?atchmnflNo=2646) | 280번, 2025-11-07 청구된 ‘부산역 층별 평면도 요청’이 2025-11-10 비공개 처리된 기록을 실제 표에서 확인 | 이 개별 청구의 처리 결과이다. 모든 공개 약도와 부분 평면도가 없다는 뜻으로 확대하지 않음 |
| [중앙 승강기 현장 영상](https://www.youtube.com/watch?v=8oK603V7gBE), [북측 승강기 현장 영상](https://www.youtube.com/watch?v=9IkgP4ChTLs) | 2022년 게시 영상의 서로 다른 실제 재생 장면 13개 확보. 중앙 1·2·4·5층 버튼과 북측 사무실 안내·배치 게시물 확인 | 2026년 현재 부서 위치의 증명이 아님. 추가 10개 캡처는 영상 픽셀이 갱신되지 않은 것이 확인돼 시간별 근거에서 제외 |

원본, 실제 브라우저 응답 및 다운로드 해시는 `asset-library/research-public/2026-10-09/five-storey-architecture/`와 `.planning/2026-10-09-interior-99-reconstruction/aside-*.txt`에 보관한다. 사진의 업로드 날짜를 현장 촬영 날짜로 간주하지 않는다.

## 현재 적용 상태 — 2026-10-10

현재 저장 씬 SHA-256: `9662eb911537fd2bed6659bd68cb320a20729d25a9ecf761c32ee4b439862640`. 아래는 실제 적용·검증 범위이며, 전체 건물 99% 일치를 뜻하지 않는다. 오브젝트 조작 기능 개발은 현재 범위에 포함하지 않는다.

| 대상 | 실제 적용한 구조 | 검증과 남은 공백 |
| --- | --- | --- |
| 본관 1층 | 공식 위치도의 본관 벽 발자국 57개를 54개 편집 메시로 생성·적용. 모순된 기존 렌더 삼각형 319개와 충돌 삼각형 252개를 정확히 제거; 화장실 바닥 10면 보존 | 새 벽 54개·개구부 52개 물리 검사 통과. 기존 벽 전체를 별도 검사하면 51/52개 개구부만 통과하며 군 관련 업무시설 동쪽 출입구 1곳은 기존 벽에 막혀 있다. 분리된 화장실 삽도의 전체 건물 위치, 보류된 기존 벽과 모든 시설 정합은 미완료 |
| 본관·맞이방 2층 | 공식 건축 배경의 벽 선분 172개·개구부 30개를 반영한 벽 메시 56개, 공개 원본 기둥 53개와 추론 표시한 하부 기둥 4개 적용. 잘못된 기존 벽·가게·벽 마감 면을 선별 제거. 도면에 없는 북측 폐쇄 공간의 삼각형 22개를 추가 제거 | 새 벽·개구부 국소 검사 및 북측 폐쇄 공간 제거 후 물리 레이 44개 통과. 일부 출입구와 승강기 뱅크 후보는 적용 전이다. 기존 화장실 칸막이와 회전문처럼 원도상 정상인 벽을 무조건 제거하지 않는다 |
| 2층 X 개방부 | 실제 건축 경계의 계단 형태에 맞춰 구조 바닥과 마감·보정 바닥, 외관측 잔여 면, 천장 6개, 유리·프레임 난간을 함께 교정. 잘못 추론한 개방부를 메우는 패치와 Native0의 잔존 바닥 22면 교정도 적용 | 원본 자동문 자산 13개 보존. 저장 씬을 실제로 다시 열고 잔존 지점 8개 비움·정상 바닥 7개 지지 검사 통과. 새 생성기의 구조 바닥·유리·프레임 3개는 정상 Save 및 패치 Restore 검증 통과. 전체 X 표본 446곳의 모든 활성 렌더 바닥 및 2층 높이 충돌이 0이고, 모두 아래 1층 바닥이 확인됨. 현장 정확도 검증은 별도 |
| 본관 2층–OfficialStation_-부산역_6 탑승구 연결 | 공식 도면의 서측 진입부에 맞춰 WALL038과 겹친 Native5 벽, 도면에 없는 Native6 구획, 잘못 놓인 남천 파사드를 교정. 서로 떨어져 있던 바닥의 정확한 틈도 연결 | 기존 플레이어 크기(높이1.72m·반경0.28m)의 경로 왕복 통과 및 연결부 바닥 레이 383개 통과. 새 NavMesh에서도 연결 경로 3구간 모두 완전 연결을 확인. 전체 경로 그래프 재생성은 기존 3층 좌표 문제로 보류. 주변 실제 서비스실 벽은 보존. 내부 첫 계단·에스컬레이터 묶음의 140개 SourcePart와 49개 이동 설비 Transform, 원본 본체 7,408 렌더 면·8,720 충돌 면을 도면에 맞춰 이동하고, 독립 바닥과 접촉 방해 면을 함께 교정해 저장. 바닥 지지 916곳·개구부 118곳·주변 보존 6곳 및 기존 캐릭터 왕복 접근 통과. 저장 씬을 다시 열어 같은 접근·바닥 검사를 재실행해 통과. 고정 계단 끝까지 내려가는 추가 검사에서는 마지막 하부 연결의 바닥 지지가 끊기는 문제가 확인되어, 전체 승강장 왕복은 미완료. 두 번째·네 번째 묶음도 162개 SourcePart·98개 이동 설비 Transform과 원본 본체를 도면에 맞춰 배치하고, 독립 바닥·개구부를 함께 교정해 저장. 저장 후 재열기에서 바닥 지지545곳·개구부253곳·주변 보존389곳과 입구6방향 통행 통과. 1a44f432 시점에 실제 플레이어의 전체 설정을 복사한 본관–탑승구 왕복·연결 바닥383곳·X개방부446곳도 통과. 세 번째 묶음은 도면의 꺾인 계단과 원본 직선 계단의 대응이 확인되지 않아 적용 보류; 원본 기둥도 보존. 추가로 도면의 빈 대합 공간을 막던 파란 칸막이 1,246면/역할과 B4 개구부를 가로지른 검은 면만 교정해9662eb91로 저장. 실제 기존 플레이어 설정의 본관·칸막이 구간 왕복, 연결 바닥383곳, X446곳, 기존B1/B24 물리검사 및 모든 원본 기록·재질663개 보존 통과. 최신 저장 화면6곳 직접 검토. 저장 씬 재열기에서도 동일한 전체 물리·왕복 검사를 통과. 최신 NavMesh도 본관 보호 경로·탑승구3구간·새 칸막이 구간의 양방향 직접 연결을 통과했고 씬·경유점·경로 파일을 보존. 하부 승강장 전체 정합은 미완료 |
| 발매기 쪽 하부 벽 | 기존 9개 개구부의 하부 벽을 생성 배치에 반영 | 하부 차단·중간 개방·상부 차단 27개 실제 레이 통과. 높이는 기존 모델 기준이며 현장 실측값이 아님 |
| 공개 3층 중층 | 공개 원본 바닥·난간·계단 메시 7개와 기존 소유자 146개의 일관된 높이 조정 적용. 7개 원본은 공식 2층 도면과 같은 XY 좌표계로 정합 | 기존 개인 모델 상층의 XY 대응은 미완료. 고정 경유점 34개를 사용하는 실제 CharacterController 왕복은 첫 계단 부근에서 실패. 충돌체 모서리를 안쪽으로 깎는 시험도 실패하여 원복; 통과 판정을 하지 않음 |
| 본관 4층 | 업무시설 사용 기록·원설계 사진·공개 원본 높이별 구조 분석 확보 | 전체 평면·벽·문·층고와 코어 대응이 확인되지 않아 전체 층을 임의 바닥판으로 만들지 않음 |
| 본관 5층 | 공식 회의실 부분 도면, 벽·경계 71개·개구부 20개·복도와 홀 7개·승강기 2개 추출. 전체 공개 원본 1,001개와 높이대 267개 대조 | 높이 후보 하나는 홀 일부만 지지하고 5개 회의실·계단 바닥 지지는 0%. 전체 층 바닥으로 채택하지 않음. 부분 도면 밖 구획·층고는 미확인 |

현재 직접 확인한 근거와 실행 기록:

- [첫 계단·에스컬레이터 묶음 실제 설치 및 저장](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/B1-coupled-transaction/apply-v6/receipt.json): 기존 씬 레코드 28,261개 속성과 663개 재질 보존. 국소 구조·통행 검사이며 전체 현장 99% 검증은 아님.

- [X 개방부 446점 실제 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/saved-a429-dense-X-proof.json), [본관·탑승구 NavMesh 연결 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/main-geometry-navigation-v1/actual/receipt.json).

- [탑승구 연결 실제 왕복 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/after-stock-body-walk-v2.json), [바닥 연결부 383점 지지](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/after-seam-383-support-v1.json), [SourceX 정상 생성·Save·Restore](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/fresh-normal-replay/v5/sourcex/proof.json), [Native0 저장 씬 실제 재열기 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/platform6-connection/saved-native0-reload-v1/proof.json).

- [1층 설치 기록](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/main1f-installation-transaction/receipt.json), [새 벽·개구부 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/main1f-live-geometry-proof.json), [기존 벽의 개구부 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/main1f-legacy-opening-proof.json).
- [북측 잘못된 폐쇄 공간 수정](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/optional0637-transaction/receipt.json), [44개 물리 레이](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/optional0637-physics-after-proof.json).
- [마감 바닥 오류 실제 씬 감사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/kit-floor-X-correction/before-audit.json): 도면 X 내부 193점을 대조하여 구조 바닥과 독립된 시각 바닥의 잔존을 확인했다.
- [실제 생성기 16개 원본 재생 검사](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/current-main-builder-source-preflight-v2-proof.json). 원본 생성과 교정 자산 해석을 검사한 결과이며 전체 씬의 정상 재생성과 현장 정확도 검증은 별도다.
- [상층 계단 충돌체 시험 거부·원복](../../.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/nosing-rejection.json).

## 도면과 컴퓨터비전의 실제 사용

외부 도면→CAD·3D 변환 서비스에는 업로드하거나 변환 작업을 의뢰하지 않았다. [도구·서비스·학술 연구 조사](../BusanStation_FloorplanToModel_Research_20261009.md) 후 공식 JPEG의 벽·개구부·기둥·코어·X 영역을 벡터화하고, 단일 도면 좌표계로 정합하여 Blender와 Unity용 요소별 메시를 직접 생성했다. 이 과정과 별도로 기존 생성기의 마감·충돌·보정 메시도 같은 경계로 교정해야 한다.

실제 영상 CV의 47카메라 상대 복원은 독립 관측 바닥 위치와 모순되어 건축 바닥으로 채택하지 않았다. 추가 사진 SfM은 카메라 3개·점 94개, 분리 관측점 오차 28.59픽셀로 벽·층고 생성에 불충분했다. 이 결과를 도면에 없는 구획이나 전체 건물의 99% 일치 근거로 사용하지 않는다. 본관의 5개 층을 같은 크기 바닥판으로 복제하거나 지붕 면을 상층 바닥으로 분류하지 않는다.

## 편집 가능한 모델

- [1층 도면 벽 모델](../BusanStation_Interior_Models_20261009/BusanStation_1F_Architecture.blend): 본관 벽 54개를 분리한 편집 모델. 분리 삽도는 미배치.
- [최신 2층 건축 모델](../BusanStation_Interior_Models_20261009/BusanStation_2F_Platform6_Architecture.blend): 저장 씬 a429cc74의 벽·기둥·구조 바닥·난간·천장 부재 123개를 분리한 편집 모델. 독립 Blender 재열기 검사 900개 통과. 하부 기둥 4개는 추론 표시 유지. 모든 승강장 설비나 전체 건물 내부를 포함한 모델은 아님.
- [공개 원본 1,001개 분리 모델](../../workers/twin/output/whole-building-registration-20261009-pass2/vworld-busan-editable-1001-source-objects.blend): 원본 좌표와 내장 이미지 573개 유지. 원본 ID 하나가 실제 단일 부품임을 뜻하지 않으며 제공자 층 이름을 실제 1~5층으로 단정하지 않음.

각 설치 당시 검사와 현재 씬 전체 검증은 구분한다. 이전 씬에서 구운 NavMesh와 경로는 최종 구조 수정 후 다시 생성해야 하며, 하나의 PathComplete나 국소 레이 통과를 모든 층의 접근성·도면 일치·현장 일치로 확대하지 않는다.
