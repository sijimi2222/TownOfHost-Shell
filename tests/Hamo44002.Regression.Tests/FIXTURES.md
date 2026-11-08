# hamo 4.40.02 regression fixtures

`UpstreamVentLock.cs` is an unmodified test-only copy from the user-supplied ZIP.
ZIP SHA-256: `8f6b4d293d185277777c279c23d0549ff20a1ad8bfa2483d2f5efe14ef22de6f`.
It is not included in TownOfHost-Shell.dll and Harmony does not run in this harness.
Tests invoke the methods with in-memory Unity/Hazel substitutes; no packets leave the process.

`NativeValidator.cs` is a verbatim extraction of CheckForInvalidMurdering from the
integrated Shell source. Other role files are compiled directly via project links.
The tests validate role decisions and state transitions, not IL2CPP ABI, native RPC,
Unity drawing, real network delivery, or live Vanilla interoperability.
