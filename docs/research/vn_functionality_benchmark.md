# VN Functionality Benchmark

## Question

Какие player-facing функциональные patterns из сильных визуальных новелл полезно учитывать для How I Fall в текущей фазе **Polished Functional Demo First**, до подключения реального сюжета?

## Context

Сюжет, routes, canonical story flags и финальный art сейчас отложены. Цель исследования — не проектировать будущий канон, а проверить quality bar функционала и UX.

Текущий HIF уже имеет большую часть базовой VN-инфраструктуры: Manual/Auto/Quick saves, Continue, Quick Menu, Game Menu, Auto, seen-aware Skip, Backlog, Shared Preferences, unified input, Hide UI, Character Hub foundation, Gallery/Replay foundation, Chat/Phone, Interactive Hotspots, Map Locations и Timed Narrative Beat. Источник текущего статуса: `docs/eternum_feature_tracker.md`.

Research не разрешает implementation автоматически. Новая механика начинается только после отдельного решения.

## Benchmark set

Используем не как рейтинг и не как источник для копирования, а как сильный набор функциональных benchmark/reference игр:

- **STEINS;GATE / STEINS;GATE 0** — mature VN navigation, read/unread skip semantics, glossary/tips-style information access.
- **The House in Fata Morgana** — strong conventional VN controls: quick save, backlog, skip, auto; также chapter-select/replay patterns после прохождения.
- **Zero Escape: Virtue's Last Reward / The Nonary Games** — story flowchart как часть player navigation между ветками и узлами.
- **AI: THE SOMNIUM FILES** — flowchart, jump к story checkpoints/chapters, Files/character information, manual save поверх autosave.
- **PARANORMASIGHT** — Story Chart, autosave at important junctures, manual save/load, automatic resume from the latest save, mouse/keyboard/gamepad-friendly navigation.
- **Muv-Luv / Muv-Luv Alternative** — conventional VN QoL и suspend/resume pattern при закрытии игры.
- **Doki Doki Literature Club!** — простой и предсказуемый baseline для Menu/Preferences/keyboard navigation.

## References

- STEINS;GATE skip behavior discussion: https://steamcommunity.com/app/825630/discussions/0/1697175413680966429/
  - Default skip targets already-read text; unread skipping can be enabled separately; useful benchmark for safe skip semantics.
- The House in Fata Morgana gameplay/controls: https://strategywiki.org/wiki/The_House_in_Fata_Morgana/Gameplay
  - Documents Skip, Auto-play, log/backlog and menu access.
- The House in Fata Morgana autosave/choice recovery discussion: https://steamcommunity.com/app/303310/discussions/0/1760230157503555414/
  - Useful evidence that autosave can protect players from costly branch mistakes.
- Zero Escape: Virtue's Last Reward controls: https://strategywiki.org/wiki/Zero_Escape%3A_Virtue%27s_Last_Reward/Controls
  - Flowchart is a first-class menu/navigation action.
- AI: THE SOMNIUM FILES FILE/Flowchart documentation: https://somniumfiles.fandom.com/wiki/FILE/nirvanA_Initiative
  - Flowchart shows branching points and allows scene/chapter jumps; FILE exposes character information.
- PARANORMASIGHT Story Chart official media material: https://www.square-enix.com/asia/newsportal/en/topics/paranormasight-tmc/post03.html
  - Story Chart unlocks with progress and is used to navigate intersecting story perspectives.
- PARANORMASIGHT controls / save / Story Chart reference: https://paranormasight.fandom.com/wiki/How_to_Play
  - Documents autosave at important junctures, manual save/load, resume from last save, Story Chart and fast-forward behavior.
- Muv-Luv Alternative suspend behavior discussion: https://steamcommunity.com/app/449840/discussions/0/2549465882920335494/
  - Closing directly can create a suspended-game save and resume from it on next launch.
- DDLC README: https://doki-doki-literature.club/README.html
  - Documents predictable menu navigation, Preferences, text speed, skip-unseen, skip-after-choice, auto-forward and audio controls.

## Functional comparison

| Pattern | Benchmark value | HIF status | Recommendation |
|---|---|---|---|
| Manual / Auto / Quick saves | Core VN safety/QoL | DONE | Preserve and polish; no redesign without need. |
| Continue newest valid save | Strong convenience | DONE | Keep. HIF already improves on some references here. |
| Auto | Core reading QoL | DONE | Only tune/QA with real content later. |
| Seen-aware Skip + optional unseen skip | Prevents accidental story loss while supporting replay | DONE | Keep current semantics; do not rebuild. |
| Backlog / History | Core recovery/readability | DONE, save-scoped restore | Keep; protect v3 compatibility. |
| Quick menu | Fast access to reading controls | DONE | Polish only. |
| Shared Preferences | Predictable settings entry from multiple contexts | DONE | Keep one shared implementation. |
| Keyboard/gamepad parity | Reduces friction and accessibility issues | Foundation exists | **NOW candidate:** full end-to-end controller/input UX audit. |
| Autosave checkpoint around important choices | Reduces fear of irreversible branch mistakes | Autosave exists, authored points are content-dependent | **LATER candidate:** define policy once real choices exist; avoid speculative route logic now. |
| Suspend/resume on app exit | Strong low-friction return UX | Continue exists, but no explicit transient session-resume contract | **NOW/LATER research candidate:** audit whether value justifies Save-system risk before implementation. |
| Story Flowchart / Story Chart | Excellent for mystery/branch-heavy VN; enables route navigation and replay from meaningful points | Not implemented | **DEFER UNTIL STORY.** Requires the real story graph and unlock semantics. |
| Chapter / scene replay | Reduces replay friction | Gallery/Replay technical foundation exists | **DEFER UNTIL STORY.** Author real replay entries only from actual content. |
| Glossary / Tips / Files | Helps dense mystery terminology and character recall | Character Hub foundation exists; no general glossary | **DEFER UNTIL CONTENT.** Do not invent terms/characters now. |
| Ending list / route completion map | Useful completionist feedback | No canonical ending model | **DEFER UNTIL STORY.** |
| Rollback | Powerful VN convenience but high state-complexity | NOT PLANNED | Keep NOT PLANNED unless a concrete product need justifies reversible execution state. |
| Minigame framework | Only valuable when story/gameplay demands it | NOT PLANNED | Keep NOT PLANNED. Build task-specific interaction only when required. |

## NOW — useful before story

### 1. Full controller / input UX audit

Recommended next functionality-quality pass after current Main Menu work.

Check one continuous player journey with keyboard and gamepad:

`Main Menu → New Game → dialogue → Quick Menu → Backlog → Game Menu → Save/Load → Preferences → return → Quit/Back`

Acceptance focus:

- deterministic default focus;
- no focus traps;
- Esc/Back semantics consistent;
- every important control reachable without mouse;
- disabled actions skipped or explained correctly;
- dropdown/sliders usable with keyboard/gamepad;
- modal focus restores predictably.

This does not depend on story content and directly improves demo quality.

### 2. Suspend/resume feasibility audit

Do **not** implement immediately. First compare against current `Continue` + SaveData v3.

Questions:

- Does an explicit session-resume save provide real value beyond Continue?
- Can it be implemented without changing the stable SaveData v3 contract?
- What happens during modal/special modes, Replay, Chat, Map or Timed Beat?
- Would it introduce more recovery risk than UX value?

Preferred outcome may legitimately be **REJECTED** if Continue already solves the user problem well enough.

## LATER — when real content exists

### Choice-safety autosave policy

Benchmarks such as Fata Morgana and PARANORMASIGHT show the value of saving around important junctures. HIF already has autosave infrastructure, so the future question is policy, not a new save system.

When actual choices/routes exist, decide:

- autosave immediately before a major branch vs after stable scene entry;
- whether every choice deserves a checkpoint or only high-impact decisions;
- interaction with cyclic Auto slots;
- spoiler-safe slot labels/screenshots.

Do not author this policy around TECH DEMO choices.

## DEFER UNTIL STORY

These are strong patterns but premature now:

- interactive route/flowchart;
- chapter select;
- canonical scene replay list;
- glossary/tips tied to real lore;
- ending list / route completion UI;
- route-specific progress indicators.

Reason: all depend on the real narrative graph, terminology, unlock conditions or content structure. Building them now would create architecture around invented canon.

## What we should NOT copy

- copyrighted code, art, text or unique layouts;
- a feature merely because a famous VN has it;
- route/navigation architecture before the HIF story graph exists;
- a large generic framework for hypothetical future content;
- rollback/minigame systems without a concrete product requirement.

## Recommendation for HIF

Current HIF functional coverage is already broad. The highest-value pre-story work is **quality and integration**, not feature count.

Recommended order after the current Main Menu visual work:

1. **Controller/input player-journey audit + regression/graphical coverage.**
2. **Suspend/resume feasibility audit** — implement only if it clearly beats existing Continue without destabilizing saves.
3. Continue polishing existing Save/Load, Preferences, Game Menu and Quick Menu when concrete UX defects are found.
4. Keep flowchart, glossary, chapter select, ending list and authored replay content deferred until real story material exists.

## Decision

**APPROVED AS RESEARCH BASELINE.**

This document records benchmark knowledge and priorities only. It does not authorize a new gameplay feature by itself.
