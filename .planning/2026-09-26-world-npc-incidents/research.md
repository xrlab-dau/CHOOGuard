# 역 전체 NPC·열차·사건 합성 — 자료와 참고 코드 (2026-09-26)

표기: A=본문 직접 확인, S=검색 결과 요약만. 게임 값은 이 자료를 근거로 압축·단순화한 것이며 운영 수치로 표시하지 않는다.

## 부산역

- [R1] 위키백과 「부산역」(Wikipedia API, 2026-09-26 열람, A): 경부선·경부고속선 **종착역**. 매표소·멤버십 라운지는 2층, 1층은 상가. 2010년 2층 맞이방 확장과 함께 ‘KTX 타는 곳’ 개찰구 설치. 1·3번 승강장은 ITX-새마을·무궁화호, **4~11번은 KTX·SRT**. 좌측 통행. 부산항 방향 뒤쪽 출구 신설. <https://ko.wikipedia.org/wiki/부산역>
- [R2] 이용 후기(네이버 블로그, S): 1층에서 에스컬레이터로 2층에 오르면 바로 매표 구역, 2층 탑승 게이트(타는 곳)에서 승강장으로 내려감, 7번 출구는 2층 높이에서 광장으로 나감. 출발 10~15분 전 승강장 도착 권장. <https://m.blog.naver.com/dmsal587/223428436492> · <https://blog.naver.com/hclneed/224094650672> · <https://blog.naver.com/peachakt/224401542184>

## KTX-산천

- [R3] 나무위키 「KTX-산천/차호별 상세설명」(2026-09-23 수정본, A): 10량 = 동력차 2 + 객차 8(1~8호차). **3호차 특실**(30~33석), 나머지 일반실 44~60석, 편성 363석(100번대)·410석(200번대 이후). **4호차 출입문은 열리지 않는다**(스낵바 상하차용이라 승객용 발판이 없음). 출입문 구동은 ICE 3와 비슷, 저상홈 발판 동작. <https://namu.wiki/w/KTX-산천/차호별%20상세설명>
- 트윈 모델 대조(실측): 객차 이름 `ET1·T2·S·T4·T1·T1·T2·ET2` 가 100번대 편성과 같고, 4호차에는 문짝 부품이 없다(DL/DR 0개). 1~3·5~8호차는 양쪽에 문짝이 있다.

## 승강설비

- [R4] 승강기 안전기준 [별표 24] 에스컬레이터 안전기준(법제처 첨부, S): 경사 30° 초과 35° 이하는 공칭속도 **0.5 m/s 이하**, 30° 이하 0.75 m/s 이하, 무빙워크 0.75 m/s 이하. 게임은 0.5 m/s 로 태운다. <https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flSeq=141709553>

## 참고 코드

- [C1] Unity Technologies `NavMeshComponents` 예제 `AgentLinkMover.cs`(MIT, 2016, A): `autoTraverseOffMeshLink=false` 로 두고 `isOnOffMeshLink` 동안 코루틴으로 몸을 옮긴 뒤 `CompleteOffMeshLink()`. 에스컬레이터·엘리베이터·열차 문 통과를 같은 구조(링크 소유자별 이동)로 구현한다. <https://github.com/Unity-Technologies/NavMeshComponents/blob/master/Assets/Examples/Scripts/AgentLinkMover.cs> · 라이선스 <https://github.com/Unity-Technologies/NavMeshComponents/blob/master/LICENSE>
- Unity 6000.3 내장 API 확인(리플렉션 실측): `NavMesh.AddLink(NavMeshLinkData, pos, rot)` → `NavMeshLinkInstance.owner`, `OffMeshLinkData.owner`, `NavMesh.AddNavMeshData(data, pos, rot)`. 패키지(com.unity.ai.navigation) 없이 링크·이동하는 navmesh 인스턴스를 쓸 수 있다.

## 압축·가정 (게임 값)

- 열차 한 주기: 도착 진입 → 문 열림 → 전원 하차 → 정비(문 닫힘) → 승차 → 출발. 실제 종착역 회차 시간보다 짧게 압축한다.
- 한 편성 363석을 모두 태우지 않는다. 화면에 보이는 인원은 게임 밀도다.
