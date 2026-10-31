@echo off
rem 심정지 연습을 직접 플레이하기 위한 Unity 에디터 실행기 (2026-10-31).
rem 연습 설정은 환경변수로 들어가므로, Hub 가 아니라 이 파일로 에디터를 띄운 뒤
rem 메뉴 ChooGuard > Practice > 심정지 환자 한 건 → 타이틀에서 플레이 시작 → 랜덤 비상상황 대응.
rem 주의: 같은 프로젝트에 배치 Unity(시험·촬영)가 돌고 있으면 먼저 끝나기를 기다릴 것 (에디터는 한 번에 하나).

set CG_PRACTICE_FAMILY=심정지
set CG_PRACTICE_ONSET=immediate
set CG_PRACTICE_HANDS_OFF=1
set CG_PRACTICE_ZONES=hall2f,main2f
set CG_PRACTICE_INDOOR=1
set CG_PRACTICE_RECORD_AFTER=900
set TYPESAFE_API_KEY=off

if exist "C:\Choo\CHOOGuard\Temp\UnityLockfile" del /q "C:\Choo\CHOOGuard\Temp\UnityLockfile"
start "" "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -force-d3d11 -projectPath "C:\Choo\CHOOGuard"
