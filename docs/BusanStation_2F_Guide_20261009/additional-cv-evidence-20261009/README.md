# 부산역 2층 추가 근거와 실제 CV 실행 결과

2026-10-09. Aside CLI 브라우저로 추가 자료를 직접 검색하고, 썸네일 대신 사진 원본과 첨부 PDF를 내려받았다. **201·202호가 직접 표시된 2024년 2층 회의실 약도**, 새 안내판 사진, 남천할매떡볶이 옆 유리 통로 사진을 확보했다. 공식 평면도 CV 제약 추출과 새 사진의 다중 시점 복원도 실제 실행했다. 이번 작업은 근거·추론 결과를 추가한 것이며 운영 씬의 벽을 수정한 작업은 아니다.

[사진·도면 비교 화면](index.html) · [원본/결정/검증 목록](evidence-catalog.json) · [안내판 정면화와 위치 대조](../../../workers/twin/output/floor2-current-guide-review-20261009/README.md)

## 새로 확보한 구체적 근거

| 자료 | 직접 확인한 내용 | 복원에 사용할 범위 |
| --- | --- | --- |
| [KIMST EDU 개최 상세장소 PDF](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/kimst2024-venues.pdf), **4쪽 부산역** | 매표소·철도경찰·7번 출구와 201·202호 번호. 약도에서 201호는 철도경찰 오른쪽, 202호는 201호의 오른쪽 위 | 두 회의실의 역사적 상대 배치·접근 방향. 네이티브 약도는 406×285px이며 치수 도면이 아님. 서울·광주·대전 쪽은 부산역 근거에서 제외 |
| [2026년 7월 방문 기록의 원본 안내판 사진](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/lounge2026-c17-0.jpg) | 3024×4032px. 역무실·철도경찰서·매표창구·26번 남천할매떡볶이 표시. 8번 아트박스·20번 크리스피크림도넛 명칭 | 원본 사각형 좌표를 기록해 2층 패널을 정면화. 표시된 시설의 상대 위치와 점포 시점 대조. 201·202호 숫자와 내부 방 경계는 없음 |
| [2026년 6월 게시 사진 c19](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/locker2026-c19.jpg), [c23](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/locker2026-c23.jpg) | 실제 남천할매떡볶이 간판, 옆 유리 통로, 철도경찰·물품보관함·화장실 방향 표지, 천장·기둥·바닥 접속부 | 같은 장소를 찾는 시각 근거. 보이는 자동유리문을 원도의 북쪽 양개문 또는 201호 문으로 동일시하지 않음 |
| [2023년 내부 사진 묶음](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/spring2023-contact-sheet.jpg) | 약 3000×2000px 대공간·상부 점포 통로·라운지·안내판 사진 15개 | 벽·천장·난간·통로 형태의 역사적 비교. 카카오프렌즈 등 당시 점포를 현재 점포의 내부 집기로 승격하지 않음 |
| [2층 접근성 조사 원본](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/access2f-contact-sheet.jpg), [3층 원본](../../../asset-library/research-public/2026-10-09/floor2-cv-source-extension/access3f-contact-sheet.jpg) | 원본 800×600px 매장 전면 사진 12개 | 점포 전면·난간·창과 접근부의 비교. 본문의 2026-07-01 정보 갱신일은 모든 사진의 촬영일을 뜻하지 않음 |

실제 브라우저 원문: [안양대 KIMST 공지](https://www.anyang.ac.kr/iacf/community/notice.do?mode=view&articleNo=31082), [7월 라운지 방문 기록](https://m.blog.naver.com/kimwurly_/224351351388), [6월 물품보관함 기록](https://m.blog.naver.com/nose_/224311088135), [2023년 역사 내부 기록](https://m.blog.naver.com/springlll8/223252084280), [휠체어 부산 2층](https://www.wheelbusan.or.kr/wheelbusan/bbs/board.php?bo_table=bo_07&wr_id=284), [3층](https://www.wheelbusan.or.kr/wheelbusan/bbs/board.php?bo_table=bo_07&wr_id=285).

사진의 게시일·작성자의 방문 주장·실제 촬영일을 분리했다. 독립 검증된 현장 촬영일은 없다. 내려받은 사진·안내 이미지 **59개 파일**은 파일 수이며, 독립 촬영이나 실측 기준점 59개가 아니다. 2022년 c06/c07은 같은 시야에 다른 주석을 붙인 이미지다. 별도 행사 문서의 실외 지도·QR 화면 12개는 부산역 실내 관측으로 세지 않는다.

실제로 6월 게시물의 안내판 c15에는 3층 현위치 핀과 카카오프렌즈·백미당·다인명가 명칭이 있고, 7월 기록의 c17에는 2층 현위치 핀과 아트박스·영주당부산과자집·크리스피크림도넛 명칭이 있다. 최근 게시물을 모두 같은 시점의 배치로 통합하지 않았다. c21의 ‘5·6·8·9’는 출구 번호가 아니라 ‘Tracks’가 붙은 승강장 번호다.

## 실제 실행한 컴퓨터비전

### 공식 원도에서 구조 제약 추출

OpenCV 윤곽·연결요소·HoughLinesP·템플릿 상관·강건 격자 피팅을 실행했다. [전체 오버레이](../../../workers/twin/output/floor2-cv-constraint-trial-20261009/plan-constraints-overlay.png)와 [편집 SVG](../../../workers/twin/output/floor2-cv-constraint-trial-20261009/plan-constraints-editable.svg), [구조 제약 JSON](../../../workers/twin/output/floor2-cv-constraint-trial-20261009/constraint-proposal.json)을 제공한다.

- 닫힌 기호 후보 47개, 긴 수평·수직 잉크선 후보 89개를 추출했다. 후보 전체를 기둥·벽으로 확정하지 않는다.
- 검토 지점 주변을 피한 기호 12개로 하부 홀 반복 간격 **25.4823×22.2336 원본 px**를 추정했다. 없는 위치에 기둥을 생성하지 않았다.
- 검토 지점 13개의 격자 잔차 중앙값/최대는 0.9007/1.5904px, 실제 검출 일치는 2px 이내 9/13이다. 첫 실행 4/15 실패와 잘못 선택한 두 수동 지점도 보존했다. 보완 후 결과는 기술 QA이며 새 블라인드 검증이나 현장 오차가 아니다.
- 3월 11일·24일·10월 7일 PDF의 건축 배경은 **동일한 631×496 JPEG**다. 세 도면을 독립적인 건축 관측 세 번으로 취급하지 않는다. 픽셀 격자를 미터 길이로 변환하지 않았다.

### 새 라운지 사진으로 다중 시점 3D 복원 시험

CPU SIFT → 전체 사진 쌍 매칭 → COLMAP 점진 SfM을 실제 실행했다. [실행 기록](../../../workers/twin/output/floor2-lounge-multiview-trial-20261009/README.md), [관측·검증점 오버레이](../../../workers/twin/output/floor2-lounge-multiview-trial-20261009/registered-observations-contact-sheet.jpg), [실제 진단용 PLY](../../../workers/twin/output/floor2-lounge-multiview-trial-20261009/run/diagnostic_sparse_relative.ply)를 보존했다.

5개 시야 중 3개 카메라가 연결됐고 점 94개가 생성됐다. 학습 관측의 평균 재투영 오차는 0.635px였지만, 특징 추출 전에 가려 둔 수동 검증점 한 곳의 투영 오차는 **28.59px**였다. 이 점의 수동 표기 불확실성은 약 ±8px이며 독립 카메라나 현장 실측 검증은 아니다. 가까운 두 카메라의 중앙 시차는 0.502°였고, 점 대부분이 포스터·키오스크·장식 그래픽에 집중됐다. 천장 영역 위주로 제한한 별도 대조 실행은 모델을 만들지 못했다.

따라서 이 실제 실행 결과를 건축 체적으로 채택하지 않았다. 상대 좌표의 진단 모델은 존재하지만 정밀한 방·천장·벽 메시로 쓰지 않는다. 이번 시험에서 NeRF/3DGS를 새로 실행했다고 주장하지 않는다.

## 이번 근거로 결정한 것과 남은 대응

새 KIMST 약도는 **201·202호의 존재·철도경찰 인접·상대 순서**를 강화한다. 그러나 최신 원도에서는 큰 방 두 개가 거의 같은 x 범위로 상하 정렬되어 있고, KIMST는 두 방을 좌우로 어긋나게 그렸다. 안내판은 같은 구역을 하나의 경찰 서비스 띠로 축약한다. 따라서 기존 `RM-2F-S-201`, `RM-2F-S-r34` 이름만으로 실제 두 방의 경계·문을 확정하지 않았다.

| 우선 영역 | 실제로 좁힌 문제 | 아직 결정할 수 없는 변경 |
| --- | --- | --- |
| T01 남쪽 서비스 구역 | 남천 전면·유리 통로의 새 사진, 경찰 인접 회의실 약도 확보. 보류된 추정 조각 64개 중 60개가 한 칸막이에 집중 | 그 칸막이의 현재 존재 여부, 사진의 정확한 원도 좌표, 회의실 내부 벽 연속성 |
| T02 북쪽 문·문턱 | 201·202호 역사적 접근 방향을 추가 비교 | 원도 양개문과 사진 자동유리문의 동일성, 문턱·현재 방의 정체 |
| T03 맞이방 반대편 | 최신 안내판의 역무실 서비스 띠를 판독 | 승강장 쪽 반대 전면과 보류 벽 5개의 실제 연속성 |

[우선 구역 그림](../../../workers/twin/output/floor2-additional-evidence-targets-20261009/priority-crop-overview.png)과 [정확한 촬영 방향·필요 표지 목록](../../../workers/twin/output/floor2-additional-evidence-targets-20261009/target-packet.json)에 다음 대조 대상을 기록했다. **MAIN40은 벽 31개+바닥 8개+문 1개**이며, **MAIN64는 기존 생성 규칙의 접합 조각 44개+벽 조각 12개+충돌 조각 8개**다. 공식 원본 쉘의 64개 면이라는 뜻이 아니다.

코레일 현재 회의실 안내도 실제 브라우저에서 다시 확인했지만 이미 확보한 면적·경찰 인접 정보를 재확인한 것이다. 운영기관 위치표는 평면도가 아니다. 파주시 교육 HWP와 KTR 설명회 PDF의 원본 내장 이미지도 확인했으며 실내 평면이 없어 구조 근거에서 제외했다.

## 검증과 현재 씬

원본·추출 자산 75개의 해시를 대조했고, 사진·안내 이미지 59개 파일이 포함된다. CV 두 실행 묶음과 안내판 검토의 봉인 파일 총 80개를 루트에서 다시 검증했다. 안내판 정면화의 원본 좌표·변환·SHA 검사 12건과 재생성 산출물 15개의 해시 일치도 확인했다. [검토 결과](../../../workers/twin/output/floor2-current-guide-review-20261009/hash-qa.json)를 함께 보관한다. 비교 화면은 실제 Aside 브라우저에서 이미지 7개 로드와 분류 버튼의 표시 개수 7/3/2/2를 검사했다.

운영 씬, NavMesh, 경로 JSON, 역 기준점 JSON의 네 해시를 기존 저장본과 대조했다. 모두 동일하다. `FpsStation.unity` SHA는 `2d9bbf83e0fbbd43bfeaa035ce62f773c6792439691f6535a8edea563b00a8b3`다. 이번에 새 벽 배치나 99% 공간 일치를 완료했다고 판정하지 않는다. 기존에 적용한 1·2층 국소 수정은 보존되어 있다.
