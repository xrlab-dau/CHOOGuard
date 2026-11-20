# 화재 대응 연습 · 맞이방(1-1) · 기틀

PR #333이 들여온 "비상상황 연습" 메뉴(심정지 환자 대응)와 같은 자리에 올라가는 두 번째 연습이다. `docs/emergency-lesson-steps.md`의 "1. 화재" > "1-1. 맞이방 화재(휴지통·보조배터리 등 작은 불)" 16단계를 범위로 한다. 심정지 튜토리얼(CardiacTutorialLesson)과 달리 고정 환자·자세·동료 리그를 새로 만들지 않는다. 자율모드(샌드박스) 화재 시뮬레이션(IncidentDirector.Fire.cs, StaffTools.Extinguisher 등)이 이미 실물로 완성돼 있어, 이 연습은 그 위에 "한 건만 일어나게 고르기"와 "순서 채점"만 얹는다.

## 들어가는 길

시작 화면 → **훈련 선택 → 비상상황 연습 → 화재 → 맞이방 작은 불**. 메뉴는 `PracticeCatalog`(카테고리 → 세부 상황, `docs/practice-menu.md`)에서 만들고, 이 항목의 Id는 `PracticeShift.FireFamily`("화재"), `Lesson`은 `PracticeLesson.Fire`라 `FireTutorialLesson`이 붙는다. 같은 화재 카테고리의 전기 설비·주방·열차 하부 화재는 순서 채점 없이 사건 한 건만 일어난다. 사건 고르기는 심정지와 같은 `IncidentDirector.Practice.cs`를 쓴다: 근무가 자리잡으면(`PracticeSettleSeconds`) 2층 맞이방(`hall2f`/`main2f`)에서 `bin_fire`·`bin_battery_fire`·`overheat` 중 첫 후보를 낮은 크기(0.2)로 실제로 일어나게 한다 — JEV 합성이나 다른 사건 없이 이 한 건만.

## 조작

자율모드와 완전히 같다. 새로 만든 조작은 없다.

- 살펴보기(우클릭 0.75초)로 연기·불을 확인.
- 가까운 발신기에 `E`로 비상벨.
- `Q` 보고 묶음에서 화재 보고(역무실 보고에 119 소방 출동이 함께 묻혀 있다 — `FireHazard.Dispatch`가 `Agency.Fire`를 자동으로 부른다).
- 소화기에 `E`로 들기 → 좌클릭 길게 눌러 안전핀 → 좌클릭 유지로 분사(쓸어 뿌리기).
- 불이 꺼지면 `Q` 보고 묶음의 "초기 진화 완료 보고".
- 필요하면 `Q` 방송으로 대피 안내.
- 소방대 도착 후 `Q` 업무·인계에서 네 항목 검토 → 팀장에게 `E` "현장 인계".
- `Q` 요청·설비 → "수신기 복구".

## 순서 점수 (기틀)

`FireTutorialLesson`(`Assets/ChooGuard/App/Fps/Emergency/FireTutorialLesson.cs`)이 100점에서 시작해, 아래 두 가지 연습 규칙 위반을 종류당 한 번만 감점한다(심정지의 `CardiacTutorialScore`와 같은 방식). 막는 판정이 아니라 기록만 한다 — 게임이 이미 막아 둔 것(안전핀 전 분사 등)은 다시 막지 않는다.

| 감점 | 조작 |
|---|---|
| 10 | 역무실 보고 전에 소화기 사용 |
| 10 | 소방대 인계 전에 수신기 복구 |

감점 값은 교육용 가중치이며 공식 평가 기준이 아니다. 결과 화면(`ShiftResult.Show`, 기존 "근무 결과" 화면 그대로)의 수치 칸에 점수와 감점 내역이 덧붙는다. 심정지처럼 따로 만든 결과 화면은 없다.

대피방송은 소화기를 먼저 사용했는지와 관계없이 기록하며 감점하지 않는다. [행정안전부 안전한TV의 다중이용시설 화재 대응 설명](https://www.safetv.go.kr/base/video/view?idx=1114&playlistManagementIdx=&searchCategory=)(04:38)은 직원의 대피 안내와 다른 근무자의 신고·초기 진화를 병행하도록 설명한다. 따라서 이 프로젝트는 소화 시도를 대피 안내의 선행 조건으로 강제하지 않는다. 이 자료를 역무원 전용 매뉴얼이나 16단계 전체의 출처로 간주하지 않는다. 수신기 복구 등 나머지 세부 절차는 현장 기준의 추가 확인이 필요하다.

## 범위 밖(명시적 미구현)

`docs/emergency-lesson-steps.md`의 16단계 중 아래는 게임에 아직 없는 조작(**(신규)** 표기)이라 이번 기틀에 넣지 않았다. 채점도 하지 않는다.

- 2 · 외치기("불이야" 외침)
- 6 · 진화 판단 표시(불길이 작은지 판단하는 UI)
- 10 · 중단 판정(진화 실패 시 중단 신호)
- 13 · 잔류자 확인

1-2(소화전 사용)·1-3(전기 설비 화재)·1-4(주방 기름·가스 화재) 분기도 이번 범위 밖이다. 추가하려면 `PracticeCatalog`의 해당 상황과 `FireTutorialLesson`의 `OnX` 메서드를 검토하고, 해당 분기의 실제 조작(소화전 밸브, 전원 차단 요청, K급 소화기 등)이 이미 있는 자리(`IncidentDirector.FireSafety.*.cs`, `IncidentDirector.KitchenGas.cs`, `IncidentDirector.ElectricPlaza.cs`)에서 훅을 호출한다.

## 검수

**이번 기틀은 노트북에서 작성했고 Unity 플레이로 검증하지 못했다.** 데스크톱에서 다음을 확인해야 한다.

- 컴파일 오류 여부(특히 `EmergencySession.cs`, `IncidentDirector.*.cs`, `StaffTools.cs`, `ShiftLog.cs`에 추가한 `session.FireLesson?.OnX()` 훅).
- 메뉴 "화재" 아래에 "맞이방 작은 불"이 뜨고, 선택하면 2층 맞이방에서 작은 불이 실제로 생기는지(`PracticeOfferSeconds`=90초 안에 후보가 없을 수 있음 — 재시도 필요할 수 있다).
- 보고 → 소화기 → 분사 → 진화 → 소방대 도착 → 인계 → 수신기 복구까지 끝까지 흐르는지.
- 순서를 어겼을 때 토스트와 결과 화면 점수가 맞게 나오는지.
- `Assets/ChooGuard/Tests/PlayMode/FireTutorialMenuTests.cs`의 PlayMode 테스트 통과 여부.
