# Market domain checks

On Windows, run from the repository root with a .NET 8 SDK installed:

```powershell
& ./Tools/MarketChecks/run.ps1
```

This script uses the newest installed .NET 8 reference pack, clears package sources for the restore, and restores its temporary process environment changes afterwards. It does not download packages or require Unity. Its exit code is nonzero if restore, compilation or any scenario fails.

With an ordinary .NET 8 SDK setup on any supported operating system, the equivalent command is:

```sh
dotnet run --project Tools/MarketChecks
```

This package-free executable compiles the actual Unity market domain source with C# 9. The same behavioral scenarios are wired into `MarketModulesTests` for the Unity Editor. The console entry targets .NET 8 only to make the tests runnable without Unity; the production source does not reference this runner or .NET 8 assemblies.

The scenarios cover stage transitions, safe return timing, dual-order reservations, simultaneous cooking, single payments, cancellation/refunds, next-day persistence, earnings and kitchen upgrades, resource environment changes, and invalid input handling.

Verified on 2026-11-11 using the offline script: **20 scenarios, 628 assertions, 0 failures**. This includes the sunny-fruit recipe and mixed menus that contain both available and unavailable dishes.

Passing here is evidence for the domain rules only. It does not establish Unity compilation, EditMode runner success, serialized reference integrity, gameplay input or a manual playthrough. Run `MarketModulesTests` in Unity Test Runner and exercise the new scene separately.
