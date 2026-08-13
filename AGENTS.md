# How I Fall

- Unity visual novel on C#; prefer minimal, task-scoped changes and no unrelated refactoring.
- Do not change game scenes, prefabs, serialized references, or `.meta` files unless the task requires it; preserve all `.meta` files.
- Add regression coverage for new or changed automatable behavior. Bug fixes should include a test that would catch the original bug where practical.
- Use EditMode NUnit tests for pure C#, state, saves, choices, and variables; PlayMode tests for runtime, UI, and scene lifecycle; keep serialized wiring and project-integrity checks in the existing smoke validators.
- Run targeted tests first, then relevant existing regression/smoke tests before finishing. Never claim a test passed unless it was run.
- Final response: short; changed files, implementation, tests actually executed, and remaining risks.