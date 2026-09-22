# 원문과 이전 자료 기록

새 방법론 원문만 이번에 확인했다. 기존 제품·에셋 자료는 원문 주소와 이전 상태를 보존했으며 원본 취득·현행성·Unity 임포트를 재실행하지 않았다.

- [METHOD-HARNESS](https://openai.com/index/harness-engineering/): 짧은 진입 문서, 저장소 정본, 실행 증거와 기계검사. 웹 DOM이나 자율 merge 정책을 Unity에 수입하지 않음.
- [METHOD-CONTEXT](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents): 작업별 문맥 선택·progressive disclosure. 특정 모델 효용 보증 아님.
- [METHOD-JSONLD](https://www.w3.org/TR/json-ld11/): 온톨로지 관계 직렬화. 런타임 scheduler 표준 아님.
- [METHOD-PROVO](https://www.w3.org/TR/prov-o/): 계획·실행·증거 구별. 예상 산출물을 실제 생성된 Entity로 표시하지 않음.
- [METHOD-JSONSCHEMA](https://json-schema.org/draft/2020-12): 스토리·실행기록의 타입과 필수필드 검사. 진실/제품 정확도 증명 아님.
- [METHOD-SHACL](https://www.w3.org/TR/shacl/): RDF 관계 제약 사양. 이 환경의 engine 실행 여부는 review에 별도 기록.
- [METHOD-SKILLS](https://agentskills.io/specification): 진입 메타데이터와 필요자료의 점진적 로드. 본 결과물은 이 규격을 완전히 구현한 skill package를 표방하지 않음.

제품 자료: [기술·매뉴얼 48개 기록](basis/v3/reference/sources.json) / [무료 에셋 25개 기록](basis/v3/reference/free-assets.json). 필요한 자료는 story.sourceIds/sourceRefs로 조회한다.
