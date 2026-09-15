#!/usr/bin/env python3
"""Escalate the still-rejected worker role to 9B; retain the unchanged 87-case oracle."""
import argparse
import json
from pathlib import Path
import open_worker as worker

BASE='86bb82bf5655562e2577cf2b83553ff261d8d5a6'
ORACLE='95610c2061b8b06f9dc081525c7e9688f3df7c80c0e2025638c7376b133f5990'
PREVIOUS_MODEL_REPO='TheStageAI/Qwen3.5-4B-GGUF'
MODEL_REPO='TheStageAI/Qwen3.5-9B-GGUF'
DIRECTIVE='''Supervisor escalation after six unsuccessful worker rounds, latest 85/87. Do NOT reuse isinstance(version, int): bool True is an int in Python, while numeric 1.0 is not an int. Required truth table for schema const 1: True->refuse, False->refuse, 1->accept, 1.0->accept, "1"->refuse, 0->refuse, 2->refuse, null->refuse, []->refuse, {}->refuse. Use the declared bool builtin and equality to the trusted schema constant; no float builtin is available or needed. Keep all other original schema-driven checks. Do not weaken or modify tests. This is implementation feedback, not permission or approval. Return complete function source in the single code JSON field.'''

def escalation_packet(packet):
    if packet.get('sourceCommit')!=BASE or packet.get('oracleSha256')!=ORACLE:
        raise ValueError('supervisor_packet_drift')
    return {**packet,'contract':packet['contract']+'\n'+DIRECTIVE,
            'priorWorkerRun':34949923059,'previousModelRepository':PREVIOUS_MODEL_REPO,
            'requestedModelRepository':MODEL_REPO,'escalationReason':'repeated_boolean_numeric_contract_regression',
            'approvalState':'NOT_APPROVED'}

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--server',required=True);ap.add_argument('--output',required=True)
    args=ap.parse_args();args.seed=104729
    original_packet=worker.packet
    worker.packet=lambda:escalation_packet(original_packet())
    worker.MODEL_REPO=MODEL_REPO
    worker.MODEL_MAX=6500000000
    try:return worker.run(args)
    except Exception as e:
        out=Path(args.output)/'public';out.mkdir(parents=True,exist_ok=True)
        (out/'blocked.json').write_text(json.dumps({'state':'BLOCKED','reason':type(e).__name__,'AAAApproved':False})+'\n')
        print('worker escalation blocked: '+type(e).__name__);return 2

if __name__=='__main__':raise SystemExit(main())
