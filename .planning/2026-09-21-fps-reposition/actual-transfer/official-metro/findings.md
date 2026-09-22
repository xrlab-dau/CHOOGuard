# Findings

## Prior graph treated only as navigation
- The existing graph points to HUMETRO station-info pages and a `113.gif` evacuation/station schematic.
- Its Exit 4 / Exit 6 plaza route and OSM registration are not accepted as authority for the actual railway-to-Metro transfer.

## Live research

- Official Busan City search result for the 2019 opening directly states that the underground passage connects the Metro Busan Station and KTX Busan Station, is 99.6 m long and 8 m wide, and contains a 50 m two-way moving walk, four escalators, and one elevator. This confirms existence and overall facilities but does not name the Metro concourse endpoint or exit number.
- HUMETRO's current official accessibility inventory lists six escalators at Busan Station. That is a station-wide count and does not identify which are platform-to-concourse versus passage/esplanade circulation.
- The official HUMETRO station-info endpoints surfaced by the prior graph are real current URLs, but the web reader could not fetch them; direct HTTP retrieval is required.
- Search results did not substantiate an official current "Exit 8" claim for Busan Station. This remains unresolved.

## Numbering systems resolved
- The live HUMETRO station-113 exit table and its downloadable station map both enumerate **Metro exits 1–7**. The table identifies Metro exits 4 and 6 with 부산역광장, but it does not show the 2019 underground railway passage.
- The live BISCO 부산역지하도상가 plan enumerates **shopping-mall surface exits 1–8**. These numbers belong to the underground mall, not to HUMETRO station 113. The apparent "current Exit 8" discrepancy is therefore a namespace mismatch, not proven Metro exit renumbering.
- An official Busan City article from 2017 mentions "부산역 10번 출구", and a 2024 city story uses "부산역 10번 출구" for the rear/north-port side of the railway station. Those references are not evidence that Metro station 113 had or has Exit 10; "Busan Station" is ambiguous across the railway station, Metro station, and underground mall.

## Current official underground-mall plan
- BISCO's official current 부산역지하도상가 page serves `images/map_n24/busan_main.png`; the server reports `Last-Modified: 2024-07-01 03:50:43 GMT`. The page remains live in 2026.
- The 1300×364 plan shows a long two-sided mall spine, shopping-mall exits 1–8, and a perpendicular branch explicitly labelled `부산역 연결통로` near the plan's left end. On the image, the branch occupies approximately x=210–260, y=190–318; mall Exit 2 is near x=282, y=235 and Exit 1 near x=351, y=139. This establishes the passage junction as the near-end segment around mall exits 1–2, not an outdoor Metro exit.
- The same BISCO page locates the mall at `동구 중앙대로 지하200(초량동), 부산역~중앙회관`, which places its one end at Busan Metro station. The plan itself only gives a continuation arrow at the far left and does not label fare gates or the precise HUMETRO wall/interface, so the final mall-to-Metro-concourse seam remains schematic.
- BISCO's 2016 statistical yearbook records a 2015-11-01 priority opening of the `부산역 연결통로 지하상가 출입구(E/S)` and describes the then-planned pedestrian passage plus a separate new entrance. This directly supports that the connection is mediated by the underground mall/its passage entrance; it is not the earlier modeled outdoor Exit 6 corridor.

## HUMETRO internal vertical circulation
- HUMETRO's current accessibility inventory lists Busan Station with **2 internal elevators and 2 external elevators**. The two internal elevators match the station schematic's one platform-to-concourse lift for each of the two side platforms.
- HUMETRO's current escalator inventory lists **6 escalators** at Busan Station, but the inventory does not assign them to individual level changes.
- The downloadable 1000×661 HUMETRO schematic shows two side platforms below a shared upper concourse, with stairs/escalators and a dedicated internal elevator from each platform to concourse. Approximate internal-lift shaft centers are (410, 426) and (648, 463) in source-image pixels; a surface/concourse lift is visible near (580, 337). The drawing is schematic, has no scale or datum, and has no published content date/Last-Modified header.

## Critical gap
- No official source found in this lane publishes a single integrated, dimensioned plan from platform through fare gates, Metro concourse, underground-mall seam, and the 2019 passage. The platform/concourse topology and the mall/passage junction are supported by separate operator plans; their exact seam and metric offsets remain insufficient.
