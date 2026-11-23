# Onmyoji targeting regression tests

Run `dotnet run --project tests/Onmyoji.Targeting.Tests -c Release -p:CopyToAmongUs=false`.

GeneratedTargeting.cs contains eight verbatim production method slices, including
killer classification, Shikigami validation/creation, nomination/voting and star display.
Tests compare every slice and its hash with Roles/Neutral/Onmyoji.cs before relying
on the compiled fixture. GeneratedRoles.cs preserves the production numeric enum.

If a reviewed production change makes a fixture stale, regenerate it using
`python tests/Onmyoji.Targeting.Tests/generate_fixture.py`, then review both the
fixture diff and behavioral expectations. Do not merely update hashes to hide failures.

Unity, role conversion, options and network side effects are synthetic substitutes.
The kill-enabled flags in tests demonstrate that the upstream whitelist does not
inspect those flags; they do not execute all 17 roles' native ability implementations.
Actual per-role option/awakening restrictions require source audit and in-game checks.

No test launches Among Us, sends game RPCs or contacts external services.