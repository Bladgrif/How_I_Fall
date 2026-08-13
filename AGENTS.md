# How I Fall

- Unity visual novel on C#; prefer minimal, task-scoped changes and no unrelated refactoring.
- Read only the files/systems relevant to the task unless a dependency requires expanding scope.
- Do not change game scenes, prefabs, serialized references, Save format, ProjectSettings, or `.meta` files unless the task requires it. Preserve existing working APIs and user changes.
- Add regression coverage for new or changed automatable behavior. Bug fixes should include a test that would catch the original bug where practical.
- Use EditMode NUnit tests for pure C#, GameState, saves, choices, variables and other non-runtime logic.
- Use PlayMode tests for runtime/UI/scene lifecycle when EditMode cannot reliably cover the behavior.
- Keep serialized wiring, prefab integrity, scene integrity and project-level checks in existing smoke tests/validators.
- Run the narrow targeted test first, then relevant regression/smoke tests. Do not run the full heavy suite after every trivial change unless risk justifies it.
- Check compilation errors after implementation.
- Never claim a test passed unless it was actually run. Explicitly mark unexecuted validation as NOT RUN.

## Manual UI QA

For any task that changes player-visible UI or a user interaction flow:

- Create or update a convenient Unity Editor launcher under:
  `How I Fall/QA/<Feature Name>`
- The launcher should bring the game as close as practical to the exact state that needs human verification with one action.
- Reuse an existing suitable QA launcher instead of creating duplicates for minor changes.
- A QA launcher does not replace automated tests.
- Final response must provide the exact QA menu path and a short list of what the human should verify, including relevant resolutions/states when needed.
- Never claim manual graphical QA PASS unless a human explicitly performed and confirmed it.

Pure logic changes do not require a UI QA launcher.

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
