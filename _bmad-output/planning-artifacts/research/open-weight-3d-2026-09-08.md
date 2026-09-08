# 오픈웨이트 3D 에셋 생성 모델 — 2026-09-08

조사 방향을 폐쇄형 상용 제작 서비스가 아니라 **실제 가중치 공개 모델 + 필요한 경우 저렴한 GPU 호스팅 API**로 수정했다. 권장 후보이지 프로젝트 하드룰이 아니다. 본 조사에서는 가중치 다운로드·이미지 업로드·유료 생성 요청을 하지 않았다.

## 결론

- 최신 공개 후보: **Pixal3D**. 2026년5월 공개된 TRELLIS.2 기반 개선판이며 실제 HF 체크포인트와 MIT 라이선스를 확인했다. 이미지와 형상의 대응을 강화한 접근이므로 참고 오브젝트 재현 후보로 적합하다. 공개 연구의 성능 주장을 본 프로젝트에서 검증한 것은 아니다.
- 실행·비용 기준선: **TRELLIS.2-4B**. 공개 가중치·코드 MIT, PBR/GLB 출력, 공식 NVIDIA24GB+ 경로. 가중치 자체는 무료지만 GPU 실행비는 별도다.
- 현재 M1/16GB에는 이 대형 CUDA 파이프라인을 무리하게 설치하지 않는다. 소량은 API 비교, 사용량이 커지면 학교 GPU 또는 자가 GPU 호스팅의 실제 비용을 비교한다.

## 모델과 실제 파일 확인

| 모델 | 확인한 원문 | 가중치 상태 | 결과/제약 |
|---|---|---|---|
| Pixal3D | [GitHub](https://github.com/TencentARC/Pixal3D), [HF](https://huggingface.co/TencentARC/Pixal3D) | ungated, revision `b0cb2e1b794cab9aa0ac38a95d794a4d9337437f`, checkpoint 파일 실제 존재 | 단일 이미지→상세 형상/PBR/GLB. 최신 코드 계열은 TRELLIS.2 기반, 논문 계열은 Direct3D-S2 기반으로 구분 |
| TRELLIS.2-4B | [GitHub](https://github.com/microsoft/TRELLIS.2), [HF](https://huggingface.co/microsoft/TRELLIS.2-4B) | ungated, revision `af44b45f2e35a493886929c6d786e563ec68364d`,9개 checkpoint 파일 합16,237,464,946B | 모든 해상도·encoder 변형 합계이지 단일 실행의 필수 다운로드/VRAM이 아님. 공식 Linux/NVIDIA24GB+, PBR GLB |
| Direct3D-S2 v1.1 | [GitHub](https://github.com/DreamTechAI/Direct3D-S2), [HF](https://huggingface.co/wushuang98/Direct3D-S2) | MIT, 실제ckpt 존재, revision `8b04a8eddb7a56a0f4e89fe5f5b840c7d5610c00` | 형상 중심. 512에10GB/1024에약24GB VRAM 안내. 완성 PBR 에셋 비용과 직접 비교하지 않음 |
| TripoSG | [GitHub](https://github.com/VAST-AI-Research/TripoSG), [HF](https://huggingface.co/VAST-AI/TripoSG) | MIT, 실제safetensors3개, revision `2c1c516d22d58db486a058d98d31bb6177344e06` | 형상 중심·CUDA8GB+. Tripo H3.1/P2 상용 API와 다른 모델 |
| Step1X-3D | [GitHub](https://github.com/stepfun-ai/Step1X-3D), [HF](https://huggingface.co/stepfun-ai/Step1X-3D) | Apache2, 실제체크포인트, revision `bf7084495b3a72222f36549b7942948aa4d9daa7` | 2025년 형상+SDXL 기반 텍스처. 공식27~29GB VRAM. 원클릭 저가 호스팅 경로는 이번 조사에서 확인하지 못함 |

Pixal3D의 Comfy-Org 배포에는 주 네트워크 INT8 파일5,584,555,824B, BF16 파일10,999,317,704B가 있다. TRELLIS.2의 Comfy-Org 주 네트워크 INT8 파일은5,253,048,192B다. **별도 image encoder/VAE/background 처리까지 포함한 총량이나 peak memory가 아니다.** 파일 크기만으로 M1 실행 가능성을 확정하지 않는다.

## 호스팅 경로와 공개 요금

| 모델/호스트 | 문서의 호출 식별자 | 문서 가격 | 비교 한계 |
|---|---|---|---|
| TRELLIS.2 / GoAPI | `Qubico/trellis2`, `POST /api/v1/task` | **$0.10/생성** | [원문](https://goapi.ai/docs/trellis2-api/create-task). 모델은 공식 TRELLIS.2 배포라고 명시하지만 해상도/PBR 세부 제어가 제한적. 동일 조건의 최저가라고 검증하지 않음 |
| TRELLIS.2 / PiAPI | `Qubico/trellis2`, `POST /api/v1/task` | **$0.10/생성** | [원문](https://piapi.ai/docs/trellis2-api/create-task). GoAPI와 같은 명칭/유사 명세이므로 독립 모델 비교군으로 세지 않음 |
| TRELLIS.2 / fal | `fal-ai/trellis-2` | **$0.25/512, $0.30/1024, $0.35/1536** | [원문](https://fal.ai/models/fal-ai/trellis-2). GLB·메시 단순화·텍스처 크기·seed 제어 명시 |
| Pixal3D / fal | `fal-ai/pixal3d` | **$0.30/1024, $0.42/1536** | [원문](https://fal.ai/models/fal-ai/pixal3d). GLB 출력. 이 resolution은3D 생성 프리셋이며 텍스처 크기와 별도 |
| TRELLIS.2 / Replicate | `fishwowater/trellis2` | **약$0.90/실행**, 입력에 따라 변동 | [원문](https://replicate.com/fishwowater/trellis2). 커뮤니티 배포, A10080GB, cold setup4~8분 경고. 현재 저비용 우선 선택에서 뒤로 둠 |

요금은2026-09-08 조회값이고 세금·환율·재시도·입력 이미지 준비·추가 변환·가입/최소충전 조건은 포함하지 않는다. 실제 API 실행·완성 에셋 평가를 아직 하지 않았으며 호스트의 정확한 체크포인트 리비전 일치도는 미검증이다. HF 모델 페이지의 ‘Inference Provider 없음’은 fal/GoAPI 같은 외부 배포까지 없다는 뜻이 아니다.

## 라이선스와 제외 사유

- Pixal3D의 [LICENSE](https://raw.githubusercontent.com/TencentARC/Pixal3D/master/LICENSE)와 [NOTICE](https://raw.githubusercontent.com/TencentARC/Pixal3D/master/NOTICE)는 공개 코드·파라미터·가중치의 MIT 적용을 명시한다. Tencent Cloud 상품이나 Hunyuan3D2.1과 동일한 라이선스로 묶지 않는다.
- TRELLIS.2 기본 pipeline.json에는 DINOv3·RMBG-2.0이 있다. core MIT만으로 모든 전처리/encoder/renderer의 사용 조건을 확정하지 않는다. 호스팅/대체 전처리 구성의 별도 약관과 구성 라이선스를 확인한다.
- Hunyuan3D2.1의 공개 라이선스는 대한민국을 Territory에서 제외한다. ‘HF 공개 모델은 한국 제한이 없다’는 검색 종합문의 단정은 틀려서 채택하지 않았다.
- Meshy7, Rodin, Tripo H3.1/P2는 이번 오픈웨이트 선택 대상에서 제외했다. TripoSG와 Tripo 상용 모델을 혼동하지 않는다.
- Meshy T2는 논문·예제 저장소는 확인했으나 실제 다운로드 가능한 가중치는 확인하지 못했다. 공개 예정이라는 문구만으로 오픈웨이트 후보로 승격하지 않는다.
- Hunyuan3D-Buffalo/WorldClaw, LATTICE/NaTex도 논문·프로젝트 페이지와 실제 가중치 배포를 구분한다. 이번 조사에서 다운로드 가능한 전체 실행 가중치를 검증하지 못한 모델은 추천 순위에서 제외했다.

## 권장 실행 순서

소량 시험은 **TRELLIS.2 $0.10 호스팅 경로와 Pixal3D $0.30 경로**를 같은 허용된 소품 입력으로 비교한다. 값싼 쪽의 출력이 PBR/GLB/형상 보존 요구를 못 맞추면 fal TRELLIS.2를 기준선으로 사용한다. 기본 추론비만 계산하면10회는각$1/$3 수준이며, 이것을10개의 수용된 완성 에셋 비용이라고 표현하지 않는다. 반복 사용량이 커질 때만 실제 학교 GPU의 처리량·유지 비용과 API 청구를 비교한다.
