# ADR 0001: Agentic Unity development

Status: accepted; MCP provider updated by the 2026-09-08 PM decision in [ADR 0006](0006-official-unity-editor-mcp.md). Earlier CoplayDev selection is superseded.

## Decision

Use Git as the source of truth, the official Unity CLI Editor MCP (`unity mcp`) with `com.unity.pipeline` as the Unity Editor control plane, idempotent project-scoped Editor builders for repeatable scene work, deterministic tests as merge gates, and physical-headset verification as the final release gate.

## Consequences

- One write agent may control a Unity Editor at a time.
- C#, declarative data, builders, tests, and manifests are preferred over ad-hoc scene mutation.
- MCP timeouts require state inspection before retry.
- Runtime scoring remains rule-based and explainable.
- Raw railway data is excluded from Git, ordinary CI, and external LLM providers.
