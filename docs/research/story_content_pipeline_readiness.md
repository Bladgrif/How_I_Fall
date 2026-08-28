# Story Content Pipeline Readiness

## Context

The functional demo is the current priority; real story, canonical routes and final art
remain deferred. The dialogue runtime is already working and this audit does not
authorize changes to it, save data, technical fixtures or Unity scenes.

There is no `docs/story/` directory yet. Existing dialogue assets are primarily
technical/demo content, including explicit `TECH DEMO ONLY / NOT CANON` builders.
They should remain separate from future authored story material.

## Current Runtime Model

`DialogueSceneData` is the runtime input:

- a registry resolves a `sceneId` to a `DialogueSceneData` asset;
- each scene has `displayName`, optional scene-level `backgroundMusic`,
  `stopMusicOnStart`, ordered `lines`, ordered `choices`, and an optional
  `defaultNextScene`;
- a line has a required `lineId`, optional background/character `Sprite`
  references, position/hide state, speaker and text;
- a choice has text, optional typed numeric conditions, result text, the nine
  existing stat deltas and an optional direct `DialogueSceneData` target.

The runtime already resolves a saved scene by `sceneId` and a saved position by
`lineId`, with line index only as a legacy fallback. Read history also keys a
line by `(sceneId, lineId)`. A selected choice is currently persisted as its
**source-list index**, with result state and pending target scene ID. Therefore
the existing asset shape is a suitable thin runtime target, but authored line
and scene IDs must stay stable and choice ordering needs care.

## Current Validation

`DialogueContentValidator` already reports errors for:

- missing registry or empty registry;
- missing, duplicated or empty registry scene IDs;
- missing scene references and duplicate scene registration;
- missing/empty lines, missing line IDs and duplicate line IDs within a scene;
- missing choice list, null/empty choice data, and source-choice count above the
  current UI capacity;
- malformed typed numeric choice conditions;
- transitions to scenes outside the registry.

It warns, rather than fails, for an empty choice result, registered scenes
unreachable from the first registry scene, and conditional choices without a
default transition. It cannot prove that a runtime condition will ever be true.

## Authoring Pain Points

Unity Inspector authoring works for the small technical fixtures, but it is not
a comfortable canonical source for long prose and branching review. Serialized
YAML mixes text with Unity object references, makes bulk editorial work awkward,
and can create avoidable merge conflicts or accidental Inspector edits.

The runtime currently uses direct Unity object references for visual/audio assets
and branch targets. That is appropriate at runtime, but not a good primary
writing surface.

## Options Compared

| Option | Benefits | Cost / risk for HIF | Verdict |
|---|---|---|---|
| A. Inspector-only ScriptableObjects | No tooling; direct fit with current runtime. | Poor long-form editing/review, noisy serialized diffs, fragile bulk edits and merges. | Reject as canonical authoring workflow. |
| B. Markdown source, manual conversion | Excellent writing/diff/branch review; zero runtime change now. | Duplicate conversion effort and human reference/ID errors once content grows. | Best immediate step. |
| C. Markdown with small deterministic importer | Keeps Markdown canonical while generating direct runtime assets; repeatable bulk updates and reference resolution. | Requires agreeing real syntax, asset naming and branch/condition needs first; premature parser risks churn. | Build later, before integrating the first substantial real scene. |
| D. Markdown -> JSON -> Unity | JSON can be machine-friendly. | Adds a second schema, files and validation boundary without solving a current runtime problem. | Do not add now. |
| E. New narrative framework/migration | Could provide authoring tools. | Replaces a working, tested runtime and save-facing contracts; disproportionate for a small project. | Reject. |

## Recommended Direction

Use Markdown as the future canonical story source, but **do not build an importer
yet**. Start with the first real story material in Markdown and make its
conversion into the existing `DialogueSceneData` assets an explicit, small
integration task. At that gate, build one deterministic Markdown-to-assets
importer only if the material has more than a trivial one-off scene or manual
conversion has already become an error-prone review burden.

This preserves the ready demo, lets writing and route review happen outside
Unity, and avoids freezing a parser around hypothetical flags, assets or story
conventions. It is a thin adapter plan, not a narrative-framework migration.

## Proposed Markdown Shape

This is an illustrative format only; it is **TECH DEMO ONLY / NOT CANON** and
does not define production syntax yet.

```markdown
---
kind: hif-dialogue-scene
scene_id: test_corridor_arrival
display_name: "TEST: Corridor Arrival"
music: Assets/HowIFall/Art/Audio/TEST_theme.ogg
stop_music_on_start: false
---

## Lines

- id: test_corridor_opening
  background: Assets/HowIFall/Art/Backgrounds/TEST_corridor.png
  character: Assets/HowIFall/Art/Characters/TEST_speaker_neutral.png
  position: left
  speaker: "TEST Speaker"
  text: |
    TEST text for authoring-format review only.

- id: test_corridor_reply
  speaker: "TEST Speaker"
  text: "TEST reply."

## Choices

- text: "TEST: proceed carefully"
  result: "TEST result."
  when:
    - self_control >= 2
  effects:
    self_control: +1
  next_scene: test_corridor_followup

- text: "TEST: leave"
  result: "TEST result."
  next_scene: test_corridor_exit
```

The eventual compiler should accept only explicitly documented keys and the
already supported typed condition/state names; it must not evaluate arbitrary
expressions. Character/background fields remain line-level because that is how
the current runtime applies them. Scene-level music maps directly to the
existing scene field.

## Stable ID Policy

- `sceneId` is a permanent, lower-case ASCII slug chosen when a scene is first
  introduced. It is an identity, not a title or filename. Editing display text
  or `displayName` must not change it.
- `lineId` is a permanent, scene-local, lower-case ASCII slug chosen when the
  beat is first introduced. It is never derived from prose or list position.
- Editing a line's prose keeps its `lineId`. Inserted lines receive new IDs;
  existing IDs and their relative order remain unchanged.
- Removed lines and renamed/moved scenes are save-affecting changes. Keep
  stable IDs when moving a branch; do not silently reuse removed IDs. A scene
  title/path may change, but its `sceneId` must not.
- Current saves retain a choice by source-list index, not a choice ID. Until a
  future save-compatible design explicitly changes that contract, do not
  reorder, insert before, or remove choices from released scenes. Append only
  when preserving old saves matters. Branch movement should preserve the
  selected choice's source index and target, or be treated as a deliberate
  compatibility break with tested migration policy.

A future importer may check IDs against its previous generated manifest/asset
state and fail a suspicious implicit rename; it must never auto-regenerate IDs
from text or order.

## Asset Reference Policy

Use project-relative Unity asset paths in Markdown, such as
`Assets/HowIFall/Art/Backgrounds/...`, and resolve them during import to the
existing direct `Sprite`/`AudioClip` references.

This is the simplest safe choice for a small Unity project: paths are readable
in review and `AssetDatabase` can validate type and existence. Unity GUIDs are
stable but opaque to writers; symbolic IDs need a separate catalogue and add
an abstraction that does not exist yet. Direct object references belong only in
the generated `.asset` files, not in Markdown.

Asset moves must update the corresponding Markdown in the same reviewed change;
the future importer should fail on a missing path or wrong asset type.

## Validation Boundary

Before or during a future import, validate only the authoring errors that
matter beyond the existing runtime validator:

1. malformed Markdown/front matter or unknown keys;
2. duplicate/missing scene or line IDs across the authored set;
3. unknown condition/state/operator names and unsupported effect keys;
4. missing/wrong-type asset paths;
5. `next_scene` IDs that do not resolve within the authored/imported set;
6. a changed generated ID or choice order that would silently invalidate
   existing save positions.

After import, run the existing `DialogueContentValidator` for the final Unity
registry, references, IDs, capacity, reachability and conditional-choice
warnings. Conditional reachability and “all choices unavailable” remain runtime
properties; the current default-transition warning is the appropriate limited
static safeguard.

## Generated Asset Policy

If the later gate approves an importer:

- authored files live under `docs/story/`; generated dialogue assets live in a
  reserved subfolder such as `Assets/HowIFall/Data/Dialogues/StoryGenerated/`;
- generated `.asset` **and `.meta`** files are committed. Their stable paths
  preserve Unity GUIDs and keep diffs/review reproducible;
- each source scene maps deterministically to one stable generated asset path;
  rerunning the importer updates only that scene, its necessary registry entry
  and directly referenced generated targets;
- removal is explicit: report orphaned generated assets and require a dedicated
  confirm/remove action, never delete them during an ordinary import;
- do not write outside the reserved generated folder or modify technical/manual
  dialogue assets. A generated marker/manifest may be added only with the
  importer implementation if needed to distinguish ownership;
- the importer resolves Markdown paths to direct Unity references and updates
  the registry deterministically, without recreating unrelated assets.

## First Real Content Workflow

1. Author and review a small real scene set in `docs/story/`.
2. Assign stable scene/line IDs and keep a short route/asset review alongside
   the Markdown.
3. Decide whether the set is still small enough for a reviewed manual
   conversion; if not, implement the narrow deterministic importer above.
4. Convert/import to `DialogueSceneData` assets, then run
   `DialogueContentValidator`.
5. Add focused runtime/save-position coverage for the introduced content
   behavior, then run the relevant dialogue smoke.
6. Run graphical/content QA only for player-visible scenes and inspect its
   screenshots.

## What We Should Not Build Yet

- an importer, JSON intermediate schema, generic narrative framework or
  Ink/Yarn migration;
- a string-key flag system, arbitrary expressions, callbacks or scripting;
- an asset-ID catalogue or a global content database;
- SaveData/GameState/runtime redesign, ID migration or choice-ID persistence;
- canonical story text, routes, characters, art or lore.

## Decision

**OPTION 2 — Use Markdown source-of-truth but delay tooling until the first real
story scene.**

Markdown best serves writing, review and Git history immediately. The existing
`DialogueSceneData` model already accepts the required runtime data, while a
Markdown importer is valuable only after real material fixes the grammar and
reveals that manual conversion is a recurring cost. Building it now would add
speculative format and maintenance work to a functional demo without a concrete
content blocker.
