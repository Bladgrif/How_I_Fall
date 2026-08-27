---
name: hif-visual-qa
description: Run How I Fall player-visible visual QA with existing Unity launchers and graphical E2E.
---

# How I Fall visual QA

1. Identify the changed player-visible state and reuse the closest existing `*QaLauncher.cs` or graphical E2E; avoid a launcher/test for every small button.
2. Use functional-area coverage and the standard **1920x1080** resolution only, unless the task explicitly requires another resolution.
3. When this functional area has a graphical E2E scenario, run it first in the real runtime without `-nographics`; do not immediately defer objective UI checks to human QA.
4. Obtain and inspect its screenshots for missing sprites/textures, clipping, overlap, incorrect visibility, malformed dropdowns, broken anchors/layout, and clear runtime/UI defects.
5. Fix a task-scoped objective defect and repeat the relevant proof. Mark graphical proof `NOT RUN` only when it was attempted but failed because of environment/infrastructure, or no objective automation exists for the area.
6. Ask for human manual QA only for subjective visual/taste, atmosphere, final aesthetic approval, or truly non-automatable interaction. Automated objective proof is not a human QA pass.
