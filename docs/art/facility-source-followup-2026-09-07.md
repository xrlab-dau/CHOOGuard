# 부산역 시설 근거 추가 확보 — 2026-09-07

원본은 ignored `private-data/facility-sources/`에 보관했다. 공개 원문 취득·텍스트/표 확인 결과이며 **준공 검수·현장 가용성·실측 좌표·훈련 절차 승인·재구성 성공을 뜻하지 않는다.** [기존 구역/연결 범위](../../foundation/world/facility-coverage.json)는 그대로 유지한다.

## 실제 추가 확보 자료

| 출처 | 직접 확인한 내용 | 사용 한계 |
|---|---|---|
| [코레일유통 굿즈 매장 설계도서 ZIP](https://www.g2b.go.kr/pn/pnp/pnpe/UntyAtchFile/downloadFile.do?bidPbancNo=R26BK01658098&bidPbancOrd=000&fileType=&fileSeq=1&prcmBsneSeCd=07) | 건축도면 PDF50쪽, 인테리어 표준시방서 PDF54쪽, 내역·기간산정 XLSX2개를 포함 | 부산역 내 **특정 신축 매장**의 설계. 전체 맞이방 준공도면이 아니며 완공/변경 상태·정확한 층은 미확인 |
| [KORAIL 에스컬레이터 설계서](https://www.g2b.go.kr/pn/pnp/pnpe/UntyAtchFile/downloadFile.do?bidPbancNo=R26BK01616421&bidPbancOrd=000&fileType=&fileSeq=3&prcmBsneSeCd=07) | 2026년 경부선 부산역+동해선11개 역 부품 개량. 별도 내역서에 부산역 E/S3·8·9호기 스텝체인 및20호기 핸드레일 교체 표기 | **교체 계획**이며 역 전체 장비대장·설치 위치·수량·공사 완료 증거가 아님 |
| [부산시2025-2459](https://www.busan.go.kr/nbgosi/view?curPage=1&gosiGbn=A&sno=73396) | 과업지시서·제안요청서. 기준2024년/목표2040년의 부산진역~부산역 철도지하화 타당성·기본계획 | 장래 계획을 현재 지하역/선로 배치로 사용하지 않음 |
| [KORAIL 국민행동요령](https://info.korail.com/info/contents.do?key=970) · [Humetro 긴급상황 안내](https://www.humetro.busan.kr/homepage/default/page/subLocation.do?menu_no=100101060202) | 승객 대상 일반 비상 안내와 장비 사용 도해. Humetro1호선 문개방·소화기·인터폰 도해3개 취득 | 부산역 직원 SOP가 아님. KORAIL 공공누리4유형; Humetro 도해 HTTP수정일2016-01-08은 현행 실차 적용 검증이 아님 |
| [Humetro 역 시설안내도](https://www.data.go.kr/data/15050407/fileData.do) | 2025-10-31 기준 ZIP의114개 항목 중113 부산역 GIF 추출 | **기존 P03과 바이트/해시 동일.** 데이터셋 날짜만으로 부산역 안내도 현행화·출구번호 불일치 해소를 선언하지 않음 |

## 설계도서에서 바로 찾아볼 쪽

건축 PDF의 **물리 페이지 번호** 기준이다. 이번 확인은 텍스트 추출이며 원본 이미지의 새로운 시각 검수는 아니다.

- p5 `SITE PLAN`: NON SCALE. 굿즈샵33.00㎡와 부산별빛샌드·올리브영 이름 표기. 이 자료만으로 전체 역사 크기·방향·층을 확정하지 않는다.
- p8 `I-004 FLOOR PLAN`: 축척1/30, 치수5,500·6,000, 매장26.62㎡+창고6.38㎡ 표기. **매장 부분의 설계치**이며 사진 복원 공간의 스케일 앵커가 아니다.
- p9~12 구조·천장 구조·천장/장비·바닥 패턴, p14~20 입면/단면/접합, p21~47 가구·진열대 상세, p49 장비일람표, p50 냉난방 배관도.
- 표지2026.07, 개별 도면2026.05, 일부2026.00.00이 섞여 있다. 단일 확정 개정일로 정규화하지 않는다. p3은 모든 치수의 현장 재확인을 요구한다. 목차의 도면번호와 실제 쪽 표제가 다른 부분도 있어 실제 쪽을 함께 기록했다.
- ZIP SHA256: `f0bd4699ba85864c452d825c8765c26c9e1d2817d18b1f2662baa01e331c67fd`. 추출 PDF 해시·구성 목록은 아래 영수증에 있다. 사용·변형·재배포 허가는 별도 확인해야 한다.

## 도시철도 부산역의 공식 수량·이력

**KORAIL 부산역의 수량으로 전용하지 않는다.** 수량은 각 기준일의 공표값이며 지금 사용 가능한 장비의 수를 보장하지 않는다.

| 원문·기준일 | 부산역 행에서 확인 | 미확인 |
|---|---|---|
| [긴급대피마스크,2025-10-31](https://www.data.go.kr/data/15136814/fileData.do) | CSV19행: 합계70개. 보관함 위치 문자열 `상선 4-2 / 하선 5-1` | 두 위치별 배분·유효기간·현장 좌표·실물 상태 |
| [편의시설,2025-12-31](https://www.data.go.kr/data/15052664/fileData.do) | CSV20행: 엘리베이터 내부2/외부2, E/S6, 비상인터폰 상선3/하선3 | 개별 기기 위치/모델·실시간 작동 상태. 물품보관함135는 단위 확인 없이135개 독립 캐비닛으로 모델링하지 않음 |
| [승강장 안전문,2026-06-30](https://www.data.go.kr/data/15061228/fileData.do) | CSV20행: 역번호113, 설치(준공)일2012-12-20, 시스템개량일 공란 | 공란을 ‘유지보수 없음’으로 해석하지 않음 |
| [AED 설치 발표,2024-07-09](https://www.humetro.busan.kr/homepage/default/board/view.do?board_no=240719EDJF&conf_no=108&menu_no=1001060501) | 1~4호선 전 역사 설치 완료라는 운영자 발표 | 부산역의 현재 상세 위치·기종·점검일 |

공공데이터포털은 해당 파일에 ‘이용허락범위 제한 없음’을 표시한다. 개별 자료/서비스의 조건을 확인하며 이 기록을 미래 게시 작업에 대한 사용자 승인으로 취급하지 않는다. CSV의 공란은 원문 그대로 보존했다. 숫자 원문은 독립 현장 검증을 받지 않았다.

## 원 게시자 영상·사진

- [Livesimple 부산→서울 KTX 여정](https://www.youtube.com/watch?v=Gv9hCGseu9o): 2026-01-09 게시, **촬영일 미확인**. 1920×1080 H.264/30fps 영상전용209,453,765B, ffprobe516.666667초. 원본 SHA256 `5a4e339118378218439781a1bf4dd5cda24a20e1e4ea50f54d86c6282ebf7be6`. 기존3개 영상은 재취득하지 않았다. 컷·부산역 종료 시각·중첩·화질·재사용권은 미검수다.
- [구포자전거오빠 원문](https://m.blog.naver.com/bang9163/222527449409): 2021-10-05 게시. 도시철도→KORAIL 부산역 에스컬레이터라는 캡션이 있으나 다른 역·거리도 혼재한다. 원문 HTML 확보와 개별 사진 검수를 구분한다.

## 연결되는 기록과 다음 작업

[조사 보고서/출처](../../_bmad-output/planning-artifacts/research/domain-korail-busan-facility-sources-2026-09-07/research.md), 같은 폴더의 `acquisition-*.json`, `goods-design-extraction.json`, `busan-equipment-rows.json`, `busan-guide-comparison.json`이 취득·추출·해시·원문 행의 증거다. 계정 설정이나 민감한 media URL은 포함하지 않는다.

다음은 기존 영상에서 **연속·중첩·시차·마스크가 검증된 한 구간**을 정하고 동일4프레임 SfM/DA3 비교를 수행하는 것이다. 위 설계치나 장비 수량을 임의로 추론 결과에 맞춰 metric/안전 수용으로 승격하지 않는다. 현행 전체 층별 준공도, KORAIL 대피·설비배치도, 실제 직원 매뉴얼은 여전히 별도 요청·확인이 필요하다.
