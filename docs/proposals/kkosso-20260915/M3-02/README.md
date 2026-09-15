# M3-02 공개 출처 준비 목록

#29의 **prepare 제안**이다. 기존 공개 참고 6개를 #28의 13개 구역에 연결하고, 참고 페이지 3개의 현재 HTML 응답을 확인했다. 사진·영상 취득, 재사용 승인 또는 candidate 공개 카탈로그가 아니다.

- [source-inventory.json](source-inventory.json): 입력 커밋·해시, 페이지 확인 시각·응답 해시, 출처별 권리/날짜/사진 해시의 미확정 상태, 13구역별 부족한 자료.
- [validation.json](validation.json): 목록 파일의 해시와 수행한 메타데이터 검사 결과.

세 페이지 모두 HTTP 200이었지만, KORAIL 응답에서는 일반 예매 화면의 제목만 확인했다. 역 안내 본문을 검증했다고 표시하지 않았다. HTML의 title 요소를 모은 문자열에는 SVG UI 제목도 포함될 수 있다. HTML 해시는 확인 시점 응답의 식별자이며 **사진 해시가 아니다**. 원본 HTML은 보관하지 않아 과거 응답 해시를 이 PR만으로 재계산할 수 없고, 동적 페이지는 다음 요청에서 해시가 달라질 수 있다.

기존 설명에 따라 참고 항목이 연결된 구역은 3개이며, 나머지 10개에는 이번에 선택한 출처가 없다. 13개 구역 모두 취득 완료 수는 0이다. 사진 URL·촬영일·원본 바이트·라이선스·개인정보 검토가 확인되기 전에는 메타데이터 인용만 가능하다. 기존 문서의 게시/수정일은 촬영일이 아니며 이번에 다시 검증하지 않았다.

#15 공개 사용 경계는 이제 게시되어 있으나 #28 체크리스트는 여전히 prepare 자료다. 기존 체크리스트의 과거 상태를 수정하지 않았다. `native-validation.md`, `native-team-contracts.md`의 자격 있는 게시 출처와 #28 candidate 입력이 전달되어야 다음 단계로 넘어갈 수 있다. 실제 시설 치수·현재 배치·안전 절차의 증거로 이 목록을 사용하지 않는다.

## 수행한 검사

Git 입력 5개의 SHA-256, 참고 ID 6개의 중복/페이지 연결, 체크리스트와 13개 구역의 정확한 일치, HTML/사진 해시 분리와 취득 승인 0건을 검사했다. `work_graph.mjs validate`도 통과했다. 이 단계에서는 그림 파일과 아트 코드를 변경하지 않았으며 candidate용 `scripts/art` 시험이나 Unity 시험은 실행하지 않았다.

아래 Python 코드를 저장소 루트에서 실행하면 핵심 출처 결속과 구역 연결을 재확인할 수 있다. Python 표준 라이브러리와 해당 Git 커밋이 필요하다.

```python
import hashlib, json, pathlib, subprocess
root = pathlib.Path("docs/proposals/kkosso-20260915/M3-02")
data = (root / "source-inventory.json").read_bytes()
inventory = json.loads(data)
validation = json.loads((root / "validation.json").read_bytes())
assert hashlib.sha256(data).hexdigest() == validation["inventorySha256"]
for item in inventory["sourceBindings"]:
    blob = subprocess.check_output(["git", "show", item["ref"] + ":" + item["path"]])
    assert hashlib.sha256(blob).hexdigest() == item["sha256"]
source = inventory["sourceBindings"][0]
checklist = json.loads(subprocess.check_output(["git", "show", source["ref"] + ":" + source["path"]]))
assert [r["regionId"] for r in checklist["rows"]] == [r["regionId"] for r in inventory["regionCoverage"]]
assert len(inventory["references"]) == 6
assert all(not r["acceptedForAcquisition"] and r["imageContentSha256"] is None for r in inventory["references"])
print("Source bindings and prepare-only region inventory verified")
```

이 제안을 검토한 뒤, 각 구역의 원본 출처와 이용조건을 자격 확인하고 실제 자료를 취득해야 한다. HTTP 응답이나 이 검사의 통과를 라이선스·시설·제품 수용으로 확대하지 않는다.
