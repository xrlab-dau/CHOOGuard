#!/usr/bin/env python3
"""Loopback-only adapter: real MLX generation and explicitly local TypeSafe-shaped Choice evaluation.

This is NOT the TypeSafe JEV model/service. It never fabricates generation or usage.
"""
import argparse
import json
import os
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
import time
import urllib.request
import urllib.error

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "output/simulator/model-benchmark"


class Handler(BaseHTTPRequestHandler):
    backend = "http://127.0.0.1:18952/v1/chat/completions"

    def log_message(self, *_):
        pass

    def send_json(self, code, body):
        data = json.dumps(body, separators=(",", ":")).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        self.send_json(200, {"service": "chooguard-local-mlx-bridge", "model": "default_model", "live_jev": False})

    def do_POST(self):
        started = time.perf_counter()
        stage = "generator" if self.path == "/v1/chat/completions" else "local_choice_evaluator"
        record = {"stage": stage, "at_unix": time.time(), "live_jev": False}
        code = 200
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if not 0 < size <= 256_000:
                raise ValueError("Bounded JSON request required")
            body = json.loads(self.rfile.read(size))
            record["request"] = body
            if self.path == "/v1/chat/completions":
                payload = dict(body)
            elif self.path == "/v1/systemone":
                question = body["questions"]["plan"]
                if question["type"] != "choice":
                    raise ValueError("This local contract adapter supports Choice only")
                payload = {"model": "default_model", "temperature": 0, "max_tokens": 40,
                           "messages": [{"role": "system", "content": "Judge a grounded simulator plan. Return only a JSON object with one key choice and a value from the supplied criteria. No Markdown or explanation. Reject unsupported object IDs/actions, dangerous actions, or insufficient information. This is a local small-model judgment, not TypeSafe JEV."},
                                        {"role": "user", "content": json.dumps({"state": body["state"], "instructions": question["instructions"], "criteria": question["criteria"]}, separators=(",", ":"))}]}
            else:
                self.send_json(404, {"error": "unknown_endpoint"})
                return
            # One pinned model, finite generation, no model-path downloads requested by clients.
            payload["model"] = "default_model"
            payload["max_tokens"] = min(512, max(1, int(payload.get("max_tokens", 256))))
            payload["stream"] = False
            data = json.dumps(payload).encode()
            with urllib.request.urlopen(urllib.request.Request(self.backend, data=data, headers={"Content-Type": "application/json"}), timeout=120) as response:
                generated = json.load(response)
            record["backend_response"] = generated
            if self.path == "/v1/chat/completions":
                result = generated
            else:
                raw = generated["choices"][0]["message"]["content"]
                choice = json.loads(raw)["choice"]
                if choice not in question["criteria"]:
                    raise ValueError("Model chose unsupported criterion")
                usage = generated["usage"]
                result = {"model": "local-qwen-choice-contract; not live JEV", "answers": {"plan": {"choice": choice}},
                          "usage": {"input_tokens": usage["prompt_tokens"], "output_tokens": usage["completion_tokens"]}, "local_generated_text": raw}
            record["response"] = result
        except Exception as error:
            code = 422
            record["error"] = type(error).__name__ + ":" + str(error)
            result = {"error": record["error"], "live_jev": False}
        record["http"] = code
        record["latency_ms"] = (time.perf_counter() - started) * 1000
        OUT.mkdir(parents=True, exist_ok=True)
        with (OUT / "service-calls.jsonl").open("a") as stream:
            stream.write(json.dumps(record, separators=(",", ":")) + "\n")
        self.send_json(code, result)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=18951)
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "bridge-process.json").write_text(json.dumps({"pid": os.getpid(), "host": "127.0.0.1", "port": args.port,
        "stop_command": "kill -TERM " + str(os.getpid())}, indent=2) + "\n")
    print("chooguard_local_bridge_ready 127.0.0.1:" + str(args.port), flush=True)
    HTTPServer(("127.0.0.1", args.port), Handler).serve_forever()
