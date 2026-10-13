# Issue #281 교육 피드백 · 2026-10-12

기존 열차·인터폰·냉난방 제어반·좌석·창가·선반, 짐 모델과 문 작동음/확인음을 재사용한다. 실제 렌더와 레이 교차 좌표로 장비와 점검 위치를 대조한다. 개인 지참 구급 가방을 고정 비품처럼 배치하던 부분은 제거했다. 무전 선택은 본 게임의 RadioWheel을 재사용한다.

최신 교육 안내 음성 13개와 인터폰/방송/무전/승객 응답 5개는 이미 설치된 Microsoft Heami(ko-KR)로 오프라인 생성한다. 삭제된 구급 단계의 guide-04는 제외하며 기존 다른 클립의 식별자는 유지한다. 엔진/음성 모델 바이너리를 재배포하지 않는다. 문장·엔진·취득 시각·파일 크기·해시는 receipts.json에 기록한다. make-feedback-voices.ps1로 생성하고 Assets/ChooGuard/Art/Tutorial/Audio/의 클립을 단계 자료에 연결한다.

공식 API: https://learn.microsoft.com/en-us/uwp/api/windows.media.speechsynthesis.speechsynthesizer

원영상과 구현의 대응은 video-reference.md에 기록했다. 초기 17개 클립의 무전기 시험 각색을 수정하여 인터폰 호출음/통화와 객실 순회·냉방 조절로 표현한다. 호출음은 기존 StationSound의 전자음 합성 방식에 따라 런타임에서 생성한다. 영상 음성을 추출하거나 실제 인물의 녹음을 사용하지 않는다.

