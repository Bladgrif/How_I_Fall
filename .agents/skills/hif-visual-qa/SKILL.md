---
name: hif-visual-qa
description: Run How I Fall player-visible visual QA with existing Unity launchers and graphical E2E.
---

# How I Fall visual QA

1. Identify the changed player-visible state and reuse the closest existing `*QaLauncher.cs` or graphical E2E; avoid a launcher/test for every small button.
2. Use functional-area coverage and the standard **1920x1080** resolution only, unless the task explicitly requires another resolution.
3. Run the real runtime flow without `-nographics`; capture screenshots for visual scenarios.
4. Inspect available screenshots for the intended state, missing sprites/textures, clipping, overlap, incorrect visibility, broken layout, and clear runtime/UI defects.
5. Fix a task-scoped obvious defect and repeat the relevant proof.
6. If a screenshot cannot actually be inspected, mark that inspection `NOT RUN`.
7. Ask for human manual QA only for subjective visual/taste criteria or a real automation gap. Automated objective proof is not a human QA pass.
