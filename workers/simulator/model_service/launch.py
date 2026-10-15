#!/usr/bin/env python3
"""Run the pinned task-local MLX backend; terminate this process to stop it."""
import os
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "output/simulator/model-benchmark"
os.environ["HF_HOME"] = str(OUT / "hf")
os.environ["HF_HUB_CACHE"] = str(OUT / "hf/hub")
os.environ["HF_HUB_DISABLE_IMPLICIT_TOKEN"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
model = OUT / "models/Qwen2.5-1.5B-Instruct-4bit"
if not (OUT / "model-receipt.json").exists():
    raise SystemExit("Run prepare.py and verify pinned model receipt first")
parser = argparse.ArgumentParser()
parser.add_argument("--device", choices=["cpu", "gpu"], default="cpu")
args = parser.parse_args()
# CPU is an explicit task-local fallback for a failed macOS MTLCompilerService connection.
# Set it before importing mlx_lm so model loading and its generation streams agree.
import mlx.core as mx
mx.set_default_device(mx.cpu if args.device == "cpu" else mx.gpu)
(OUT / "server-process.json").write_text(json.dumps({"pid": os.getpid(), "device": args.device,
    "host": "127.0.0.1", "port": 18952, "max_concurrency": 1, "python": sys.version,
    "stop_command": "kill -TERM " + str(os.getpid())}, indent=2) + "\n")
sys.argv = ["mlx_lm.server", "--model", str(model), "--host", "127.0.0.1", "--port", "18952", "--decode-concurrency", "1", "--prompt-concurrency", "1", "--max-tokens", "256", "--prompt-cache-size", "0", "--prefill-step-size", "256", "--log-level", "INFO"]
import mlx_lm.server as server
if args.device == "cpu":
    original_load = server.load
    def load_float32(*values, **options):
        model, tokenizer = original_load(*values, **options)
        # M1 has no native bfloat16 instructions. The int4 artifact remains immutable on disk.
        # Dequantize once in RAM so CPU matrix multiplication can use Accelerate/BLAS.
        model.set_dtype(mx.float32)
        from mlx_lm.utils import dequantize_model
        model = dequantize_model(model)
        mx.eval(model.parameters())
        return model, tokenizer
    server.load = load_float32
server.main()
