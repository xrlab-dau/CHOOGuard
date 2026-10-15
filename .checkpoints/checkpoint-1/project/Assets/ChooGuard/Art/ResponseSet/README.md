# 부산 교통·재난 대응 시각 자료

`art/blender/build_response_set.py`가 Blender 4.5.9 LTS에서 실제 생성·내보낸 표현용 자료이다. `art/blender/jev-parameters.json`의 제한된 선택을 검증하고 기하/재질 연산으로 적용한다. Jev는 파라미터 선택 출처이며 메시·코드 작성자나 이미지 검토자가 아니다.

- `BusanFacade`: 부산역 공개 OSM 곡면 외곽과 제공된 실제 역사 사진을 참고한 유리·멀리언·철골·캐노피·계단. 게임 높이와 세부는 저작된 근사 표현이다.
- `HighSpeedCab`, `HighSpeedCoach`: 흰색/남색 고속열차 표현. 특정 실제 차량의 정밀 복제품이 아니다.
- `FireEngine119`, `Ambulance119`: 소방·의료 차량 표현. 수납 셔터, 바퀴, 유리, 경광등, 119 표식, 장비를 구분한다.
- `CrewOperations`, `CrewFire`, `CrewMedical`: 연속 의복 메시, 작은 머리와 장비, 역할별 복장. 기존 Kenney Mini Characters의 armature 및 실제 idle/walk/interact clips를 재사용한다. 원본 animation action slot을 명시적으로 연결하고 프레임14 walk를 실제 렌더한다. 원본 rig는 6개 뼈이므로 팔꿈치·무릎의 완전한 인체 리깅은 아니다.
- 재질: 생성한 고정 명칭에 Unity URP Lit을 매핑한다. 기존 ambientCG Concrete031·Metal032 CC0 albedo를 활용하며, emissive 청록색 표현에 의존하지 않는다.

원본/실행 증거: `art/blender/response_set.blend`, `build-receipt.json`, `response-set-contact.png`, `response-set-front.png`, `crew-walk-frame14.png`. 공개 형상 출처와 실측·실제 물리 보정 주장은 구분한다. 아트 교체는 물리 워커 또는 게임 규칙의 검증을 추가하지 않는다.
