# ADR 0001: Agentic Unity development

Status: accepted

## Decision

Use Git as the source of truth, CoplayDev unity-mcp as the Unity Editor control plane, idempotent project-scoped Editor builders for repeatable scene work, deterministic tests as merge gates, and physical-headset verification as the final release gate.

## Consequences

- One write agent may control a Unity Editor at a time.
- C#, declarative data, builders, tests, and manifests are preferred over ad-hoc scene mutation.
- MCP timeouts require state inspection before retry.
- Runtime scoring remains rule-based and explainable.
- Raw railway data is excluded from Git, ordinary CI, and external LLM providers.
