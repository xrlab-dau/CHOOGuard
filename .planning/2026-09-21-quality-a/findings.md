# Findings

현재 Unity6000.3.23f1 연결/컴파일 정상. 이전 검증은 참조 홀30×20m/50명/두 출동 거점/6논리팀8표현차량. 건물 선택 UI와 내부Jev재출동준비시간식은 native 검증됐지만 도시전체·현장물리·장기게임성은 미수용.

- 현재 워커: JuPedSim1.4.2 CFSV3 dt.05; FDS6.11.1 0.5m/19200cells/0–120s 단층 참조. 문/환기/소화 양방향 반영·압착 접촉력·임상·현장 보정 미구현. A물리 게이트 미충족. 설치된 JuPedSim에는 SocialForce/GCFM/Anticipation 모델도 있으나 설치 가능성은 검증 근거가 아님.
- 공식 NIST TN1822는 피난모델 검증/검수 절차 근거. 현장 보정과 수치V&V를 별도 평가해야 함.
- prompts/README.md의 치수완화/Bootstrap만구현 문구는 최신요구/현행코드와 불일치. 현재MVP_DIRECTION 및현행증거를 우선하고 후속 동기화.

- Official archive identity: initialKTX files arerollingstock, notstation. CorrectstationZIP subsequentlyreceived116458955bytes; SHA b892a7ed9c9be58e5e4bcaae56188098a439062f2ebff91c05b3b26db807cd0e. StationSDK reports370materials/761componentdefinitions/80cameras; authoredlayersincludeStation/ExitRoof/RailUpper/Parking/Surroundings. Interior topologyunverified.
- Native capture evidence correction: Screen.width/height returned1230x1129 while true PlayModeView.m_TargetTexture=1920x1080. Prior Screen-sizedcaptures wereaspect-distorted; also staleGPUframewhilebackground. Do notuse themascurrentstateproof. Use actualtargetdimensions andfreshframechecks; camera-onlyrenderdoesnotincludeoverlayUI.

- Processhandoffgotcha: closingchildagent ended itsFDS+monitorprocesses despiteprovided sessionIDs/PIDs. Rootpsconfirmedbothabsent andtimestampsfrozen09:39:30; ~849/1800sreference NOTcompleted/no restartfile. Preservepartial; do notcountaslonghorizonvalidation. Futurelongcompute mustremainwithliveowner/rootprocess. Newofficialsourceworldtakespriorityoverrerunningoutdatedreferencecase.
