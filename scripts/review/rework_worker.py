#!/usr/bin/env python3
"""Resume the failed pilot worker with sealed supervisor criticism and unchanged oracle."""
import argparse
import json
from pathlib import Path
import open_worker as worker

BASE='86bb82bf5655562e2577cf2b83553ff261d8d5a6'
ORACLE='95610c2061b8b06f9dc081525c7e9688f3df7c80c0e2025638c7376b133f5990'
MODEL='f8e45572b9cc35161d4772b09bccfd383fe0bb03fc6d69b40a9138731302290b'
VERDICT='b35b68faa21a581f144c5a1bda520099e96f5ac5fb18da24c02ee17afa20038e'
DIRECTIVE='''Supervisor REWORK. Your previous candidate passed only 13/87 frozen cases. It used float, which is not a declared available builtin. Even with float available, True, string "1", null, list and object versions were incorrectly accepted. Preserve the original function's schema-driven checks. Explicitly reject a boolean record version, and reject any version not equal to the trusted constant. Numeric 1.0 must remain accepted. Remove unavailable builtins and unrelated schema-type branches. Do not add special cases for an expected-schema boolean: the canonical trusted const is numeric 1. Do not weaken the unchanged oracle, grant permissions, or edit any other function. Return only the complete single-function code JSON.'''


def add_feedback(packet):
    if packet.get('sourceCommit')!=BASE or packet.get('oracleSha256')!=ORACLE:
        raise ValueError('supervisor_packet_drift')
    return {**packet,'contract':packet['contract']+'\n'+DIRECTIVE,
            'supervisorVerdictSha256':VERDICT,'priorWorkerRun':34948061722,
            'priorCandidateSha256':'6713140cac46d0dc4f259f1281de27c534c19babafc3a0529d96f01168840570'}


def verify_model(receipt):
    if receipt.get('sha256')!=MODEL:raise ValueError('model_changed_since_blind_review')


def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--server',required=True);ap.add_argument('--output',required=True)
    args=ap.parse_args();args.seed=104729
    original_packet=worker.packet; original_download=worker.model_download
    worker.packet=lambda:add_feedback(original_packet())
    def download(root):
        model,receipt=original_download(root);verify_model(receipt);return model,receipt
    worker.model_download=download
    try:return worker.run(args)
    except Exception as e:
        out=Path(args.output)/'public';out.mkdir(parents=True,exist_ok=True)
        (out/'blocked.json').write_text(json.dumps({'state':'BLOCKED','reason':type(e).__name__,'AAAApproved':False})+'\n')
        print('worker rework blocked: '+type(e).__name__);return 2

if __name__=='__main__':raise SystemExit(main())
