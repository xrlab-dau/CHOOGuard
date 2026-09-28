// Play-mode route probe using the same movement, gravity and collision code as keyboard input.
// Only reset places the player. A leg completes only at the actual grounded 3D destination.
// args: [reset|step|release, routeJson, stepsPerCall]

using System;
using System.Collections.Generic;
using System.IO;
using ChooGuard.App.Fps;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

public static class RouteWalk
{
    const string Prefix = "chooguard.routewalk.";
    const float DeltaSeconds = 1f / 30f;
    const float ArrivalRadius = .18f;
    const float ArrivalHeight = .20f;

    public static void Main(string[] args)
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("RouteWalk requires Play mode.");
        string mode = args != null && args.Length > 0 ? args[0] : "step";
        var actor = UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
        if (actor == null) throw new InvalidOperationException("FPS responder not found.");
        if (mode == "release")
        {
            actor.SetExternalInputMode(false);
            SessionState.EraseString(Prefix + "route");
            Debug.Log("ROUTE_RELEASED: normal Game-view input restored.");
            return;
        }
        if (mode != "reset" && mode != "step")
            throw new ArgumentException("Use reset, step or release.");
        string path = Path.GetFullPath(args != null && args.Length > 1 ? args[1]
            : ".planning/2026-09-22-station-interior-build/route-waypoints.json");
        var points = ReadWaypoints(path);
        if (points.Count < 2) throw new InvalidOperationException("At least two route points are required.");
        var body = actor.GetComponent<CharacterController>();
        if (!body.enabled) throw new InvalidOperationException("Player collision controller is disabled.");

        if (mode == "reset")
        {
            actor.SetExternalInputMode(true);
            body.enabled = false;
            actor.transform.position = points[0] + Vector3.up * .1f;
            body.enabled = true;
            Physics.SyncTransforms();
            actor.Resume(false);
            SessionState.SetString(Prefix + "route", path);
            SessionState.SetInt(Prefix + "leg", 0);
            SessionState.SetInt(Prefix + "stalled", 0);
            SessionState.SetFloat(Prefix + "dist", 0);
            Report(actor, body, points, 0, 0, 0, "RESET");
            return;
        }
        if (SessionState.GetString(Prefix + "route", "") != path || !actor.ExternalInputMode)
            throw new InvalidOperationException("Reset this route before stepping it.");
        if (!actor.Resume(false)) throw new InvalidOperationException("Player cannot resume.");
        int count = args != null && args.Length > 2 ? int.Parse(args[2]) : 40;
        if (count < 1 || count > 2000) throw new ArgumentOutOfRangeException("stepsPerCall");
        int leg = SessionState.GetInt(Prefix + "leg", 0);
        int stalled = SessionState.GetInt(Prefix + "stalled", 0);
        float travelled = SessionState.GetFloat(Prefix + "dist", 0);
        for (int i = 0; i < count && leg < points.Count - 1 && stalled < 90; i++)
        {
            Vector3 delta = points[leg + 1] - actor.transform.position;
            Vector3 horizontal = new Vector3(delta.x, 0, delta.z);
            if (horizontal.magnitude <= ArrivalRadius && Mathf.Abs(delta.y) <= ArrivalHeight && body.isGrounded)
            {
                leg++;
                stalled = 0;
                continue;
            }
            float desiredYaw = horizontal.sqrMagnitude > .0001f
                ? Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg : actor.YawDegrees;
            float turn = Mathf.DeltaAngle(actor.YawDegrees, desiredYaw);
            float movement = horizontal.magnitude > ArrivalRadius && Mathf.Abs(turn) < 10f ? 1f : 0f;
            Vector3 before = actor.transform.position;
            if (!actor.StepInput(new Vector2(0, movement), new Vector2(turn, actor.PitchDegrees),
                    false, false, false, DeltaSeconds))
                throw new InvalidOperationException("The actual FPS movement adapter rejected a step.");
            float moved = Vector3.Distance(before, actor.transform.position);
            travelled += moved;
            stalled = moved < .005f && Mathf.Abs(turn) < 10f ? stalled + 1 : 0;
        }
        SessionState.SetInt(Prefix + "leg", leg);
        SessionState.SetInt(Prefix + "stalled", stalled);
        SessionState.SetFloat(Prefix + "dist", travelled);
        string status = leg == points.Count - 1 ? "COMPLETE" : stalled >= 90 ? "BLOCKED" : "WALKING";
        if (status != "WALKING") actor.Pause();
        Report(actor, body, points, leg, travelled, stalled, status);
    }

    static void Report(FirstPersonResponder actor, CharacterController body, List<Vector3> points,
        int leg, float travelled, int stalled, string status)
    {
        Vector3 position = actor.transform.position;
        Vector3 target = points[Mathf.Min(leg + 1, points.Count - 1)];
        var report = new JObject
        {
            ["status"] = status,
            ["completedLegs"] = leg,
            ["totalLegs"] = points.Count - 1,
            ["position"] = new JArray(position.x, position.y, position.z),
            ["target"] = new JArray(target.x, target.y, target.z),
            ["targetDistance3D"] = Vector3.Distance(position, target),
            ["heightError"] = Mathf.Abs(position.y - target.y),
            ["grounded"] = body.isGrounded,
            ["travelledMetres"] = travelled,
            ["stalledSteps"] = stalled,
            ["collisionFlags"] = actor.LastCollisionFlags.ToString(),
            ["movementSource"] = "FirstPersonResponder.StepInput",
            ["videoPoseRegistrationVerified"] = false
        };
        SessionState.SetString(Prefix + "result", report.ToString());
        Debug.Log("ROUTE_STATE " + report.ToString(Newtonsoft.Json.Formatting.None));
    }

    static List<Vector3> ReadWaypoints(string path)
    {
        var document = JObject.Parse(File.ReadAllText(path));
        var result = new List<Vector3>();
        float theta = 16.2f * Mathf.Deg2Rad;
        float sin = Mathf.Sin(theta), cos = Mathf.Cos(theta);
        foreach (JArray point in (JArray)document["waypoints"])
        {
            float u = (float)point[0], v = (float)point[1], y = (float)point[2];
            if (float.IsNaN(u) || float.IsNaN(v) || float.IsNaN(y)
                || float.IsInfinity(u) || float.IsInfinity(v) || float.IsInfinity(y))
                throw new InvalidOperationException("Route coordinates must be finite.");
            result.Add(new Vector3(u * sin + v * cos, y, u * cos - v * sin));
        }
        return result;
    }
}
