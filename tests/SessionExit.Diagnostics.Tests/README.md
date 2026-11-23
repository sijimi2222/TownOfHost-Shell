# Session-exit observability tests

Tests compile the production development-only observer with synthetic Unity/Harmony adapters. RecordedExitStages.json contains only fixed stages from the 2026-11-14 15:16 exit; no user or authentication data is included. TitleFrameTests verifies the newly observed VersionShower/first-frame boundaries and compile-time guards.

The current 48 tests pass. The same tests against the preceding coarse observer reproduce 30 observability failures and 18 successes. This is not a reproduction of the native process crash, nor evidence that it is fixed. Real Harmony application and game object lifetimes need an actual game test.

The title-frame-v2 observer logs each stage once and the first repeat of seven important entry boundaries, capped at 64 lines per exit. repeat=2plus means at least two observations, not an exact count. Finalizers preserve the original exception. A normal early return can legitimately skip inner stages; read the production guards before interpreting a missing Returned.

The project compiles its observer link for testing only. Public MOD Release excludes the entire Development/DebugController folder; CredentialsPatch source probes are guarded with SHELL_DEBUG_CONTROLLER. Actual public DLL IL is separately compared to the prior build.
