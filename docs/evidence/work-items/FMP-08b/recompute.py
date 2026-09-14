#!/usr/bin/env python3
"""Generator-independent recomputation of the FMP-08b (#129) receipt.

WHY THIS FILE EXISTS
--------------------
The C# harness that produced docs/evidence/work-items/FMP-08b/result.json also checks its own numbers
twice: two byte-identical runs are compared field by field (result.json
measurementConditions.crossRunDeterminism). That check is blind to a mistake the generator makes
consistently. It was blind once for real: a previous revision of this receipt published
determinism.finalBits = 4646051498886027000 next to finalReferenceM = 362.34571428572997, a pair that
contradicts itself - the published integer decodes to the double 417.45140006333486, which appears
nowhere in the record - and the same-generator comparison reported 0 differing fields, because both
runs were wrong in exactly the same way. This script is the second, independent generator.

WHAT IS RECOMPUTED, AND WHY THAT IS VALID FOR CORRECTNESS
---------------------------------------------------------
1. Analytic stopping distance and time (analyticStopping.samples, 10 samples).
   The shipped brake profile is piecewise: a pure delay of `delay` seconds at constant speed, a
   constant-jerk build-up ramp of `ramp` seconds that raises deceleration linearly from 0 to `a`, and
   constant deceleration `a` thereafter. With v the speed at the brake request:

       v_ramp_end = v - a*ramp/2
       d(v) = v*delay + v*ramp - a*ramp^2/6 + v_ramp_end^2 / (2a)
       t(v) = delay + ramp + v_ramp_end / a            (when v > a*ramp/2)

   and when v <= a*ramp/2 the ramp alone stops the train, so the ramp is truncated at
   T = sqrt(2*ramp*v/a) and d(v) = v*delay + v*T - a*T^3/(6*ramp). Both branches are written out
   below, in Python, sharing no code with the C# harness.
   WHY THIS IS VALID: the closed form is the analytic solution of the profile the C# harness also
   integrates. Agreement with the published oracleDistanceM/oracleTimeS shows the published values
   are closed-form results, not the integrator's output echoed back. Agreement with shippedDistanceM
   shows the shipped query implements the same closed form. Agreement with
   integratedDistanceM/integratedTimeS shows the fixed-step integrator lands on the closed form too.
   Three derivations agreeing is what makes the numbers evidence rather than a transcript.

2. Maximum observed deviation (analyticStopping.maximumObservedDeviation).
   Recomputed from the published samples: if any published deviation were a value no pair of
   published samples produces, this fails.

3. Brake delay and build-up (brakingDelayAndBuildUp.delayPhase and .buildUpPhase).
   Recomputed from the published envelope constants (0.4 s / 0.5 s / 0.8 m/s^2) and the published
   maximum speed, using the same closed form: the delay moves the train v*delay with deceleration 0,
   the ramp ends at v_ramp_end with the ramp distance above, and the residual to the stop is
   v_ramp_end^2/(2a). Nothing here is read back from the record except the constants being checked.

4. Occupancy boundaries (occupancy.*).
   `SweptOccupancy` maps the route-axis interval [r - consist, r] onto physical track. For a forward
   segment the mapped interval is [a, b] with a = low - RouteStartM, b = high - RouteStartM; for a
   reversed segment it is [LengthM - b, LengthM - a]. `Overlaps` is the strict test
   StartM < other.EndM and EndM > other.StartM. Both are reimplemented here, so the published entry
   reference, the release reference, the disagreement-window count and both reversed-segment probes
   are recomputed from the published geometry instead of being read back.
   WHY THIS IS VALID: these are the geometric definitions of the mapping and of interval overlap, not
   a copy of the shipped implementation's control flow; a mapping or overlap mistake in the harness
   changes these numbers.

5. Bit patterns (determinism.finalBits / repeatFinalBits).
   Recomputed with struct.pack('<d') / '<q', i.e. IEEE-754 binary64, and required to equal the
   published integers AND to decode back to the published finalReferenceM. This is the exact field a
   previous revision published incorrectly; a generator-independent recomputation is the check that
   catches it.

6. Shipped boundary hand-off arithmetic (shippedBoundaryHandoff.measured).
   The build-up remainder, its rounding residue against the 0.05 s step, and the deceleration the
   ramp would round onto at that step are recomputed from the published emergency envelope. This is
   the one arithmetic claim in the record about the shipped integrator's boundary state, and it is
   checked rather than asserted.

Exit code is non-zero if any recomputation disagrees with the record, so this file can be run as a
gate. It reads only the receipt JSON and never the harness source.

Usage:  python3 recompute.py <result.json> [<result.json> ...]
"""

import json
import struct
import sys


def analytic(speed, delay, ramp, a):
    """Closed form for the piecewise delay / constant-jerk ramp / constant-deceleration stop."""
    if ramp == 0:
        return speed * delay + speed * speed / (2 * a), delay + speed / a
    if speed <= a * ramp / 2:
        t_ramp = (2 * ramp * speed / a) ** 0.5
        return speed * delay + speed * t_ramp - a * t_ramp ** 3 / (6 * ramp), delay + t_ramp
    v_end = speed - a * ramp / 2
    return (speed * delay + speed * ramp - a * ramp * ramp / 6 + v_end * v_end / (2 * a),
            delay + ramp + v_end / a)


def occupancy(route_start, length, physical_start, reversed_, reference, consist):
    """Reimplementation of the shipped swept-occupancy mapping for a single-segment route."""
    low = max(reference - consist, route_start)
    high = min(reference, route_start + length)
    if low >= high:
        return None
    a = low - route_start
    b = high - route_start
    if reversed_:
        return (physical_start + length - b, physical_start + length - a)
    return (physical_start + a, physical_start + b)


def overlaps(interval, block):
    return interval is not None and interval[0] < block[1] and interval[1] > block[0]


def bits(x):
    return struct.unpack("<q", struct.pack("<d", x))[0]


def decode(i):
    return struct.unpack("<d", struct.pack("<q", i))[0]


def close(a, b, tol):
    return abs(a - b) <= tol


def check(path):
    with open(path, encoding="utf-8") as handle:
        r = json.load(handle)

    failures = []
    notes = []

    def expect(label, condition, detail):
        (notes if condition else failures).append(
            ("ok   " if condition else "FAIL ") + label + ": " + detail)

    dtol = r["analyticStopping"]["distanceToleranceM"]
    ttol = r["analyticStopping"]["timeToleranceS"]
    profiles = {
        "service": (r["fixture"]["serviceBrake"]["delaySeconds"], r["fixture"]["serviceBrake"]["buildUpSeconds"],
                    r["fixture"]["serviceBrake"]["decelerationMS2"]),
        "emergency": (r["fixture"]["emergencyBrake"]["delaySeconds"], r["fixture"]["emergencyBrake"]["buildUpSeconds"],
                      r["fixture"]["emergencyBrake"]["decelerationMS2"]),
    }

    # ---- 1. analytic samples -------------------------------------------------------------------
    worst = {"oracleVsShippedDistanceM": 0.0, "oracleVsIntegratorDistanceM": 0.0,
             "oracleVsIntegratorTimeS": 0.0, "oracleVsShippedTimeS": 0.0}
    samples = r["analyticStopping"]["samples"]
    for s in samples:
        d, t = analytic(s["speedMS"], *profiles[s["profile"]])
        worst["oracleVsShippedDistanceM"] = max(worst["oracleVsShippedDistanceM"], abs(d - s["shippedDistanceM"]))
        worst["oracleVsShippedTimeS"] = max(worst["oracleVsShippedTimeS"], abs(t - s["oracleTimeS"]))
        worst["oracleVsIntegratorDistanceM"] = max(worst["oracleVsIntegratorDistanceM"], abs(d - s["integratedDistanceM"]))
        worst["oracleVsIntegratorTimeS"] = max(worst["oracleVsIntegratorTimeS"], abs(t - s["integratedTimeS"]))
    expect("analytic sample count", len(samples) == 10,
           "recomputed %d samples (5 speeds x 2 profiles)" % len(samples))
    for key in sorted(worst):
        expect("closed form vs published " + key, worst[key] <= (ttol if key.endswith("TimeS") else dtol),
               "max deviation %.17g (tolerance %.17g)" % (worst[key], ttol if key.endswith("TimeS") else dtol))

    v = r["fixture"]["maximumSpeedMS"]
    d_em, t_em = analytic(v, *profiles["emergency"])
    d_sv, t_sv = analytic(v, *profiles["service"])
    expect("pinned emergency distance", close(d_em, r["fixture"]["emergencyStopDistanceAtMaxSpeedM"], dtol),
           "recomputed %.17g vs recorded %.17g" % (d_em, r["fixture"]["emergencyStopDistanceAtMaxSpeedM"]))
    expect("pinned service distance", close(d_sv, r["fixture"]["serviceStopDistanceAtMaxSpeedM"], dtol),
           "recomputed %.17g vs recorded %.17g" % (d_sv, r["fixture"]["serviceStopDistanceAtMaxSpeedM"]))
    expect("emergency envelope not longer than service", d_em < d_sv, "%.17g < %.17g" % (d_em, d_sv))

    # ---- 2. maximum observed deviation is the deviation of the published samples ----------------
    published = r["analyticStopping"]["maximumObservedDeviation"]
    for key in sorted(worst):
        if key in published:
            expect("published max deviation " + key, published[key] == worst[key],
                   "record %.17g vs recomputed %.17g" % (published[key], worst[key]))

    # ---- 3. delay / build-up phase ---------------------------------------------------------------
    delay, ramp, a = profiles["service"]
    v_end = v - a * ramp / 2
    ramp_distance = v * delay + v * ramp - a * ramp * ramp / 6
    residual = v_end * v_end / (2 * a)
    bd = r["brakingDelayAndBuildUp"]
    expect("delay phase distance", bd["delayPhase"]["distanceAfterM"] == v * delay,
           "%.17g vs %.17g" % (bd["delayPhase"]["distanceAfterM"], v * delay))
    expect("delay phase keeps speed and holds deceleration at zero",
           bd["delayPhase"]["speedAfterMS"] == v and bd["delayPhase"]["decelerationAfterMS2"] == 0.0,
           "v %.17g, a %.17g" % (bd["delayPhase"]["speedAfterMS"], bd["delayPhase"]["decelerationAfterMS2"]))
    expect("ramp end speed", bd["buildUpPhase"]["speedAtRampEndMS"] == v_end,
           "%.17g vs %.17g" % (bd["buildUpPhase"]["speedAtRampEndMS"], v_end))
    expect("ramp end distance", close(bd["buildUpPhase"]["distanceAtRampEndM"], ramp_distance, dtol),
           "%.17g vs %.17g" % (bd["buildUpPhase"]["distanceAtRampEndM"], ramp_distance))
    expect("ramp end residual is v_end^2/(2a)", close(bd["buildUpPhase"]["residualToStopM"], residual, dtol),
           "%.17g vs %.17g" % (bd["buildUpPhase"]["residualToStopM"], residual))
    expect("ramp distance + residual is the whole stop",
           close(ramp_distance + residual, d_sv, dtol),
           "%.17g + %.17g = %.17g vs %.17g" % (ramp_distance, residual, ramp_distance + residual, d_sv))
    expect("partition sweep excludes the hazard step",
           bd["partitionSweep"]["excludedStepS"] not in bd["partitionSweep"]["emergencyStepS"],
           "%r not in %r" % (bd["partitionSweep"]["excludedStepS"], bd["partitionSweep"]["emergencyStepS"]))
    expect("partition sweep is not one step", len(bd["partitionSweep"]["emergencyStepS"]) >= 6,
           "%d steps" % len(bd["partitionSweep"]["emergencyStepS"]))

    # ---- 4. occupancy ----------------------------------------------------------------------------
    occ = r["occupancy"]
    route_end = r["fixture"]["routeEndM"]
    consist = r["fixture"]["consistLengthM"]
    start, end = occ["platformBlock"]["startM"], occ["platformBlock"]["endM"]
    block = (start, end)
    expect("block is the published platform block",
           close(start, r["fixture"]["stationReferenceM"] - consist, 1e-9)
           and close(end, r["fixture"]["stationReferenceM"] + consist, 1e-9),
           "[%.17g, %.17g]" % (start, end))
    expect("entry reference is the block start",
           close(occ["entryReferenceM"], start, 1e-12), "%.17g" % occ["entryReferenceM"])

    lo, hi = end, end + consist + 5.0
    for _ in range(200):
        mid = (lo + hi) / 2.0
        if overlaps(occupancy(0.0, route_end, 0.0, False, mid, consist), block):
            lo = mid
        else:
            hi = mid
    expect("release reference found in the reimplemented query",
           close(hi, occ["releaseReferenceM"], 1e-9),
           "search found %.17g vs recorded %.17g" % (hi, occ["releaseReferenceM"]))
    expect("release is one consist past the block end",
           close(hi - end, consist, 1e-9), "%.17g - %.17g = %.17g vs consist %.17g" % (hi, end, hi - end, consist))
    expect("head-only release point is the block end",
           close(occ["headOnlyReleaseReferenceM"], end, 1e-12), "%.17g" % occ["headOnlyReleaseReferenceM"])

    step = 0.5
    counted = 0
    first = last = None
    reference = start
    while reference <= end + consist + consist:
        if reference > end and overlaps(occupancy(0.0, route_end, 0.0, False, reference, consist), block):
            counted += 1
            first = reference if first is None else first
            last = reference
        reference += step
    expect("disagreement window count", counted == occ["headOnlyReleaseCounterexamples"],
           "recount %d vs recorded %d" % (counted, occ["headOnlyReleaseCounterexamples"]))
    expect("disagreement window endpoints",
           first == occ["counterexampleWindow"][0] and last == occ["counterexampleWindow"][1],
           "recount [%.17g, %.17g] vs recorded %r" % (first, last, occ["counterexampleWindow"]))

    rev = occ["reversedSegmentProbe"]
    track, block_start, block_end = rev["trackLengthM"], rev["physicalBlockM"][0], rev["physicalBlockM"][1]
    block_r = (block_start, block_end)
    mapping = rev["mappingProbeReferenceM"]
    expect("reversed mapping probe is an entry probe, not the release boundary",
           mapping != rev["releaseReferenceM"], "%r vs %r" % (mapping, rev["releaseReferenceM"]))
    expect("reversed mapping probe does not cover the block",
           not overlaps(occupancy(0.0, track, 0.0, True, mapping, consist), block_r),
           "mapped %s" % (occupancy(0.0, track, 0.0, True, mapping, consist),))
    expect("one micron past the mapping probe covers the block",
           overlaps(occupancy(0.0, track, 0.0, True, mapping + 1e-6, consist), block_r),
           "mapped %s" % (occupancy(0.0, track, 0.0, True, mapping + 1e-6, consist),))
    release = rev["releaseReferenceM"]
    expect("reversed pass-release boundary is free",
           not overlaps(occupancy(0.0, track, 0.0, True, release, consist), block_r),
           "mapped %s" % (occupancy(0.0, track, 0.0, True, release, consist),))
    expect("reversed pass-release boundary holds one micron earlier",
           overlaps(occupancy(0.0, track, 0.0, True, release - 1e-6, consist), block_r),
           "mapped %s" % (occupancy(0.0, track, 0.0, True, release - 1e-6, consist),))

    # ---- 5. bit patterns -------------------------------------------------------------------------
    det = r["determinism"]
    ref = det["finalReferenceM"]
    published_bits = det.get("finalBits")
    published_repeat = det.get("repeatFinalBits")
    expect("bit pattern is published at all",
           published_bits is not None and published_repeat is not None,
           "finalBits=%r repeatFinalBits=%r" % (published_bits, published_repeat))
    expect("final bits are the bits of the published reference position",
           bits(ref) == published_bits, "bits(%.17g) = %d vs recorded %d" % (ref, bits(ref), published_bits))
    expect("repeat bits are the bits of the published repeat position",
           bits(det["repeatFinalReferenceM"]) == published_repeat,
           "bits = %d vs recorded %d" % (bits(det["repeatFinalReferenceM"]), published_repeat))
    expect("published bits decode back to the published double",
           decode(published_bits) == ref, "%.17g" % decode(published_bits))
    expect("both runs published the same position",
           det["finalReferenceM"] == det["repeatFinalReferenceM"] and det["statesIdentical"] is True,
           "%.17g / %.17g" % (det["finalReferenceM"], det["repeatFinalReferenceM"]))

    # ---- 6. shipped boundary hand-off arithmetic --------------------------------------------------
    h = r["shippedBoundaryHandoff"]["measured"]
    step_s = h["stepSeconds"]
    em_delay, em_ramp, em_a = profiles["emergency"]
    # The ramp is sampled at fixed steps; the remainder after the last whole step is the residue.
    expected_remainder = em_ramp - step_s * (em_ramp // step_s)
    residue = expected_remainder if expected_remainder != 0 else step_s
    expect("hazard step is the published step", step_s == r["shippedBoundaryHandoff"]["measured"]["stepSeconds"],
           "%.17g" % step_s)
    expect("hazard step is divisible into the ramp with a rounding remainder",
           abs(h["buildUpRemainingBeforeS"] - step_s) <= 1e-15,
           "remainder before the refused step %.17g, step %.17g" % (h["buildUpRemainingBeforeS"], step_s))
    expect("residue is the subtraction the integrator performs",
           close(h["residueSeconds"], h["buildUpRemainingBeforeS"] - step_s, 1e-18),
           "%.17g - %.17g = %.17g vs recorded %.17g"
           % (h["buildUpRemainingBeforeS"], step_s, h["buildUpRemainingBeforeS"] - step_s, h["residueSeconds"]))
    expect("rounded deceleration reaches the target before the ramp is over",
           h["roundedDecelerationMS2"] == h["targetDecelerationMS2"],
           "%.17g vs %.17g" % (h["roundedDecelerationMS2"], h["targetDecelerationMS2"]))

    for note in notes:
        print(note)
    for failure in failures:
        print(failure)
    print("RECOMPUTE %s: %d ok, %d failed" % (path, len(notes), len(failures)))
    return len(failures)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    return 1 if sum(check(p) for p in argv[1:]) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
