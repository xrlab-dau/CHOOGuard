# 부산역 상세 위치도 원본 수집 — 2026-10-09

Aside CLI `repl`로 코레일유통 공식 모집공고·실제 첨부 DOM과 다운로드 함수를 확인한 뒤, 같은 출처의 브라우저 fetch로 PDF 원본 4개를 내려받았다. 바이트·PDF 시그니처·SHA·페이지와 텍스트를 확인했고 원본을 변경하지 않았다. 일반 HTTP 다운로드는 빈 응답을 반환했으며 실패 영수증으로 보존했다. 광고제안서 URL은 오류 HTML이므로 PDF 근거로 쓰지 않는다.

| 원문 | 자료 | 용도 |
|---|---|---|
| [2026-01-30, 제2차 상설](https://www.korailretail.com/board/boardDetail.do?boardTypeNum=4&boardNum=100083259) | `official-100083259-store-plan.pdf` 1p | 1층 상세 배치, 기존 2024 사용자 제공 PDF와 비교 |
| [2026-03-11, 제4차 상설](https://www.korailretail.com/board/boardDetail.do?boardTypeNum=4&boardNum=100083313) | `official-100083313-store-plan.pdf` 2p | 1p 부산역 2층, 2p 진주역은 제외 |
| [2026-03-24, 제5차 상설](https://www.korailretail.com/board/boardDetail.do?boardTypeNum=4&boardNum=100083333) | `official-100083333-store-plan.pdf` 1p | 2층 전체 구조·중앙통로 도넛 매장 51.6㎡ |
| [2026-10-07, 제11차 단기](https://www.korailretail.com/board/boardDetail.do?boardTypeNum=4&boardNum=100083811) | `official-100083811-store-plan.pdf` 2p | 최신 1p 부산역 2층 배치; 2p 울산역은 제외 |

원본 폴더: `asset-library/research-public/2026-10-09/floor2-architectural-pdf-search/`. SHA·브라우저 응답 헤더·취득 시각은 `download-receipts.json`, 원래 PDF 안의 숨겨지지 않은 구조 래스터 추출은 `embedded-image-receipts.json`에 있다. 1층 구조 원본은 960×572, 2층은 631×496 래스터이므로 확대 렌더가 원래 세부 해상도를 높이지 않는다. 상점 표시·일부 치수는 PDF 벡터 텍스트이며 독립 측량으로 승격하지 않는다.

공식 광고 배치 PNG도 원본 보존했으나 ‘맞이방(3층)’이라는 자료의 층 표기와 현재 층 대응은 아직 검증하지 않았다. 위 첨부 PDF는 명시적으로 부산역 2층을 표시하고 본관과 맞이방 영역을 한 평면에서 확인할 수 있다. 현재 건물 CAD 원본을 확보한 것은 아니며, PDF 배치·벽·문·계단과 실제 사진 및 원본 모델을 정합하는 작업에 사용한다.
