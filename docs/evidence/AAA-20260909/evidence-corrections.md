# Evidence corrections for the 2026-09-09 review

This is an append-only audit of commit `d169b560762ed7c3d4969e845a2750760941ee84`. Historical responses, verdicts and logs remain unchanged. This audit is an internal OpenAI pass; it grants no independent-provider approval.

All **45 raw response hashes**, **17 manifest hashes**, and **131 source-file manifest entries** were recomputed and matched. All 45 sessions and response hashes are unique. The 11-response `round2.json` is an earlier duplicate snapshot and is excluded from the total. The complete files contain 21 + 12 + 8 + 4 responses.

The historical index's `effectiveOutcome` is the parser result. It is not the accepted evidence decision. Round 1 core/safety and capture/spec still say `approved` in that field although the orchestrator rejected their unexecuted-test claims. Use [the adjudication overlay](historical-adjudication-overlay.json) to read raw outcomes and accepted historical scope separately. Only the questionnaire, context and PR98 graph have the three accepted historical lenses reported by the original disposition. The overlay creates no new approval.

Exact historical prompts/message arrays and the inference harness are unavailable. All 45 prompt hashes are preserved but **none was recomputed**. The no-truncation, lossless-compaction and fresh-session statements remain historical harness reports; this audit does not independently prove them or allege that truncation occurred.

The historical PR97 review and 107/103 passed-test counts cover `39e5dc24892744feacec09eac0c2e71951cb31c8`. The root's 2026-09-09 02:01:35 UTC observation records the newer head `9af125b22ac3989a18593238c3331031496c164f`. Old findings, outcomes and counts are not carried to that head. The initial observation omitted per-run `head_sha`. The root supplied full GitHub run metadata, and this audit verified run `34299012185` → `9af125b` and run `34298931594` → `d169`, both successful. Those run conclusions do not transfer the older policy test counts to newer code.

Historical PR104 evidence is 37 core + 12 filesystem NUnit tests and 92 reconstruction Python tests. Nominal success of the Unity workflow does not mean Editor tests ran. Unity compile/render/lifecycle, new Unity 13 and canonical 15 cases, school-PC/HMD interaction and field acceptance remain unexecuted in this review. Asset 15 tests and 33 matching registry records do not establish new visual inspection.

#14's delivery/reply and institutional approval, #52's actual policy authority/pin and unavailable original 105-key journal disposition, #64's real team handoff, and #10's new FMP contracts remain separate pending conditions. The historic independent-review gates for #25/#26/#32/#52/#65 remain unsatisfied. New user-authorized corrections may proceed, but same-provider passes do not convert those gates to independent-provider approval.

Detailed hashes, source scopes, verified counts, historical board dispositions and exact corrections are in [the evidence audit](evidence-audit.json). The new critic/supervisor comparison is pending and has not been read or summarized here.
