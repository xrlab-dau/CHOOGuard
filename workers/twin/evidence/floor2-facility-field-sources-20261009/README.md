# 부산역 MAIN2F 이동 시설 현장 참조 — 2026-10-09

Aside CLI 직접 `repl` 브라우저에서 표적 질의 12개와 새 원문 10개를 확인했다. 원문 관련 구간의 **39개 원본 파일(38개 고유 SHA, 126,363,949바이트)**을 보존하고, 실제 대조에 필요한 **8개**만 선정했다. SHA·바이트·해상도 모두 재검증했다. 다른 floor2 영수증 묶음과 동일 SHA는 없었다. 공식 코레일 2023 사진과 개인 2023 사진 한 장은 동일 SHA이므로 독립 증거로 세지 않는다.

사진은 `reference_only`다. 독립 측량·현재 모델 정합·실측 정확성은 인증하지 않았다. 원본은 `asset-library/research-public/2026-10-09/floor2-facility-field-sources/`에 보존하며, [영수증](receipts.json)에 원문·원본 URL, SHA, 바이트, 해상도, 취득시각, 게시시점, EXIF 날짜 구분, 라이선스와 진위 경계를 기록했다.

| 선정 자료 | 직접 보이는 내용 | 적용 경계 |
|---|---|---|
| [nose006 원본](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-006.jpg) · [6월 원문](https://m.blog.naver.com/nose_/224311088135) | 한 화면에 여행센터, AV/냉방기 금속 기둥, 남천할매떡볶이, 중앙 하행 입구, 오른쪽 상행 2대 에스컬레이터, 낮은 원형 천장과 높은 갤러리 경계 | 중앙 작은 표지의 승강장 번호·숨은 부품·원본 모델 소유자·미터 치수 미확정. 4032×3024. |
| [nose008 원본](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-nose-008.jpg) · 같은 원문 | 남천할매떡볶이 바로 왼쪽 유리 입구, 타는곳/나오는곳 표지, 내부 회색 서비스 패널·검정/노랑 모서리, 문턱 점자 경로 | 사진 안쪽의 계단·깊이·정확한 s34 벽-기둥 간격은 보이지 않음. 1521×1316. |
| [parking004 원본](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/parking-himawari-004.jpg) · [7월 원문](https://m.blog.naver.com/himawari_tw/224348442114) | 왼쪽 8번 선상주차장 통로와 오른쪽 **나오는곳 Arrival** 유리 입구, 그 뒤 1·4번 승강장 표지 일부, 금속 기둥·천장 | 오른쪽을 9번 출구로 대응하지 않는다. 열린 경첩문 모습만 확인. 5712×4284. |
| [parking013 원본](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/parking-himawari-013.jpg) · 같은 원문 | 실제 노란 9번 출구 표지, 경첩·세로 손잡이를 가진 유리문 전실, 열린 왼쪽 문과 닫힌 오른쪽 문, 바깥 지붕 통로 | **자동 슬라이딩 구동이나 2.6m 폭을 증명하지 않는다.** 현재 exit9 모델의 문 종류와 대조할 수 있다. 5712×4284. |
| [harang006 원본](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/port-route-harang-006.jpg) · [2월 원문](https://m.blog.naver.com/ha_rang5/224190350694) | 별도 시점의 9번 출구·왼쪽 작은 키오스크·외부 통로 | 900×1201 부분 사진. 승강기·계단은 함께 보이지 않음. |
| [kimwurly005 원본](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/lounge-kimwurly-005.jpg) · [7월 원문](https://m.blog.naver.com/kimwurly_/224351351388) | 실제 안내판의 중앙 7번/주입구 쪽 20번 크리스피를 별개로 유지, 27번 라운지와 시설 아이콘 | 안내도는 도식이다. 20번 실제 점포 전면이나 실측 경계를 보여 주지 않는다. |
| [kongmistar013](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-kongmistar-013.jpg) · [2023 원문](https://m.blog.naver.com/kongmistar/223163023064) | 실제 3·4번 하행 표지, 청록 유리 경계·위쪽 가로 부재 | **2023년 과거 자료**. 현재 s34 입구 폭·원본 벽 오류를 확정하지 않음. |
| [kongmistar014](../../../../asset-library/research-public/2026-10-09/floor2-facility-field-sources/lockers-kongmistar-014.jpg) · 같은 원문 | 3·4번 표지와 같은 화면의 보관함·자동판매기·넓은 기둥·외부 유리벽 | **2023년 과거 자료**. 현재 2026 형상·하행 랜딩 여유 폭은 미확정. |

[선정 제어점](source-controls.json)은 원본 경로와 해시, 보이는 부분·공백을 분리했다. nose006/008의 픽셀 영역은 육안 대조를 위한 대략적 영역이며 정합점이나 측량값이 아니다. [연락판](selected-controls-contact-sheet.jpg)은 파생 검토용이고 원본 바이트는 변경하지 않았다.

10개 원문에서 확보한 새 자료는 통로 관계와 출구 문 형상을 보완한다. **11개 안내도 시설의 고유 원본 모델 소유자는 이 수집만으로 확정하지 않았다.** 구조 대조 담당자에게 nose006/008 원본과 제어점 파일을 전달했다. s34 상부 랜딩의 특정 원본 벽·기둥을 수정할 정확한 최근 현장 제어점은 아직 부족하다. 20번 크리스피 실제 전면+주입구/이웃 점포 동시 화면도 찾지 못했으며, 7번 사진으로 대체하지 않았다.

현재 공식 [코레일 회의실 안내](https://www.korail.com/ticket/train/stationGuide/room?tabIdx=3&csIdx=50)는 1층 2번 출입구 내부, 2층 매표소 앞, 2층 7번 출입구 오른쪽 승강기로 5층에 접근한다고 명시한다. 이는 경로 이름을 보완하는 문서 근거이고, 사진·원본 승강기 형상 정합을 대신하지 않는다.

부산항 여객터미널 승강기, 도시철도 부산역, 초량168계단, 호텔 사진은 MAIN2F 구조에서 제외했다. SRTPlay 2021 자료는 본문의 층 표기가 현재 안내판과 일치하지 않아 현재 층 대응에 사용하지 않았다. EXIF 306 날짜는 파일 수정시각이며 독립 촬영시각으로 승격하지 않았다. 사진에 명시적 AI 생성 표시는 확인하지 못했지만, 사진 진위를 독립 감식하지는 않았다. 공개 게시 사실과 공식 발행은 재사용 라이선스를 뜻하지 않는다.

오류: 잘못된 `m.m.blog.naver.com` 주차장 URL은 정확한 원문으로 한 번 수정했다. `.mpo`로 잘못 요청한 두 이미지 검토 경로는 영수증의 실제 `.jpg` 경로로 수정했다. 다운로드 실패는 없었다. Unity·Assets·씬·공용 문서·Aside 설정·인증은 수정하지 않았다.
