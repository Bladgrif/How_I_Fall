---
name: hif-visual-qa
description: Выполняет визуальный QA player-facing интерфейса How I Fall через существующие Unity launcher'ы и graphical E2E.
---

# Визуальный QA How I Fall

1. Определи изменённое player-facing состояние и переиспользуй ближайший существующий `*QaLauncher.cs` или graphical E2E. Не создавай отдельный launcher/test для каждой мелкой кнопки.
2. Проверяй функциональную область на стандартном разрешении **1920x1080**, если задача явно не требует другого разрешения.
3. Если для области существует graphical E2E, сначала запусти его в реальном runtime без `-nographics`; объективные UI-проверки не перекладывай сразу на ручной QA.
4. Получи и просмотри скриншоты: missing sprite/texture, clipping, overlap, неправильная visibility, malformed dropdown, сломанные anchors/layout и очевидные runtime/UI дефекты.
5. Исправь объективный дефект в рамках задачи и повтори релевантное доказательство. Ставь `NOT RUN` только если graphical proof реально пытались запустить, но он заблокирован инфраструктурой, либо объективной автоматизации для области действительно нет.
6. Проси ручной QA пользователя только для субъективного визуального вкуса, атмосферы, финального aesthetic approval или реально неавтоматизируемого взаимодействия. Автоматическая проверка Codex не называется человеческим QA pass.
7. После значимого визуального прохода обновляй `docs/visual-baselines/` только если это действительно новый/изменённый accepted baseline; не меняй baseline лишь ради публикации свежего proof и не копируй туда весь `QAArtifacts`.
8. Если screenshots materially нужны reviewer'у, опубликуй reviewer-visible proof по контракту `docs/product/agent_orchestration.md`: GitHub `visual-review` artifact для изменённых curated baselines; иначе Drive `03 — UI — implementation & QA proof`; при недоступном Drive — временная `evidence/<task>` ветка с bounded PNG set + manifest (`source PR/head SHA`, filenames, SHA-256). Evidence-ветку не включай в task PR/master.
9. В финальном отчёте укажи точное место опубликованного proof и какие кадры реально просмотрены. Local-only path недостаточен, если publish route доступен. Если публикация после реальной попытки невозможна, явно отметь `REVIEWER VISUAL PROOF NOT AVAILABLE` и причину.
