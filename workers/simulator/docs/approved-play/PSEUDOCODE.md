# 비상조건 생성·NPC 이동/행동·시드 의사코드

[계획](PLAN.md)과 [아키텍처](ARCHITECTURE.md)의 구현 순서를 읽기 위한 의사코드다. 실제 코드와 씬은 이 문서 PR에 포함하지 않는다. 현재 로컬 기본 시드20261016과 매번 새 시드를 생성하는 목표 정책은 구분한다.

## 시드 선택과 초기 생성

```text
# 새 시드 자동 선택은 다음 구현에서 명시적으로 연결할 정책이다.
if mode == recorded_replay:
    seed = tape.run_seed
else if user explicitly supplied a seed:
    seed = parse_unsigned_seed(user_input)
else:
    seed = new_random_master_seed()
record(seed, source_epoch, engine_version, rng_version, fixed_step, configuration)

selection = lock_user_selected(provider, account, model, catalog, contract)
require subscription_use_authorized and stopped_tick_zero_world
world = initialize_from_current_objects_without_moving_source_geometry(seed)
request = lossless_all_world_graph(world, coverage)
key = hash(applicability, exact_coverage, logical_selection, actual_full_body_sha,
           prompt_contract, material_contract, revision)

if exact_key_has_immutable_material(key):
    material = read_material(key)              # 새로운 모델 요청/예약 없음
else singleflight(key):
    require explicit_initial_generation_allowance
    reserve_one_bounded_initial_request()     # NPC 반복 예산과 분리
    reply = await selected_model.propose_upstream_priors(request)
    reject_if_invalid_schema_or_seed_or_basis_or_usage(reply)
    reject_if_unknown_target_or_capability_or_out_of_range_or_derived_outcome(reply)
    material = freeze_exact_sorted_priors(reply)

require selection_still_current and caller_not_cancelled
require same_source_epoch and stopped_tick_zero_world and same_exact_basis
for prior in stable_entity_property_order(material):
    u = Unit(rng_version, seed, "initial-material:" + material.sha + prior.property,
             tick=0, entity=prior.entity_id, draw=0)
    value = exact_endpoint_if_zero_width_else_lower_plus_width_times_u(prior)
validate_all_changes_then_apply_atomically(world)
record(material, accepted_reply, sampled_values, pre_digest, post_digest)
start_run_using_exact_prepared_initial_state()
```

새 모델 요청은 seed만으로 재현되지 않는다. 위 새 seed 선택은 연결할 목표 정책이며 현재 로컬 저장 씬은20261016으로 고정돼 있다.

## 고정 틱과 개인 행동

```text
while run_is_active:
    before_kernel:
        ingest_ordered_player_commands_through_guarded_executor()
        ingest_actual_or_recorded_motion_LOS_arrival_failure()
        ingest_mechanical_response_and_navigation_feedback()

    validate_all_world_numbers_and_counters_before_writing()
    causal_kernel.step(fixed_dt, independent_seeded_streams)

    for npc in stable_actor_order:
        observed = perceive_local_evidence_and_known_places(npc)
        update_memory_social_information_stress_and_reaction_delay(npc, observed)
        if immobile_or_reaction_delay_not_elapsed(npc): continue
        keep_goal_until_arrived_failed_or_observed_danger_invalidates(npc)
        goal = routine_or_assist_or_evacuate_or_shelter_from_known_evidence(npc)

        if dependency_bound_cached_plan_is_valid(actor, goal, nav, contracts):
            plan = reuse_cached_plan()
        else:
            coalesce_and_queue_async_model_request_if_meaningful_change()
            obey_cooldown_request_token_concurrency_budgets()
            use_only_supported_guarded_local_behavior_while_waiting()

    poll_ready_model_replies()
    for reply in ready_replies:
        reject_if_cancelled_stale_selection_actor_goal_nav_versions(reply)
        validate_typed_supported_operations_and_dependencies(reply)
        cache_only_valid_plan(reply)

    execute_ready_steps_only_if_current_preconditions_hold()
    record_ordered_decisions_inputs_observations_transitions_and_tick_digest()
```

## 실제 이동과 피드백

```text
for npc_with_active_goal:
    if agent_not_on_navmesh:
        report_navigation_failure_and_replan()
        continue
    target = sample_goal_on_same_qualified_walking_surface()
    path = calculate_navmesh_path(actual_position, target)
    if target_missing_or_path_not_complete:
        report_unreachable_without_teleporting()
        continue
    navmesh_agent.set_path(path)               # 위치/속도를 소유하는 유일한 실행 주체
    animation.speed = actual_agent_velocity
    if arrived:
        report_arrival_and_destination_visit()
    if blocked_or_target_invalidated:
        report_failure_invalidate_dependencies_and_replan()
```

## 재현 조건

```text
u = Unit(rng_version, seed, stream, tick, entity_id, draw_index)
# UI/다른 NPC/다른 사건의 draw 횟수가 이 tuple을 바꾸지 않는다.
replay_inputs = source_epoch + versions + seed + fixed_step/configuration
              + immutable_material + accepted_external_outputs
              + ordered_player_actions + recorded_physical_observations
require exact_initial_digest_and_ordered_record_consumption()
replay_without_provider_requests(replay_inputs)
```

같은 material·world 구성·seed의 로컬 샘플링 결정성과 새 외부 응답의 결정성을 혼동하지 않는다. 다른 시드/행동/source/material/dt는 다른 입력이다. 재생에서 NavMesh/물리 관측을 기록으로 공급한 결과를 독립 Unity 물리 재시뮬레이션으로 부르지 않는다.
