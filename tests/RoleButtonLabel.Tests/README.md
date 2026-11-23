# Display button label regressions

The actual ConfigureLabel and UpdateLabel methods are extracted from RoleSettingsDisplayUi.cs; the mode model is linked from product source. Native Unity/TMP types are simulated to verify parenting, AspectPosition removal, font/material reuse, anchors, rendering order and text refresh lifecycle. This does not emulate real fonts, clipping or GPU rendering; those require an in-game check.

Regenerate with reports/role-display-label/generate-label-tests.py after modifying the product methods. Outputs stay isolated; no DLL installation.
