#!/usr/bin/env python3
"""Create private, local-only rehearsal invitations from a generated public slice layout."""
import argparse
import copy
import hashlib
import json
import os
import secrets
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]


def private_json(path, data):
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def create(layout, output, clients):
    if not 3 <= clients <= 20:
        raise ValueError("Use 3 through 20 clients, including one instructor")
    if output.exists():
        raise ValueError("A session directory already exists; use its invitations or choose a new directory")
    output.mkdir(mode=0o700, parents=True)
    (output / "records").mkdir(mode=0o700)
    world = copy.deepcopy(layout)
    world["ShiftId"] = "shift-" + secrets.token_hex(8)
    world["Participants"] = []
    world["Reports"] = []
    world["Receipts"] = []
    world["Sequence"] = 0
    target = next(e for e in world["Entities"] if e["EntityId"] == "anchor-01")
    position = target["Position"]
    incident = "incident-" + secrets.token_hex(8)
    world["Entities"].append({"EntityId": incident, "RegionId": "hall", "Kind": 2,
                              "Position": {"X": position["X"] + 1.5, "Y": .1, "Z": position["Z"] - 2},
                              "Active": True, "Revision": 0, "RequiredRoleId": "", "LeaderId": ""})
    tickets = []
    for i in range(clients):
        participant = "instructor" if i == 0 else "participant-" + str(i)
        team = "command" if i == 0 else "team-a"
        world["Participants"].append({"ParticipantId": participant, "TeamId": team,
            "RoleId": "instructor" if i == 0 else "role-01", "IsInstructor": i == 0,
            "InputEnabled": False, "RegionId": "hall", "ObservedIds": [],
            "Position": {"X": position["X"] + 1.5, "Y": .1, "Z": position["Z"] + max(0, i - 1)}})
        secret = secrets.token_urlsafe(32)
        tickets.append({"ParticipantId": participant, "SecretSha256": hashlib.sha256(secret.encode()).hexdigest()})
        private_json(output / (participant + ".invitation.json"), {"WorldId": world["WorldId"],
                     "ShiftId": world["ShiftId"], "ParticipantId": participant, "Secret": secret})
    private_json(output / "server-session.json", {"World": world, "Tickets": tickets})
    private_json(output / "participant-1.probe.json", {"ExitAfterSeconds": 12, "Steps": [
        {"AtSeconds": 3, "CommandId": "race-a", "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 0},
        {"AtSeconds": 4, "CommandId": "race-a", "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 0},
        {"AtSeconds": 5, "CommandId": "discover-a", "Kind": 1, "TargetId": incident},
        {"AtSeconds": 6, "CommandId": "report-a", "Kind": 2, "TargetId": incident}]})
    private_json(output / "participant-2.probe.json", {"ExitAfterSeconds": 12, "Steps": [
        {"AtSeconds": 3, "CommandId": "race-b", "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 0},
        {"AtSeconds": 4, "CommandId": "race-b", "Kind": 0, "TargetId": "anchor-01", "ExpectedRevision": 0}]})
    private_json(output / "instructor.probe.json", {"ExitAfterSeconds": 12, "Steps": []})
    return {"participants": clients, "worldId": world["WorldId"], "shiftId": world["ShiftId"],
            "scope": "synthetic local protocol rehearsal; no facility, physics, voice or internet acceptance"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--layout", type=Path, default=PROJECT / "foundation/network/slice-layout.json")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--clients", type=int, default=3)
    args = parser.parse_args()
    print(json.dumps(create(json.loads(args.layout.read_text()), args.output.resolve(), args.clients), indent=2))


if __name__ == "__main__":
    main()
