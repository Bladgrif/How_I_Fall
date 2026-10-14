---
name: hif-agent-team
description: Организует роли How I Fall, ближайший repository-план и handoff PM → UI designer → coder → independent review. Для координации команды, не нового scheduler или разрешения production.
---

# Команда How I Fall

Прочитай `AGENTS.md` и `docs/product/agent_team.md`. Model/quota/session/Git policy —
`agent_orchestration.md`, acceptance — `review_workflow.md`; не дублируй control plane.

Project profiles: `.codex/agents/hif_*.toml` — `hif_pm`, `hif_ui_designer`,
`hif_coder`, `hif_reviewer`; `hif_qa` только при полезном отдельном QA scope.
Не запускай весь состав одновременно. Одна active implementation, затем fresh
independent review; параллельное research только при непересекающемся scope.

До dispatch задай accepted base/checkout, goal, allowed paths, protected contracts,
acceptance, approval/Git permissions и evidence handoff. Проверь fresh quota:
Sol profile не даёт bypass QUOTA_SAVE; existing authorized Flash lane или wait.

Для нового visual direction: research → видимый target → user choice →
approved production brief → coder → independent review. Не замени это генерацией
вариантов и immediate coding. Reviewer получает actual diff/test/remote originals.

PM ведёт ближайший bounded план в existing `docs/technical_plan.md`; native
execution status/receipts остаются в existing queue/state. Галочки подзадач не DONE.

Если tool не поддерживает named custom agent, координатор может прочесть TOML
instructions и передать их одной реальной bounded сессии. Не выдавай manual prompt
handoff за native discovery, mock fixture за live запуск или local file за remote proof.
