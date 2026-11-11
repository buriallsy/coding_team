# Unity snapshot verification

On November 9, 2026 (Singapore time), the upload snapshot was checked with Unity 6000.5.4f1 on macOS. The 597 retained project files match the authoring project byte for byte. Skywell's scene SHA-256 is `35c3c52f20ac2445a39117fa3d8ac8e9054be1d9450f48e049189dd2e08bfc63`.

| Check | Result |
| --- | --- |
| Source import | Unity resolved the locked packages and compiled the project in a temporary copy. |
| EditMode | 117 passed, zero failed or skipped. [Test result](evidence/2026-11-09-editmode.txt) |
| Serialized references | Four authored scenes and one prefab passed. Unity inspected 8,341 components. [Reference audit](evidence/2026-11-09-references.txt) |

Unity cleared its derived URP runtime settings list in the validation copy during import. The upload carries the saved authoring asset. The upload checks cover import, rules, and scene references. The route and build results below retain their original dates and test scope.

Run the checks from **Borrowing Sun > Validation** in the Unity editor. Use **Run existing EditMode tests**, wait for completion, then use **Check all authored scenes and prefabs**. Both write results under `unity_prototype/validation/current/project/`.

For batch checks, set `UNITY` to the installed editor executable and run from the repository root. Run one command at a time.

```sh
"$UNITY" -batchmode -nographics -projectPath "$PWD/unity_prototype/BorrowingSun" \
  -runTests -testPlatform EditMode -testResults "$PWD/editmode-results.xml" -logFile "$PWD/editmode.log"

"$UNITY" -batchmode -nographics -projectPath "$PWD/unity_prototype/BorrowingSun" \
  -executeMethod BorrowingSun.Editor.ProjectValidation.CheckReferences \
  -quit -logFile "$PWD/references.log"
```

## Flower Court market whitebox · November 11, 2026

The graphical Unity Game View replay on macOS passed 312 checks. It started at the original entrance and used keyboard and pointer input through cargo handover, gathering, local rain, both recipes, day rollover, and reset. EditMode passed 122 tests, including five market cases. The serialized reference audit covered five scenes, one prefab, and 12,352 components.

To replay, open `05_Skywell_FlowerMarket.unity`, enter Play Mode, and select **Borrowing Sun → Skywell → Check complete Flower Market route** with the editor in the foreground. The original ladder interaction requires actual window focus. The published EditMode XML omits machine-local paths. [Playtest record and timings](04_Iterations/Skywell_Flower_Market_Whitebox.md) · [Play Mode report](evidence/flower-market/playmode.txt) · [EditMode results](evidence/flower-market/editmode.xml) · [Serialized references](evidence/flower-market/references.txt)

## Flower Court day and night · November 11, 2026

The macOS graphical Unity Game View passed 346 checks from the original entrance through delivery, both dusk choices, local rain, day rollover and reset from night. The replay used the shop's early-return controls and verified the gathering clocks while walking. The EditMode suite verified both natural countdown transitions and passed 128 tests, including 11 Flower Court cases. The serialized reference audit covered five scenes, one prefab and 12,358 components.

Open the Flower Market scene and run **Borrowing Sun → Skywell → Check complete Flower Market route** in focused Play Mode. The published EditMode XML omits machine-local paths. [Playtest and tuning notes](04_Iterations/Skywell_Flower_Market_Day_Night.md) · [Play Mode report](evidence/flower-market-day-night/playmode.txt) · [EditMode results](evidence/flower-market-day-night/editmode.xml) · [Serialized references](evidence/flower-market-day-night/references.txt)

## Flower Court dual burners and kitchen upgrade · November 11, 2026

The macOS Unity Game View replay passed 372 checks from the original entrance through cargo handover and both dusk choices. It accepted and reserved two orders, cooked them concurrently, served each window, upgraded the kitchen, and closed with a refund for the undelivered fruit dish. The third day kept 22 in cash, 58 in gross revenue, the upgrade, and the exact refunded stock. EditMode passed all 133 tests, including 16 market cases. Five scenes and one prefab passed the reference audit across 12,358 components.

In focused Play Mode on the Flower Market scene, run **Borrowing Sun → Skywell → Check complete Flower Market route**. [Playtest and tuning notes](04_Iterations/Skywell_Flower_Market_Kitchen.md) · [Play Mode report](evidence/flower-market-kitchen/playmode.txt) · [EditMode results](evidence/flower-market-kitchen/editmode.xml) · [Serialized references](evidence/flower-market-kitchen/references.txt)

## Dated L5 evidence

| Date | Scope and result |
| --- | --- |
| Oct 22, 2026 | 266 fixed-camera and 279 over-the-shoulder checks. Each route starts at the normal entrance and uses gameplay inputs through delivery and reset. [Fixed](evidence/2026-10-22-main-fixed.txt) · [Over-the-shoulder](evidence/2026-10-22-main-shoulder.txt) |
| Oct 28, 2026 | 267 fixed-camera and 273 over-the-shoulder checks on the cave, deer, and four-cloud route. Each starts from one East Village placement and then uses gameplay input. [Fixed](evidence/2026-10-28-cloud-fixed.txt) · [Over-the-shoulder](evidence/2026-10-28-cloud-shoulder.txt) |
| Oct 28, 2026 | 117 EditMode checks and 8,341 component reference checks. The local L5 macOS development build succeeded with zero errors and one warning. The locally signed app passed 48 headless startup checks and exited with code 0. [EditMode](evidence/2026-10-28-editmode.txt) · [References](evidence/2026-10-28-references.txt) |
| Oct 29, 2026 | Play Mode captures for separate weather states. The validation used explicit start positions and ran 59 capture-related checks. [Rain raises the tower](evidence/skywell-rain.png) · [Sunlight reaches the tower mirror](evidence/skywell-sun.png) |

Windows desktop testing, a complete playthrough in the standalone app, and first-time player testing remain open for the Unity prototype.
