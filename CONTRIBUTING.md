# Contributing

WinLedger favors deterministic, auditable behavior over heuristics.

Before contributing:

- keep domain models independent from Windows APIs;
- write tests for diff, rollback, and export behavior;
- avoid telemetry, hidden network calls, and cloud-only features;
- document Windows behavior that is non-obvious;
- do not add dependencies with licenses that conflict with MIT.
