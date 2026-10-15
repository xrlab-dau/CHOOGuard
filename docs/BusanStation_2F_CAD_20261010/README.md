# 부산역 공식 2층 도면의 편집 가능한 CAD

[DXF 열기](BusanStation_2F_OfficialPlan_Editable_SourcePixels.dxf) · [원도 대조 미리보기](BusanStation_2F_OfficialPlan_Editable_Preview.svg)

외부 변환 서비스에 업로드하지 않고 로컬 ezdxf 1.4.4로 실제 CAD 파일을 생성했습니다. 공식 631×496 JPEG에서 추출·검토한 벡터를 사용합니다. 벽 56개 외곽과 내부 구멍 14개, 개구부 30개, 기둥 축 97개, 기호 105개, 공개 원본 기둥 윤곽 53개, 코어 26개, X 개방부 4개 등 총 386개 편집 가능한 엔티티입니다.

좌표는 X=도면 픽셀X, Y=-도면 픽셀Y, Z=0입니다. 단위는 픽셀이며 DXF INSUNITS=0입니다. 임의 밀리미터 축척이나 실제 층고를 부여하지 않았습니다. 소스·단일 정합 프레임·설치 벽 패키지 해시는 entity-manifest.json에 기록했습니다. 독립 DXF 재읽기 검사에서 좌표 차이 0픽셀, CAD 구조 검사 오류·자동 수정 0개였습니다. 이것은 데이터 보존 검사이며 현장 99% 일치 판정이 아닙니다.

X_NO_2F_FLOOR 레이어의 4개 폐곡선은 2층 바닥이 없어 1층을 내려다보는 개방부입니다. Unity의 바닥 마감 메시에도 별도 반영해야 합니다.

원본 생성·독립 검증 코드는 workers/twin/output/floor2-editable-cad-20261010/에 보존했습니다.
