# VN Development Playbook

## Purpose

Durable synthesis of practical visual-novel development guidance, player UI sentiment and current How I Fall decisions.

This document is **research guidance**, not automatic authorization to implement features. Current repository/product docs remain authoritative for HIF state. External sources are patterns and evidence, never a template to copy one-to-one.

Current phase: **Polished Functional Demo First**. Story routes, canonical flags, final art and final visual identity remain deferred until explicitly reopened.

## Evidence classes used here

- **A — HIF repository/product truth.** Current master, `docs/product/*`, `docs/research/*`, `docs/eternum_feature_tracker.md`, `AGENTS.md`.
- **B/C — practitioner/developer guidance.** Useful first-person production experience, but not a universal VN standard.
- **D — player/community sentiment.** Useful for friction, expectations and discoverability; never sole authority for architecture.

## 1. Production principles

### A visual novel is not automatically a small/cheap project

A developer postmortem can begin with a three-month solo expectation and quickly expand into art, animation, testing, localization, marketing and sound work. HIF should therefore keep bounded passes, explicit scope and a feature-creep guard even though the core genre looks technically simple.

**HIF rule:** optimize for a polished small product slice, not maximum feature count.

### Define what the project is trying to prove

Before expensive production, know the product goal, target audience, constraints and what success means. HIF currently has a concrete goal: **Polished Functional Demo First**. Do not silently turn that into final-story production, final art production or a generic VN framework.

### Every mechanic needs a reason to exist

Before adding a system, ask:

1. Does the target audience actually benefit from it?
2. Can the current team/project scope support it safely?
3. Is it necessary for this HIF experience?
4. Does it fit the rest of the product rather than competing with it?

For a VN, a bespoke mechanic should normally support narrative, atmosphere, pacing or meaningful player choice. Unity being capable of a mechanic is not sufficient justification.

**HIF implication:** keep generic minigames, generic QTEs, large relationship dashboards and speculative systems deferred. Build task-specific interaction only when real content needs it.

### Planning should expose cost, not create bureaucracy

A useful lightweight chain is:

`vision/product goal -> prioritized feature list -> roadmap -> content plan -> bounded implementation passes`

HIF already has the needed equivalents in repository docs, feature tracker and Drive roadmap. Do not add another project-management framework merely to mirror external terminology.

## 2. Story/content workflow — LATER, when story is explicitly reopened

Do not start this workflow during the current no-canon demo phase.

Recommended order:

1. Write the broad story skeleton before implementation-heavy content work.
2. Identify only story-relevant characters, locations and major branch points.
3. Maintain concise character cards for consistency: internal/external persona, motivation, loyalties, relevant backstory, speech traits and only visually important details.
4. Expand broad blocks into chapter/scene nodes.
5. For each node record at minimum: location, core dramatic purpose, important links/branches and whether any authored interaction is actually required.
6. Write/play/read the material repeatedly and revise how it feels in motion, not only how it reads as prose.
7. Only then author story-dependent mechanics, autosave checkpoints, flowcharts, replay entries or lore UI.

**HIF storage rule:** story work starts in `docs/story` / Markdown, not by editing scenes, prefabs or C#.

Do not create a giant world encyclopedia. Document what this game needs to stay internally consistent.

## 3. Narrative design principle

Narrative is larger than dialogue text. It can be communicated by:

- choices and mechanics;
- environment and spatial staging;
- character visual presentation;
- music and sound;
- transitions/camera/cinematic direction;
- diegetic surfaces such as phone/chat;
- authored hotspots, map use or timed beats.

This strongly matches HIF's Unity strategy: use Unity later for **specific cinematic, spatial or interactive narrative moments**, not for generic system proliferation.

Existing HIF foundations such as Chat/Phone, Map, Interactive Hotspots and Timed Narrative Beat remain foundations until real content gives them a concrete job.

## 4. Player UI sentiment — reusable patterns

The Reddit UI discussions are community evidence, not a statistical survey, but several themes repeat and align with mature VN conventions.

### Readability first

Players explicitly value a readable text window/surface and dislike text laid directly over imagery when contrast is unreliable.

**HIF:** already adopted. Preserve the neutral dark readable dialogue surface and 125% text readability. UI should not compete with character/background art.

### High-frequency controls should be easy to reach but visually quiet

Players praise convenient Skip/Auto/History/save actions, but also praise interfaces that can hide or collapse most chrome. Summer Pockets is repeatedly cited for combining a full control set with a much more minimal reading mode.

**HIF:** current compact strip is a good direction:

`History | Skip | Auto | Quick Save`

Ordinary navigation remains in `Esc -> Game Menu`. Do not re-add persistent controls merely to look feature-rich.

### History is recovery; rollback is stronger recovery

Players value very easy access to text history. Several comments separately praise returning to previous text/scenes, including actual rollback rather than merely opening a log.

**HIF:** Backlog/History is already DONE and should stay easy to reach. Do not pretend History equals rollback.

**Current rollback status:** **REOPENED FOR FEASIBILITY**, not implemented. Repeated player sentiment strengthens its product value, but implementation still requires state-safe restoration. Prefer the existing planned checkpoint/barrier feasibility model; never add a visual-only previous-line button that leaves GameState/choices out of sync.

If an older research line says rollback is simply `NOT PLANNED`, treat that as superseded by the current capability map/roadmap and this 2026-08-31 decision.

### Hover/focus/state must be coherent

A control should visibly react to interaction, while mouse hover and keyboard/controller focus must not leave unrelated elements looking selected.

**HIF:** already adopted and regression-covered. Preserve one coherent visual hierarchy and safe modal defaults.

### Rich settings can be valuable, but settings count is not the goal

Community examples praise granular text/auto speed, window opacity, fonts, voice controls and behavior settings. Hoshizora no Memoira is cited as an example of extensive configurability.

For HIF, only expose options that have real runtime meaning and justify their complexity. Semantic control type and clear persistence matter more than matching another VN's settings count.

### Avoid distracting permanent chrome

Community criticism repeatedly targets clutter, oversized permanent icons and over-animated advance indicators. Minimal or auto-hiding interfaces are often praised.

**HIF:** art-first composition and restrained chrome are preferred. Advance affordance should be quiet. Do not add a large animated logo/cursor merely as decoration.

### Do not move the player's mouse cursor automatically

Community sentiment includes explicit dislike of interfaces that warp/move the cursor to menu items.

**HIF:** do not introduce cursor warping. EventSystem focus may change internally, but mouse position remains player-controlled.

## 5. HIF decisions after this research pass

### ADOPT NOW / PRESERVE

- Readable dialogue surface over arbitrary demo backgrounds.
- Compact high-frequency Quick Menu; ordinary navigation stays in Esc/Game Menu.
- Easy History access.
- Seen-aware Skip safety.
- Coherent mouse/keyboard/controller focus and hover presentation.
- Clear Save/Load family semantics; Manual Save remains Manual-only.
- Minimal task-scoped implementation and feature-creep guard.
- Rollback/Rewind feasibility remains a near-term research priority because both user demand and player sentiment support the recovery value.

### STORY / CONTENT LATER

- Story skeleton -> chapter/scene graph -> authored branch semantics.
- Concise character dossiers and dialogue-voice consistency.
- Content-informed autosave checkpoints.
- Flowchart/Story Chart, chapter select, ending/route completion.
- Glossary/Tips/Files tied to actual lore.
- Voice-driven features when final voice content exists.
- Bespoke investigation, map, hotspot, phone/chat or timed interactions only when a real scene needs them.
- Cinematic/spatial Unity presentation for concrete authored scenes.

### CANDIDATES — NOT BACKLOG ITEMS YET

These are worth remembering, not implementing automatically:

- voice replay from History/current line;
- optional shortcut/key hints or tooltips for discoverability;
- optional minimal/auto-hide reading chrome if future hands-on shows value beyond current Hide UI + compact strip;
- skip-to-next-choice / skip-to-next-scene variants for replay-heavy real content;
- per-character voice controls after real voiced characters exist;
- timeline/bookmark-style navigation only after real story topology makes it meaningful.

Each candidate requires a separate product need and current-HIF comparison before implementation.

### DEFER / REJECT NOW

- Generic minigame framework.
- Generic QTE framework.
- Visible relationship/reputation meter as the default HIF feedback model.
- Large permanent icon strips or decorative HUD clutter.
- Forced mouse-cursor movement.
- Giant settings expansion without demonstrated player need.
- Branch timeline/flowchart before real story exists.
- Any feature copied because one praised VN has it.

## 6. Strong hands-on benchmark candidates

If web screenshots/manuals are not enough and a real installed-game audit becomes worthwhile, prioritize a **small** set:

1. **Summer Pockets** — full vs minimal reading chrome, History/voice replay, save access, Esc/options split.
2. **Modern Yuzusoft title (for example Senren Banka-era or later)** — mature QoL, skip variants, timeline/bookmark ideas, settings density.
3. **Hoshizora no Memoira** — configurability and text-window preferences.
4. **Katawa Shoujo** — rollback/history recovery interaction.
5. **9-nine** — Save/Load presentation, only if additional Save/Load research is needed.

Do not install/audit all of them by default. Choose the title that answers the current product question.

For a hands-on audit, inspect the concrete flow, not just screenshots:

`Main Menu -> reading -> History -> Auto/Skip -> Choice -> Save/Load -> Preferences -> Back/Esc -> rollback/recovery if present`

Record clicks/keys, focus behavior, visibility, screenshots and what problem each pattern solves. Do not copy art/assets/layout one-to-one.

## Sources

### Practitioner/developer guidance

- Konstantin Sakhnov / Kallist, Habr profile: https://habr.com/ru/users/Kallist/
- Visual novel guide, Part 1 — preparation: https://habr.com/ru/companies/miip/articles/824424/
- Part 2 — scenario writing: https://habr.com/ru/companies/miip/articles/838680/
- Part 3 — game design: https://habr.com/ru/companies/miip/articles/840926/
- Narrative design overview: https://habr.com/ru/articles/740746/
- Planning / vision / roadmap overview: https://habr.com/ru/articles/734978/

Treat personal production/market/style opinions as practitioner evidence, not universal law.

### Player/community UI sentiment

- Best VN menu systems / user interfaces: https://www.reddit.com/r/visualnovels/comments/s7x16/best_vn_menu_systemsuser_interfaces/
- Which VNs have amazing / hated UI: https://www.reddit.com/r/visualnovels/comments/ual80h/which_vns_have_an_amazing_ui_and_which_have_ui/

### Strong primary cross-check

- Summer Pockets official operation manual: https://key.visualarts.gr.jp/summer/manual/index.html
  - Documents Main Menu, Save/Load, Q.Save/Q.Load, Back, Auto, seen-aware Skip, voice replay, Log, lockable menu and Preferences behavior.

## Decision

**APPROVED AS REUSABLE RESEARCH GUIDANCE.**

This pass validates the current HIF direction and strengthens the priority of a bounded Rollback/Rewind feasibility contract. It does **not** add new implementation work before the ordered roadmap unless a future product decision explicitly promotes one of the candidates.