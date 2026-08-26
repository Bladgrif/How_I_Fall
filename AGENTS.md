# How I Fall

- Unity visual novel on C#; prefer minimal, task-scoped changes and no unrelated refactoring.
- Read only the files/systems relevant to the task unless a dependency requires expanding scope.
- Do not change game scenes, prefabs, serialized references, Save format, ProjectSettings, or `.meta` files unless the task requires it. Preserve existing working APIs and user changes.
- Any new or changed game behaviour needs regression coverage when it is reasonably automatable. Use EditMode NUnit for pure logic, PlayMode NUnit for runtime/UI lifecycle, and existing smoke/validators for scene, prefab, serialized and project integrity. A bug fix should catch the original bug where practical; docs-only changes normally need no test.
- Prefer ordinary Unity Test Framework NUnit tests. Use custom graphical/runtime E2E only for real runtime flows, Game View, screenshots, or Editor lifecycle.
- After implementation: check compile errors, run a narrow targeted test, then relevant regression/smoke checks. For player-visible work, run runtime/E2E proof when the environment supports it, inspect available visual proof, fix clear task-scoped defects, and repeat the relevant check.
- Compile success is not proof of correct behaviour. Never claim a test passed unless it was actually run; mark unexecuted validation `NOT RUN`.

## Visual and manual QA

- Standard QA resolution is **1920x1080**. Do not add multi-resolution automation unless a task explicitly requires responsive/resolution compatibility.
- Reuse an existing convenient QA launcher under `How I Fall/QA/<Feature Name>` for player-visible work; do not duplicate one for minor changes. A launcher does not replace automated tests.
- Graphical/runtime E2E and screenshots must not use `-nographics`. Codex inspection is automated evidence, not a human Manual QA PASS.
- Request human manual QA only when a subjective visual/taste/atmosphere decision remains, visual proof is unavailable, or an important scenario has a real automation gap. Objective acceptance criteria can be technically verified by automated coverage plus runtime proof.

## Git / completion

- Do not stage unrelated user changes and do not use `git add .` when the worktree contains unrelated modifications.
- Do not destructively reset user changes.
- Do not commit or push unless the task explicitly requests it.
- A technical task is not considered fully verified after push until the relevant GitHub CI checks pass:
  `Unity Test Framework` and `Unity smoke tests`.

## Final response

Keep it short:
1. changed files;
2. implementation;
3. tests actually executed + results;
4. NOT RUN checks;
5. manual QA path/checklist when applicable;
6. remaining risks.