# Unity agent QA guide

- Read the Unity version from `ProjectSettings/ProjectVersion.txt`; do not rely on memory.
- Standard QA resolution is **1920x1080**. Test other resolutions only when a task explicitly requires it.
- Keep a minimal diff. Treat scenes, prefabs, `.meta`, serialized references and save compatibility as high-risk.
- Add regression coverage for changed behaviour where practical: EditMode for pure logic, PlayMode for runtime/UI lifecycle, and existing smoke/validators for scene/prefab/project integrity.
- Validate targeted-first: compile errors, narrow test, relevant regression/smoke; do not claim unrun checks passed.
- Player-visible work should reuse an existing QA launcher/E2E where possible. Graphical E2E must not use `-nographics`.
- Capture visual proof only after the target runtime state is ready; a screenshot that merely exists is not proof. Inspect it, fix clear task-scoped defects, and rerun proof.
- Automated evidence can technically verify objective criteria. Request human QA only for subjective visual/taste criteria or an actual automation gap; never call Codex inspection a human QA pass.
- Keep logs, results and screenshots in ignored temporary locations unless a task explicitly needs an artifact committed.
- For long multi-stage work, use an existing relevant tracker or spec as durable progress state; do not create status files for ordinary short tasks.
