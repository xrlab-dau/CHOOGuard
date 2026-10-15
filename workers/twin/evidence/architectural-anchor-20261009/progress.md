# Progress

2026-10-09: planning-with-files read; bounded direct-column investigation started.

- OpenCV LSD returned Nx4 instead of assumed Nx1x4; normalized with reshape(-1,4). Direct-edge candidates generated.

- Executed new direct 4-corner fit; both parities reject. Direct near/far-foot triangulations hold out at <=2.53px; side foot 8.63px and top 23.05px show annotation/current camera incompatibility. This does not prove current column dimensions wrong.
- Original-source fixed-bank receipt inspected: different bank controls, rejected, held-out RMS24.70px. No reuse. Inspect source-observed column plane junctions once as an alternative geometric control.
