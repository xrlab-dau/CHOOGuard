# Findings

- directly_supported: current mission controller initializes exactly fire-1 and medical-1, hardcodes Central119 and two positional routes, uses one mission per onsite team.
- directly_supported: onsite worker and station map exactly ops-1/fire-1/medical-1; every unrecognized team ID maps to medical index in MvpTeamTaskController.Index.
- directly_supported: city height uses source numeric height, else levels*3, else OSM numeric ID modulo four (6/9/12/15m), then clamps to 6..192m and hardcodes feature 764492639 to 8m. Unknown heights cannot establish accurate twin geometry.
- directly_supported: current session graph remains IN_PROGRESS and NOT_ACCEPTED; current agency checkpoint is bounded native verification, not production/twin acceptance.
- near_match_only: memory describes older native MVP limits and preference for bounded implementation; current code and current user directions control this plan.

- directly_supported: local data has 1,150 building features: 95 explicit heights, 390 levels only, 665 neither. IDs 987862339/987862340 each have 200m tags but current builder truncates to 192m.
- directly_supported: route generator uses first catalog agency only; directed road graph already supports arbitrary start/end, so loop over enabled source agencies and key routes by agency+direction.
- directly_supported: station builder adds two decorative fixed vehicles separate from dispatched vehicles; regenerate without those untracked clones.
- navigation gotcha: do not grep minified world-layers.json; one line exceeds 24MB. Use selected JSON fields.
