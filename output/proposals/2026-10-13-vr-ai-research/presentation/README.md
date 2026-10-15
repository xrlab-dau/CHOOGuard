# 발표자료

표지+필수본문6장, 총7장. 원본은 audience_revision.py, 공통 렌더러는 build_deck.py다. scene.json에서 HTML·PDF·Figma 문구를 맞춘다. 생성 배치 스크립트는 신규 슬라이드를 만들 수 있으므로 기존 Figma 수정에는 사용하지 않는다. 현재 native 슬라이드ID는40:2,3:2,3:15,3:33,3:74,5:2,5:40 순서다.

사용 그림은 object-interaction.png 한 종이며 image2.5 원본·알파 파생본·승인된 네 색상 파생본을 generated에 보존했다. 네 색상 보정은 quantize_figure_palette.py, 검사 결과는 palette_correction_checks.json이다. 색상 외 치수·알파·그림 범위는 동일하다.

재생성은 build_deck.py 다음 export_pdf.py 순서다. 최종 검증은 browser_accessibility_checks.json, pdf_accessibility_checks.json, figma_native_verification.json, opendesign_verification.json에 남긴다. 발표문은 PRESENTER_NOTES.md와 Figma speakerNotes에 들어간다. 실제 영상은 결과물 형상 장에 연결할 예정이다.
