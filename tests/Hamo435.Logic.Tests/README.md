# hamo v4.35 integration local tests

From release-development:

1. `python reports/hamo435/audit_integration.py`
2. `python reports/hamo435/generate-logic-tests.py`
3. `dotnet run --project tests/Hamo435.Logic.Tests/Hamo435.Logic.Tests.csproj -c Release -p:AllowIsolatedRoleBuild=true -p:CopyToAmongUs=false -p:NuGetAudit=false`

The generator extracts the current product role enum, classification, partner availability, command parser, SenderState and SendAllGameOptions method. Hamo435RoleOrder is linked directly.
Unity, PlayerControl, vote-mode selection, transport and message rendering are substituted. This does not test actual Among Us RPC delivery, native rendering or game victory animation.

The static audit scans all role/RPC/death IDs and statically resolvable Option IDs. It reports unresolved dynamic option-factory call sites rather than claiming to execute them. Full native Options.Load still requires an in-game test.

Existing Relay tests remain under MatchmakingRelay.Tests, MatchmakingLifecycle.Tests and workers/matchmaking-relay/test. Do not enable real network or install the DLL when running these local tests.