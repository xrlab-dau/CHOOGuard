# 하드룰 — 자산 재사용 우선 (2026-09-22 사용자 지시)

**바퀴부터 만들지 않는다.** 무엇이든 만들기 전에, 그것이 이미 세상에 있는지 먼저 찾는다.

## 순서 (건너뛸 수 없다)

1. **프로젝트 내부** — 씬·프리팹·FBX·머티리얼·스크립트에 이미 있는가.
   이름 검색만으로 끝내지 않는다. 기하로 검출한다(예: 에스컬레이터는 법선 30° 경사면,
   계단은 단차 반복, 승강기는 수직 샤프트). 원본 모델은 배치 메시라 이름이 없을 수 있다.
2. **인터넷 OSS 생태계** — 공격적으로 찾는다. 저작권·라이선스는 탐색을 제한하지 않는다.
   라이선스는 메타데이터로 기록만 하고, 그 때문에 후보를 버리지 않는다.
3. **위 둘에 없는 것만 신규 제작한다.**

## 반드시 뒤지는 곳

- 3D: Sketchfab(CC 다운로드 API), Objaverse / Objaverse-XL(HuggingFace, 80만+), Poly Haven,
  Quaternius, Kenney, ambientCG, BlenderKit, OpenGameArt, Free3D, CGTrader/TurboSquid 무료,
  3D Warehouse, Thingiverse, Smithsonian 3D, GitHub 리포
- 재질·텍스처: ambientCG, Poly Haven, texturecan, ShareTextures
- 코드·스니펫: GitHub, GitLab, Unity Asset Store 무료, itch.io, 논문 구현체
- 데이터: data.go.kr, 국가공간정보포털, OSM/Overpass, V-World, 각 기관 공개 API

## 기록 의무

취득한 것은 전부 `asset-library/research-public/<날짜>/<주제>/` 에 내려받고
`receipts.json` 에 url · sha256 · bytes · content-type · 취득시각 · 라이선스표기를 남긴다.
"찾아봤는데 없더라"도 산출물이다 — 어떤 질의를 돌렸고 무엇이 없었는지 명시한다.

## 금지

- 기존 자산을 확인하지 않고 원시 도형(Cube/Cylinder)으로 대체물을 만들어 넣는 것.
- 라이선스가 불편하다는 이유로 탐색 단계에서 후보를 제외하는 것.
- 이미 씬에 있는 오브젝트와 같은 기능의 물건을 새로 만드는 것.
