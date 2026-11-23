# Guesser role-name regression tests

`generate_fixture.py` extracts the actual alias switch, normalization table, role lookup, guess command parser, target-match predicate and teller restriction from production. It also extracts the unchanged numeric role enum. Tests verify the source hashes before running; these are compiled source slices with synthetic players, not a running IL2CPP game.

Run `python tests/Guesser.RoleNames.Tests/generate_fixture.py` only after reviewing production changes, then `dotnet run --project tests/Guesser.RoleNames.Tests -c Release -p:CopyToAmongUs=false`. Do not recapture `BaselineMappings.json` merely to hide a regression. It records the 1,516 canonical English/Japanese lookup inputs before this fix; the expected change is the Japanese AmateurTeller entry.

The reviewed correction changes only `GetString("BeginnerTeller")` to `GetString("AmateurTeller")`. Production RoleId 179, option base 30100, translations, target restrictions and firing code remain unchanged. The same tests on an isolated copy of the previous source reproduce nine failures; the corrected source passes all 32 tests. Meeting UI, controller interaction and actual meeting kill synchronization still require a game test.
