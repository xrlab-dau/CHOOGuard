# Lane D 조사 결과 — 절차·판단·보고·안내 수준 (SWAT 4 / Ready or Not / Papers, Please / 112·911 Operator / Hitman WoA / Phasmophobia)

작성 2026-10-04 · 담당 `LaneD_ProcedureJudgment` · 조사 전용(코드·씬·설정 무수정).
증거 등급: **[공식]** 제작사 매뉴얼·스토어·변경 기록·개발 로그·게임 내 UI 문구 / **[2차]** 위키·공략·커뮤니티(교차 확인 표시) / **[관찰 URL t=mm:ss]** 직접 확인한 영상 프레임 / **[추론]** 내 추정.
영상·프레임은 `media/D/` 에만 저장했다(`.gitignore` 대상). 시점은 원본 영상 기준 근사값이며, 다운로드 구간 시작 오차(키프레임)로 ±5 s 가능.
현행 CHOOGuard 진단은 `findings.md §0`(이하 "현행 §0.x")을 인용한다.

---

## 0. 한눈에 보는 결론 (D 레인)

1. **"지시받는 보고"와 "스스로 하는 보고"는 다른 문법이다.** SWAT 4·Ready or Not에서 보고는 *내가 본 사람/물건을 바라보고 능동 입력*하고, 본부(TOC)는 `Roger that. Trailers standing by.` 처럼 **확인응답만** 한다 — 다음 할 일을 지시하지 않는다 [관찰 SWAT4 TiuBdq7O5XE 원본 t≈2:16–3:09]. CHOOGuard 는 반대다(무전 답변이 다음 단계를 명령, 현행 §0.2-2).
2. **보고 누락은 즉시 막히지 않고 사후 집계로 깎인다.** SWAT 4 디브리핑 `Report status to TOC 7/7 → 5점`, Ready or Not `EVIDENCE SECURED 7/14`·`CIVILIANS SECURED 4/5` 처럼 분모만 공개되고 **어디에 있었는지는 끝까지 말하지 않는다** [관찰]. 지각하지 않은 것을 노출하지 않는 CHOOGuard 하드룰과 양립하려면 분모를 "플레이어가 지각한 것"으로 한정해야 한다(SWAT 4 의 ESC 카운터는 아직 닿지 않은 민간인까지 센다는 누수 보고가 있다 [2차 GameSpot 가이드 p.6]).
3. **평가는 HP·타이머가 아니라 "절차 항목 점수 + 위반 감점 + 통과 임계"다.** SWAT 4: 임무 완료 40(전부-아니면-0) + 비례 보너스 + 감점, 난이도별 통과 0/50/75/95 [공식 매뉴얼 + 관찰]. 112 Operator: `Preventable deaths`·`Unresolved incidents`·`Efficiency`·`Reputation` [관찰]. CHOOGuard 의 `No score`(현행 §0.3)와 정반대 극.
4. **판단이 존재하려면 오답이 선택지에 있어야 한다.** 112 Operator 의 CPR 안내 분기는 `REPEAT UNTIL AMBULANCE ARRIVES / REPEAT BREATHS / MAKE 30, WAIT FOR AMBULANCE` 처럼 틀린 항목이 같이 나온다 [관찰 Tj9O-DDGXHY t≈28:33]. Papers, Please 는 "무엇이 맞는지"를 규칙집 줄로만 제공하고 정답은 말하지 않는다. CHOOGuard Q 휠은 오답이 없다(현행 §0.2-3).
5. **피드백은 결정 *뒤*, 원인만.** Papers, Please 의 입국자 퇴장 후 인쇄되는 위반 통지서 `M.O.A. CITATION / Protocol Violated / Passport: Invalid issuing city` 는 *무엇이 틀렸는지*만 말하고 *다음에 뭘 하라*고는 하지 않는다 [관찰 l9mVDhPMbHI t≈0:12]. Pope: "The immediate feedback also allows me to skip a lot of introductory tutorial text" [공식 개발 로그 2013-03].
6. **안내 수준은 독립 노브이고, 값이 매겨진다.** Hitman: `Mission Story Guidance [FULL/MINIMAL/OFF]`, `Objectives [AUTO/ALWAYS ON/ALWAYS OFF]`, `Instinct [ON/OFF]` [공식 UI 문구, 관찰]. Phasmophobia 커스텀 난이도는 설정마다 `Rewards ×3.05` 같은 **실시간 보상 배율**이 바뀐다(0×–15×) [관찰 + 2차]. 안내를 낮추면 점수 배율이 올라가는 구조다.
7. **안내 자료 자체를 지각으로 해금한다.** Hitman Mission Story 는 "도청·사건 목격·물건 조사 뒤에야 드러난다" [2차 위키]. Phasmophobia 저널은 증거 체크를 **플레이어가 직접** 하고 게임은 끝에서만 채점한다 [2차 위키 + 공식 Steam 문구 "minimal user interface"]. CHOOGuard 하드룰(미지각 위험 비노출)과 구조가 같다.

---

## 1. SWAT 4 (Irrational Games, 2005) — 보고·ROE·점수의 원형

### 1.1 출처
| # | URL | 유형 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| S1 | https://archive.org/details/swat4instructionmanual (djvu 텍스트 로컬 `media/D/swat4_manual.txt`) | [공식] 설명서 스캔(OCR 훼손 있음) | 2005 | 난이도별 통과 점수 `Easy…regardless of score / Normal 50 / Hard 75 / Elite 95`, `REPORTING TO TOC`, `SECURING EVIDENCE`, `PAUSE MENU`, `COMPLETING AND FAILING MISSIONS`, `SCORING`, 명령 인터페이스, 컨텍스트 홀드 행동 |
| S2 | https://induktio.net/download/SWAT4_Gamespot_guide.pdf | [2차] GameSpot 공식 가이드 PDF(Radcliffe) | 2005 | 점수 항목 40/5/10/5/5/5/5/25, 감점 −5/−10/−15, ESC 에서 누락 보고·무기 수 확인, `Failing to do so subtracts 5 points` |
| S3 | https://policequest.fandom.com/wiki/Mission_Score | [2차] 위키 | 2026 | 바닐라/Elite Force(SEF) 점수·감점 표, 랭크 칭호, "SEF 에서는 대부분의 감점을 발생 즉시 표시" |
| S4 | https://policequest.fandom.com/wiki/Rules_of_Lethal_Engagement | [2차] 위키 | 2026 | ROE: 결과(무력화) 기준, 허용/불허 사례 |
| S5 | https://policequest.fandom.com/wiki/Mission_Difficulty | [2차] 위키 | 2026 | 난이도 표(0/50/75/95, 플레이어 피해 배율 0.5/1.0/1.5/1.5), SEF 에서 Elite 90 |
| S6 | https://www.youtube.com/watch?v=TiuBdq7O5XE (`media/D/swat4_foodwall_100_*.webm`) | [관찰] Food Wall Restaurant Elite 100/100. 원본 3:12 중 말미 61 s 를 내려받음(클립 0:00 ≈ 원본 2:11) — 본문 t 표기는 **원본 기준** | — | **결과 화면 프레임**, 인게임 TOC 채팅 로그, 컨텍스트 명령 박스 |

버전: 매뉴얼=바닐라 2005. 위키 표의 "SEF"=SWAT 4: Elite Force 모드(바닐라 아님).

### 1.2 조작표 (verb → 입력)
| 동사 | 입력 | 근거 |
|---|---|---|
| 순응 외침(shout compliance) | 마우스 가운데 버튼, 조준점이 대상 위일 때 | [공식 S1] `shout compliance (Default Key: Middle Mouse Button while … on target)` |
| 수갑(restrain) | 대상 응시 + `Fire (Hold)` — 게이지 | [관찰 S6 t≈2:15, 하단 중앙 막대 `Fire (Hold) Restrain Civilian`; 해상도 낮아 문구 일부 판독] |
| **TOC 상태 보고** | 대상 지목 + `Use` 키(기본 마우스 가운데 버튼) | [공식 S1] `must be reported by targeting them and pressing the Use key (Default Key: Middle Mouse Button)` |
| 증거(무기) 확보 | 걸어가 지목 + `Use` | [공식 S1] `SECURING EVIDENCE` |
| 문 열기 | `Use` | [관찰 S6 `Use: Open Door`] |
| 잠금 따기/폭탄 해체/쐐기/광학봉 | 해당 도구 + `FIRE (Hold)` | [공식 S1] `Pointing at Locked Door with Toolkit FIRE (Hold): pick Lock` 등 |
| 팀 명령 | 기본: 스페이스(`quick command`), 라디얼: 오른쪽 버튼, 팀 전환: Tab(Blue/Red/Gold) | [공식 S1] |
| 목표·절차 화면 | ESC / B,M,O | [공식 S1] `Display Objectives and Scores key (Default Keys: B, M, and O)` |

### 1.3 조작 문법
- **지각 → 대상 → 단일 맥락 입력**. 보고는 "대상 지목 + Use" 한 번이다. 보고 가능한 대상은 인물 상태 변화(부상·사망·체포)와 임무 불능이 된 아군 [공식 S1].
- **컨텍스트 기본 명령 박스**: 화면 우하단 상자가 조준한 것(문·사람)에 따라 `MOVE & CLEAR` / `RESTRAIN` / `OPEN & CLEAR` 로 바뀌고 상자 색(노랑=전체)이 수신 팀을 표시 [공식 S1 + 관찰 S6 t≈2:15/2:23/2:31].
- **홀드 제스처가 "시간 비용"을 만든다**: 수갑·따기·설치는 홀드(게이지) [공식·관찰].
- **음성 보고가 곧 UI 피드백**: 채팅 로그에 `You: TOC, this is entry team. Civilian secured and ready to evacuate.` → `T.O.C.: Roger that. Trailers standing by.` [관찰 S6 t≈2:16]. 증거: `You: That's our gun.` → `You: TOC, this is entry team. Evidence secured.` → `T.O.C.: TOC to entry team, objectives complete.` [관찰 S6 t≈3:04].
- **구속 선행**: 민간인 구출 목표는 "순응 + 수갑" 상태여야 인정 [공식 S1 `must be compliant and handcuffed`].

### 1.4 안내·교육 모델
- 임무 브리핑 + 목표 목록(`Bring order to chaos`, `Rescue all of the civilians`, 이름 있는 대상). 맵 위 목표 마커는 없다(설명서·관찰에서 확인되지 않음) [추론: 마커 없음은 관찰 영상 범위 내 판단].
- **진행 중 점검은 ESC 화면**: "현재 목표, 절차(procedures), 팀 상태, 지도" [공식 S1]. GameSpot: "ESC 로 놓친 보고·무기 수를 볼 수 있다. 다만 아직 닿지 않은 민간인까지 센다" [2차 S2 p.6]. → *카운트는 주지만 위치는 주지 않는다* + *누수 위험*.
- 종료 안내: `You have COMPLETED the mission! / Press 'Pause' to proceed to Debrief.` — **종료를 플레이어가 선택**하고, 설명서는 "**디브리핑 전에 무기를 확보하고 모든 상태를 TOC 에 보고하라**(점수 상승)"고 권한다 [공식 S1 `COMPLETING AND FAILING MISSIONS`, 관찰 S6 t≈3:04].
- 난이도 설명 문구: `Raising the difficulty will make suspects harder to apprehend and you will need a higher score` [공식 S1].

### 1.5 실패·결과 모델
- 실패 조건: 목표 하나라도 실패, 또는 플레이어 사망 [공식 S1]. 민간인 사망 시 임무 실패 [공식 S1 OCR "If any civilians are killed during a mission, … will fail"].
- 점수 미달 = 미통과: 설명서는 난이도별 점수 미만이면 `advance`(다음 임무 진행)를 못 한다고 쓰고, 위키는 목표 달성 후 점수와 난이도를 비교해 pass/fail 을 정한다고 쓴다 [공식 S1; 2차 S3]. 즉 *목표 달성 + 절차 점수*의 이중 관문이다.
- **ROE(결과 기준)**: 치명 무력은 (a) 대상이 누군가에게 총구를 겨눔 (b) 임무 시작 후 발사 (c) 누군가를 부상·무력화·사망시킨 경우만 정당. 도주 중인 무장자, 무기를 내리거나 버린 자, 비무장 자가 보조무기를 꺼내는 순간, 경계 중 총구를 훑는 자에게는 불허. **ROE 는 행위가 아니라 결과(대상이 무력화·사망했는가)에 걸린다** [2차 S4].
- 부상 효과: 팔 피해=조준 정확도, 다리 피해=이동 저하 [공식 S1 `TAKING DAMAGE`].

### 1.6 평가 모델 (정확한 항목)
**통과 임계(난이도)** [공식 S1, 수치 일치 2차 S5]
| 난이도 | 통과 점수 | 플레이어 피해 배율 [2차 S5] |
|---|---|---|
| Easy | 0 (`regardless of score`) | 0.5 |
| Normal | 50 | 1.0 |
| Hard | 75 | 1.5 |
| Elite | 95 (SEF 는 90) | 1.5 |

**점수 항목(바닐라)** — 시작 0점, 소수는 버림 [2차 S3, 관찰로 일치 확인]
| 보너스 | 최대 | 규칙 |
|---|---|---|
| Mission completed | 40 | 전부-아니면-0. 목표 실패·플레이어 무력화 시 0 |
| Suspects arrested | 25 | 비례 |
| Suspects incapacitated | 15 | 비례(체포·사망과 중복 불가) |
| Suspects neutralized | 0 | 비례(바닐라에서 0점) |
| No suspects neutralized | 5 | 전부-아니면-0 |
| No civilians injured | 5 | 전부-아니면-0, "피해를 조금이라도 입으면" 실패 |
| No officers down | 10 | 비례(플레이어 무력화도 차감) |
| Player uninjured | 5 | 전부-아니면-0 |
| **Report status to TOC** | **5** | **비례** |
| All evidence/weapons secured | 5 | 비례 |

| 감점 | 값(바닐라) | SEF |
|---|---|---|
| Unauthorized use of force | −5 /건 | −5 |
| Unauthorized use of deadly force | −10 /건 | −20 |
| **Failed to report a downed officer** | **−5 /건** | −5 |
| Injured a fellow officer | −5 | −10 |
| Incapacitated a fellow officer | −15 | −25 |
| Incapacitated a hostage | −5 | −25 |
| Killed a hostage | −15 (임무 실패) | −50 |
| Failed to report a downed civilian / suspect | — | −5 /건 (SEF 신설) |
| Failed to apprehend fleeing suspect | −5 (Fresnal St. Station 한정) | −5 |

SEF 는 감점 대부분을 **발생 즉시 표시**한다(바닐라는 디브리핑 때 일괄) [2차 S3].
랭크 칭호(점수): 100 Chief Inspector / 95 Inspector / 90 Captain / 85 Lieutenant / 80 Sergeant / 75 Patrol Officer / 70 Reserve Officer / 60 Non-sworn Officer / 50 Recruit / 35 Washout / 20 Vigilante / 0 Menace [2차 S3].

**디브리핑 화면 직접 관찰** [관찰 https://www.youtube.com/watch?v=TiuBdq7O5XE t≈3:09, `media/D/swat4_foodwall_100_*.webm` clip 0:58]
```
MISSION COMPLETED!           (우상단 QUIT)
목표: Secure the MAC-10 / Rescue all of the civilians / Bring order to chaos /
      Neutralize Lian Niu / Neutralize Alex Jimenez  — 모두 Completed (초록)
BONUSES:                                         (분모)  (점수)
  Mission completed                                        40
  Suspects arrested                              3/3       25
  Suspects incapacitated                         0/3        0
  Suspects neutralized                           0/3        0
  No suspects neutralized                                    5
  No civilians injured                                       5
  No officers down                               0/5       10
  Player uninjured                               1/1        5
  Report status to TOC                           7/7        5
  All weapons secured                            4/4        5      TOTAL 100
PENALTIES: No Penalties Deducted!                                  TOTAL 0
Required score: 95   |  Total: 100/100   |  Ranking: Chief Inspector
[RESTART MISSION] [EQUIPMENT] [DEBRIEFING(회색)] [NEXT MISSION]
```
핵심: **분모(`7/7`)는 이 임무에서 보고 가능했던 상태 수**이며, 임무 중 화면에는 나오지 않는다. 디브리핑에서만 공개 [관찰].

### 1.7 CHOOGuard 적용
| 판정 | 기제 | 대응 CHOOGuard 동사 | 구체 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 보고 = 지각한 대상 응시 + 맥락 입력, 본부 답은 *확인응답만* | 무전 보고, 기관 요청, 사상자 평가 | 대상(사람/위험/물건)을 시야로 확인한 때만 보고 프롬프트 노출(시야 판정은 현행 §0.4 `IncidentDirector.cs:456-474` 재사용 [추론]). 역무실 답변은 `확인했습니다` 수준으로 줄이고 `초기 진화 가능하면 시도하세요`(Hazards.cs:268) 류 지시문 제거 | S1 `targeting them and pressing the Use key`; 관찰 `T.O.C.: Roger that.` |
| **Adopt** | 종료를 플레이어가 선택, 미완이면 점수만 깎임 | 인계, 근무 종료 | 지휘 기관 도착 100 s 자동 종료(현행 §0.3, IncidentDirector.cs:176-182) 대신 `Press 'Pause' to proceed to Debrief` 형 "근무 종료" 선택. 종료 전 미보고·미인계는 평정 감점 | S1 `recommended that you secure all weapons and report all statuses … before debriefing`; 관찰 t≈3:04 |
| **Adopt** | 분모는 디브리핑에서만, 진행 중엔 위치 비공개 | 근무 종료 평가 | 평정 화면에 `보고 n/m`, `인계 n/m` 표기(`m` 은 **플레이어가 지각한 개체 수**). 진행 중 Tab 에 "미보고 개체 k개"(위치 없음)만 허용 | 관찰 `Report status to TOC 7/7`; S2 누수 경고 |
| **Adapt** | 점수=전부-아니면-0 기본점 + 비례 보너스 + 항목 감점 | 평가 | 기본점=`현장 인계 완료`(all-or-nothing). 비례: 사람 보호(발견 시점 대비 악화 없음), 보고 완전성, 접근 통제, 본인 노출(smoke/heat). 감점: 지각한 위험 방치, 전원 살아 있는 설비에 물 사용·위험 구역 진입처럼 *역무원이 해선 안 되는 행동*. 항목은 **Hazard 공통 속성**(인명·확산·공급원·노출)으로만 정의 | S3 표; 하드룰: 종류 비고정, 역무원 권한 |
| **Adapt** | 난이도=통과 임계 + 피해 배율 | 근무 평정, 위험 | 평정은 5등급 정도로 축소(SWAT 의 12칭호는 과함), 통과선·위험 배율은 안내 수준 노브(§5·§6)와 묶어 옵션 | S1·S5 |
| **Adapt** | ROE 의 *결과 기준* 판정 | 비정상 조치 감점 | "행위를 했는가"가 아니라 "그 결과 사람이 더 다쳤는가/설비가 악화됐는가"를 감점 기준으로(예: 재투입 재발화는 이미 존재, 현행 §0.4) | S4 |
| **Reject** | 팀 AI 명령(Space/RMB/Tab Gold·Red·Blue) | — | 역무원은 분대를 지휘하지 않음. Q 휠(현행) 유지 | S1 |
| **Reject** | 칭호 12단계 랭크 | — | 과도. 5등급이면 충분 | S3 |

---

## 2. Ready or Not (VOID Interactive, 1.0=2023-12-13, 현행 v1.4.3a 2026-05-19)

### 2.1 출처
| # | URL | 유형 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| R1 | https://store.steampowered.com/api/appdetails?appids=1144200 (store.steampowered.com/app/1144200) | [공식] 스토어 | 출시 2023-12-13 | "Follow the rules of engagement", "Squadmate and hostage deaths take a profound psychological toll on surviving team members…" |
| R2 | https://voidinteractive.net/category/all/updates/ | [공식] 변경 기록 목록 | 2026-07-30 최신 | 현행 v1.4.3a(2026-05-19). 점수·보고 규칙 관련 문구는 열람 가능한 본문에서 못 찾음 |
| R3 | https://www.youtube.com/watch?v=ESIuvmTg2dQ (`media/D/ron_training_*`) | [관찰] Training 튜토리얼(콘솔 입력 아이콘) | — | 인게임 교육 패널 전문 |
| R4 | https://www.youtube.com/watch?v=NBlCbWl4xFA (`ron_pc_training_*`) | [관찰] PC Training | — | 월드 공간 프롬프트 `PRESS … TO REPORT EVIDENCE`, 대상 위 `RESTRAINING` 링 |
| R5 | https://www.youtube.com/watch?v=PNZWrJcsP90 (`ron_tycA_*`) | [관찰] Thank You, Come Again S/Hard | — | **점수 화면**, 분대 HUD `[Z] FALL IN` |
| R6 | https://www.youtube.com/watch?v=BRw8-_1WinI (`ron_B_*`) | [관찰] Twisted Nerve B랭크, 화면 `VERSION 39903 (DEC 13 2023)` | — | 점수 화면(미완 집계), `MISSION SOFT COMPLETE / End The Mission?` |
| R7 | https://readyornot.wiki.gg/wiki/Grading , /wiki/Missions , /wiki/Guides/Getting_started , /wiki/Options_TEAMWORK | [2차] 위키 | 2025-06~2026 | 등급 정의, 주요/소프트 목표, 기본 키 |
| R8 | https://www.youtube.com/watch?v=7HApv2vS3EI (자막) | [2차] 플레이어 분석 | — | 보고·구속·증거 3단계, +5 보고 점수, 증거 누락 원인 |
| R9 | https://progameguides.com/ready-or-not/ready-or-not-how-to-get-s-rank/ | [2차] | 2023-12-16 | 점수 구성 6항목 요약 |

### 2.2 조작표
| 동사 | 입력 | 근거 |
|---|---|---|
| 순응 외침 | `F`(Interact / Call for surrender) | [2차 R7 keyboardista·shortcutposters]. Training 문구: `PRESS (A) to YELL FOR COMPLIANCE` [관찰 R3 t≈13:55] |
| 비순응자 제압 | `HOLD (B) to MELEE, then YELL FOR COMPLIANCE` | [관찰 R3] |
| 구속(restrain) | 순응·무력화 대상 근처 `PRESS (A) TO RESTRAIN`, 대상 위 `RESTRAINING` 진행 링 | [관찰 R3·R4] |
| **보고(report)** | 구속된/무력화된 대상, 또는 증거를 *바라보고* 프롬프트가 뜨면 `PRESS (A) TO REPORT` | [관찰 R3 t≈14:09, R4 t≈12:50] |
| 증거 확보 | 떨어진 무기를 보고 `HOLD (A) TO SECURE EVIDENCE` | [관찰 R3 t≈18:50] |
| 기본 명령 | `Z` Issue Default Command(조준한 것 기준) | [2차 R7 Options_TEAMWORK] + [관찰 R5/R6 HUD `[Z] FALL IN`] |
| 명령 인터페이스 | `MMB` Open SWAT Command Interface, 휠 위/아래 요소 순환, `F5/F6/F7` Gold/Blue/Red, `Left Shift` Hold Command, 숫자 `1–9` 명령 키, `Tab` Back | [2차 R7] (콘솔 아이콘은 위 A/B) |
| 태블릿(목표) | `Tab` 길게 | [2차 R7 Getting_started `Press and hold TAB to bring up your Tablet… Inside a mission, … review your current objectives`] |

### 2.3 조작 문법
- **3단계 분리: Restrain → Report → Secure Evidence**. 구속했다고 보고가 되지 않으며, 보고했다고 증거가 확보되지 않는다. 각각 점수 항목이 따로다. 플레이어 분석: "무력화된 용의자를 쓰러뜨린 뒤 **보고하고, 구속하고, 증거를 줍는** 세 가지를 해야 한다. 구속은 잊기 쉽다" [2차 R8 t≈4:21–4:34].
- **월드 공간 프롬프트는 바라본 대상에만 뜬다.** 교육 문구: `Look at the evidence. If a prompt appears, press (A) to report.` [관찰 R3 t≈18:59]. 증거의 *존재 여부*를 모르면 프롬프트도 없다 → 지각 기반.
- **보고 대상의 정의**: `Evidence can include contraband, casualties, large amount of money etc.` [관찰 R3 t≈18:52]. 즉 **사상자·금전·밀수품도 "증거 보고" 대상**.
- **상태 구분 UI**: 무력화(어지러운 아이콘) vs 사망(해골). 용의자는 해골을 가장할 수 있지만 무력화는 가장할 수 없다 [2차 R8 t≈0:35–0:53]. → "겉보기 상태와 실제 상태가 다를 수 있음"을 판단 요소로 사용.
- **분대 HUD**: 우하단에 대원별 `INJURED` 상태와 현재 명령(`FALL IN / SINGLE FILE`, `RESTRAIN / RESTRAINING SUSPECT·CIVILIAN`, `MOVE AND CLEAR`), 맨 아래 `[Z] FALL IN`(지금 Z 를 누르면 나갈 기본 명령) [관찰 R5 t≈3:00–3:30, R6 t≈20:46].

### 2.4 안내·교육 모델
- **Training**: 개별 행동마다 패널(`RESTRAIN POTENTIAL SUSPECTS`, `SECURE EVIDENCE`, `REPORT EVIDENCE`)과 월드 프롬프트. 패널 문구 원문:
  - `RESTRAIN POTENTIAL SUSPECTS — PRESS (A) to YELL FOR COMPLIANCE. For non-compliant individuals, HOLD (B) to MELEE, then YELL FOR COMPLIANCE. Once compliant or incapacitated, approach and PRESS (A) to RESTRAIN. If the individual is arrested or incapacitated, PRESS (A) to REPORT.` [관찰 R3 t≈13:55–14:20]
  - `SECURE EVIDENCE — Incapacitated or compliant Suspects can drop EVIDENCE. This can include WEAPONS. LOOK at the dropped weapon and HOLD (A) to SECURE EVIDENCE. Collecting evidence counts towards your total mission score.` [관찰 R3 t≈18:50]
  - `REPORT EVIDENCE — Report any EVIDENCE of a crime in the scene. Evidence can include contraband, casualties, large amount of money etc. LOOK at the EVIDENCE. If a prompt appears, PRESS (A) to REPORT.` [관찰 R3 t≈18:52]
- 본임무에는 **월드 목표 마커가 보이지 않는다** [관찰 R5·R6 프레임 전반에서 마커 없음; 공식 문서로 확인한 것은 아님]. 목표 목록은 태블릿(`Tab`)에서만, 주요 목표 이름은 브리핑에 있고 **소프트 목표는 태블릿·브리핑에 없다** [2차 R7 Missions: "hidden objectives that are not shown on the Tablet or Briefing"] — 이름은 **임무 종료 후 점수 화면**에서 공개된다(아래).
- **소프트 완료 게이트**: 주요 목표가 끝나면 좌상단에 `MISSION SOFT COMPLETE / End The Mission? / Y - Vote Yes 0` 가 뜬다 [관찰 R6 t≈20:46]. 그대로 끝낼 수 있지만 **남은 소프트 목표·증거는 포기**. 모든 소프트 목표를 끝내면 `hard complete` 로 자동 종료 [2차 R7 Missions].
- 정답 안내 없음: 보고 후 응답은 TOC 음성 한 줄이며, 다음 행동 지시는 확인되지 않았다. 교육 영상에는 자막 줄(`Judge: Get down NOW`, `Unknown: No, get away from me!`)이 있으나 보고 직후 TOC 대사의 *내용*은 직접 확인하지 못했다 [관찰 R3; 미확인].

### 2.5 실패·결과 모델
- 임무 실패/`F`: 임무 실패 또는 재앙적 완료 [2차 R7 Grading].
- 영구 결과: Commander 모드에서 분대원·인질 사망이 생존 대원의 **심리적 부담→성능 저하·은퇴**로 이어짐 [공식 R1]. (CHOOGuard 는 캠페인이 없어 직접 이식 불가 — 개념만.)
- 즉시 결과: 분대 HUD 의 `INJURED` 상태 [관찰].

### 2.6 평가 모델 (직접 관찰한 점수 화면 2건)
**① Thank You, Come Again — S / Hard** [관찰 https://www.youtube.com/watch?v=PNZWrJcsP90 t≈3:50–4:03, `media/D/ron_score_final.png`]
```
THANK YOU, COME AGAIN        TIME 03:51:02   PERSONAL BEST 06:36:02
            (원형 게이지 안) S      2,890      PERSONAL BEST S  HARD
SCORES
 4/4 MISSION OBJECTIVES                         2,000
        BRING ORDER TO CHAOS 500 / RESCUE ALL OF THE CIVILIANS 500 /
        FIND CRYSTAL LEIGHTON 500 / FIND THE STORE MANAGER 500
 1/1 SOFT OBJECTIVES                               50
        REPORTED INCAPACITATED VETERAN             50   ← 숨은 소프트 목표, 종료 후에야 이름 공개
 4/4 SUSPECTS SECURED                             140
 5/5 CIVILIANS SECURED                            175
 5/5 EVIDENCE SECURED                             125
 4/4 NO OFFICERS DEAD                             400
```
합계 검산: 2,000+50+140+175+125+400 = 2,890 ✔.

**② Twisted Nerve — B** [관찰 https://www.youtube.com/watch?v=BRw8-_1WinI t≈21:10, `media/D/ron_B_final.png`, 집계 애니메이션 중간(3,118)]
```
TWISTED NERVE  / MISSION RATING        TIME 20:46.01
 5/5 MISSION OBJECTIVES                         2,500
        BRING ORDER TO CHAOS / ARREST 2 SUSPECTS / RESCUE ALL CIVILIANS /
        LOCATE 2 CRYSTAL METH LABORATORIES / LOCATE CRYSTAL METH STORAGE  (각 500)
 1/2 SOFT OBJECTIVES                               50
        FOUND INCAPACITATED MINOR                  50   (나머지 1개는 이름조차 표시되지 않음)
 13/13 SUSPECTS SECURED                           245
 4/5 CIVILIANS SECURED                            170
 7/14 EVIDENCE SECURED                            175
```
**읽을 점**: (a) 부족분은 **`4/5`, `7/14`, `1/2` 분모/분자로만** 알려지고 *어디 있었는지*는 알려주지 않는다. (b) 항목 점수가 균일하지 않다(TYCA 민간인 35점/명 vs TN 4/5=170) — 산식 비공개 [미확인]. (c) 생존 항목(`NO OFFICERS DEAD`)이 목표 묶음(2,000) 다음으로 큰 단일 항목(400) [관찰]. (d) 숨은 소프트 목표의 이름이 **보고·발견 행동**(`REPORTED INCAPACITATED VETERAN`, `FOUND INCAPACITATED MINOR`)이다.

**등급(S–F)**: S=전 주요·소프트 목표, 사망 없음, 증거 전부, ROE 위반 없음. A/B/C/D 는 `Few/Some/Several/Many`(사망·증거 누락·소프트 목표 누락·ROE 위반 중 *하나라도*). F=임무 실패 [2차 R7 Grading]. 치명 무력으로 용의자를 사살하면 최고 `A+` [2차 R7 Missions].
**보고 점수**: HUD 에 `+5` 팝업(보고), 구속 점수는 안 보이나 반영됨, 무력화 용의자 구속+보고+증거 확보 시 35점 [2차 R8 t≈3:47, 4:46]. "무력화=체포와 같은 점수, 단 ROE 위반 없을 때" [2차 R8 t≈3:13].

### 2.7 CHOOGuard 적용
| 판정 | 기제 | 대응 CHOOGuard 동사 | 구체 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 월드 프롬프트는 *바라본* 대상에만, 존재를 모르면 없음 | 위험 발견/보고, 사상자 | 보고 프롬프트(`보고하기`)를 시야 확인된 개체에만 노출. 아직 시야에 들어오지 않은 위험은 프롬프트·마커·카운트에 포함하지 않음 | 관찰 R3 `Look at the evidence. If a prompt appears…` |
| **Adopt** | 점수 화면 `x/y` + **위치 비공개** + 숨은 소프트 목표 사후 공개 | 근무 종료 평가 | 평정 항목을 Hazard 공통 사실로 생성(예: `쓰러진 사람 발견·보고`, `방송 전 접근 통제`) — 이름·존재는 종료 후 공개. **LLM 합성 사건이므로 항목은 '지각된 개체 목록'에서 파생**(종류·개수 비고정, 하드룰) | 관찰 `REPORTED INCAPACITATED VETERAN` |
| **Adopt** | 소프트 완료 게이트(조기 종료 가능, 대신 미완 포기) | 인계, 종료 | 인계 후 `근무 종료?` 게이트 — 종료 전 남은 미보고·미확인 개체 수를 *개수만* 힌트 | 관찰 `MISSION SOFT COMPLETE / End The Mission?` |
| **Adapt** | 구속→보고→증거확보 3단계 분리 | 사상자 평가·보고·인계 | `상태 확인`(행동) → `보고`(독립 입력) → `인계 시 전달`(인계 완료 시 요약 전달 여부). 현행은 확인·진정·AED 내려놓기가 로그만 남음(현행 §0.3) → 각 단계를 평정 항목으로 승격 | 관찰 R3, 2차 R8 |
| **Adapt** | 겉보기 상태≠실제 상태(사망 가장) | 사상자 평가 | 역무원은 **사망 선언 권한이 없다**. 상태 어휘를 `반응 있음/반응 없음/호흡 없음`으로 한정. "반응 없음 확인 후 심정지 의심 보고"가 올바른 분기. 가짜 안정 상태(조금 뒤 악화)는 기존 0–4 단계 모델(현행 §0.4)로 표현 | 2차 R8 |
| **Adapt** | 큰 비중의 "아무도 악화/사망 없음" | 평가 | 가중치 최대 단일 항목 = `플레이어가 지각한 사람 중 악화 없음` | 관찰 `NO OFFICERS DEAD 400` |
| **Reject** | MMB 라디얼 + 숫자키 명령 체계 | — | 분대 지휘 불필요. 현행 Q 휠 유지(단 오답 포함, §4) | 2차 R7 |
| **Reject** | Commander 캠페인 스트레스 | — | 단판 근무에 부적합 | 공식 R1 |

---

## 3. Papers, Please (Lucas Pope, 개발 2012-11~2014, Steam 2013-08-08)

### 3.1 출처
| # | URL | 유형 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| P1 | https://dukope.com/devlogs/papers-please/ (tig-00…tig-13) | [공식] 개발자 devlog(원래 TIGSource 스레드) | 2012-11 ~ 2013-08 | 설계 의도: 규칙집, 검사 모드, 위반 통지(citation), 일일 공지, 오류 수 |
| P2 | https://store.steampowered.com/app/239030 (appdetails) | [공식] 스토어 | 2013-08-08 | "Using only the documents provided by travelers and the Ministry of Admission's primitive inspect, search, and fingerprint systems you must decide who can enter…" |
| P3 | https://papersplease.fandom.com/wiki/Inspection_mode , /Citation , /Discrepancy | [2차] 위키 | 2025-26 | 검사 모드 문구, 위반 통지 벌금 수열, 발견 유형 |
| P4 | https://www.youtube.com/watch?v=fHw0ggLlJfM (자막+프레임), ZZnN4tTKSPg, l9mVDhPMbHI | [관찰]/[2차] | — | 규칙집 UI, 검사 모드 화면, Day 1 규칙, 위반 통지서 |

### 3.2 조작표
| 동사 | 입력 | 근거 |
|---|---|---|
| 규칙집 열기 | 책상의 규칙집 클릭(페이지 넘김) | [공식 P1 tig-00] "rule book … tab" |
| **검사 모드 진입** | 우하단 붉은 아이콘 클릭; 업그레이드로 스페이스·더블클릭 | [2차 P3] |
| **불일치 선택** | 화면의 두 정보(서류 필드·규칙 줄·책상 빈칸·시계)를 차례로 클릭 | [공식 P1 tig-01 `Highlight any two pieces of information`] |
| 결과 | `No correlation` / `Matching data` / `Discrepancy detected`(심문 버튼) / `Discrepancy cleared` | [2차 P3] + 관찰 `DISCREPANCY DETECTED` |
| 승인/거부 | 스탬프 바에서 `APPROVED`/`DENIED` 스탬프를 서류 위에 내림 | [공식 P1 tig-01 "Lemme Just Stamp This"] |

### 3.3 조작 문법
- **정보는 전부 화면에 있고, 사용자는 두 개를 *선택*해 충돌을 *주장*한다.** 예: 서류 누락 = "빈 책상 + 규칙 `All travelers must have a valid entry permit`" 선택 [공식 P1 tig-01]. 한 인터페이스로 모든 종류의 오류를 표현한다("every kind of error can be pointed out using this simple interface").
- **시간이 비용**: "TIME COSTS TIME" — 서류 정리·규칙 찾기·질의 모두 실시간, 별도 HUD·설명 없음 [공식 P1 tig-01]. 600 개 정도의 사실 쌍(약 35 개 정보)이 있어 모두 눌러 보기엔 시간이 안 된다 [공식 P1 tig-01].
- **절차의 번거로움이 재미**: "The gameplay is really about procedure and rigamarole so I wouldn't want to skip through elements like this" [공식 P1 tig-01, 2012-12].
- **오류는 한 번에 하나만**: "The counterintuitive thing is that more errors makes the game easier, not harder. So in general, each immigrant will have at most one thing wrong." [공식 P1 tig-06, 2013-05]. 위키: 서류당 사소한 오류(심문으로 해소)와 중대한 오류 각 1 개 이하 [2차 P3].
- **판정 시점**: 항목은 "입국자가 부스를 떠나는 순간의 불일치 상태"로 채점. 검사 모드로 짚었는지는 필수가 아님(초기에는 거부 스탬프를 곧바로 찍어도 됨; 18일차부터 거부 사유 스탬프 필요) [2차 P3 Discrepancy].

### 3.4 안내·교육 모델
- **시작 규칙집이 2줄**: Day 1 `Entrant must have a passport` / `Arstotzkan citizens only` [관찰 https://www.youtube.com/watch?v=ZZnN4tTKSPg t≈3:30–5:00]. 이후 날마다 규칙·서류가 추가(`All documents must be current`, `No weapons or contraband`, `Citizens require an id card`, `Foreigners require an entry permit`, `Workers must have a work pass` …) [관찰 https://www.youtube.com/watch?v=fHw0ggLlJfM t≈1:10–2:30].
- **"새 메커니즘이 생길 때 오류도 그것을 쓴다"**: "every time a new mechanic is introduced, the errors will tend to use it much more … it definitely feels like the immigrants are somehow cooperating with the inspector's learning process" [공식 P1 tig-04, 2013-03].
- **일일 공지(Daily Bulletin)** 가 매일 아침 책상에 놓임. 규칙 변경·수배자 사진 [공식 P1 tig-02, 2013-01]. 그러나 "people will probably miss important bulletin info no matter what so a detailed warning report seems necessary" → 위반 통지서로 보완 [공식 P1 tig-03, 2013-02].
- **위반 통지(citation)가 튜토리얼을 대체**: "citations are something I decided are absolutely necessary … I find it really frustrating to not know exactly what I missed when getting cited. The immediate feedback also allows me to skip a lot of introductory tutorial text and let the player learn from their mistakes as they go." [공식 P1 tig-04, 2013-03].
- **깨달음 장벽 인정**: "the leap at this moment from just stamping to being gated by inspection is too much. Citations and bulletin hints or not." [공식 P1 tig-05, 2013-04] — 규칙집을 눌러 쓰는 방법을 모르는 플레이어가 많았다는 보고 다수(tig-02 스레드).
- **난이도 설정 없음**; 압박은 `quota`(하루 최소 처리 인원, 미달분은 급여 없이 마저 처리) + 실시간 시계 [공식 P1 tig-03].

### 3.5 실패·결과 모델
- 위반 통지서는 입국자가 *떠난 뒤 약 3 초* 후 인쇄(부스 반쯤 갔을 때) [2차 P3 Citation].
- **점진적 관용**: 하루 첫 2건은 경고(무벌금, 처리 수당 5 만 손실), 이후 5,5,10,15,20,25,30 … 증가 [2차 P3: "0, 0, 5, 5, 10, 15, 20, 25, 30"]. 개발 중 답변에는 "약 5건이면 그날 밤 게임 오버"(escalating penalties 구상)가 있었다 [공식 P1 tig-05]. 최종 판에서는 통지 수만으로 끝나지 않고 **벌금을 못 내 가계가 무너질 때(난방·식비·약)** 게임 오버 [2차 P3].
- **일일 가계 결산**: `END OF DAY 1 / Manage your expenses using the checkboxes below. SAVINGS 30 / SALARY (13) 65 / RENT -20 / FOOD -20 / HEAT -10 / $45` [관찰 ZZnN4tTKSPg t≈5:20, 13명×5=65].
- 도덕·서사 결과는 날마다 분기(위키: 20 엔딩) — D 레인 범위 밖.

### 3.6 평가 모델
- 별도 점수 화면 없음. 평가는 (a) 위반 통지서(원인 명시) (b) 하루 정산 (c) 10·20일의 plaque(통지 수에 따름) [2차 P3 Citation: "plaques … on days 10 and 20"].
- **위반 통지서 문구**(원인 명시): `M.O.A. CITATION / Protocol Violated / Passport: Invalid issuing city` [관찰 https://www.youtube.com/watch?v=l9mVDhPMbHI t≈0:12–0:15]. 위키 목록: `Missing documents`, `Invalid expiration date`, `Fingerprints do not match record`, `Applicant clear for entry`(무고한 사람을 거부함) 등 [2차 P3].
- **양방향 오류**를 모두 처벌: 거부해야 할 사람을 승인(위반)했을 때뿐 아니라 **정상인을 거부**해도 `Applicant clear for entry` [2차 P3].

### 3.7 CHOOGuard 적용
| 판정 | 기제 | 대응 CHOOGuard 동사 | 구체 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 정답을 알려 주지 않는 프롬프트 — 조치 이름 대신 *대상 이름과 상태*만 | 가스·전기·셔터·에스컬레이터 조작 | 현행 `가스 중간밸브 잠그기`(KitchenGas) 같은 정답 명시 프롬프트(현행 §0.2-1)를 `가스 중간밸브 — 열림`처럼 **대상+현재 상태**로 바꾸고, 가능한 동사(잠그기/열기/확인)는 플레이어가 선택. 오답은 결과(재투입 재발화 등 현행 §0.4)로 드러남 | 공식 P1 tig-00 "rule book … highlighted"; P2 "Using only the documents…" |
| **Adopt** | 사후·원인만 알려 주는 쪽지, 다음 행동은 안 알려 줌 | 실수 피드백 | 현행의 "실수 뒤 토스트가 정답을 알려 줌"(현행 §0.2-6)을 `결과 기록: 무엇이 악화됐나`(원인만)로. 구체 정답은 평정 화면 사유 칸에서만 | 관찰 `Passport: Invalid issuing city`; 공식 P1 tig-04 |
| **Adopt** | 점진적 관용(0,0,5,5,10…) | 평정/위험 | 같은 종류 실수 반복 시 감점 계단(첫 2회 경고). 다른 종류 실수는 새로 계산 | 2차 P3 |
| **Adopt** | "TIME COSTS TIME" — 조회·규칙 찾기에 시간이 흐름, 일시정지 없음 | 지도·상황판·수첩 | 조회 UI 열람 중에도 위험이 진행. HUD 설명 없이 | 공식 P1 tig-01 |
| **Adapt** | 규칙집을 *참조 도구*로 비치, 규칙은 점진 해금 | 행동 수첩 | `비상 대응 수첩`(공개 행동요령 형식, 내부 SOP 로 표기하지 않음 — 과업 제약). 첫 근무 2–3 줄, 이후는 **플레이어가 지각·처리한 위험 속성**(공급원 있음, 사람 있음, 확산 중)에 따라 줄이 생김(종류 이름 아님). 날짜 기반 해금은 불가(비고정 시나리오) | 관찰 Day 1 규칙 2줄; 공식 P1 tig-04 |
| **Adapt** | 두 정보 선택으로 불일치 주장 | 보고 문장 구성 | 선택적 고급 보고: 지각 메모 두 개(예: `쓰러진 사람` + `연기`)를 골라 연결 → 보고 문장 생성. FPS 입력(Q 휠 유지)과 충돌하므로 **옵션**. 기본 보고는 §1.7·§2.7 방식 | 공식 P1 tig-01 |
| **Adapt** | 한 개체당 중대 오류 ≤ 1 | 숨은 불량·위험 | 소화기 숨은 불량(현행 §0.4)처럼 *결정적 미지수는 개체당 1개*. 여러 개 쌓으면 오히려 쉬워짐 | 공식 P1 tig-06 |
| **Reject** | 도장 조작 같은 의식적 마찰 자체 | — | CHOOGuard 는 이미 `잠금표`·`분전반` 절차 의식이 있음(현행 §0.4). 추가 불필요 | 공식 P1 tig-01 |
| **Reject** | 메인 점수·난이도 설정이 없는 구조 | — | CHOOGuard 는 안내 수준 노브를 두는 편이 목적(§5·§6) | 공식 P1 tig-04 |

---

## 4. 112 Operator (Jutsu Games, 2020-04-23) · 911 Operator (2017-02-24)

### 4.1 출처
| # | URL | 유형 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| O1 | https://store.steampowered.com/app/793460 , /503560 (appdetails) | [공식] 스토어 | 2020-04-23 / 2017-02-24 | "instruct someone on performing CPR", "Sometimes your mistakes will only be followed by a reprimanding e-mail, sometimes they'll influence your own life." / "Real First Aid instructions" |
| O2 | https://steamcommunity.com/sharedfiles/filedetails/?id=2074146407 (Sigurd "All the basics") | [2차] 가이드 | 2020-05~2022 | 접수 흐름, 아이콘 색, 평판, 근무 후 요약, 최고 난이도 일시정지 불가 |
| O3 | https://steamcommunity.com/sharedfiles/filedetails/?id=2493311426 | [2차] 가이드 | 2022-2025 | `Wait for Backup`(ALT), 단축키 |
| O4 | https://911-operator.fandom.com/wiki/Duty , /Career | [2차] 위키 | 2026 | Duty≈10분, 평판 요구치, 모드(`negative reputations will fail duties`) |
| O5 | https://www.youtube.com/watch?v=Tj9O-DDGXHY (Medic427, `media/D/op112_*`) | [관찰] 실제 구급대원 플레이 | — | 통화·CPR 안내 UI, 근무 결과 화면 |

### 4.2 조작표 (D 레인 관련 동사만)
| 동사 | 입력 | 근거 |
|---|---|---|
| 통화 수락 | 화면 하단 중앙 초록 원 클릭(비프음) → 자동 인사 `112 what's your emergency?` | [2차 O2] |
| 통화 중 선택 | 우측 `DIALOG` 패널의 선택지 버튼 클릭(`INFO / DIALOG / ONSITE` 탭) | [관찰 O5 t≈28:33] |
| 통화 종료 | 빨간 `END CALL` | [관찰 O5] |
| 무시 | 메뉴 하단 `IGNORE` | [2차 O2] |
| 출동 | 유닛 선택 후 사건에 우클릭; Shift=여러 사건 큐; Ctrl+숫자=그룹; Alt=`wait for backup` | [2차 O2·O3] |
| 시간 | `Z` 느리게, `X` 빠르게 | [2차 O4 Duty] |

### 4.3 조작 문법
- **통화 = 대사 트리**. 발신자의 상황 설명을 듣고(배경 소음·총성까지) 주소 확인 → 필요 부대 선택 → 현장 안내 [2차 O2 `Be careful to listen to the person but also the background`].
- **안내(instruction) 단계에 *오답이 같이 있다***. 관찰한 CPR 분기 마지막 선택지: `REPEAT UNTIL AMBULANCE ARRIVE[S]` / `REPEAT BREATHS` / `MAKE 30, WAIT FOR AMBULANCE` [관찰 O5 t≈28:33]. 발신자 반응이 대사로 돌아온다: `Can I do that..? Should I… Shouldn't I ask for a consent?` → `Trust me, you have consent!` [관찰 O5 t≈28:14].
- **사건 아이콘 색=필요 유닛 종류**: 빨강=소방, 파랑=경찰, 흰=구급, 초록=대형 행사. 섞이면 복수 유닛. 아이콘 안 기호(수갑·불·의사)가 추가 힌트 [2차 O2]. 사건 목록에는 색 칩(예: 파랑 1·흰 1·빨강 1)이 필요/도착 현황을 표시 [관찰 O5 t≈27:47–28:14 우측 `INCIDENTS`].
- **적절한 규모 조정(customize help)**: "If you forget this, it can result in fatally with death or injured crew" [2차 O2]; "If you're told that someone's shooting at each other, it's probably not smart to send a police bike" [2차 O2].

### 4.4 안내·교육 모델
- 통화 UI 안에는 정답 근거 표시가 없다: 안내문은 플레이어가 선택지 중에서 고르고, 발신자가 대사로 반응한다 [관찰 O5 t≈27:47–28:36].
- **선택 목표(Objectives)**: "not necessary to complete, but it will give you a bonus" [2차 O2]. 상관 이메일(`E-mail`)에 중요한 정보. 이메일 오류 지적 `reprimanding e-mail` [공식 O1].
- **레벨 계층**: Free Game 에서 Junior/Middle/Senior/Principal 선택 — 관리 범위·사건 난이도·장비가 달라짐 [2차 O2]. `Incident Multiplier` 슬라이더(사건 빈도) [2차 O3].
- **소음 노브**: 설정 `muffle typical calls`(통화 청취 난이도) [2차 O4]. **최고 난이도는 일시정지 불가** [2차 O2].

### 4.5 실패·결과 모델
- 평판(`Reputation`) 증감: 사건·통화를 잘 처리하면 +, 못하면 − [2차 O2]. 캠페인 `negative reputations will fail duties` [2차 O4 Career]. 승진 요구 평판(Kapolei 100 … Washington 500) [2차 O4].
- 오파견 결과: 사상자·대원 부상 [2차 O2]; 사망·부상은 `Preventable deaths`/`Hospitalized` 로 집계 [관찰 O5 아래].
- 서사 결과: 감독 이메일부터 플레이어 생활 영향까지 [공식 O1].

### 4.6 평가 모델 — `DUTY SUMMARY` 직접 관찰 [관찰 https://www.youtube.com/watch?v=Tj9O-DDGXHY t≈19:00, `media/D/op_sum_full.png`]
```
DUTY SUMMARY                                  (우상단 ?)
[사건 목록]  SPEEDING SOLVED (+1/1) / HOUSEHOLD CHEMICALS POISONING SOLVED (+4/4) /
             A BRIBERY ATTEMPT SOLVED (+1/1) / CORONAVIRUS SYMPTOMS SOLVED (+3/3) /
             THE STRANGLER SOLVED (+3/3) / LOUD RENOVATION SOLVED (+1/1) / TOO MANY PEOPLE SOLVED (+21/21) …
[통계]  Total incidents resolved: 26
          In jail 1 / Billed 18 / Hospitalized 6 / Preventable deaths 0 / Aided 9 / Reputation 85
        Unresolved incidents 0 · Efficiency 98.84% · Career Points +3
[재무]  Base income $35,125 · Rewards $0 · Fines $53,897 · Upkeep -$9,096 ·
        Vehicle maintenance -$3,316 · Total profit +$76,610
[승진]  Jack James — Promoted     [CONTINUE]
```
`(+x/y)` 의 의미는 사건별 평판 획득/만점으로 보이나 **확정 못 함 [추론]**. 사건을 펼치면 대화·조치 로그를 `small summary and, if necessary, a detailed synopsis` 로 볼 수 있다 [2차 O2 `After a shift`].
핵심: **`Preventable deaths`(예방 가능 사망)를 별도 집계** → 불가항력 사망과 귀책을 구분.

### 4.7 CHOOGuard 적용
| 판정 | 기제 | 대응 CHOOGuard 동사 | 구체 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 오답이 포함된 안내 분기 + 청자 반응 대사 | 승객 대피 안내, 진정, 사상자 안내 | Q 휠/말걸기 선택지에 **틀린 문장도 함께** 표시(예: 대피 안내 시 `그냥 두세요`/`뛰지 마세요, 아래로`). 시민 반응이 결과로 돌아옴. 선택지는 Hazard 공통 상황(사람 수·위험 확산·혼잡)에서 생성 | 관찰 O5 t≈28:33; 현행 §0.2-3 Q 휠 오답 없음 |
| **Adopt** | 사건 아이콘 색=필요 기관 종류, 필요/도착 칩 | 기관 요청, 인계 | 상황판의 *지각된 위험* 한 줄에 필요 기관 칩(소방/경찰/구급)을 보여 주되 플레이어가 요청해야 도착. 오요청은 도착 지연(즉사 아님). 칩은 지각된 사실에서만 | 2차 O2 아이콘 색; 관찰 O5 |
| **Adopt** | `Preventable deaths` 분리 집계 | 근무 종료 평가 | 평정 항목 `예방 가능했던 악화` — 보고/조치 지연으로 인한 것만 귀책, 불가항력은 `발견 시 이미 심정지` 같은 별도 표기 | 관찰 `Preventable deaths 0` |
| **Adapt** | 사건별 `SOLVED (+x/y)` + 열면 로그 | 평가 상세 | 평정 화면에서 지각된 위험별 `처리/방치/악화` 한 줄, 선택 시 타임라인(보고→요청→조치→인계). 타임라인은 현행 ShiftLog 가 이미 기록(현행 §0.3 "로그만 남는 행동") | 관찰 O5, 2차 O2 |
| **Adapt** | 감독 이메일(질책·칭찬 메시지) | 평가 | 평정 말미에 *지시 아닌 평*(예: `보고가 늦었습니다`) 한 줄. 구체 정답 지시는 생략 | 공식 O1 |
| **Adapt** | 평판이 음수면 근무 실패(모드별) | 평가 | 통과선은 옵션(§1.7). 기본은 실패 없음 유지 | 2차 O4 |
| **Reject** | 지도 단위 유닛 배치·재무·승진 | — | 역무원 직무 밖, 메타 경영 | 공식 O1 |
| **Reject** | `muffle typical calls`(통화 소음) | — | 접근성 충돌, 노브로서 가치 낮음 | 2차 O4 |

---

## 5. Hitman World of Assassination (IOI) — 안내 수준 노브

### 5.1 출처
| # | URL | 유형 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| H1 | https://www.youtube.com/watch?v=Yixn6FiXkUw (`media/D/hitman_settings_*.mkv`, `hm_*.png`, `hmc_*.png`) | [관찰] 게임 내 Options 화면(Windows, 업로드 2024-11-03) | — | **게임 내 설명 문구 원문**(`Mission Story Guidance`, `Objectives`, `Instinct`, `Global Hints`, `Picture in Picture`) |
| H2 | https://hitman.fandom.com/wiki/Mission_Stories | [2차] 위키 | 2026 | 가이드 3단계, "Master 에서 모든 Mission Story 가이드 자동 비활성", 해금 방식 |
| H3 | https://www.ludo.guide/guide/hitman-world-of-assassination/hitman-world-of-assassination-part-11 ; https://www.gamepressure.com/hitman-iii/difficulty-levels/z7e10a | [2차] 난이도 비교 | 2021-01 / 2026 | Casual/Professional/Master 차이 |
| H4 | https://steamcommunity.com/app/1659040/discussions/0/3770113150026958914/ | [2차] Steam 토론 | 2023-02-22 | `Minimal` 도 "Guide Objectives… every next step" 체크리스트가 남는다는 불만, `Off` 는 일부 Diana 대사 손실 |
| H5 | https://steamcommunity.com/app/1659040/discussions/0/3886099862947497168/ | [2차] Steam 토론 | — | "Master… by default you still get instinct and HUD items" |
| H6 | https://ioi.dk/hidden/h3-patchnotes | [공식] 패치 노트 | 최신 3.280 (2026-08-26) | 가이드 도입 문구는 열람 가능 본문에서 못 찾음 [미확인] |

### 5.2 노브와 "무엇을 숨기는가" (게임 내 설명문 원문) [관찰 H1 + 공식 UI 문구]
| 설정 | 선택지 | 숨기는/보이는 것 (원문) |
|---|---|---|
| **Mission Story Guidance** | `FULL` / `MINIMAL` / `OFF` | `Full: All Guide hints are visible. In game tracker icons appear for a tracked Mission Story. Objectives are shown in the HUD.` / `Minimal: In game tracker icons are hidden. Guide hints can still be accessed, and Guide objectives are still tracked in the HUD.` / `Off: All Guide communication is turned off.` (기본 Full) [관찰 t≈2:04] |
| **Objectives** | `AUTO` / `ALWAYS ON` / `ALWAYS OFF` | `Auto: The Objective Tracker is shown and hidden automatically. Always On: … always shown in the HUD. Always Off: The Objective Tracker is hidden.` (기본 Always On) [관찰 t≈1:56] |
| **Instinct** | `ON` / `OFF` + 활성화 `HOLD`/`TOGGLE`(Ctrl) | `On: Instinct Mode is available, and characters, targets, items and traversal options are highlighted. Off: Instinct Mode is turned off.` [관찰 t≈0:26] |
| **Global Hints** | `ON` / `OFF` | `On: Gameplay Hints will be displayed at appropriate points during play. Off: Hints will not interrupt gameplay, except during missions in the ICA Facility.` [관찰 t≈2:12] |
| **Picture in Picture** | `ON` / `OFF` | `On: Display Picture in Picture notifications for important events. Off: Do not display Picture in Picture notifications for any event.` (예: `BODY FOUND`) [관찰 t≈2:20] |
| 그 밖에 목록 | — | Vital Info Messages, Mission Timer HUD, Performance/Scoring HUD, Challenges[FULL], Challenges HUD, Target Information, NPC Icons, Weapon HUD, Reload HUD, Silent Assassin HUD, Difficulty HUD, Autosave HUD, Mini Map[OFF] + North Indicator / NPC Indicators / Rotation / Target Indicators, Limited Vision Area, Attention Alert, Camera Grid [관찰 H1 t≈1:39–3:40 목록] |

### 5.3 조작·문법·교육
- **안내(Mission Story)는 지각으로 해금**: "certain window of opportunities that typically reveal themselves either after eavesdropping in on an NPC conversation, chancing upon a certain event, or after examining an object" [2차 H2]. 안내 대상이 *이미 일어난 지각*에 의존.
- **안내 수준은 난이도와 직교**: HUD 노브는 난이도와 별개(Master 도 `Instinct`/HUD 기본 유지 [2차 H5]), **예외는 Mission Story**: Master 에서는 자동 비활성 [2차 H2·H3].
- **Minimal 의 한계(사용자 보고)**: 추적 아이콘은 사라지지만 좌상단 `Guide objectives` 체크리스트가 "다음 단계를 전부 알려 준다"는 평가 — `Off` 로 가야 완전한 자력 해결, 대신 일부 Diana 대사 손실 [2차 H4].
- 난이도별 차이(참고): Casual — 무제한 저장, Mission Story 전부, 카메라 없음; Professional — 카메라 적발·적대 증가; Master — 저장 1회, Mission Story 가이드 없음, 카메라·단속자 증가, 혈흔 위장 무효, 소리 민감 [2차 H3]. 공식 1차 자료 [미확인].

### 5.4 실패·결과·평가
- 안내 수준 자체는 **점수에 가격이 없다**(Hitman 은 별도 `Silent Assassin` 도전과제·`Performance/Scoring HUD` 로 평가). 안내를 낮춘 보상은 도전과제·리더보드(Master). 가격화는 Phasmophobia 가 더 명시적이다(§6).

### 5.5 CHOOGuard 적용
| 판정 | 기제 | 대응 CHOOGuard 동사 | 구체 방법 | 근거 |
|---|---|---|---|---|
| **Adapt** | 3단계 안내 노브 `FULL/MINIMAL/OFF` | 현행 4개 안내 채널: ① 바닥 길 안내·나침반 ② Tab ○/● 조치 체크리스트 ③ 정답 명시 프롬프트 ④ 무전 답변의 다음 단계 지시 (현행 §0.2) | **FULL(현행 유지)** = ①②③④ 모두. **MINIMAL** = ① 끔, ② 끔(대신 *지각한 사실* 목록만), ③ 대상+상태만, ④ 확인응답만. **OFF** = 위 + 지각 알림 토스트 끔, 사후 평정에서만 피드백. 노브 값은 Hazard 종류와 무관 | 관찰 H1 `Mission Story Guidance` |
| **Adapt** | Minimal 의 체크리스트 잔존 문제 | Tab 상황판 | MINIMAL 에서도 `조치` 열(○/●)은 **제거**하고 `사실` 열(플레이어가 지각한 것)만. H4 의 불만을 사전 차단 | 2차 H4 |
| **Adopt** | `Objectives AUTO/ALWAYS ON/OFF` | HUD 목표 표시 | `AUTO`: 목표가 바뀌는 순간만 잠깐 표시 | 관찰 H1 |
| **Adopt** | 안내 자체가 지각으로 해금 | 길 안내, 힌트 | 길 안내선은 **지각한 위험 위치**에만 생성(현행 `WorldRouteGuide` 기본 켜짐, 현행 §0.2-5 → 지각 후에만). 하드룰 준수 | 2차 H2 |
| **Adopt** | `Global Hints`/`PiP` 알림 개별 끄기 | 발견 토스트 | `BODY FOUND` 류 이벤트 알림(`새 위험 발견`)을 노브로 | 관찰 H1 |
| **Reject** | `Instinct`(벽 너머 강조) | — | **지각하지 않은 것을 노출**하므로 하드룰 위반. 대안은 `주변 확인`(홀드로 *이미 시야 내* 개체만 윤곽 강조) 정도 | 관찰 H1 설명문 `characters, targets, items … highlighted` |
| **Adapt** | 안내 노브를 난이도와 분리 | 위험·안내 | `위험 강도`(현행 vignette·노출 위험)와 `안내 수준`을 독립 설정 | 2차 H5 |

---

## 6. Phasmophobia (Kinetic Games, Steam 2020-09-18) — 증거·저널·가격화된 노브

### 6.1 출처
| # | URL | 유형 | 날짜 | 확인한 것 |
|---|---|---|---|---|
| F1 | https://store.steampowered.com/api/appdetails?appids=739630 | [공식] 스토어 | 2020-09-18 | "minimal user interface", "Custom Difficulty: Create your own games to tailor the difficulty… with proportional rewards" |
| F2 | https://phasmophobia.fandom.com/wiki/Difficulty , /Difficulty/Custom | [2차] 위키 | 2025-2026 | 프리셋 5종, 커스텀 파라미터 전체 + 보상 수정치 표 |
| F3 | https://phasmophobia.fandom.com/wiki/Journal , /Money | [2차] 위키(저널 문서 갱신 2026-07-22) | 2026 | 저널 증거 UI, 정답 식별과 보상 배율 |
| F4 | https://www.youtube.com/watch?v=qzQyn7j3T9k (`media/D/phasmo_custom_*.mkv`, `m_phasmo.png`) | [관찰] 커스텀 난이도 UI | — | `Player/Ghost/Contract` 탭, 하단 `Rewards ×3.00`~`×3.35` 실시간 배율, 설정별 설명문 |

### 6.2 조작표
| 동사 | 입력 | 근거 |
|---|---|---|
| 저널 열기 | Journal 버튼(콘텐츠 페이지+상단 책갈피) | [2차 F3] |
| 증거 표시 | 증거 항목 체크 또는 줄 긋기(취소선); 유령 유형을 원으로 선택하거나 줄 긋기 | [2차 F3 `Evidence can be either checked off or crossed out… Ghost types can either be selected by circling them, or crossed out`] |
| 커스텀 난이도 | 로비 `Difficulty: Custom` → `Player/Ghost/Contract` 탭, `Presets`, `Apply` | [관찰 F4] |

### 6.3 조작 문법·안내 모델
- **저널은 플레이어 소유 장부**: 체크·취소선·원으로 *플레이어가 판정*한다. 위키에는 중간 검증 기능 서술이 없고, 정답 식별은 종료 시 보상 계산에서만 쓰인다 [2차 F3; "중간에 맞다고 확인해 주지 않음"은 서술 부재에 근거한 [추론]]. 증거를 체크하면 유령 후보가 회색으로 소거되는 것은 *기계적 연결*일 뿐이다 [2차 F3]. 다른 플레이어 선택은 색선으로 공유(0.19.0) [2차 F3 History].
- **오직 최소 UI**: 스토어가 직접 "minimal user interface ensure a totally immersive experience" [공식 F1].
- **선택 목표 위치**: 트럭의 `Objective Board` 와 저널 `Overview`(유령 이름·성별, 스피릿박스 응답 대상, 수집 품목, 모든 선택 목표) — **월드 마커는 없다** [2차 F3·F2].
- **정보 공급량 노브**: `Evidence given` 0/1/2/3 — 게임이 *몇 종류의 증거를* 낼지(Nightmare 는 2, Insanity 는 1) [2차 F2]. `Fingerprint chance/duration`, `Setup time`, `Fuse box visible on map`, `Sanity monitor`/`Activity monitor`(on/off) [2차 F2/Custom].
- **오정보 노브(상위 난이도)**: `The Sanity Monitor and Site Activity Monitor will be damaged and display random values` (Nightmare·Insanity) [2차 F2].
- **정보 *세부도*** 노브: Professional 이상 `The objective board will not specify if the ghost responds to everyone or people who are alone` [2차 F2].

**노브 → 숨기는/바꾸는 정보 (커스텀 파라미터, 위키 표 기준 [2차 F2/Custom])**
| 노브 | 값 | 무엇을 숨기거나 바꾸는가 | 보상 수정치 |
|---|---|---|---|
| `Evidence given` | 3 / 2 / 1 / 0 | 게임이 *내주는* 증거 종류 수(`How many types of evidence you can gather` [관찰 F4]). 줄수록 플레이어가 더 많이 추론 | 기준 / +0.50× / +2.00× / +4.00× |
| `Sanity monitor` | On / Off | 트럭의 정신력 계기 표시 | Off +0.05× |
| `Activity monitor` | On / Off | 현장 활동 계기 표시 | Off +0.05× |
| `Fuse box visible on map` | On / Off | 트럭 지도에 두꺼비집 위치 표시 | Off +0.05× (시작 상태가 `Broken` 이면 Off 강제) |
| `Fuse box at start of contract` | On / Off / Broken | 전원 시작 상태(어둠 → 손전등 의존) | Off +0.10×, Broken +0.50× |
| `Flashlights` | On / Off | 손전등 사용 가능 여부 | Off +0.45× |
| `Fingerprint chance (%)` / `duration (s)` | 25–100 % / 15–∞ s | 단서(지문) 발생 확률·지속 | 낮을수록 +0.03×~+0.10× / 길수록 −1.50×~−3.00× |
| `Setup time (s)` | 0–300 | 준비(안전) 시간 | 0 일수록 +0.30×, 길수록 상한(clamp) 1.00×까지 하향 |
| 프리셋 한정 | Professional 이상 | `Objective Board` 가 유령이 "모두에게/혼자 있는 사람에게" 반응하는지를 *표기하지 않음* | 프리셋 배율 3× 이상 |
| 프리셋 한정 | Nightmare·Insanity | 계기 두 개가 *고장나 임의 값 표시*(오정보) | 프리셋 배율 4×·6× |

### 6.4 실패·결과·평가
- 사망 시 **장비 손실** + 보험 환급(Amateur 50%, Intermediate 25%, 이상 0%) — 단 `Lose items and consumables: Off` 는 보상 0× [2차 F3/F2].
- **평가는 한 번, 끝에서**: 정답 유령 식별 시 `Investigation Bonus = Ghost identification + Completed objectives`(= 사실상 2배), 완벽 조사(정답·목표 3개·뼈·고유 미디어) 추가 $50 [2차 F3 Money]. **오답이면 커스텀 보상 배율이 가장 가까운 기본 난이도로 내림** [2차 F3 Money].
- **보상 배율 = 난이도 가격**: 프리셋 Amateur 1× / Intermediate 2× / Professional 3× / Nightmare 4× / Insanity 6×, 커스텀 0×–15×. 도움을 줄이면 배율↑, 도움을 늘리거나 위험을 없애면 배율↓(예: `Evidence given` 3=기본, 2=+0.50×, 1=+2.00×, 0=+4.00×; `Friendly ghost On`=0×; `Sanity drain speed 0`=0×) [2차 F2/Custom].
- 커스텀 UI 에서 **각 설정을 바꿀 때마다 하단 `Rewards ×N.NN` 이 즉시 갱신**(초록=증가, 주황=감소)되고 각 설정 아래에 한 줄 설명(`Evidence given — How many types of evidence you can gather`, `Friendly ghost — The ghost will never hunt. Turning this setting on will result in zero rewards`) [관찰 F4 t≈0:50–2:40].

### 6.5 CHOOGuard 적용
| 판정 | 기제 | 대응 CHOOGuard 동사 | 구체 방법 | 근거 |
|---|---|---|---|---|
| **Adopt** | 저널=플레이어 소유 장부, 게임은 끝에서만 채점 | Tab 상황판, 위험 발견/인계 | Tab 에서 `조치` 체크리스트(현행 §0.2-4)를 **폐기**하고 `내가 확인한 사실` 목록(자동 기록, 지각한 것만)만 둠. 종료 시 *플레이어 보고 vs 실제 상황* 정확도 평정 | 2차 F3 |
| **Adopt** | `Evidence given 0–3` — 초기 단서 개수 노브 | 무전 보고, 안내 | 사건 시작 시 역무실이 알려 주는 *확정 사실* 수를 0–3 으로(지각 전 위험의 위치는 안 알림). 낮을수록 평정 배율 ↑ | 2차 F2/Custom |
| **Adopt** | 노브 → 보상 배율 *실시간 표시* | 안내 수준 설정 화면 | `안내 수준` 설정 시 `평정 배율 ×1.0~×2.0` 하단 표시. 도움 큰 값(현행 = FULL)이 기준(1.0) | 관찰 F4 |
| **Adapt** | 오정보 노브(계기가 틀린 값) | 승객·역무실 보고 | 높은 수준에서 **일부 외부 보고(승객 신고 등)가 부정확**. 단 *지각하지 않은 위험을 노출*하지 않으며, **난수는 디렉터 RNG 와 분리된 스트림**(하드룰: 부가 기능이 디렉터·세계 난수 소비 금지) | 2차 F2 |
| **Adapt** | 정답 식별이 배율을 게이트 | 평가 | 상황 판단(`무엇이 위험의 원인이었나` 선택)을 종료 시 1문 질문으로, 오답이면 배율 한 단계 하향 | 2차 F3 Money |
| **Adapt** | `Sanity monitor` on/off | 플레이어 위험(연기·열 노출) | 현행은 vignette 만(현행 §0.3). 노출 게이지를 *보일지 말지*를 노브로 | 2차 F2/Custom |
| **Reject** | 사망 시 장비 손실·보험 | — | 영속 인벤토리 없음 | 2차 F3 |
| **Reject** | 정체불명 유령 유형 추론 | — | 해당 도메인 아님. 구조(단서→후보 소거→최종 식별)만 참고 | 2차 F3 |

---

## 7. 교차 패턴 (D 레인 6 게임 공통)

| # | 패턴 | 어떤 게임 | CHOOGuard 시사점 |
|---|---|---|---|
| X1 | **보고 = 지각한 대상에 대한 능동 입력, 응답은 확인만** | SWAT 4, Ready or Not | 무전 답변에서 "다음 지시" 제거, 보고 프롬프트는 지각한 개체에만 |
| X2 | **누락은 사후 `x/y` 로, 위치 없이** | SWAT 4(`7/7`), Ready or Not(`7/14`), 112 Operator(`Unresolved incidents`) | 평정 화면 중심 평가. 분모는 지각 개체로 한정(미지각 노출 금지) |
| X3 | **채점=전부-아니면-0 기본점 + 비례 항목 + 항목별 감점 + 통과 임계** | SWAT 4(40점 all-or-nothing), Ready or Not(주요/소프트 목표) | 기본점=`현장 인계`, 비례=`보호·보고·통제·노출`, 감점=`방치·위험 조치`, 임계는 옵션 |
| X4 | **종료는 플레이어 선택, 단 서둘러 끝내면 점수를 포기** | SWAT 4(`Press 'Pause' to proceed to Debrief`), Ready or Not(`End The Mission?`) | 100 s 자동 종료(현행 §0.3) → 선택형 종료 |
| X5 | **숨은 보너스는 사후에 이름 공개, 이름 자체가 *행동*** | Ready or Not(`REPORTED INCAPACITATED VETERAN`) | 보너스 항목을 지각 개체에서 파생·사후 공개 |
| X6 | **오답이 선택지에 있다** | 112 Operator(CPR 분기), Papers Please(규칙집은 참조뿐) | Q 휠에 틀린 문장 포함. 정답 명시 프롬프트 폐기 |
| X7 | **사후·원인만 알려 주는 피드백** | Papers Please(citation 쪽지), 112 Operator(요약) | 실수 직후 정답 토스트(현행 §0.2-6)를 원인 기록으로 대체 |
| X8 | **점진적 규칙 도입(첫날 2줄)** | Papers Please | 수첩은 *지각한 속성*에 따라 줄이 생김(날짜 해금 불가) |
| X9 | **안내 수준 노브가 독립적이며 값이 매겨짐** | Hitman(3단계), Phasmophobia(실시간 `Rewards ×`) | `안내 수준 FULL/MINIMAL/OFF` + 평정 배율. 난이도와 분리 |
| X10 | **안내 *자료*조차 지각으로 해금** | Hitman(Mission Story), Phasmophobia(저널 자체 기록) | 길 안내·수첩·상황판 모두 지각 이후 |
| X11 | **한 개체당 결정적 미지수는 하나** | Papers Please(`at most one thing wrong`) | 숨은 불량류는 개체당 1개 |
| X12 | **귀책 분리(예방 가능 vs 불가)** | 112 Operator(`Preventable deaths`) | 평정에 `예방 가능했던 악화` 분리 |

### D 레인 종합 제안 스케치 (Main 종합용; 구현 아님)
1. **보고 재설계**: 지각한 대상 응시 → 보고 입력 → (선택) 상태 어휘 선택(`반응 있음/없음`, `호흡 없음`, `연기 확산 중` 등 Hazard 공통 어휘) → 역무실은 `확인했습니다`.
2. **Q 휠**: 지금 맞는 다음 단계만 보여 주는 현행을 폐기하고 *해당 대상 맥락의 선택지 4–6개(오답 포함)*. 고정 문장 목록은 만들지 말고 Hazard/Casualty 공통 상태에서 생성(하드룰: 종류 비고정).
3. **Tab 보드**: `조치` 열 삭제, `사실` 열(지각한 것) 유지. 미보고 개수만 위치 없이.
4. **평정**: `기본점=인계 완료` + 비례(보호·보고·통제·노출) + 감점(방치·역할 밖 조치) + `예방 가능했던 악화` 분리 + 숨은 소프트 항목 사후 공개. 5등급.
5. **노브**: `안내 수준 FULL/MINIMAL/OFF`(+`Objectives AUTO`, 초기 단서 0–3, 외부 오정보 on/off), 설정 화면에 `평정 배율` 실시간 표시.
6. **종료**: 인계 후 `근무 종료?` 선택, 남은 미보고 수만 힌트.

---

## 8. 공백·미검증 (정직한 목록)

1. SWAT 4: ESC `procedures` 카운터 화면을 **직접 관찰하지 못했다**(설명서·GameSpot 서술만) → "누락 수를 보여 준다 / 닿지 않은 민간인 포함" 은 [2차]. 공식 설명서 OCR 이 일부 훼손되어 `Rescue…must be compliant and handcuffed` 등은 글자 복원본.
2. SWAT 4: 바닐라 감점이 디브리핑 때 *어떤 문구로* 나오는지 직접 못 봤다(완벽 점수 화면 `No Penalties Deducted!` 만 관찰).
3. Ready or Not: ① 점수 화면의 **감점 줄(예: ROE 위반)** 문구를 직접 보지 못했다(관찰한 두 화면은 감점 없음/집계 중). ② 항목별 점수 산식(35/명 vs 42.5/명)은 비공개 [미확인]. ③ 아군 사상자 보고 여부는 Steam 스레드 제목(`reporting a dead officer`)만 확인 [2차 스니펫]. ④ 공식 변경 기록 본문에서 점수·보고 규칙 문구를 열람하지 못했다. ⑤ PC 기본 키 `F` 는 2차 표(콘솔 영상의 `A` 와 대응 추정).
4. Ready or Not: 보고 후 **TOC 음성 대사 내용**은 영상 자막 부재로 직접 확인 못 함(SWAT 4 는 텍스트 로그가 있어 확인).
5. Papers, Please: 개발 로그 문장은 직접 읽었으나 일부 발췌의 정확한 일자(스레드 내 포스트 단위)는 월 단위로만 표기. 위반 통지 벌금 수열 `0,0,5,5,10,15,…` 는 2차 위키(개발 로그에는 "약 5건이면 게임 오버" 구상만).
6. 112 Operator: `(+x/y)` 의미, 오파견 *수치 규칙*(평판 증감값)은 확인 못 함. 911 Operator 는 스토어 문구 외 직접 관찰 없음.
7. Hitman: Master 난이도 세부(Instinct 유지, Mission Story 비활성)는 [2차] — 공식 IOI 출처를 못 찾았다. 노브 설명문은 게임 내 UI 에서 직접 관찰(공식 문구).
8. Phasmophobia: 저널 UI 직접 프레임은 확보하지 않았다(커스텀 난이도 UI 만 관찰). 난이도·보상표는 [2차] 위키.
9. 어떤 게임도 **"플레이어 전용 직무 범위"를 벗어난 행동**을 막는 메커니즘의 직접 대응이 없다(SWAT 4 의 ROE 가 가장 가깝다) → CHOOGuard 의 "역무원 권한 밖 행동" 감점 설계는 [추론].

---

## 부록 A. 미디어 목록 (`media/D/`, 커밋 금지)
| 파일 | 원본 | 용도 |
|---|---|---|
| `swat4_foodwall_100_TiuBdq7O5XE.webm`, `s4_a.png`, `s4_b.png`, `s4c_all.png`, `montage_swat4.png` | https://www.youtube.com/watch?v=TiuBdq7O5XE | SWAT 4 디브리핑·TOC 로그·컨텍스트 명령 박스 |
| `ron_tycA_score_hi_PNZWrJcsP90.webm`, `ron_score_final.png`, `ron_tycA_S_*`, `ron_tycA_mid_hi_*`, `hud_crops.png` | https://www.youtube.com/watch?v=PNZWrJcsP90 | RoN S/Hard 점수 화면, 분대 HUD |
| `ron_B_BRw8-_1WinI.mkv`, `ron_B_final.png`, `ron_B_tablet.png` | https://www.youtube.com/watch?v=BRw8-_1WinI | RoN B 랭크 점수 화면, `MISSION SOFT COMPLETE` |
| `ron_training_report_ESIuvmTg2dQ.webm`, `ron_training_evidence_ESIuvmTg2dQ.webm`, `t_rep.png`, `t_sec.png`, `t_evi.png`, `ron_pc_training_NBlCbWl4xFA.mkv`, `m_pc_tr.png` | https://www.youtube.com/watch?v=ESIuvmTg2dQ , https://www.youtube.com/watch?v=NBlCbWl4xFA | RoN 교육 패널·월드 프롬프트 |
| `pp_howto_fHw0ggLlJfM.webm`, `pp_howto2_*`, `pp_day1_ZZnN4tTKSPg.mkv`, `pp_cit_l9mVDhPMbHI.mkv`, `m_pp*.png` | https://www.youtube.com/watch?v=fHw0ggLlJfM , ZZnN4tTKSPg , l9mVDhPMbHI | PP 규칙집·검사 모드·Day 1·위반 통지서 |
| `op112_cpr_Tj9O-DDGXHY.mkv`, `op112_summary_*.mkv`, `op_sum_full.png`, `op_cpr_*.png` | https://www.youtube.com/watch?v=Tj9O-DDGXHY | 112 Operator 통화·CPR 분기·DUTY SUMMARY |
| `hitman_settings_Yixn6FiXkUw.mkv`, `hm_*.png`, `hmc_*.png`, `m_hitman.png` | https://www.youtube.com/watch?v=Yixn6FiXkUw | Hitman 옵션 설명문 |
| `phasmo_custom_qzQyn7j3T9k.mkv`, `m_phasmo.png` | https://www.youtube.com/watch?v=qzQyn7j3T9k | Phasmophobia 커스텀 난이도 UI |
| `swat4_manual.txt` | https://archive.org/download/swat4instructionmanual/ | SWAT 4 설명서 OCR 텍스트 |

## 부록 B. 하드룰 점검 (본 문서의 모든 제안)
- 사건 종류·장소·시각·개수 비고정: 항목·보너스·수첩 줄·Q 휠 선택지를 **Hazard/Casualty 공통 속성**에서 생성(§2.7·§3.7·§4.7).
- 미지각 위험 비노출: 분모·카운트·안내선·`Instinct` 모두 *지각한 개체*에만(§1.7·§5.5). `Instinct`(벽 너머 강조)는 Reject.
- 역무원 권한: 사망 선언 금지(§2.7), 분대 지휘 Reject, 기관 요청·시민 안내·기본 심폐 안내만.
- 공개 행동요령 ≠ 내부 SOP: 수첩은 공개 행동요령 형식(§3.7).
- 부가 기능이 디렉터·세계 난수 소비 금지: 오정보 노브는 별도 RNG 스트림(§6.5).
